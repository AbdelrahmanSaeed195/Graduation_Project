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
        // INDEX
        // =====================================
        public async Task<IActionResult> Index()
        {
            var examSchedules = _context.ExamSchedules
                .Include(e => e.Committee)
                .Include(e => e.Exam)
                    .ThenInclude(ex => ex.Subject) 
                .OrderBy(e => e.ScheduledDate)
                .ThenBy(e => e.StartTime);

            return View(await examSchedules.ToListAsync());
        }
        //  =====================================
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
        public async Task<IActionResult> Create([Bind("ExamScheduleId,ScheduledDate,StartTime,EndTime,ExamId,CommitteeId")] ExamSchedule examSchedule)
        {
            if (examSchedule.EndTime <= examSchedule.StartTime)
            {
                ModelState.AddModelError("EndTime", "يجب أن يكون وقت الانتهاء بعد وقت البدء.");
            }
            if (ModelState.IsValid)
            {
                _context.Add(examSchedule);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إضافة موعد الامتحان بنجاح."; 
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
        public async Task<IActionResult> Edit(int id, [Bind("ExamScheduleId,ScheduledDate,StartTime,EndTime,ExamId,CommitteeId")] ExamSchedule examSchedule)
        {
            if (id != examSchedule.ExamScheduleId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(examSchedule);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث موعد الامتحان بنجاح."; 
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ExamScheduleExists(examSchedule.ExamScheduleId)) return NotFound();
                    else throw;
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "حدث خطأ غير متوقع أثناء التعديل.";
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
                try
                {
                    _context.ExamSchedules.Remove(examSchedule);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم حذف موعد الامتحان بنجاح.";
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "لا يمكن حذف الموعد لوجود بيانات مرتبطة به (مثل التكليفات).";
                }
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
                    Name = $"{e.Subject.SubjectName} ({e.TargetAcademicYear})"
                }).ToList();

            var committeesQuery = _context.Committees
                .Select(c => new
                {
                    Id = c.CommitteeID,
                    Number = "Committee " + c.CommitteeNumber
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