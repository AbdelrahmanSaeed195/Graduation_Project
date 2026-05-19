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

            // 2. بناء الاستعلام مع العلاقات المعتمدة
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
        // 2. ConfirmAutoAssign (GET): شاشة بدء التوزيع التلقائي للمراقبين
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
        // 3. RunAutoAssign (POST): تشغيل محرك التوزيع التلقائي الذكي المطور 🌟
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunAutoAssign(int hallId, int examId)
        {
            // تم التعديل هنا لربط الصالة عبر البلوك مباشرة ومنع خطأ الموديل القديم
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

            // التحقق من تعارض الأوقات
            var conflictMessage = await _assignmentService.CheckTimeConflictAsync(schedule.ExamScheduleId);
            if (!string.IsNullOrEmpty(conflictMessage))
            {
                TempData["Error"] = conflictMessage;
                return RedirectToAction(nameof(ConfirmAutoAssign));
            }

            // استدعاء الباكيند لتنفيذ التوزيع الذكي الفعلي وحفظ الطواقم
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
        // 5. Create
        // ============================================================
        public IActionResult Create()
        {
            LoadDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CommitteesAssignment assignment)
        {
            if (ModelState.IsValid)
            {
                var isBusy = await _context.CommitteesAssignments
                    .AnyAsync(a => a.ExamScheduleId == assignment.ExamScheduleId && a.PersonId == assignment.PersonId);

                if (isBusy)
                {
                    ModelState.AddModelError("", "هذا الموظف مشغول بتكليف آخر بالفعل في نفس الجلسة امتحانية!");
                    LoadDropdowns(assignment);
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
            LoadDropdowns(assignment);
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

            LoadDropdowns(assignment);
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
                        LoadDropdowns(assignment);
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

            LoadDropdowns(assignment);
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
        // 8. شاشة التحضير لطباعة المحاضر الرسمية (تعتمد على المادة والمبنى/البلوك) 🌟
        // ============================================================
        //[HttpGet]
        //public async Task<IActionResult> PreparePrintSheet()
        //{
        //    var exams = await _context.Exams
        //        .Include(e => e.Subject)
        //        .OrderByDescending(e => e.ExamDate)
        //        .ToListAsync();

        //    var blocks = await _context.Blocks
        //        .Include(b => b.Hall)
        //        .OrderBy(b => b.Hall.HallName)
        //        .ThenBy(b => b.BlockName)
        //        .Select(b => new {
        //            BlockId = b.BlockId,
        //            DisplayName = $"{b.Hall.HallName} - {b.BlockName}"
        //        })
        //        .ToListAsync();

        //    ViewBag.Exams = new SelectList(exams, "ExamId", "Subject.SubjectName");
        //    ViewBag.Blocks = new SelectList(blocks, "BlockId", "DisplayName");

        //    return View();
        //}

        //// ============================================================
        //// 9. توليد بيانات محضر التوزيع الفعلي للمبنى المختار - PrintControlSheet 🌟
        //// ============================================================
        //[HttpPost]
        //public async Task<IActionResult> PrintControlSheet(int examId, int blockId)
        //{
        //    var exam = await _context.Exams
        //        .Include(e => e.Subject)
        //        .FirstOrDefaultAsync(e => e.ExamId == examId);

        //    var block = await _context.Blocks
        //        .Include(b => b.Hall)
        //        .FirstOrDefaultAsync(b => b.BlockId == blockId);

        //    if (exam == null || block == null) return NotFound();

        //    var allAssignments = await _context.CommitteesAssignments
        //        .Include(a => a.Person)
        //        .Include(a => a.Block)
        //        .Include(a => a.Committee).ThenInclude(c => c.Block)
        //        .Include(a => a.ExamSchedule)
        //        .Where(a => a.ExamSchedule.ExamId == examId &&
        //                   (a.BlockId == blockId || (a.Committee != null && a.Committee.BlockId == blockId)))
        //        .ToListAsync();

        //    if (!allAssignments.Any())
        //    {
        //        TempData["ErrorMessage"] = $"عفواً، لا توجد تكليفات أو طواقم مراقبة مسجلة لمبنى ({block.BlockName}) في مادة ({exam.Subject.SubjectName}) حتى الآن.";
        //        return RedirectToAction(nameof(PreparePrintSheet));
        //    }

        //    var viewModel = new ExamControlSheetViewModel
        //    {
        //        SubjectName = exam.Subject?.SubjectName ?? "---",
        //        TargetYear = exam.Subject?.AcademicYear.ToString() ?? "---",
        //        ExamDate = exam.ExamDate.ToString("yyyy/MM/dd"),
        //        ExamDay = exam.ExamDate.ToString("dddd", new System.Globalization.CultureInfo("ar-EG")),
        //        ExamTime = $"{DateTime.Today.Add(exam.StartTime):hh:mm tt} - {DateTime.Today.Add(exam.EndTime):hh:mm tt}",
        //        HallName = $"{block.Hall?.HallName ?? "---"} - {block.BlockName}",

        //        // طاقم قطاع 1
        //        MainHead1 = allAssignments.FirstOrDefault(a => a.RoleType == "رئيس صالة أساسي (القطاع الأول)")?.Person?.FullName ?? "................",
        //        ReserveHead1 = allAssignments.FirstOrDefault(a => a.RoleType == "رئيس صالة احتياطي")?.Person?.FullName ?? "................",
        //        ReserveObserver1 = allAssignments.FirstOrDefault(a => a.RoleType == "مراقب احتياطي للصالة (تحت إدارة رئيس الصالة)")?.Person?.FullName ?? "................",

        //        // طاقم قطاع 2
        //        MainHead2 = allAssignments.FirstOrDefault(a => a.RoleType == "رئيس صالة أساسي (القطاع الثاني)")?.Person?.FullName ?? "................",
        //        ReserveHead2 = "................",
        //        ReserveObserver2 = "................",

        //        // الطواقم الطبية العامة
        //        DoctorName = allAssignments.FirstOrDefault(a => a.RoleType == "دكتور الصالة")?.Person?.FullName ?? "................",
        //        NurseName = allAssignments.FirstOrDefault(a => a.RoleType == "ممرض الصالة")?.Person?.FullName ?? "................",

        //        Blocks = new List<BlockGroupItem>()
        //    };

        //    var blockItem = new BlockGroupItem
        //    {
        //        BlockName = block.BlockName,
        //        BlockObserverName = allAssignments.FirstOrDefault(a => a.BlockId == blockId && a.RoleType == "مراقب")?.Person?.FullName ?? "................",
        //        CommitteeObservers = new List<ObserverRowItem>()
        //    };

        //    // جلب الملاحظين الفعليين داخل اللجان التابعة للبلوك المختار
        //    var activeObservers = allAssignments
        //        .Where(a => a.Committee != null && a.Committee.BlockId == blockId && a.RoleType == "ملاحظ لجنة" && a.Person != null)
        //        .OrderBy(a => a.Committee.CommitteeNumber)
        //        .Select(a => new ObserverRowItem
        //        {
        //            ObserverName = a.Person.FullName,
        //            CommitteeNumber = "لجنة " + a.Committee.CommitteeNumber.ToString()
        //        })
        //        .ToList();

        //    blockItem.CommitteeObservers.AddRange(activeObservers);

        //    // جلب الملاحظين الاحتياطيين الـ 5% المسجلين في هذا المبنى
        //    var reserveObservers = allAssignments
        //        .Where(a => a.BlockId == blockId && a.RoleType == "ملاحظ احتياطي للكلية (تحت إدارة المراقب)" && a.Person != null)
        //        .Select(a => new ObserverRowItem
        //        {
        //            ObserverName = a.Person.FullName,
        //            CommitteeNumber = "ملاحظ احتياطي بالمبنى"
        //        })
        //        .ToList();

        //    blockItem.CommitteeObservers.AddRange(reserveObservers);
        //    viewModel.Blocks.Add(blockItem);

        //    viewModel.ReserveNotes1.Add("طاقم القطاع الأول ملتزم بالخطة الزمنية لتوزيع الأسئلة.");
        //    viewModel.ReserveNotes2.Add("طاقم القطاع الثاني مستعد لأي حالات طارئة أو غياب.");

        //    return View(viewModel);
        //}
        [HttpGet]
        public async Task<IActionResult> PreparePrintSheet()
        {
            var exams = await _context.Exams
                .Include(e => e.Subject)
                .OrderByDescending(e => e.ExamDate)
                .ToListAsync();

            var blocks = await _context.Blocks
                .Include(b => b.Hall)
                .OrderBy(b => b.Hall.HallName)
                .ThenBy(b => b.BlockName)
                .Select(b => new {
                    BlockId = b.BlockId,
                    DisplayName = $"{b.Hall.HallName} - {b.BlockName}"
                })
                .ToListAsync();

            ViewBag.Exams = new SelectList(exams, "ExamId", "Subject.SubjectName");
            ViewBag.Blocks = new SelectList(blocks, "BlockId", "DisplayName");

            return View();
        }

        // ============================================================
        // 9. توليد بيانات محضر التوزيع الفعلي للمبنى المختار - PrintControlSheet 🌟
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrintControlSheet(int examId, int blockId)
        {
            var exam = await _context.Exams
                .Include(e => e.Subject)
                .FirstOrDefaultAsync(e => e.ExamId == examId);

            var block = await _context.Blocks
                .Include(b => b.Hall)
                .FirstOrDefaultAsync(b => b.BlockId == blockId);

            if (exam == null || block == null) return NotFound();

            var allAssignments = await _context.CommitteesAssignments
                .Include(a => a.Person)
                .Include(a => a.Block)
                .Include(a => a.Committee).ThenInclude(c => c.Block)
                .Include(a => a.ExamSchedule)
                .Where(a => a.ExamSchedule.ExamId == examId &&
                           (a.BlockId == blockId || (a.Committee != null && a.Committee.BlockId == blockId)))
                .ToListAsync();

            if (!allAssignments.Any())
            {
                TempData["ErrorMessage"] = $"عفواً، لا توجد تكليفات أو طواقم مراقبة مسجلة لمبنى ({block.BlockName}) في مادة ({exam.Subject.SubjectName}) حتى الآن.";
                return RedirectToAction(nameof(PreparePrintSheet));
            }

            var viewModel = new ExamControlSheetViewModel
            {
                SubjectName = exam.Subject?.SubjectName ?? "---",
                TargetYear = exam.Subject?.AcademicYear.ToString() ?? "---",
                ExamDate = exam.ExamDate.ToString("yyyy/MM/dd"),
                ExamDay = exam.ExamDate.ToString("dddd", new System.Globalization.CultureInfo("ar-EG")),
                ExamTime = $"{DateTime.Today.Add(exam.StartTime):hh:mm tt} - {DateTime.Today.Add(exam.EndTime):hh:mm tt}",
                HallName = $"{block.Hall?.HallName ?? "---"} - {block.BlockName}",

                // طاقم قطاع 1
                MainHead1 = allAssignments.FirstOrDefault(a => a.RoleType == "رئيس صالة أساسي (القطاع الأول)")?.Person?.FullName ?? "................",
                ReserveHead1 = allAssignments.FirstOrDefault(a => a.RoleType == "رئيس صالة احتياطي")?.Person?.FullName ?? "................",
                ReserveObserver1 = allAssignments.FirstOrDefault(a => a.RoleType == "مراقب احتياطي للصالة (تحت إدارة رئيس الصالة)")?.Person?.FullName ?? "................",

                // طاقم قطاع 2
                MainHead2 = allAssignments.FirstOrDefault(a => a.RoleType == "رئيس صالة أساسي (القطاع الثاني)")?.Person?.FullName ?? "................",
                ReserveHead2 = "................",
                ReserveObserver2 = "................",

                // الطواقم الطبية العامة
                DoctorName = allAssignments.FirstOrDefault(a => a.RoleType == "دكتور الصالة")?.Person?.FullName ?? "................",
                NurseName = allAssignments.FirstOrDefault(a => a.RoleType == "ممرض الصالة")?.Person?.FullName ?? "................",

                Blocks = new List<BlockGroupItem>()
            };

            var blockItem = new BlockGroupItem
            {
                BlockName = block.BlockName,
                BlockObserverName = allAssignments.FirstOrDefault(a => a.BlockId == blockId && a.RoleType == "مراقب")?.Person?.FullName ?? "................",
                CommitteeObservers = new List<ObserverRowItem>()
            };

            // جلب الملاحظين الفعليين داخل اللجان التابعة للبلوك المختار
            var activeObservers = allAssignments
                .Where(a => a.Committee != null && a.Committee.BlockId == blockId && a.RoleType == "ملاحظ لجنة" && a.Person != null)
                .OrderBy(a => a.Committee.CommitteeNumber)
                .Select(a => new ObserverRowItem
                {
                    ObserverName = a.Person.FullName,
                    CommitteeNumber = "لجنة " + a.Committee.CommitteeNumber.ToString()
                })
                .ToList();

            blockItem.CommitteeObservers.AddRange(activeObservers);

            // جلب الملاحظين الاحتياطيين الـ 5% المسجلين في هذا المبنى
            var reserveObservers = allAssignments
                .Where(a => a.BlockId == blockId && a.RoleType == "ملاحظ احتياطي للكلية (تحت إدارة المراقب)" && a.Person != null)
                .Select(a => new ObserverRowItem
                {
                    ObserverName = a.Person.FullName,
                    CommitteeNumber = "ملاحظ احتياطي بالمبنى"
                })
                .ToList();

            blockItem.CommitteeObservers.AddRange(reserveObservers);
            viewModel.Blocks.Add(blockItem);

            // ملء الملاحظات الافتراضية للقطاعات لحماية الـ View من حقول الـ Null
            viewModel.ReserveNotes1.Add("طاقم القطاع الأول ملتزم بالخطة الزمنية لتوزيع الأسئلة.");
            viewModel.ReserveNotes2.Add("طاقم القطاع الثاني مستعد لأي حالات طارئة أو غياب.");

            return View(viewModel);
        }

        // ============================================================
        // 10. Helpers روابط مساعدة لإدخال البيانات يدوياً
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

            ViewBag.PersonID = new SelectList(activeStaff, "PersonId", "FullNameWithJob", assignment?.PersonId);
            ViewBag.RoleID = new SelectList(_context.Roles, "RoleId", "RoleDescription", assignment?.RoleId);

            int? effectiveHallId = assignment?.HallId;

            if (effectiveHallId == null && assignment != null)
            {
                if (assignment.CommitteeId != null)
                {
                    var com = _context.Committees.Include(c => c.Block).FirstOrDefault(c => c.CommitteeId == assignment.CommitteeId);
                    effectiveHallId = com?.Block?.HallId;
                }
                else if (assignment.BlockId != null)
                {
                    var block = _context.Blocks.FirstOrDefault(b => b.BlockId == assignment.BlockId);
                    effectiveHallId = block?.HallId;
                }
            }

            ViewBag.HallId = new SelectList(_context.Halls, "HallId", "HallName", effectiveHallId);

            if (effectiveHallId != null)
            {
                ViewBag.BlockId = new SelectList(_context.Blocks.Where(b => b.HallId == effectiveHallId), "BlockId", "BlockName", assignment?.BlockId);
                ViewBag.CommitteeID = new SelectList(_context.Committees.Where(c => c.Block.HallId == effectiveHallId), "CommitteeId", "CommitteeNumber", assignment?.CommitteeId);
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

        private bool AssignmentExists(int id) => _context.CommitteesAssignments.Any(e => e.AssignmentId == id);
    }
}