using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using projectweb.ViewModel;

namespace projectweb.Controllers
{
    public class StudentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================
        // INDEX
        // =====================================
        public async Task<IActionResult> Index()
        {
            var students = await _context.Students
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Committee)
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Exam)
                .OrderBy(s => s.AcademicYear)
                .ThenBy(s => s.FullName)
                .ToListAsync();

            return View(students);
        }

        // =====================================
        // SEARCH
        // =====================================
        [HttpGet]
        public async Task<IActionResult> Search(string searchTerm)
        {
            var query = _context.Students
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Committee)
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Exam)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower();
                query = query.Where(s => s.FullName.ToLower().Contains(searchTerm)
                                      || s.NationalId.Contains(searchTerm));
            }

            var students = await query
                .OrderBy(s => s.AcademicYear)
                .ThenBy(s => s.FullName)
                .ToListAsync();

            ViewBag.Search = searchTerm;
            return View("Index", students);
        }

        // =====================================
        // DETAILS
        // =====================================
        public async Task<IActionResult> Details(int id)
        {
            var student = await _context.Students
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Committee)
                .Include(s => s.Relatives)
                .FirstOrDefaultAsync(s => s.StudentId == id);

            if (student == null)
                return NotFound();

            return View(student);
        }

        // =====================================
        // CREATE
        // =====================================
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(StudentCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            model.NationalId = model.NationalId?.Replace(" ", "").Trim();

            bool exists = await _context.Students
                .AnyAsync(s => s.NationalId == model.NationalId);

            if (exists)
            {
                ModelState.AddModelError("NationalId", "هذا الرقم القومي مسجل بالفعل");
                return View(model);
            }

            var student = new Student
            {
                FullName = model.FullName,
                NationalId = model.NationalId,
                AcademicYear = model.AcademicYear,
                SeatNumber = 0,
                ExamScheduleId = null
            };

            _context.Students.Add(student);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("NationalId", "هذا الرقم القومي مسجل بالفعل");
                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // EDIT
        // =====================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var student = await _context.Students.FindAsync(id);

            if (student == null)
                return NotFound();

            var model = new StudentCreateViewModel
            {
                FullName = student.FullName,
                NationalId = student.NationalId,
                AcademicYear = student.AcademicYear
            };

            ViewBag.StudentId = student.StudentId;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, StudentCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var student = await _context.Students.FindAsync(id);
            if (student == null)
                return NotFound();

            model.NationalId = model.NationalId?.Replace(" ", "").Trim();

            bool exists = await _context.Students
                .AnyAsync(s => s.NationalId == model.NationalId && s.StudentId != id);

            if (exists)
            {
                ModelState.AddModelError("NationalId", "هذا الرقم القومي مستخدم لطالب آخر");
                return View(model);
            }

            student.FullName = model.FullName;
            student.NationalId = model.NationalId;
            student.AcademicYear = model.AcademicYear;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError("NationalId", "هذا الرقم القومي مستخدم بالفعل");
                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // DELETE
        // =====================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return BadRequest();

            var student = await _context.Students
                .Include(s => s.Relatives)
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Committee)
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null)
                return NotFound();

            return View(student);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _context.Students
                .Include(s => s.Relatives)
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null)
                return NotFound();

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // DISTRIBUTE STUDENTS
        // =====================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DistributeStudents()
        {
            // جيب الطلاب الغير موزعين
            var students = await _context.Students
                .Where(s => s.ExamScheduleId == null)
                .OrderBy(s => s.AcademicYear)
                .ThenBy(s => s.FullName)
                .ToListAsync();

            // جيب الجلسات مع اللجان والامتحانات
            var schedules = await _context.ExamSchedules
                .Include(e => e.Committee)
                .Include(e => e.Exam)
                .OrderBy(e => e.Exam.ExamDate)
                .ThenBy(e => e.Exam.StartTime)
                .ThenBy(e => e.Committee.CommitteeNumber)
                .ToListAsync();

            // جيب عدد الطلاب الحاليين في كل جلسة (query واحدة بدل N queries)
            var scheduleCounts = await _context.Students
                .Where(s => s.ExamScheduleId != null)
                .GroupBy(s => s.ExamScheduleId)
                .Select(g => new { ExamScheduleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ExamScheduleId!.Value, x => x.Count);

            // قسّم الطلاب على حسب فرقتهم
            var groupedStudents = students
                .GroupBy(s => s.AcademicYear)
                .ToList();

            foreach (var group in groupedStudents)
            {
                string yearAsString = group.Key.ToString();

                // الجلسات المخصصة لنفس الفرقة فقط
                var relevantSchedules = schedules
                    .Where(e => e.Exam.TargetAcademicYear == yearAsString)
                    .ToList();

                int scheduleIndex = 0;

                foreach (var student in group)
                {
                    // دور على جلسة فيها مكان
                    while (scheduleIndex < relevantSchedules.Count)
                    {
                        var schedule = relevantSchedules[scheduleIndex];
                        var currentCount = scheduleCounts.GetValueOrDefault(schedule.ExamScheduleId, 0);

                        if (currentCount < schedule.Committee.NumberOfStudent)
                            break;

                        scheduleIndex++;
                    }

                    // مفيش جلسات كافية لهذه الفرقة
                    if (scheduleIndex >= relevantSchedules.Count)
                        break;

                    var assignedSchedule = relevantSchedules[scheduleIndex];
                    student.ExamScheduleId = assignedSchedule.ExamScheduleId;

                    // حدّث العداد في الميموري
                    if (scheduleCounts.ContainsKey(assignedSchedule.ExamScheduleId))
                        scheduleCounts[assignedSchedule.ExamScheduleId]++;
                    else
                        scheduleCounts[assignedSchedule.ExamScheduleId] = 1;
                }
            }

            await _context.SaveChangesAsync();

            // أعد حساب أرقام الجلوس للجلسات المتأثرة فقط
            var affectedScheduleIds = students
                .Where(s => s.ExamScheduleId != null)
                .Select(s => s.ExamScheduleId!.Value)
                .Distinct()
                .ToList();

            foreach (var scheduleId in affectedScheduleIds)
                await RecalculateSeatNumbers(scheduleId);

            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // STUDENT COMMITTEE
        // =====================================
        public async Task<IActionResult> Committee(int id)
        {
            var student = await _context.Students
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Committee)
                .FirstOrDefaultAsync(s => s.StudentId == id);

            if (student == null)
                return NotFound();

            if (student.ExamSchedule == null)
            {
                ViewBag.Message = "هذا الطالب لم يُعيَّن بعد";
                return View("NoCommittee");
            }

            var model = new StudentCommitteeViewModel
            {
                StudentId = student.StudentId,
                FullName = student.FullName,
                AcademicYear = student.AcademicYear,
                SeatNumber = student.SeatNumber,
                CommitteeId = student.ExamSchedule.Committee.CommitteeID,
                CommitteeNumber = student.ExamSchedule.Committee.CommitteeNumber,
                NumberOfStudent = student.ExamSchedule.Committee.NumberOfStudent
            };

            return View(model);
        }

        // =====================================
        // RECALCULATE SEATS
        // =====================================
        private async Task RecalculateSeatNumbers(int examScheduleId)
        {
            var students = await _context.Students
                .Where(s => s.ExamScheduleId == examScheduleId)
                .OrderBy(s => s.FullName)
                .ToListAsync();

            for (int i = 0; i < students.Count; i++)
                students[i].SeatNumber = i + 1;

            await _context.SaveChangesAsync();
        }
    }
}