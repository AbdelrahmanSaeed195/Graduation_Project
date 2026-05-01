using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;

namespace projectweb.Controllers
{
    public class SubjectsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SubjectsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================
        // INDEX
        // =====================================
        public async Task<IActionResult> Index()
        {
            
            var subjects = await _context.Subjects
                .Include(s => s.Exams)
                .OrderBy(s => s.SubjectName)
                .ToListAsync();

            return View(subjects);
        }

        // =====================================
        // DETAILS
        // =====================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var subject = await _context.Subjects
                .Include(s => s.Exams)
                .FirstOrDefaultAsync(m => m.SubjectId == id);
            if (subject == null)
            {
                return NotFound();
            }

            return View(subject);
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
        // تم تحديث الـ Bind ليشمل كود المادة والسنة الدراسية
        public async Task<IActionResult> Create([Bind("SubjectId,SubjectCode,SubjectName,AcademicYear")] Subject subject)
        {
            if (ModelState.IsValid)
            {
                _context.Add(subject);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إنشاء المادة بنجاح";
                return RedirectToAction(nameof(Index));
            }
            return View(subject);
        }

        // =====================================
        // EDIT
        // =====================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null)
            {
                return NotFound();
            }
            return View(subject);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        // تم تحديث الـ Bind هنا أيضاً لضمان تحديث الحقول الجديدة
        public async Task<IActionResult> Edit(int id, [Bind("SubjectId,SubjectCode,SubjectName,AcademicYear")] Subject subject)
        {
            if (id != subject.SubjectId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(subject);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث المادة بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SubjectExists(subject.SubjectId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(subject);
        }

        // =====================================
        // DELETE
        // =====================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var subject = await _context.Subjects
                .FirstOrDefaultAsync(m => m.SubjectId == id);
            if (subject == null)
            {
                return NotFound();
            }

            return View(subject);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var subject = await _context.Subjects
                .Include(s => s.Exams)
                .FirstOrDefaultAsync(s => s.SubjectId == id);

            if (subject != null)
            {
                if (subject.Exams != null && subject.Exams.Any())
                {
                    TempData["ErrorMessage"] = "لا يمكن حذف المادة لوجود امتحانات مرتبطة بها في النظام.";
                    return RedirectToAction(nameof(Index));
                }

                try
                {
                    _context.Subjects.Remove(subject);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم حذف المادة بنجاح";
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "حدث خطأ غير متوقع أثناء عملية الحذف.";
                }
            }

            return RedirectToAction(nameof(Index));
        }

        private bool SubjectExists(int id)
        {
            return _context.Subjects.Any(e => e.SubjectId == id);
        }
    }
}