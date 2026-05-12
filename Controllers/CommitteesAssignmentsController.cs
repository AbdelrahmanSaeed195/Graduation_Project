using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using projectweb.Models.ViewModels;
using projectweb.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace projectweb.Controllers
{
    public class CommitteesAssignmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICommitteesAssignmentsService _assignmentService;

        public CommitteesAssignmentsController(ApplicationDbContext context, ICommitteesAssignmentsService assignmentService)
        {
            _context = context;
            _assignmentService = assignmentService;
        }

        // ============================================================
        // 1. Index: وظيفة بحث فقط (بالامتحان أو المستوى الدراسي)
        // ============================================================
        public async Task<IActionResult> Index(int? examId, string academicYear)
        {
            var examsWithSchedules = await _context.ExamSchedules
                .Include(s => s.Exam).ThenInclude(e => e.Subject)
                .Select(s => s.Exam)
                .Distinct()
                .OrderByDescending(e => e.ExamDate)
                .Select(e => new {
                    Id = e.ExamId,
                    Name = $"{e.Subject.SubjectName} - {e.ExamDate.ToString("yyyy/MM/dd")}"
                }).ToListAsync();

            ViewBag.Exams = new SelectList(examsWithSchedules, "Id", "Name", examId);

            var levels = new List<string> { "المستوى الأول", "المستوى الثاني", "المستوى الثالث", "المستوى الرابع" };
            ViewBag.AcademicYears = new SelectList(levels, academicYear);

            var query = _context.CommitteesAssignments
                .Include(a => a.Person)
                .Include(a => a.Role)
                .Include(a => a.Hall)
                .Include(a => a.Block).ThenInclude(b => b.Hall)
                .Include(a => a.Committee).ThenInclude(c => c.Block).ThenInclude(b => b.Hall)
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam).ThenInclude(e => e.Subject)
                .AsQueryable();

            if (examId.HasValue)
            {
                query = query.Where(a => a.ExamSchedule.ExamId == examId);
            }

            if (!string.IsNullOrEmpty(academicYear))
            {
                query = query.Where(a => a.ExamSchedule.Exam.TargetAcademicYear == academicYear);
            }

            var assignments = await query
                .OrderBy(a => a.HallId == null)
                .ThenBy(a => a.BlockId == null)
                .ThenBy(a => a.CommitteeID == null)
                .ToListAsync();

            foreach (var item in assignments)
            {
                if (item.Person != null)
                {
                    ViewData["Job_" + item.PersonID] = GetEnumDisplayName(item.Person.JobRole);
                }
            }

            ViewBag.SelectedExamId = examId;
            return View(assignments);
        }

        // ============================================================
        // 2. ConfirmAutoAssign (GET): تختار فيها الصالة والامتحان يدوياً
        // ============================================================
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> ConfirmAutoAssign()
        {
            ViewBag.Halls = new SelectList(await _context.Halls.ToListAsync(), "HallId", "HallName");

            var allExams = await _context.Exams
                .Include(e => e.Subject)
                .OrderByDescending(e => e.ExamDate)
                .Select(e => new {
                    Id = e.ExamId,
                    Name = $"{e.Subject.SubjectName} - {e.ExamDate.ToString("yyyy/MM/dd")}"
                }).ToListAsync();

            ViewBag.ExamsList = new SelectList(allExams, "Id", "Name");

            return View();
        }

        // ============================================================
        // 3. RunAutoAssign (POST): تنفيذ التوزيع بناءً على الاختيارات
        // ============================================================
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunAutoAssign(int hallId, int examId)
        {
            var schedule = await _context.ExamSchedules
                .Include(s => s.Exam)
                .Where(s => s.ExamId == examId && s.Committee.Block.HallId == hallId)
                .FirstOrDefaultAsync();

            if (schedule == null)
            {
                TempData["Error"] = "عفواً، لا توجد لجان محجوزة لهذا الامتحان داخل هذه الصالة.";
                return RedirectToAction(nameof(ConfirmAutoAssign));
            }

            var conflictMessage = await _assignmentService.CheckTimeConflictAsync(schedule.ExamScheduleId);
            if (!string.IsNullOrEmpty(conflictMessage))
            {
                TempData["Error"] = conflictMessage;
                return RedirectToAction(nameof(ConfirmAutoAssign));
            }

            var success = await _assignmentService.RunAssignmentAsync(schedule.ExamScheduleId);

            if (success)
                TempData["Success"] = "تم تشغيل التوزيع التلقائي بنجاح للصالة المختارة.";
            else
                TempData["Error"] = "فشل التوزيع. تأكد من توفر الموظفين واللجان.";

            return RedirectToAction(nameof(Index), new { examId = examId });
        }

        // ============================================================
        // 4. Details
        // ============================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var assignment = await _context.CommitteesAssignments
                .Include(a => a.Person)
                .Include(a => a.Role)
                .Include(a => a.Hall)
                .Include(a => a.Block).ThenInclude(b => b.Hall)
                .Include(a => a.Committee)
                    .ThenInclude(c => c.Block)
                        .ThenInclude(b => b.Hall)
                .Include(a => a.ExamSchedule)
                    .ThenInclude(es => es.Exam)
                        .ThenInclude(e => e.Subject)
                .FirstOrDefaultAsync(m => m.AssignmentID == id);

            if (assignment == null) return NotFound();

            if (assignment.Person != null)
            {
                ViewBag.ArabicJobRole = GetEnumDisplayName(assignment.Person.JobRole);
            }

            return View(assignment);
        }

        // ============================================================
        // 5. Create
        // ============================================================
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            LoadDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(CommitteesAssignment assignment)
        {
            if (ModelState.IsValid)
            {
                var isBusy = await _context.CommitteesAssignments
                    .AnyAsync(a => a.ExamScheduleId == assignment.ExamScheduleId && a.PersonID == assignment.PersonID);

                if (isBusy)
                {
                    ModelState.AddModelError("", "هذا الموظف مشغول بتكليف آخر بالفعل في نفس الجلسة!");
                    LoadDropdowns(assignment);
                    return View(assignment);
                }

                assignment.AssignmentType = "Manual";
                var role = await _context.Roles.FindAsync(assignment.RoleID);
                assignment.RoleType = role?.RoleDescription ?? "تكليف يدوي";

                _context.Add(assignment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة التكليف اليدوي بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            LoadDropdowns(assignment);
            return View(assignment);
        }

        // ============================================================
        // 6. Edit
        // ============================================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var assignment = await _context.CommitteesAssignments
                .Include(a => a.Hall)
                .Include(a => a.Block).ThenInclude(b => b.Hall)
                .Include(a => a.Committee).ThenInclude(c => c.Block).ThenInclude(b => b.Hall)
                .Include(a => a.ExamSchedule)
                .FirstOrDefaultAsync(m => m.AssignmentID == id);

            if (assignment == null) return NotFound();


            if (assignment.CommitteeID != null && assignment.Committee != null)
            {
                if (assignment.BlockId == null)
                    assignment.BlockId = assignment.Committee.BlockID;

                if (assignment.HallId == null)
                    assignment.HallId = assignment.Committee.Block.HallId;
            }
            else if (assignment.BlockId != null && assignment.Block != null)
            {
                if (assignment.HallId == null)
                    assignment.HallId = assignment.Block.HallId;
            }

            LoadDropdowns(assignment);
            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, CommitteesAssignment assignment)
        {
            if (id != assignment.AssignmentID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var isBusy = await _context.CommitteesAssignments
                        .AnyAsync(a => a.ExamScheduleId == assignment.ExamScheduleId &&
                                         a.PersonID == assignment.PersonID &&
                                         a.AssignmentID != assignment.AssignmentID);

                    if (isBusy)
                    {
                        ModelState.AddModelError("", "هذا الموظف مشغول بتكليف آخر بالفعل في نفس الجلسة!");
                        LoadDropdowns(assignment);
                        return View(assignment);
                    }

                    var role = await _context.Roles.FindAsync(assignment.RoleID);
                    assignment.RoleType = role?.RoleDescription ?? "تعديل يدوي";

                    assignment.AssignmentType = "Manual";

                    _context.Update(assignment);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "تم تحديث بيانات التكليف بنجاح.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AssignmentExists(assignment.AssignmentID)) return NotFound();
                    else throw;
                }
            }

            LoadDropdowns(assignment);
            return View(assignment);
        }

        // ============================================================
        // 7. Delete
        // ============================================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var assignment = await _context.CommitteesAssignments
                .Include(a => a.Person)
                .Include(a => a.Hall)
                .Include(a => a.Block)
                .Include(a => a.Committee)
                .Include(a => a.ExamSchedule)
                    .ThenInclude(es => es.Exam)
                        .ThenInclude(e => e.Subject)
                .FirstOrDefaultAsync(m => m.AssignmentID == id);

            if (assignment == null) return NotFound();

            return View(assignment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {

            var assignment = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule)
                .FirstOrDefaultAsync(a => a.AssignmentID == id);

            if (assignment != null)
            {
                int? examId = assignment.ExamSchedule?.ExamId;

                _context.CommitteesAssignments.Remove(assignment);
                await _context.SaveChangesAsync();

                TempData["Success"] = "تم حذف التكليف بنجاح.";


                return RedirectToAction(nameof(Index), new { examId = examId });
            }

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 8. Helpers
        // ============================================================
        [HttpGet]
        public async Task<JsonResult> GetHallDetails(int hallId)
        {
            // جلب البلوكات التابعة للصالة
            var blocks = await _context.Blocks
                .Where(b => b.HallId == hallId)
                .Select(b => new { id = b.BlockID, name = b.BlockName })
                .ToListAsync();

            // جلب اللجان التابعة للصالة من خلال البلوكات
            var committees = await _context.Committees
                .Include(c => c.Block)
                .Where(c => c.Block.HallId == hallId)
                .Select(c => new { id = c.CommitteeID, name = "لجنة " + c.CommitteeNumber })
                .ToListAsync();

            return Json(new { blocks = blocks, committees = committees });
        }

        private void LoadDropdowns(CommitteesAssignment? assignment = null)
        {
            var activeStaff = _context.Persons
                .Where(p => p.IsActiveForAssignment)
                .OrderBy(p => p.FullName)
                .AsEnumerable() 
                .Select(p => new {
                    p.PersonId,
                    FullNameWithJob = $"{p.FullName} ({GetEnumDisplayName(p.JobRole)})"
                })
                .ToList();

            ViewBag.PersonID = new SelectList(activeStaff, "PersonId", "FullNameWithJob", assignment?.PersonID);
            ViewBag.RoleID = new SelectList(_context.Roles, "RoleID", "RoleDescription", assignment?.RoleID);

            int? effectiveHallId = assignment?.HallId;

            if (effectiveHallId == null && assignment != null)
            {
                if (assignment.CommitteeID != null)
                {
                    var com = _context.Committees
                        .Include(c => c.Block)
                        .FirstOrDefault(c => c.CommitteeID == assignment.CommitteeID);
                    effectiveHallId = com?.Block?.HallId;
                }
                else if (assignment.BlockId != null)
                {
                    var block = _context.Blocks.FirstOrDefault(b => b.BlockID == assignment.BlockId);
                    effectiveHallId = block?.HallId;
                }
            }

            ViewBag.HallId = new SelectList(_context.Halls, "HallId", "HallName", effectiveHallId);

            if (effectiveHallId != null)
            {
                ViewBag.BlockId = new SelectList(_context.Blocks.Where(b => b.HallId == effectiveHallId), "BlockID", "BlockName", assignment?.BlockId);
                ViewBag.CommitteeID = new SelectList(_context.Committees.Where(c => c.Block.HallId == effectiveHallId), "CommitteeID", "CommitteeNumber", assignment?.CommitteeID);
            }
            else
            {
                ViewBag.BlockId = new SelectList(Enumerable.Empty<SelectListItem>());
                ViewBag.CommitteeID = new SelectList(Enumerable.Empty<SelectListItem>());
            }

            var schedules = _context.ExamSchedules
                .Include(s => s.Exam).ThenInclude(e => e.Subject)
                .AsEnumerable()
                .GroupBy(s => s.ExamId)
                .Select(g => g.First())
                .OrderByDescending(s => s.Exam.ExamDate)
                .Select(s => new {
                    Id = s.ExamScheduleId,
                    Name = $"{s.Exam.Subject.SubjectName} - {s.Exam.ExamDate.ToString("yyyy/MM/dd")} ({DateTime.Today.Add(s.Exam.StartTime).ToString("hh:mm tt")})"
                }).ToList();

            ViewBag.ExamScheduleId = new SelectList(schedules, "Id", "Name", assignment?.ExamScheduleId);
        }

        private string GetEnumDisplayName(Enum enumValue)
        {
            if (enumValue == null) return "غير محدد";

            return enumValue.GetType()
                            .GetMember(enumValue.ToString())
                            .FirstOrDefault()?
                            .GetCustomAttribute<DisplayAttribute>()?
                            .GetName() ?? enumValue.ToString();
        }

        private bool AssignmentExists(int id) => _context.CommitteesAssignments.Any(e => e.AssignmentID == id);
    }
}