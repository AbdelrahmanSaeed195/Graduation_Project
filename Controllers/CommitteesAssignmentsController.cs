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
        // 1. Index: شاشة عرض وتصفية التكليفات القائمة
        // ============================================================
        public async Task<IActionResult> Index(int? examId, string academicYear)
        {
            var examsWithSchedules = await _context.ExamSchedules
                .Include(s => s.Exam).ThenInclude(e => e.Subject)
                .Where(s => s.Exam != null && s.Exam.Subject != null)
                .Select(s => s.Exam)
                .Distinct()
                .OrderByDescending(e => e.ExamDate)
                .Select(e => new {
                    Id = e.ExamId,
                    Name = e.Subject.SubjectName + " - " + e.ExamDate.ToString("yyyy/MM/dd")
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
                query = query.Where(a => a.ExamSchedule != null && a.ExamSchedule.ExamId == examId.Value);
            }

            if (!string.IsNullOrEmpty(academicYear))
            {
                AcademicLevel targetLevel = academicYear switch
                {
                    "المستوى الأول" => AcademicLevel.FirstYear,
                    "المستوى الثاني" => AcademicLevel.SecondYear,
                    "المستوى الثالث" => AcademicLevel.ThirdYear,
                    "المستوى الرابع" => AcademicLevel.FourthYear,
                    _ => AcademicLevel.FirstYear
                };

                query = query.Where(a => a.ExamSchedule != null &&
                                         a.ExamSchedule.Exam != null &&
                                         a.ExamSchedule.Exam.Subject != null &&
                                         a.ExamSchedule.Exam.Subject.AcademicYear == targetLevel);
            }

            var assignments = await query
                .OrderBy(a => a.HallId == null)
                .ThenBy(a => a.BlockId == null)
                .ThenBy(a => a.CommitteeId == null)
                .ToListAsync();

            foreach (var item in assignments)
            {
                if (item.Person != null)
                {
                    ViewData["Job_" + item.PersonId] = GetEnumDisplayName(item.Person.JobRole);
                }
            }

            ViewBag.SelectedExamId = examId;
            return View(assignments);
        }

        // ============================================================
        // 2. ConfirmAutoAssign (GET)
        // ============================================================
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
        // 3. RunAutoAssign (POST)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunAutoAssign(int hallId, int examId)
        {
            var schedule = await _context.ExamSchedules
                .Include(s => s.Exam)
                .Include(s => s.Block)
                .Where(s => s.ExamId == examId && s.Block.HallId == hallId)
                .FirstOrDefaultAsync();

            if (schedule == null)
            {
                TempData["Error"] = "عفواً، لا توجد صالات أو بلوكات محجوزة لهذا الامتحان داخل هذه الصالة حالياً.";
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
                TempData["Success"] = "تم تشغيل التوزيع التلقائي الذكي وحفظ طاقم العمل والملاحظين بالبلوكات بنجاح.";
            else
                TempData["Error"] = "فشل التوزيع التلقائي، تأكد من وجود طاقة بشرية كافية في جداول القوى العاملة للكلية.";

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
                .Include(a => a.Committee).ThenInclude(c => c.Block).ThenInclude(b => b.Hall)
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam).ThenInclude(e => e.Subject)
                .FirstOrDefaultAsync(m => m.AssignmentId == id);

            if (assignment == null) return NotFound();

            if (assignment.Person != null)
            {
                ViewBag.ArabicJobRole = GetEnumDisplayName(assignment.Person.JobRole);
            }

            return View(assignment);
        }

        // ============================================================
        // 5. Create (GET)
        // ============================================================
        public async Task<IActionResult> Create()
        {
            await LoadDropdownsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CommitteesAssignment assignment)
        {
            if (assignment == null)
            {
                await LoadDropdownsAsync();
                return View(new CommitteesAssignment());
            }

            if (ModelState.IsValid)
            {
                var isBusy = await _context.CommitteesAssignments
                    .AnyAsync(a => a.ExamScheduleId == assignment.ExamScheduleId && a.PersonId == assignment.PersonId);

                if (isBusy)
                {
                    ModelState.AddModelError("", "هذا الموظف مشغول بتكليف آخر بالفعل في نفس الجلسة امتحانية!");
                    await LoadDropdownsAsync(assignment);
                    return View(assignment);
                }

                assignment.AssignmentType = "Manual";
                var role = await _context.Roles.FindAsync(assignment.RoleId);
                assignment.RoleType = role?.RoleDescription ?? "تكليف يدوي";

                _context.Add(assignment);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم إضافة التكليف اليدوي بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            await LoadDropdownsAsync(assignment);
            return View(assignment);
        }

        // ============================================================
        // 6. Edit
        // ============================================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var assignment = await _context.CommitteesAssignments
                .Include(a => a.Hall)
                .Include(a => a.Block).ThenInclude(b => b.Hall)
                .Include(a => a.Committee).ThenInclude(c => c.Block).ThenInclude(b => b.Hall)
                .Include(a => a.ExamSchedule)
                .FirstOrDefaultAsync(m => m.AssignmentId == id);

            if (assignment == null) return NotFound();

            if (assignment.CommitteeId != null && assignment.Committee != null)
            {
                if (assignment.BlockId == null) assignment.BlockId = assignment.Committee.BlockId;
                if (assignment.HallId == null) assignment.HallId = assignment.Committee.Block.HallId;
            }
            else if (assignment.BlockId != null && assignment.Block != null)
            {
                if (assignment.HallId == null) assignment.HallId = assignment.Block.HallId;
            }

            await LoadDropdownsAsync(assignment);
            return View(assignment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CommitteesAssignment assignment)
        {
            if (id != assignment.AssignmentId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var isBusy = await _context.CommitteesAssignments
                        .AnyAsync(a => a.ExamScheduleId == assignment.ExamScheduleId &&
                                       a.PersonId == assignment.PersonId &&
                                       a.AssignmentId != assignment.AssignmentId);

                    if (isBusy)
                    {
                        ModelState.AddModelError("", "هذا الموظف مشغول بتكليف تنظيم آخرى في هذه الجلسة!");
                        await LoadDropdownsAsync(assignment);
                        return View(assignment);
                    }

                    var role = await _context.Roles.FindAsync(assignment.RoleId);
                    assignment.RoleType = role?.RoleDescription ?? "تعديل يدوي";
                    assignment.AssignmentType = "Manual";

                    _context.Update(assignment);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "تم تحديث بيانات التكليف بنجاح.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AssignmentExists(assignment.AssignmentId)) return NotFound();
                    else throw;
                }
            }

            await LoadDropdownsAsync(assignment);
            return View(assignment);
        }

        // ============================================================
        // 7. Delete
        // ============================================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var assignment = await _context.CommitteesAssignments
                .Include(a => a.Person)
                .Include(a => a.Hall)
                .Include(a => a.Block)
                .Include(a => a.Committee)
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam).ThenInclude(e => e.Subject)
                .FirstOrDefaultAsync(m => m.AssignmentId == id);

            if (assignment == null) return NotFound();

            return View(assignment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var assignment = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule)
                .FirstOrDefaultAsync(a => a.AssignmentId == id);

            if (assignment != null)
            {
                int? examId = assignment.ExamSchedule?.ExamId;
                _context.CommitteesAssignments.Remove(assignment);
                await _context.SaveChangesAsync();

                TempData["Success"] = "تم حذف وإلغاء إسناد التكليف من النظام.";
                return RedirectToAction(nameof(Index), new { examId = examId });
            }

            return RedirectToAction(nameof(Index));
        }
        // ============================================================
        // 8. PreparePrintSheet (GET)
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> PreparePrintSheet()
        {
            var exams = await _context.Exams
                .Include(e => e.Subject)
                .OrderByDescending(e => e.ExamDate)
                .ToListAsync();

            var halls = await _context.Halls
                .OrderBy(h => h.HallName)
                .ToListAsync();

            ViewBag.Exams = new SelectList(exams, "ExamId", "Subject.SubjectName");
            ViewBag.Halls = new SelectList(halls, "HallId", "HallName");

            return View();
        }

        // ============================================================
        // 9. PrintControlSheet (POST) - النسخة المحدثة لطباعة كل مراقب/بلوك مستقل بالتوزيع الفعلي للطلاب
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrintControlSheet(int examId, int hallId)
        {
            var exam = await _context.Exams
                .Include(e => e.Subject)
                .FirstOrDefaultAsync(e => e.ExamId == examId);

            var hall = await _context.Halls
                .FirstOrDefaultAsync(h => h.HallId == hallId);

            if (exam == null || hall == null) return NotFound();

            // جلب كافة تكليفات المراقبة داخل هذه الصالة بجميع لجانها وبلوكاتها
            var allAssignments = await _context.CommitteesAssignments
                .Include(a => a.Person)
                .Include(a => a.Block)
                .Include(a => a.Committee).ThenInclude(c => c.Block)
                .Include(a => a.ExamSchedule)
                .Where(a => a.ExamSchedule.ExamId == examId &&
                            (a.HallId == hallId ||
                             a.Block.HallId == hallId ||
                             (a.Committee != null && a.Committee.Block.HallId == hallId)))
                .ToListAsync();

            if (!allAssignments.Any())
            {
                TempData["ErrorMessage"] = $"عفواً، لا توجد تكليفات أو طواقم مراقبة مسجلة لصالة ({hall.HallName}) في مادة ({exam.Subject?.SubjectName ?? ""}) حتى الآن.";
                return RedirectToAction(nameof(PreparePrintSheet));
            }

            var pagesList = new List<ExamControlSheetViewModel>();

            var blocksInHall = await _context.Blocks
                .Where(b => b.HallId == hallId)
                .OrderBy(b => b.BlockName)
                .ToListAsync();

            // تحديد السنة الدراسية (الفرقة) المستهدفة من المادة الحالية لفلترة الطلاب بناءً عليها
            var targetAcademicLevel = exam.Subject?.AcademicYear;

            foreach (var b in blocksInHall)
            {
                // 1. تصفية الملاحظين الفعليين وحساب عدد الكراسات (الطلاب) ديناميكياً بربط اللجنة بالفرقة الدراسية للمادة
                var activeObservers = allAssignments
                    .Where(a => a.Committee != null && a.Committee.BlockId == b.BlockId && a.RoleType.Contains("ملاحظ لجنة") && a.Person != null)
                    .OrderBy(a => a.Committee.CommitteeNumber)
                    .Select(a => new ObserverRowItem
                    {
                        ObserverName = a.Person.FullName,
                        CommitteeNumber = "لجنة " + a.Committee.CommitteeNumber.ToString(),

                        // 💡 الحل الجذري والنهائي: نعد الطلاب المقيدين في هذه اللجنة والذين يدرسون في نفس فرقة المادة الحالية
                        BookletsCount = _context.Students.Count(s => s.CommitteeId == a.CommitteeId &&
                                                                    s.AcademicYear == targetAcademicLevel)
                    })
                    .ToList();

                var blockReserveObservers = allAssignments
                    .Where(a => a.BlockId == b.BlockId && a.RoleType.Contains("ملاحظ احتياطي للمبنى") && a.Person != null)
                    .Select(a => new ObserverRowItem
                    {
                        ObserverName = a.Person.FullName,
                        CommitteeNumber = "ملاحظ احتياطي بالمبنى",
                        BookletsCount = 0
                    })
                    .ToList();

                var blockItem = new BlockGroupItem
                {
                    BlockName = b.BlockName,
                    BlockObserverName = allAssignments.FirstOrDefault(a => a.BlockId == b.BlockId && a.RoleType.Contains("مراقب"))?.Person?.FullName ?? "................",
                    CommitteeObservers = activeObservers.Concat(blockReserveObservers).ToList()
                };

                if (!blockItem.CommitteeObservers.Any() && blockItem.BlockObserverName == "................")
                {
                    continue;
                }

                var pageModel = new ExamControlSheetViewModel
                {
                    SubjectName = exam.Subject?.SubjectName ?? "---",
                    TargetYear = exam.Subject?.AcademicYear switch
                    {
                        AcademicLevel.FirstYear => "المستوى الأول",
                        AcademicLevel.SecondYear => "المستوى الثاني",
                        AcademicLevel.ThirdYear => "المستوى الثالث",
                        AcademicLevel.FourthYear => "المستوى الرابع",
                        _ => "---"
                    },
                    ExamDate = exam.ExamDate.ToString("yyyy/MM/dd"),
                    ExamDay = exam.ExamDate.ToString("dddd", new System.Globalization.CultureInfo("ar-EG")),
                    ExamTime = $"{DateTime.Today.Add(exam.StartTime):hh:mm tt} - {DateTime.Today.Add(exam.EndTime):hh:mm tt}",
                    HallName = hall.HallName,

                    MainHead1 = allAssignments.FirstOrDefault(a => a.RoleType.Contains("رئيس صالة أساسي (القطاع الأول)"))?.Person?.FullName ?? "................",
                    ReserveHead1 = allAssignments.FirstOrDefault(a => a.RoleType.Contains("رئيس صالة احتياطي"))?.Person?.FullName ?? "................",
                    ReserveObserver1 = allAssignments.FirstOrDefault(a => a.RoleType.Contains("مراقب احتياطي للصالة"))?.Person?.FullName ?? "................",

                    MainHead2 = allAssignments.FirstOrDefault(a => a.RoleType.Contains("رئيس صالة أساسي (القطاع الثاني)"))?.Person?.FullName ?? "................",
                    ReserveHead2 = "................",
                    ReserveObserver2 = "................",

                    DoctorName = allAssignments.FirstOrDefault(a => a.RoleType.Contains("دكتور"))?.Person?.FullName ?? "................",
                    NurseName = allAssignments.FirstOrDefault(a => a.RoleType.Contains("ممرض"))?.Person?.FullName ?? "................",

                    Blocks = new List<BlockGroupItem> { blockItem },

                    GeneralReserveObservers = allAssignments
                        .Where(a => a.RoleType.Contains("احتياطي عام") || a.RoleType.Contains("ملاحظ احتياطي للكلية"))
                        .Select(a => a.Person?.FullName)
                        .Where(name => name != null)
                        .ToList()
                };

                pagesList.Add(pageModel);
            }

            if (!pagesList.Any())
            {
                TempData["ErrorMessage"] = "عفواً، لا توجد لجان ممتلئة لعرضها داخل البلوكات حالياً.";
                return RedirectToAction(nameof(PreparePrintSheet));
            }

            return View(pagesList);
        }
        // ============================================================
        // 10. Ajax Helpers
        // ============================================================
        [HttpGet]
        public async Task<JsonResult> GetHallDetails(int hallId)
        {
            var blocks = await _context.Blocks
                .Where(b => b.HallId == hallId)
                .Select(b => new { id = b.BlockId, name = b.BlockName })
                .ToListAsync();

            var committees = await _context.Committees
                .Include(c => c.Block)
                .Where(c => c.Block.HallId == hallId)
                .Select(c => new { id = c.CommitteeId, name = "لجنة " + c.CommitteeNumber })
                .ToListAsync();

            return Json(new { blocks = blocks, committees = committees });
        }

        private async Task LoadDropdownsAsync(CommitteesAssignment assignment = null)
        {
            var staffList = await _context.Persons.Where(p => p.IsActiveForAssignment).AsNoTracking().ToListAsync() ?? new List<Person>();

            // ============================================================
            // 💡 التعديل الرئيسي هنا: استخدام دالة GetEnumDisplayName لعرض الوظيفة بالعربي
            // ============================================================
            var activeStaff = staffList.Select(p => new {
                PersonId = p.PersonId,
                FullNameWithJob = $"{p.FullName} ({GetEnumDisplayName(p.JobRole)})"
            }).ToList();
            ViewBag.PersonId = new SelectList(activeStaff, "PersonId", "FullNameWithJob", assignment?.PersonId);

            var roles = await _context.Roles.AsNoTracking().ToListAsync() ?? new List<Role>();
            ViewBag.RoleId = new SelectList(roles, "RoleID", "RoleDescription", assignment?.RoleId);

            int? effectiveHallId = assignment?.HallId;
            if (effectiveHallId == null && assignment != null)
            {
                if (assignment.CommitteeId != null)
                {
                    var com = await _context.Committees.Include(c => c.Block).FirstOrDefaultAsync(c => c.CommitteeId == assignment.CommitteeId);
                    effectiveHallId = com?.Block?.HallId;
                    assignment.HallId = effectiveHallId;
                }
                else if (assignment.BlockId != null)
                {
                    var block = await _context.Blocks.FirstOrDefaultAsync(b => b.BlockId == assignment.BlockId);
                    effectiveHallId = block?.HallId;
                    assignment.HallId = effectiveHallId;
                }
            }

            var halls = await _context.Halls.AsNoTracking().ToListAsync() ?? new List<Hall>();
            ViewBag.HallId = new SelectList(halls, "HallId", "HallName", effectiveHallId);

            if (effectiveHallId != null && effectiveHallId > 0)
            {
                var blocks = await _context.Blocks.Where(b => b.HallId == effectiveHallId).AsNoTracking().ToListAsync() ?? new List<Block>();
                var committees = await _context.Committees.Where(c => c.Block.HallId == effectiveHallId).AsNoTracking().ToListAsync() ?? new List<Committee>();

                ViewBag.BlockId = new SelectList(blocks, "BlockId", "BlockName", assignment?.BlockId);
                ViewBag.CommitteeId = new SelectList(committees, "CommitteeId", "CommitteeNumber", assignment?.CommitteeId);
            }
            else
            {
                ViewBag.BlockId = new SelectList(new List<Block>(), "BlockId", "BlockName");
                ViewBag.CommitteeId = new SelectList(new List<Committee>(), "CommitteeId", "CommitteeNumber");
            }

            var dbSchedules = await _context.ExamSchedules
                .Include(s => s.Exam)
                    .ThenInclude(e => e.Subject)
                .AsNoTracking()
                .ToListAsync() ?? new List<ExamSchedule>();

            var schedules = dbSchedules
                .Where(s => s.Exam != null && s.Exam.Subject != null)
                .GroupBy(s => s.ExamId)
                .Select(g => g.First())
                .Select(s => {
                    string arabicLevel = "غير محدد";

                    var subjectEntity = s.Exam?.Subject;
                    if (subjectEntity != null)
                    {
                        arabicLevel = subjectEntity.AcademicYear switch
                        {
                            AcademicLevel.FirstYear => "المستوى الأول",
                            AcademicLevel.SecondYear => "المستوى الثاني",
                            AcademicLevel.ThirdYear => "المستوى الثالث",
                            AcademicLevel.FourthYear => "المستوى الرابع",
                            _ => "غير محدد"
                        };
                    }

                    string subjectName = subjectEntity?.SubjectName ?? "مادة غير محددة";
                    string examDate = s.Exam != null ? s.Exam.ExamDate.ToString("yyyy/MM/dd") : DateTime.Now.ToString("yyyy/MM/dd");

                    string startTime = "00:00";
                    if (s.Exam != null)
                    {
                        try
                        {
                            startTime = DateTime.Today.Add(s.Exam.StartTime).ToString("hh:mm tt");
                        }
                        catch { startTime = s.Exam.StartTime.ToString(@"hh\:mm"); }
                    }

                    return new
                    {
                        ExamScheduleId = s.ExamScheduleId,
                        Name = $"{subjectName} - ({arabicLevel}) | {examDate} ({startTime})"
                    };
                })
                .OrderByDescending(x => x.ExamScheduleId)
                .ToList();

            ViewBag.ExamScheduleId = new SelectList(schedules, "ExamScheduleId", "Name", assignment?.ExamScheduleId);
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

        private bool AssignmentExists(int id) => _context.CommitteesAssignments.Any(e => e.AssignmentId == id);
    }
}