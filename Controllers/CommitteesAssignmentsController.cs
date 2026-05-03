using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using projectweb.Services;
using System;
using System.Collections.Generic;
using System.Linq;
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

        // 1. Index: عرض التوزيعات مع فلترة ذكية بالامتحان والمستوى
        public async Task<IActionResult> Index(int? examId, string academicYear)
        {
            // تجهيز قائمة الامتحانات للبحث (المادة + التاريخ)
            var examsList = await _context.Exams
                .Include(e => e.Subject)
                .OrderByDescending(e => e.ExamDate)
                .Select(e => new {
                    Id = e.ExamId,
                    Name = $"{e.Subject.SubjectName} - {e.ExamDate.ToString("yyyy/MM/dd")}"
                }).ToListAsync();

            ViewBag.Exams = new SelectList(examsList, "Id", "Name", examId);

            // تجهيز قائمة المستويات الدراسية
            var levels = new List<string> { "المستوى الأول", "المستوى الثاني", "المستوى الثالث", "المستوى الرابع" };
            ViewBag.AcademicYears = new SelectList(levels, academicYear);

            // الاستعلام الأساسي لجلب التوزيعات
            var query = _context.CommitteesAssignments
                .Include(a => a.Person)
                .Include(a => a.Role)
                .Include(a => a.Hall)
                .Include(a => a.Block)
                .Include(a => a.Committee).ThenInclude(c => c.Block).ThenInclude(b => b.Hall)
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam).ThenInclude(e => e.Subject)
                .AsQueryable();

            // تطبيق الفلترة بالامتحان
            if (examId.HasValue)
            {
                query = query.Where(a => a.ExamSchedule.ExamId == examId);
            }

            // تطبيق الفلترة بالمستوى الدراسي
            if (!string.IsNullOrEmpty(academicYear))
            {
                query = query.Where(a => a.ExamSchedule.Exam.TargetAcademicYear == academicYear);
            }

            var assignments = await query
                .OrderBy(a => a.HallId == null) // الرؤساء أولاً
                .ThenBy(a => a.BlockId == null) // المراقبين ثانياً
                .ThenBy(a => a.CommitteeID == null) // الملاحظين ثالثاً
                .ToListAsync();

            // حفظ الـ ExamId الحالي لاستخدامه في زر التوزيع التلقائي إذا لزم الأمر
            ViewBag.SelectedExamId = examId;

            return View(assignments);
        }

        // 2. Details
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var assignment = await _context.CommitteesAssignments
                .Include(a => a.Person)
                .Include(a => a.Role)
                .Include(a => a.Hall)
                .Include(a => a.Block)
                .Include(a => a.Committee)
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam).ThenInclude(e => e.Subject)
                .FirstOrDefaultAsync(m => m.AssignmentID == id);

            if (assignment == null) return NotFound();
            return View(assignment);
        }

        // 3. Create (GET)
        public IActionResult Create()
        {
            LoadDropdowns();
            return View();
        }

        // 4. Create (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CommitteesAssignment assignment)
        {
            if (ModelState.IsValid)
            {
                var isBusy = await _context.CommitteesAssignments
                    .AnyAsync(a => a.ExamScheduleId == assignment.ExamScheduleId && a.PersonID == assignment.PersonID);

                if (isBusy)
                {
                    ModelState.AddModelError("", "هذا الموظف لديه تكليف آخر بالفعل في نفس هذه الجلسة!");
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

        // 5. Edit (GET)
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var assignment = await _context.CommitteesAssignments.FindAsync(id);
            if (assignment == null) return NotFound();
            LoadDropdowns(assignment);
            return View(assignment);
        }

        // 6. Edit (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
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
                        ModelState.AddModelError("", "هذا الموظف مشغول بتكليف آخر في نفس الجلسة.");
                        LoadDropdowns(assignment);
                        return View(assignment);
                    }

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

        // 7. Delete (GET)
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var assignment = await _context.CommitteesAssignments
                .Include(a => a.Person)
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .FirstOrDefaultAsync(m => m.AssignmentID == id);

            if (assignment == null) return NotFound();
            return View(assignment);
        }

        // 8. Delete (POST)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var assignment = await _context.CommitteesAssignments.FindAsync(id);
            if (assignment != null)
            {
                _context.CommitteesAssignments.Remove(assignment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف التكليف بنجاح.";
            }
            return RedirectToAction(nameof(Index));
        }

        // 9. AutoAssign: يتم التشغيل بناءً على أول جلسة (Schedule) مرتبطة بالامتحان المختار
        [HttpPost]
        public async Task<IActionResult> AutoAssign(int examId)
        {
            // جلب أول جلسة توزيع مرتبطة بهذا الامتحان لتشغيل المحرك
            var scheduleId = await _context.ExamSchedules
                .Where(s => s.ExamId == examId)
                .Select(s => s.ExamScheduleId)
                .FirstOrDefaultAsync();

            if (scheduleId == 0)
            {
                TempData["Error"] = "لا توجد جلسات توزيع (Schedule) مرتبطة بهذا الامتحان.";
                return RedirectToAction(nameof(Index));
            }

            var success = await _assignmentService.RunAssignmentAsync(scheduleId);

            if (success)
            {
                TempData["Success"] = "تم التوزيع التلقائي بنجاح لجميع لجان هذا الامتحان.";
            }
            else
            {
                TempData["Error"] = "فشل التوزيع. تأكد من توفر الموظفين واللجان.";
            }

            return RedirectToAction(nameof(Index), new { examId = examId });
        }

        // 10. LoadDropdowns
        private void LoadDropdowns(CommitteesAssignment? assignment = null)
        {
            var activeStaff = _context.Persons.Where(p => p.IsActiveForAssignment).OrderBy(p => p.FullName).ToList();
            ViewBag.PersonID = new SelectList(activeStaff, "PersonId", "FullName", assignment?.PersonID);

            ViewBag.RoleID = new SelectList(_context.Roles, "RoleID", "RoleDescription", assignment?.RoleID);
            ViewBag.HallId = new SelectList(_context.Halls, "HallId", "HallName", assignment?.HallId);
            ViewBag.BlockId = new SelectList(_context.Blocks, "BlockID", "BlockName", assignment?.BlockId);
            ViewBag.CommitteeID = new SelectList(_context.Committees, "CommitteeID", "CommitteeNumber", assignment?.CommitteeID);

            var schedules = _context.ExamSchedules
                .Include(s => s.Exam).ThenInclude(e => e.Subject)
                .OrderByDescending(s => s.Exam.ExamDate)
                .ToList()
                .Select(s => new {
                    Id = s.ExamScheduleId,
                    Name = $"{s.Exam.Subject.SubjectName} - {s.Exam.ExamDate.ToString("yyyy/MM/dd")} ({DateTime.Today.Add(s.Exam.StartTime).ToString("hh:mm tt")})"
                }).ToList();

            ViewBag.ExamScheduleId = new SelectList(schedules, "Id", "Name", assignment?.ExamScheduleId);
        }

        private bool AssignmentExists(int id) => _context.CommitteesAssignments.Any(e => e.AssignmentID == id);
    }
}