using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
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
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
        public async Task<IActionResult> Delete(int id)
        {
            var student = await _context.Students.FindAsync(id);

            if (student == null)
                return NotFound();

            var examScheduleId = student.ExamScheduleId;

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            if (examScheduleId.HasValue)
                await RecalculateSeatNumbers(examScheduleId.Value);

            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // DISTRIBUTE STUDENTS
        // =====================================
        public async Task<IActionResult> DistributeStudents()
        {
            var students = await _context.Students
                .Where(s => s.ExamScheduleId == null)
                .OrderBy(s => s.AcademicYear)
                .ThenBy(s => s.FullName)
                .ToListAsync();

            var schedules = await _context.ExamSchedules
                .Include(e => e.Committee)
                .Include(e => e.Exam) 
                .OrderBy(e => e.Exam.ExamDate) 
                .ThenBy(e => e.Exam.StartTime) 
                .ThenBy(e => e.Committee.CommitteeNumber)
                .ToListAsync();

            var grouped = students.GroupBy(s => s.AcademicYear).ToList();

            int scheduleIndex = 0;

            foreach (var group in grouped)
            {
                foreach (var student in group)
                {
                    while (scheduleIndex < schedules.Count)
                    {
                        var scheduleId = schedules[scheduleIndex].ExamScheduleId;

                        var count = await _context.Students
                            .CountAsync(s => s.ExamScheduleId == scheduleId);

                        if (count < schedules[scheduleIndex].Committee.NumberOfStudent)
                            break;

                        scheduleIndex++;
                    }

                    if (scheduleIndex >= schedules.Count)
                        break;

                    student.ExamScheduleId = schedules[scheduleIndex].ExamScheduleId;
                }

                scheduleIndex++;
            }

            await _context.SaveChangesAsync();

            foreach (var schedule in schedules)
                await RecalculateSeatNumbers(schedule.ExamScheduleId);

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