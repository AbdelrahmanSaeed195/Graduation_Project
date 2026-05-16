using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace projectweb.Controllers
{
    public class ExamsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExamsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================
        // 1. القائمة الرئيسية - INDEX
        // =====================================
        public async Task<IActionResult> Index()
        {
            var exams = _context.Exams
                .AsNoTracking()
                .Include(e => e.Subject) // ضروري لعرض السنة الدراسية من المادة
                .Include(e => e.ExamSchedules)
                    .ThenInclude(es => es.Committee)
                .OrderByDescending(e => e.ExamDate);

            return View(await exams.ToListAsync());
        }

        // =====================================
        // 2. التفاصيل - DETAILS
        // =====================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var exam = await _context.Exams
                .AsNoTracking()
                .Include(e => e.Subject)
                .Include(e => e.ExamSchedules)
                    .ThenInclude(es => es.Committee)
                        .ThenInclude(c => c.Block)
                            .ThenInclude(b => b.Hall)
                .FirstOrDefaultAsync(m => m.ExamId == id);

            if (exam == null) return NotFound();

            return View(exam);
        }

        // =====================================
        // 3. إنشاء امتحان جديد - CREATE
        // =====================================
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            PopulateSubjects();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("ExamId,ExamDate,StartTime,EndTime,SubjectID")] Exam exam)
        {
            // التحقق من المدة والتداخل
            var durationMinutes = (exam.EndTime - exam.StartTime).TotalMinutes;
            if (durationMinutes > 180)
            {
                ModelState.AddModelError("EndTime", "عفواً، لا يمكن أن تتجاوز مدة الامتحان 3 ساعات (180 دقيقة).");
            }
            if (exam.EndTime <= exam.StartTime)
            {
                ModelState.AddModelError("EndTime", "يجب أن يكون وقت الانتهاء بعد وقت البدء.");
            }

            bool isOverlapping = await _context.Exams.AsNoTracking().AnyAsync(e =>
                e.ExamDate.Date == exam.ExamDate.Date &&
                ((exam.StartTime >= e.StartTime && exam.StartTime < e.EndTime) ||
                 (exam.EndTime > e.StartTime && exam.EndTime <= e.EndTime)));

            if (isOverlapping)
            {
                ModelState.AddModelError("", "يوجد امتحان آخر مسجل في نفس هذا الموعد.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(exam);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إضافة الامتحان بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            PopulateSubjects(exam.SubjectID);
            return View(exam);
        }

        // =====================================
        // 4. تعديل امتحان - EDIT
        // =====================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var exam = await _context.Exams.AsNoTracking().Include(e => e.Subject).FirstOrDefaultAsync(x => x.ExamId == id);
            if (exam == null) return NotFound();

            PopulateSubjects(exam.SubjectID);
            return View(exam);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("ExamId,ExamDate,StartTime,EndTime,SubjectID")] Exam exam)
        {
            if (id != exam.ExamId) return NotFound();

            var durationMinutes = (exam.EndTime - exam.StartTime).TotalMinutes;
            if (durationMinutes > 180)
            {
                ModelState.AddModelError("EndTime", "عفواً، لا يمكن أن تتجاوز مدة الامتحان 3 ساعات.");
            }
            if (exam.EndTime <= exam.StartTime)
            {
                ModelState.AddModelError("EndTime", "يجب أن يكون وقت الانتهاء بعد وقت البدء.");
            }

            bool isOverlapping = await _context.Exams.AsNoTracking().AnyAsync(e =>
                e.ExamId != exam.ExamId &&
                e.ExamDate.Date == exam.ExamDate.Date &&
                ((exam.StartTime >= e.StartTime && exam.StartTime < e.EndTime) ||
                 (exam.EndTime > e.StartTime && exam.EndTime <= e.EndTime)));

            if (isOverlapping)
            {
                ModelState.AddModelError("", "هذا التعديل يتسبب في تداخل مع موعد امتحان آخر.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(exam);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث بيانات الامتحان بنجاح.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ExamExists(exam.ExamId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            PopulateSubjects(exam.SubjectID);
            return View(exam);
        }

        // =====================================
        // 5. الحذف - DELETE
        // =====================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var exam = await _context.Exams
                .AsNoTracking()
                .Include(e => e.Subject)
                .Include(e => e.ExamSchedules)
                .FirstOrDefaultAsync(m => m.ExamId == id);

            if (exam == null) return NotFound();

            return View(exam);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var exam = await _context.Exams.FindAsync(id);
            if (exam != null)
            {
                try
                {
                    _context.Exams.Remove(exam);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم حذف الامتحان بنجاح.";
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "لا يمكن حذف الامتحان لوجود لجان أو بيانات مرتبطة به.";
                }
            }
            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // روابط مساعدة (AJAX)
        // =====================================

        [HttpGet]
        public async Task<JsonResult> GetSubjectYear(int subjectId)
        {
            var subject = await _context.Subjects.AsNoTracking().FirstOrDefaultAsync(s => s.SubjectId == subjectId);
            return Json(new { academicYear = subject?.AcademicYear ?? "" });
        }

        [HttpGet]
        public async Task<JsonResult> CheckConstraints(DateTime date, TimeSpan start, TimeSpan end, int? excludeId)
        {
            var durationMinutes = (end - start).TotalMinutes;
            var isValidDuration = durationMinutes > 0 && durationMinutes <= 180;

            bool isOverlapping = await _context.Exams.AsNoTracking().AnyAsync(e =>
                (excludeId == null || e.ExamId != excludeId) &&
                e.ExamDate.Date == date.Date &&
                ((start >= e.StartTime && start < e.EndTime) ||
                 (end > e.StartTime && end <= e.EndTime)));

            return Json(new { isValidDuration = isValidDuration, isOverlapping = isOverlapping });
        }

        private void PopulateSubjects(object selectedSubject = null)
        {
            var subjectsQuery = _context.Subjects.AsNoTracking().OrderBy(s => s.SubjectName);
            ViewBag.SubjectID = new SelectList(subjectsQuery, "SubjectId", "SubjectName", selectedSubject);
        }

        private bool ExamExists(int id)
        {
            return _context.Exams.Any(e => e.ExamId == id);
        }
    }
}