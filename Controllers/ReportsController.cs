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
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================
        // INDEX
        // =====================================
        public async Task<IActionResult> Index()
        {
           
            var reports = _context.Reports
                .Include(r => r.ExamSchedule)
                    .ThenInclude(s => s.Exam)
                .Include(r => r.ExamSchedule)
                    .ThenInclude(s => s.Committee)
                .OrderByDescending(r => r.CreatedDate);

            return View(await reports.ToListAsync());
        }

        // =====================================
        // DETAILS
        // =====================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var report = await _context.Reports
                .Include(r => r.ExamSchedule)
                    .ThenInclude(s => s.Exam)
                .Include(r => r.ExamSchedule)
                    .ThenInclude(s => s.Committee)
                .Include(r => r.ReportPersons)
                    .ThenInclude(rp => rp.Person)
                .FirstOrDefaultAsync(m => m.ReportID == id);

            if (report == null) return NotFound();

            return View(report);
        }
        // =====================================
        // CREATE
        // =====================================
        public IActionResult Create()
        {
            PopulateSchedulesDropDownList();
            return View();
        }

     
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ReportID,Status,Notes,ScheduleID")] Report report)
        {
            if (ModelState.IsValid)
            {
                report.CreatedDate = DateTime.Now;
                _context.Add(report);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إنشاء المحضر بنجاح";
                return RedirectToAction(nameof(Index));
            }

            PopulateSchedulesDropDownList(report.ScheduleID);
            return View(report);
        }

        // =====================================
        // EDIT
        // =====================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var report = await _context.Reports.FindAsync(id);
            if (report == null) return NotFound();

            PopulateSchedulesDropDownList(report.ScheduleID);
            return View(report);
        }

       
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ReportID,CreatedDate,Status,Notes,ScheduleID")] Report report)
        {
            if (id != report.ReportID) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(report);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث المحضر بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReportExists(report.ReportID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            PopulateSchedulesDropDownList(report.ScheduleID);
            return View(report);
        }

        // =====================================
        // DELETE
        // =====================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var report = await _context.Reports
                .Include(r => r.ExamSchedule)
                    .ThenInclude(s => s.Exam)
                .FirstOrDefaultAsync(m => m.ReportID == id);

            if (report == null) return NotFound();

            return View(report);
        }

        
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var report = await _context.Reports.FindAsync(id);
            if (report != null)
            {
                try
                {
                    _context.Reports.Remove(report);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم حذف المحضر بنجاح";
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "لا يمكن حذف المحضر لوجود أشخاص مرتبطين به في جدول التوقيعات.";
                }
            }
            return RedirectToAction(nameof(Index));
        }


        private void PopulateSchedulesDropDownList(object selectedSchedule = null)
        {
            var schedules = _context.ExamSchedules
                .Include(s => s.Exam)
                .Include(s => s.Committee)
                .OrderBy(s => s.ScheduledDate)
                .Select(s => new
                {
                    ID = s.ExamScheduleId,
                    Text = $"{s.Exam.Subject.SubjectName} | اللجنة: {s.Committee.CommitteeNumber} | التاريخ: {s.ScheduledDate:dd/MM/yyyy}"
                })
                .ToList();

            ViewData["ScheduleID"] = new SelectList(schedules, "ID", "Text", selectedSchedule);
        }

        private bool ReportExists(int id)
        {
            return _context.Reports.Any(e => e.ReportID == id);
        }
    }
}