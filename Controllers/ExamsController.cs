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

        // ============================================================
        // 1. Index: عرض الامتحانات
        // ============================================================
        public async Task<IActionResult> Index()
        {
            var exams = _context.Exams
                .AsNoTracking()
                .Include(e => e.Subject)
                .Include(e => e.ExamSchedules)
                    .ThenInclude(es => es.ExamLocation)
                .OrderByDescending(e => e.ExamDate);

            return View(await exams.ToListAsync());
        }

        // ============================================================
        // 2. Details: تفاصيل الامتحان
        // ============================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var exam = await _context.Exams
                .AsNoTracking()
                .Include(e => e.Subject)
                .Include(e => e.ExamSchedules)
                    .ThenInclude(es => es.ExamLocation)
                .FirstOrDefaultAsync(m => m.ExamId == id);

            if (exam == null) return NotFound();

            return View(exam);
        }

        // ============================================================
        // 3. Create: شاشة إضافة جديدة
        // ============================================================
        public IActionResult Create()
        {
            PopulateSubjects();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ExamId,ExamDate,StartTime,EndTime,SubjectID")] Exam exam)
        {
            var durationMinutes = (exam.EndTime - exam.StartTime).TotalMinutes;
            if (durationMinutes > 180)
            {
                ModelState.AddModelError("EndTime", "عفواً، لا يمكن أن تتجاوز مدة الامتحان 3 ساعات (180 دقيقة).");
            }
            if (exam.EndTime <= exam.StartTime)
            {
                ModelState.AddModelError("EndTime", "يجب أن يكون وقت الانتهاء بعد وقت البدء.");
            }

            // شرط التداخل الزمني المحدث
            bool isOverlapping = await _context.Exams.AsNoTracking().AnyAsync(e =>
                e.ExamDate.Date == exam.ExamDate.Date &&
                (exam.StartTime < e.EndTime && exam.EndTime > e.StartTime));

            if (isOverlapping)
            {
                ModelState.AddModelError("", "تنبيه: يوجد امتحان آخر مسجل في نفس الفترة الزمنية. يرجى مراجعة الجدول.");
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

        // ============================================================
        // 4. Edit: شاشة التعديل
        // ============================================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var exam = await _context.Exams.AsNoTracking().Include(e => e.Subject).FirstOrDefaultAsync(x => x.ExamId == id);
            if (exam == null) return NotFound();

            string arabicYear = "غير محدد";
            if (exam.Subject != null)
            {
                arabicYear = exam.Subject.AcademicYear switch
                {
                    AcademicLevel.FirstYear => "المستوى الأول",
                    AcademicLevel.SecondYear => "المستوى الثاني",
                    AcademicLevel.ThirdYear => "المستوى الثالث",
                    AcademicLevel.FourthYear => "المستوى الرابع",
                    _ => "غير محدد"
                };
            }
            ViewBag.InitialAcademicYear = arabicYear;

            PopulateSubjects(exam.SubjectID);
            return View(exam);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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

            // شرط التداخل الزمني المحدث مع استثناء الامتحان الحالي
            bool isOverlapping = await _context.Exams.AsNoTracking().AnyAsync(e =>
                e.ExamId != exam.ExamId &&
                e.ExamDate.Date == exam.ExamDate.Date &&
                (exam.StartTime < e.EndTime && exam.EndTime > e.StartTime));

            if (isOverlapping)
            {
                ModelState.AddModelError("", "هذا التعديل قد يتسبب في تداخل زمني مع امتحان آخر مسجل.");
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

        // ============================================================
        // 5. Delete: شاشة الحذف
        // ============================================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var exam = await _context.Exams
                .AsNoTracking()
                .Include(e => e.Subject)
                .Include(e => e.ExamSchedules)
                    .ThenInclude(es => es.ExamLocation)
                .FirstOrDefaultAsync(m => m.ExamId == id);

            if (exam == null) return NotFound();

            return View(exam);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var exam = await _context.Exams
                .Include(e => e.ExamSchedules)
                .FirstOrDefaultAsync(e => e.ExamId == id);

            if (exam != null)
            {
                bool hasAssignments = await _context.CommitteesAssignments.AnyAsync(a => a.ExamSchedule.ExamId == id);

                if (hasAssignments || exam.ExamSchedules.Any())
                {
                    TempData["ErrorMessage"] = "فشل الحذف: لا يمكن حذف الامتحان بسبب وجود ارتباطات في جدول توزيع المقرات.";
                    return RedirectToAction(nameof(Index));
                }

                try
                {
                    _context.Exams.Remove(exam);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم حذف الامتحان بنجاح.";
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "لا يمكن حذف الامتحان لوجود سجلات معتمدة عليه.";
                }
            }
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // 6. Ajax & Dropdowns Helpers
        // ============================================================
        [HttpGet]
        public async Task<JsonResult> GetSubjectYear(int subjectId)
        {
            var subject = await _context.Subjects.AsNoTracking().FirstOrDefaultAsync(s => s.SubjectId == subjectId);
            if (subject != null)
            {
                string arabicYear = subject.AcademicYear switch
                {
                    AcademicLevel.FirstYear => "المستوى الأول",
                    AcademicLevel.SecondYear => "المستوى الثاني",
                    AcademicLevel.ThirdYear => "المستوى الثالث",
                    AcademicLevel.FourthYear => "المستوى الرابع",
                    _ => "غير محدد"
                };
                return Json(new { academicYear = arabicYear });
            }
            return Json(new { academicYear = "" });
        }

        [HttpGet]
        public async Task<JsonResult> CheckConstraints(DateTime date, TimeSpan start, TimeSpan end, int? excludeId)
        {
            var durationMinutes = (end - start).TotalMinutes;
            var isValidDuration = durationMinutes > 0 && durationMinutes <= 180;

            bool isOverlapping = await _context.Exams.AsNoTracking().AnyAsync(e =>
                (excludeId == null || e.ExamId != excludeId) &&
                e.ExamDate.Date == date.Date &&
                (start < e.EndTime && end > e.StartTime));

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