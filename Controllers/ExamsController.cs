using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;

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
        // INDEX
        // =====================================
        public async Task<IActionResult> Index()
        {
            var exams = _context.Exams
                .Include(e => e.Subject)
                .OrderByDescending(e => e.ExamDate);
            return View(await exams.ToListAsync());
        }

        // =====================================
        // DETAILS
        // =====================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var exam = await _context.Exams
                .Include(e => e.Subject)
                .FirstOrDefaultAsync(m => m.ExamId == id);

            if (exam == null) return NotFound();

            return View(exam);
        }

        // =====================================
        // CREATE
        // =====================================
        public IActionResult Create()
        {
            PopulateSubjects();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ExamId,ExamDate,StartTime,EndTime,TargetAcademicYear,SubjectID")] Exam exam)
        {
            // التحقق من منطق الوقت
            if (exam.EndTime <= exam.StartTime)
            {
                ModelState.AddModelError("EndTime", "يجب أن يكون وقت انتهاء الامتحان بعد وقت البدء.");
            }

            if (ModelState.IsValid)
            {
                _context.Add(exam);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إضافة بيانات الامتحان بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "فشل في حفظ البيانات، يرجى التأكد من المدخلات.";
            PopulateSubjects(exam.SubjectID);
            return View(exam);
        }

        // =====================================
        // EDIT
        // =====================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var exam = await _context.Exams.FindAsync(id);
            if (exam == null) return NotFound();

            PopulateSubjects(exam.SubjectID);
            return View(exam);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ExamId,ExamDate,StartTime,EndTime,TargetAcademicYear,SubjectID")] Exam exam)
        {
            if (id != exam.ExamId) return NotFound();

            // التحقق من منطق الوقت
            if (exam.EndTime <= exam.StartTime)
            {
                ModelState.AddModelError("EndTime", "يجب أن يكون وقت انتهاء الامتحان بعد وقت البدء.");
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
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "حدث خطأ غير متوقع أثناء التعديل.";
                }
                return RedirectToAction(nameof(Index));
            }

            PopulateSubjects(exam.SubjectID);
            return View(exam);
        }

        // =====================================
        // DELETE
        // =====================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var exam = await _context.Exams
                .Include(e => e.Subject)
                .FirstOrDefaultAsync(m => m.ExamId == id);

            if (exam == null) return NotFound();

            return View(exam);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var exam = await _context.Exams.FindAsync(id);
            if (exam != null)
            {
                try
                {
                    _context.Exams.Remove(exam);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم حذف بيانات الامتحان بنجاح.";
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "لا يمكن حذف هذا الامتحان لوجود جداول امتحانات أو لجان مرتبطة به.";
                }
            }
            return RedirectToAction(nameof(Index));
        }

        // دالة مساعدة لتعبئة قائمة المواد لتجنب تكرار الكود
        private void PopulateSubjects(object selectedSubject = null)
        {
            var subjectsQuery = _context.Subjects.OrderBy(s => s.SubjectName);
            ViewBag.SubjectID = new SelectList(subjectsQuery, "SubjectId", "SubjectName", selectedSubject);
        }

        private bool ExamExists(int id)
        {
            return _context.Exams.Any(e => e.ExamId == id);
        }
    }
}