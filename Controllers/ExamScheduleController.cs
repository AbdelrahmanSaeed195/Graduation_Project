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
    public class ExamSchedulesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExamSchedulesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================
        // INDEX - عرض توزيع اللجان
        // =====================================
        public async Task<IActionResult> Index()
        {
            // تم تعديل الترتيب ليعتمد على بيانات موديل الامتحان المرتبط
            var examSchedules = _context.ExamSchedules
                .Include(e => e.Committee)
                .Include(e => e.Exam)
                    .ThenInclude(ex => ex.Subject)
                .OrderBy(e => e.Exam.ExamDate)
                .ThenBy(e => e.Exam.StartTime);

            return View(await examSchedules.ToListAsync());
        }

        // =====================================
        // DETAILS
        // =====================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var examSchedule = await _context.ExamSchedules
                .Include(e => e.Committee)
                .Include(e => e.Exam)
                    .ThenInclude(ex => ex.Subject)
                .FirstOrDefaultAsync(m => m.ExamScheduleId == id);

            if (examSchedule == null) return NotFound();

            return View(examSchedule);
        }

        // =====================================
        // CREATE
        // =====================================
        public IActionResult Create()
        {
            PopulateDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ExamScheduleId,ExamId,CommitteeId")] ExamSchedule examSchedule)
        {
            // تم حذف التحقق من الوقت هنا لأنه يتبع الموعد الرئيسي للامتحان
            if (ModelState.IsValid)
            {
                _context.Add(examSchedule);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تخصيص اللجنة للامتحان بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            TempData["ErrorMessage"] = "حدث خطأ أثناء الإضافة، يرجى التحقق من البيانات.";
            PopulateDropdowns(examSchedule.ExamId, examSchedule.CommitteeId);
            return View(examSchedule);
        }

        // =====================================  
        // EDIT
        // =====================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var examSchedule = await _context.ExamSchedules.FindAsync(id);
            if (examSchedule == null) return NotFound();

            PopulateDropdowns(examSchedule.ExamId, examSchedule.CommitteeId);
            return View(examSchedule);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ExamScheduleId,ExamId,CommitteeId")] ExamSchedule examSchedule)
        {
            if (id != examSchedule.ExamScheduleId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(examSchedule);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث التوزيع بنجاح.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ExamScheduleExists(examSchedule.ExamScheduleId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            PopulateDropdowns(examSchedule.ExamId, examSchedule.CommitteeId);
            return View(examSchedule);
        }

        // =====================================
        // DELETE
        // =====================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var examSchedule = await _context.ExamSchedules
                .Include(e => e.Committee)
                .Include(e => e.Exam)
                    .ThenInclude(ex => ex.Subject)
                .FirstOrDefaultAsync(m => m.ExamScheduleId == id);

            if (examSchedule == null) return NotFound();

            return View(examSchedule);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var examSchedule = await _context.ExamSchedules.FindAsync(id);
            if (examSchedule != null)
            {
                _context.ExamSchedules.Remove(examSchedule);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف التوزيع بنجاح.";
            }
            return RedirectToAction(nameof(Index));
        }

        private void PopulateDropdowns(object selectedExam = null, object selectedCommittee = null)
        {
            var examsQuery = _context.Exams
                .Include(e => e.Subject)
                .Select(e => new
                {
                    Id = e.ExamId,
                    Name = $"{e.Subject.SubjectName} - {e.ExamDate.ToShortDateString()} ({e.TargetAcademicYear})"
                }).ToList();

            var committeesQuery = _context.Committees
                .Select(c => new
                {
                    Id = c.CommitteeID,
                    Number = "لجنة " + c.CommitteeNumber
                }).ToList();

            ViewData["ExamId"] = new SelectList(examsQuery, "Id", "Name", selectedExam);
            ViewData["CommitteeId"] = new SelectList(committeesQuery, "Id", "Number", selectedCommittee);
        }

        private bool ExamScheduleExists(int id)
        {
            return _context.ExamSchedules.Any(e => e.ExamScheduleId == id);
        }
    }
}