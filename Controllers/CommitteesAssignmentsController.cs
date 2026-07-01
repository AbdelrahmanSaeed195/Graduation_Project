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
                .Include(a => a.ExamLocation)
                    .ThenInclude(l => l.ParentLocation)
                    .ThenInclude(p => p.ParentLocation)
                    .ThenInclude(r => r.ParentLocation)
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

                query = query.Where(a => (a.ExamSchedule != null &&
                                          a.ExamSchedule.Exam != null &&
                                          a.ExamSchedule.Exam.Subject != null &&
                                          a.ExamSchedule.Exam.Subject.AcademicYear == targetLevel)
                                          || a.ExamLocation.Type == LocationType.Hall
                                          || a.ExamLocation.Type == LocationType.Row
                                          || a.RoleType.Contains("رئيس")
                                          || a.RoleType.Contains("مراقب احتياطي")
                                          || a.RoleType.Contains("دكتور"));
            }

            var assignments = await query
                .OrderByDescending(a => a.RoleType.Contains("رئيس"))
                .ThenByDescending(a => a.RoleType.Contains("مراقب"))
                .ThenBy(a => a.LocationId == null)
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
            var mainHalls = await _context.ExamLocations
                .Where(l => l.Type == LocationType.Hall)
                .OrderBy(l => l.LocationName)
                .ToListAsync();

            ViewBag.Halls = new SelectList(mainHalls, "LocationId", "LocationName");

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
        public async Task<IActionResult> RunAutoAssign(int locationId, int examId)
        {
            var schedule = await _context.ExamSchedules
                .Include(s => s.Exam)
                .Include(s => s.ExamLocation)
                .FirstOrDefaultAsync(s => s.ExamId == examId && s.LocationId == locationId);

            if (schedule == null)
            {
                TempData["Error"] = "عفواً، لا توجد جلسة حجز رئيسية مسجلة لهذا الامتحان داخل هذا الجراش حالياً.";
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
                TempData["Success"] = "تم تشغيل التوزيع التلقائي الذكي وحفظ طاقم العمل والملاحظين بالمقرات بنجاح.";
            else
                TempData["Error"] = "فشل التوزيع التلقائي، تأكد من وجود طاقة بشرية كافية أو تحقق من عدم تخطي القادة لسقوف التكليفات.";

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
                .Include(a => a.ExamLocation).ThenInclude(l => l.ParentLocation)
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

        // ============================================================
        // 5. Create (POST) - مدعوم بفحص السقف المتغير ديناميكياً
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CommitteesAssignment assignment)
        {
            if (ModelState.IsValid)
            {
                var person = await _context.Persons.FindAsync(assignment.PersonId);
                if (person != null && (person.JobRole == JobTitle.Professor ||
                                       person.JobRole == JobTitle.AssistantProfessor ||
                                       person.JobRole == JobTitle.ProfessorEmeritus))
                {
                    string currentYearCode = "2025 / 2026";

                    int maxAllowedLimit = await _context.AssignmentSettings
                        .Where(s => s.AcademicYearCode == currentYearCode && s.JobRole == person.JobRole)
                        .Select(s => s.MaxAssignmentsLimit)
                        .FirstOrDefaultAsync();

                    if (maxAllowedLimit == 0) maxAllowedLimit = 4; // قيمة افتراضية لحماية المنظومة

                    int currentAssignmentsCount = await _context.CommitteesAssignments
                        .CountAsync(a => a.PersonId == assignment.PersonId);

                    if (currentAssignmentsCount >= maxAllowedLimit)
                    {
                        ModelState.AddModelError("", $"عفواً، تخطى عضو هيئة التدريس ({person.FullName}) الحد الأقصى المسموح به لهذه السنة وهو ({maxAllowedLimit} تكليفات) على مستوى الكلية.");
                        await LoadDropdownsAsync(assignment);
                        return View(assignment);
                    }
                }

                var role = await _context.Roles.FindAsync(assignment.RoleId);
                assignment.RoleType = role?.RoleDescription ?? "تكليف يدوي";
                assignment.AssignmentType = "Manual";
                _context.Add(assignment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            await LoadDropdownsAsync(assignment);
            return View(assignment);
        }

        // ============================================================
        // 6. Edit (GET)
        // ============================================================
        public async Task<IActionResult> Edit(int id)
        {
            var assignment = await _context.CommitteesAssignments
                .Include(a => a.ExamLocation)
                    .ThenInclude(l => l.ParentLocation)
                .FirstOrDefaultAsync(a => a.AssignmentId == id);

            if (assignment == null) return NotFound();

            ViewBag.SelectedHallId = assignment.ExamLocation?.ParentLocation?.ParentLocationId
                                  ?? assignment.ExamLocation?.ParentLocationId
                                  ?? assignment.LocationId;

            ViewBag.SelectedBlockId = assignment.ExamLocation?.ParentLocationId != null
                                    ? assignment.LocationId
                                    : null;

            await LoadDropdownsAsync(assignment);
            return View(assignment);
        }

        // ============================================================
        // 6. Edit (POST) - مدعوم بحماية السقف الديناميكي أثناء التعديل الحركي
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CommitteesAssignment assignment)
        {
            if (id != assignment.AssignmentId) return NotFound();

            var current = await _context.CommitteesAssignments
                .AsNoTracking()
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .FirstOrDefaultAsync(a => a.AssignmentId == id);

            if (current == null) return NotFound();

            var now = DateTime.Now;
            var examDate = current.ExamSchedule.Exam.ExamDate.Date;
            var examStart = examDate.Add(current.ExamSchedule.Exam.StartTime);
            var examEnd = examDate.Add(current.ExamSchedule.Exam.EndTime);

            var allowedStartTime = examStart.AddHours(-2);
            var allowedEndTime = examEnd.AddHours(2);

            if (now < allowedStartTime || now > allowedEndTime)
            {
                ModelState.AddModelError("", "عذراً، التعديل متاح فقط في الفترة من ساعتين قبل الامتحان وحتى ساعتين بعده.");
                await LoadDropdownsAsync(assignment);
                return View(assignment);
            }

            // ✅ الفحص المطور لحماية السقف أثناء التعديل
            var person = await _context.Persons.FindAsync(assignment.PersonId);
            if (person != null && (person.JobRole == JobTitle.Professor ||
                                   person.JobRole == JobTitle.AssistantProfessor ||
                                   person.JobRole == JobTitle.ProfessorEmeritus))
            {
                string currentYearCode = "2025 / 2026";

                int maxAllowedLimit = await _context.AssignmentSettings
                    .Where(s => s.AcademicYearCode == currentYearCode && s.JobRole == person.JobRole)
                    .Select(s => s.MaxAssignmentsLimit)
                    .FirstOrDefaultAsync();

                if (maxAllowedLimit == 0) maxAllowedLimit = 4;

                // استثناء نفس السجل الحالي (AssignmentId != id) لكي لا يحسبه كتكليف إضافي عند التعديل
                int currentAssignmentsCount = await _context.CommitteesAssignments
                    .CountAsync(a => a.PersonId == assignment.PersonId && a.AssignmentId != id);

                if (currentAssignmentsCount >= maxAllowedLimit)
                {
                    ModelState.AddModelError("", $"عفواً، لا يمكن نقله أو تعديله لهذا الموظف لأنه تخطى السقف المسموح له وهو ({maxAllowedLimit} تكليفات).");
                    await LoadDropdownsAsync(assignment);
                    return View(assignment);
                }
            }

            bool isDuplicate = await _context.CommitteesAssignments
                .AnyAsync(a => a.PersonId == assignment.PersonId
                            && a.ExamScheduleId == assignment.ExamScheduleId
                            && a.AssignmentId != id);

            if (isDuplicate)
            {
                ModelState.AddModelError("", "هذا الموظف لديه بالفعل تكليف في نفس الامتحان.");
                await LoadDropdownsAsync(assignment);
                return View(assignment);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Entry(assignment).State = EntityState.Modified;
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AssignmentExists(id)) return NotFound();
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
                .Include(a => a.ExamLocation)
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

            var mainHalls = await _context.ExamLocations
                .Where(l => l.Type == LocationType.Hall)
                .OrderBy(l => l.LocationName)
                .ToListAsync();

            ViewBag.Exams = new SelectList(exams, "ExamId", "Subject.SubjectName");
            ViewBag.Halls = new SelectList(mainHalls, "LocationId", "LocationName");

            return View();
        }

        // ============================================================
        // 9. PrintControlSheet (POST)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrintControlSheet(int examId, int locationId)
        {
            var exam = await _context.Exams
                .Include(e => e.Subject)
                .FirstOrDefaultAsync(e => e.ExamId == examId);

            var hall = await _context.ExamLocations
                .FirstOrDefaultAsync(l => l.LocationId == locationId && l.Type == LocationType.Hall);

            if (exam == null || hall == null) return NotFound();

            var allAssignments = await _context.CommitteesAssignments
                .Include(a => a.Person)
                .Include(a => a.ExamLocation).ThenInclude(l => l.ParentLocation).ThenInclude(p => p.ParentLocation)
                .Include(a => a.ExamSchedule)
                .Where(a => a.ExamSchedule.ExamId == examId &&
                            (a.LocationId == locationId ||
                             a.ExamLocation.ParentLocationId == locationId ||
                             a.ExamLocation.ParentLocation.ParentLocationId == locationId ||
                             a.ExamLocation.ParentLocation.ParentLocation.ParentLocationId == locationId))
                .ToListAsync();

            if (!allAssignments.Any())
            {
                TempData["ErrorMessage"] = $"عفواً، لا توجد تكليفات مسجلة لموقع ({hall.LocationName}).";
                return RedirectToAction(nameof(PreparePrintSheet));
            }

            var pagesList = new List<ExamControlSheetViewModel>();

            var rowsInHall = await _context.ExamLocations
                .Where(l => l.ParentLocationId == locationId && l.Type == LocationType.Row)
                .OrderBy(l => l.LocationName)
                .ToListAsync();

            bool hasTwoFloors = (hall.Floor == 2);

            foreach (var r in rowsInHall)
            {
                var blocksInRow = await _context.ExamLocations
                    .Where(l => l.ParentLocationId == r.LocationId && l.Type == LocationType.Block)
                    .OrderBy(l => l.LocationName)
                    .ToListAsync();

                var rowBlockGroups = new List<BlockGroupItem>();

                foreach (var b in blocksInRow)
                {
                    var activeObservers = allAssignments
                        .Where(a => a.ExamLocation != null && a.ExamLocation.ParentLocationId == b.LocationId && a.RoleType == "ملاحظ لجنة")
                        .Select(a => new ObserverRowItem
                        {
                            ObserverName = a.Person.FullName,
                            CommitteeNumber = a.ExamLocation.LocationName,
                            BookletsCount = a.ExamLocation.StudentCapacity ?? 0
                        }).ToList();

                    var blockReserveObservers = allAssignments
                        .Where(a => a.LocationId == b.LocationId && a.RoleType == "ملاحظ احتياطي")
                        .Select(a => new ObserverRowItem
                        {
                            ObserverName = a.Person.FullName,
                            CommitteeNumber = "احتياطي صالة",
                            BookletsCount = 0
                        }).ToList();

                    var combinedObservers = activeObservers.Concat(blockReserveObservers).ToList();

                    rowBlockGroups.Add(new BlockGroupItem
                    {
                        BlockName = b.LocationName,
                        FloorNumber = hall.Floor,
                        BlockObserverName = allAssignments.FirstOrDefault(a => a.LocationId == b.LocationId && a.RoleType == "مراقب")?.Person?.FullName ?? "................",
                        CommitteeObservers = combinedObservers,
                        TotalStudentsInBlock = combinedObservers.Sum(o => o.BookletsCount)
                    });
                }

                var pageModel = new ExamControlSheetViewModel
                {
                    SubjectName = exam.Subject?.SubjectName ?? "---",
                    TargetYear = exam.Subject?.AcademicYear switch { AcademicLevel.FirstYear => "المستوى الأول", AcademicLevel.SecondYear => "المستوى الثاني", AcademicLevel.ThirdYear => "المستوى الثالث", AcademicLevel.FourthYear => "المستوى الرابع", _ => "---" },
                    ExamDate = exam.ExamDate.ToString("yyyy/MM/dd"),
                    ExamDay = exam.ExamDate.ToString("dddd", new System.Globalization.CultureInfo("ar-EG")),
                    ExamTime = $"{DateTime.Today.Add(exam.StartTime):hh:mm tt} - {DateTime.Today.Add(exam.EndTime):hh:mm tt}",
                    HallName = $"{hall.LocationName} - {r.LocationName}",

                    MainHead1 = allAssignments.FirstOrDefault(a => a.RoleType == "رئيس جراش" && a.SubRoleType == "قطاع أول")?.Person?.FullName ?? "................",
                    MainHead2 = allAssignments.FirstOrDefault(a => a.RoleType == "رئيس جراش" && a.SubRoleType == "قطاع ثاني")?.Person?.FullName ?? "................",
                    MainHead3 = hasTwoFloors ? (allAssignments.FirstOrDefault(a => a.RoleType == "رئيس جراش" && a.SubRoleType == "قطاع ثالث")?.Person?.FullName ?? "................") : null,

                    ReserveHead1 = hasTwoFloors
                        ? (allAssignments.FirstOrDefault(a => a.RoleType == "رئيس جراش احتياطي" && a.SubRoleType == "احتياطي دور أول")?.Person?.FullName ?? "................")
                        : (allAssignments.FirstOrDefault(a => a.RoleType == "رئيس جراش احتياطي" && a.SubRoleType == "احتياطي")?.Person?.FullName ?? "................"),

                    ReserveHead2 = hasTwoFloors ? (allAssignments.FirstOrDefault(a => a.RoleType == "رئيس جراش احتياطي" && a.SubRoleType == "احتياطي دور ثاني")?.Person?.FullName ?? "................") : null,

                    ReserveObserver1 = hasTwoFloors
                        ? (allAssignments.FirstOrDefault(a => a.RoleType == "مراقب احتياطي" && a.SubRoleType == "الدور الأول")?.Person?.FullName ?? "................")
                        : (allAssignments.FirstOrDefault(a => a.RoleType == "مراقب احتياطي" && a.SubRoleType == "لجراش")?.Person?.FullName ?? "................"),

                    ReserveObserver2 = hasTwoFloors ? (allAssignments.FirstOrDefault(a => a.RoleType == "مراقب احتياطي" && a.SubRoleType == "الدور الثاني")?.Person?.FullName ?? "................") : null,

                    DoctorName = allAssignments.FirstOrDefault(a => a.RoleType == "دكتور")?.Person?.FullName ?? "................",
                    NurseName = allAssignments.FirstOrDefault(a => a.RoleType == "مساعد دكتور")?.Person?.FullName ?? "................",

                    Blocks = rowBlockGroups,
                    GeneralReserveObservers = allAssignments.Where(a => a.RoleType == "ملاحظ احتياطي" && a.SubRoleType == "احتياطي صالة").Select(a => a.Person.FullName).ToList()
                };

                pagesList.Add(pageModel);
            }

            return View(pagesList);
        }
        // ============================================================
        // 10. PrintHallStructureSheet (GET) - شاشة اختيار الجراش لمعاينة هيكله
        // ============================================================
        [HttpGet]
        public async Task<IActionResult> PrepareHallStructurePrint()
        {
            var mainHalls = await _context.ExamLocations
                .Where(l => l.Type == LocationType.Hall)
                .OrderBy(l => l.LocationName)
                .ToListAsync();

            ViewBag.Halls = new SelectList(mainHalls, "LocationId", "LocationName");
            return View();
        }

        // ============================================================
        // 11. PrintHallStructureSheet (POST) - توليد كشف الهيكل الإحصائي للجراش
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrintHallStructureSheet(int locationId)
        {
            var hall = await _context.ExamLocations
                .FirstOrDefaultAsync(l => l.LocationId == locationId && l.Type == LocationType.Hall);

            if (hall == null) return NotFound();

            // 1. سحب الصفوف داخل الجراش
            var rows = await _context.ExamLocations
                .Where(l => l.ParentLocationId == locationId && l.Type == LocationType.Row)
                .OrderBy(l => l.LocationName)
                .ToListAsync();

            var rowIds = rows.Select(r => r.LocationId).ToList();

            // 2. سحب الصالات داخل هذه الصفوف
            var blocks = await _context.ExamLocations
                .Where(l => l.ParentLocationId != null && rowIds.Contains(l.ParentLocationId.Value) && l.Type == LocationType.Block)
                .OrderBy(l => l.LocationName)
                .ToListAsync();

            var blockIds = blocks.Select(b => b.LocationId).ToList();

            // 3. سحب اللجان الدقيقة داخل الصالات
            var committees = await _context.ExamLocations
                .Where(l => l.ParentLocationId != null && blockIds.Contains(l.ParentLocationId.Value) && l.Type == LocationType.Committee)
                .OrderBy(l => l.LocationName)
                .ToListAsync();

            // تمرير البيانات للـ View عبر الـ ViewBag بشكل منظم
            ViewBag.Hall = hall;
            ViewBag.Rows = rows;
            ViewBag.Blocks = blocks;
            ViewBag.Committees = committees;

            // حساب إجمالي السعة الشاملة للجراش بالكامل
            ViewBag.TotalHallCapacity = committees.Sum(c => c.StudentCapacity ?? 0);

            return View();
        }
        // ============================================================
        // 12. Ajax Helpers
        // ============================================================
        [HttpGet]
        public async Task<JsonResult> GetHallDetails(int hallId)
        {
            var rows = await _context.ExamLocations
                .Where(r => r.ParentLocationId == hallId && r.Type == LocationType.Row)
                .Select(r => new { id = r.LocationId, name = r.LocationName })
                .ToListAsync();

            return Json(new { rows = rows });
        }

        // ============================================================
        // 13. LoadDropdownsAsync
        // ============================================================
        private async Task LoadDropdownsAsync(CommitteesAssignment assignment = null)
        {
            var staffList = await _context.Persons.Where(p => p.IsActiveForAssignment).AsNoTracking().ToListAsync() ?? new List<Person>();

            var activeStaff = staffList.Select(p => new {
                PersonId = p.PersonId,
                FullNameWithJob = $"{p.FullName} ({GetEnumDisplayName(p.JobRole)})"
            }).ToList();

            ViewBag.PersonId = new SelectList(activeStaff, "PersonId", "FullNameWithJob", assignment?.PersonId);

            ViewBag.JobTitlesJson = Newtonsoft.Json.JsonConvert.SerializeObject(
                Enum.GetValues(typeof(JobTitle))
                    .Cast<JobTitle>()
                    .ToDictionary(e => e.ToString(), e => GetEnumDisplayName(e))
            );

            var roles = await _context.Roles.AsNoTracking().ToListAsync() ?? new List<Role>();
            ViewBag.RoleId = new SelectList(roles, "RoleID", "RoleDescription", assignment?.RoleId);

            var mainHalls = await _context.ExamLocations.Where(l => l.Type == LocationType.Hall).AsNoTracking().ToListAsync();
            ViewBag.LocationId = new SelectList(mainHalls, "LocationId", "LocationName", assignment?.LocationId);

            var dbSchedules = await _context.ExamSchedules
                .Include(s => s.Exam).ThenInclude(e => e.Subject)
                .Include(s => s.ExamLocation)
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
                    string startTime = s.Exam != null ? DateTime.Today.Add(s.Exam.StartTime).ToString("hh:mm tt") : "00:00";
                    return new { ExamScheduleId = s.ExamScheduleId, Name = $"{subjectName} - ({arabicLevel}) | 📅 {examDate} ({startTime}) | 📍 [{s.ExamLocation?.LocationName ?? "جراش المادة"}]" };
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