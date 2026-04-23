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
                .Include(s => s.Committee)
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
                .Include(s => s.Committee)
                .Include(s => s.Relatives)
                .FirstOrDefaultAsync(s => s.StudentId == id);

            if (student == null)
                return NotFound();

            return View(student);
        }

        // =====================================
        // GET: CREATE
        // =====================================
        public IActionResult Create()
        {
            return View();
        }

        // =====================================
        // POST: CREATE
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StudentCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var student = new Student
            {
                FullName = model.FullName,
                NationalId = model.NationalId,
                AcademicYear = model.AcademicYear
            };

            var (success, message) = await AssignToCommittee(student);

            if (!success)
            {
                ModelState.AddModelError("", message);
                return View(model);
            }

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            await RecalculateSeatNumbers(student.CommitteeId!.Value);

            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // GET: EDIT
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

        // =====================================
        // POST: EDIT
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StudentCreateViewModel model)
        {
            var student = await _context.Students.FindAsync(id);

            if (student == null)
                return NotFound();

            var oldCommitteeId = student.CommitteeId;
            bool yearChanged = student.AcademicYear != model.AcademicYear;

            student.FullName = model.FullName;
            student.NationalId = model.NationalId;
            student.AcademicYear = model.AcademicYear;

            if (yearChanged)
            {
                student.CommitteeId = null;

                var (success, message) = await AssignToCommittee(student);

                if (!success)
                {
                    ModelState.AddModelError("", message);
                    return View(model);
                }
            }

            await _context.SaveChangesAsync();

            if (oldCommitteeId.HasValue)
                await RecalculateSeatNumbers(oldCommitteeId.Value);

            if (student.CommitteeId.HasValue && student.CommitteeId != oldCommitteeId)
                await RecalculateSeatNumbers(student.CommitteeId.Value);

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

            var committeeId = student.CommitteeId;

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            if (committeeId.HasValue)
                await RecalculateSeatNumbers(committeeId.Value);

            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // STUDENT COMMITTEE PAGE
        // =====================================
        public async Task<IActionResult> Committee(int id)
        {
            var student = await _context.Students
                .Include(s => s.Committee)
                .FirstOrDefaultAsync(s => s.StudentId == id);

            if (student == null)
                return NotFound();

            if (student.Committee == null || student.CommitteeId == null)
            {
                ViewBag.Message = "هذا الطالب لم يُعيَّن في أي لجنة بعد";
                return View("NoCommittee");
            }

            var model = new StudentCommitteeViewModel
            {
                StudentId = student.StudentId,
                FullName = student.FullName,
                AcademicYear = student.AcademicYear,
                SeatNumber = student.SeatNumber,
                CommitteeId = student.Committee.CommitteeID,
                CommitteeNumber = student.Committee.CommitteeNumber,
                NumberOfStudent = student.Committee.NumberOfStudent
            };

            return View(model);
        }

        // =====================================
        // HELPERS
        // =====================================

        private async Task<(bool Success, string Message)> AssignToCommittee(Student student)
        {
            var committee = await _context.Committees
                .Where(c =>
                    !c.Students.Any() ||
                    c.Students.All(s => s.AcademicYear == student.AcademicYear)
                )
                .Select(c => new
                {
                    c.CommitteeID,
                    c.NumberOfStudent,
                    CurrentCount = c.Students.Count()
                })
                .Where(c => c.CurrentCount < c.NumberOfStudent)
                .OrderBy(c => c.CurrentCount)
                .FirstOrDefaultAsync();

            if (committee == null)
                return (false, "No available committee for this student");

            student.CommitteeId = committee.CommitteeID;
            return (true, "");
        }

        private async Task RecalculateSeatNumbers(int committeeId)
        {
            var students = await _context.Students
                .Where(s => s.CommitteeId == committeeId)
                .OrderBy(s => s.AcademicYear)
                .ThenBy(s => s.FullName)
                .ToListAsync();

            for (int i = 0; i < students.Count; i++)
                students[i].SeatNumber = i + 1;

            await _context.SaveChangesAsync();
        }
    }





}