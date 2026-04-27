using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using projectweb.Services;

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

        // 1. Index: عرض التوزيعات مع تصفية تلقائية لأحدث جلسة
        public async Task<IActionResult> Index(int? scheduleId)
        {
            // إذا لم يتم اختيار جلسة، قم بجلب آخر جلسة تم إضافتها تلقائياً
            if (!scheduleId.HasValue)
            {
                scheduleId = await _context.ExamSchedules
                    .OrderByDescending(s => s.ExamScheduleId)
                    .Select(s => s.ExamScheduleId)
                    .FirstOrDefaultAsync();
            }

            ViewBag.CurrentScheduleId = scheduleId;

            var query = _context.CommitteesAssignments
                .Include(a => a.Person)
                .Include(a => a.Role)
                .Include(a => a.Hall)
                .Include(a => a.Block)
                .Include(a => a.Committee)
                .Include(a => a.ExamSchedule.Exam.Subject)
                .Where(a => a.ExamScheduleId == scheduleId) // عرض تكليفات الجلسة المحددة فقط
                .AsQueryable();

            return View(await query.ToListAsync());
        }

        // 2. Details: عرض تفاصيل التكليف بالكامل
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var assignment = await _context.CommitteesAssignments
                .Include(a => a.Person)
                .Include(a => a.Role)
                .Include(a => a.Hall)
                .Include(a => a.Block)
                .Include(a => a.Committee)
                .Include(a => a.ExamSchedule.Exam.Subject)
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

        // 4. Create (POST): مع إضافة فحص التكرار ومنع الخطأ
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CommitteesAssignment assignment)
        {
            if (ModelState.IsValid)
            {
                // أ- فحص هل اللجنة المختارة لها مراقب بالفعل في هذه الجلسة؟
                if (assignment.CommitteeID.HasValue)
                {
                    var committeeHasObserver = await _context.CommitteesAssignments
                        .AnyAsync(a => a.ExamScheduleId == assignment.ExamScheduleId &&
                                       a.CommitteeID == assignment.CommitteeID);

                    if (committeeHasObserver)
                    {
                        ModelState.AddModelError("", "عفواً، هذه اللجنة تم تخصيص مراقب لها بالفعل في هذه الجلسة!");
                        LoadDropdowns(assignment);
                        return View(assignment);
                    }
                }

                // ب- فحص هل الموظف مشغول بتكليف آخر في نفس الجلسة؟ (لمنع Duplicate Key Error)
                var isBusy = await _context.CommitteesAssignments
                    .AnyAsync(a => a.ExamScheduleId == assignment.ExamScheduleId &&
                                   a.PersonID == assignment.PersonID);

                if (isBusy)
                {
                    ModelState.AddModelError("", "هذا الموظف لديه تكليف آخر بالفعل في نفس موعد هذه الجلسة!");
                    LoadDropdowns(assignment);
                    return View(assignment);
                }

                assignment.AssignmentType = "Manual";
                var role = await _context.Roles.FindAsync(assignment.RoleID);
                assignment.RoleType = role?.RoleDescription ?? "تكليف";

                _context.Add(assignment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { scheduleId = assignment.ExamScheduleId });
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
                    // 1. فحص هل الموظف الجديد (بعد التعديل) مشغول في نفس الجلسة؟
                    // بنستثني التكليف الحالي (assignment.AssignmentID) عشان ميفحصش نفسه
                    var isBusy = await _context.CommitteesAssignments
                        .AnyAsync(a => a.ExamScheduleId == assignment.ExamScheduleId &&
                                       a.PersonID == assignment.PersonID &&
                                       a.AssignmentID != assignment.AssignmentID);

                    if (isBusy)
                    {
                        ModelState.AddModelError("", "عفواً، هذا الموظف لديه تكليف آخر بالفعل في هذه الجلسة!");
                        LoadDropdowns(assignment);
                        return View(assignment);
                    }

                    // 2. فحص هل اللجنة المختارة محجوزة لشخص آخر؟
                    if (assignment.CommitteeID.HasValue)
                    {
                        var committeeTaken = await _context.CommitteesAssignments
                            .AnyAsync(a => a.ExamScheduleId == assignment.ExamScheduleId &&
                                           a.CommitteeID == assignment.CommitteeID &&
                                           a.AssignmentID != assignment.AssignmentID);

                        if (committeeTaken)
                        {
                            ModelState.AddModelError("", "هذه اللجنة تم تخصيص مراقب آخر لها بالفعل!");
                            LoadDropdowns(assignment);
                            return View(assignment);
                        }
                    }

                    // لو كل الفحوصات سليمة، نفذ التعديل
                    _context.Update(assignment);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index), new { scheduleId = assignment.ExamScheduleId });
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
                .Include(a => a.ExamSchedule.Exam.Subject)
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
            int? scheduleId = assignment?.ExamScheduleId;
            if (assignment != null) _context.CommitteesAssignments.Remove(assignment);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index), new { scheduleId = scheduleId });
        }

        // 9. AutoAssign: استدعاء الخدمة الذكية
        [HttpPost]
        public async Task<IActionResult> AutoAssign(int scheduleId)
        {
            var success = await _assignmentService.RunAssignmentAsync(scheduleId);
            if (success) TempData["Success"] = "تم التوزيع التلقائي بنجاح وفقاً للهيكل الإداري.";
            else TempData["Error"] = "فشل التوزيع التلقائي، تأكد من توفر الموظفين المتاحين.";

            return RedirectToAction(nameof(Index), new { scheduleId = scheduleId });
        }

        // دالة موحدة لتحميل البيانات مع تصفية الموظفين النشطين
        private void LoadDropdowns(CommitteesAssignment? assignment = null)
        {
            // جلب الموظفين النشطين فقط لتسهيل الاختيار
            var activeStaff = _context.Persons.Where(p => p.IsActiveForAssignment).ToList();
            ViewBag.PersonID = new SelectList(activeStaff, "PersonId", "FullName", assignment?.PersonID);

            ViewBag.RoleID = new SelectList(_context.Roles, "RoleID", "RoleDescription", assignment?.RoleID);
            ViewBag.HallId = new SelectList(_context.Halls, "HallId", "HallName", assignment?.HallId);
            ViewBag.BlockId = new SelectList(_context.Blocks, "BlockID", "BlockName", assignment?.BlockId);
            ViewBag.CommitteeID = new SelectList(_context.Committees, "CommitteeID", "CommitteeNumber", assignment?.CommitteeID);

            var schedules = _context.ExamSchedules.Include(s => s.Exam.Subject).Select(s => new {
                Id = s.ExamScheduleId,
                Name = $"{s.Exam.Subject.SubjectName} - {s.ScheduledDate.ToShortDateString()}"
            });
            ViewBag.ExamScheduleId = new SelectList(schedules, "Id", "Name", assignment?.ExamScheduleId);
        }

        private bool AssignmentExists(int id) => _context.CommitteesAssignments.Any(e => e.AssignmentID == id);
    }
}