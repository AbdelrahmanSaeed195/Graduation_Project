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
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            var examsQuery = _context.Exams
                .Include(e => e.Subject)
                .Select(e => new
                {
                    Id = e.ExamId,
                    Name = $"{e.Subject.SubjectName} - {e.ExamDate.ToShortDateString()} ({e.Subject.AcademicYear})"
                }).ToList();

            ViewData["ExamId"] = new SelectList(examsQuery, "Id", "Name");
            ViewData["CommitteeId"] = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");

            return View();
        }
        [HttpGet]
        public async Task<IActionResult> GetAvailableCommittees(int examId)
        {
            var selectedExam = await _context.Exams.FindAsync(examId);
            if (selectedExam == null) return Json(new List<object>());

            var busyCommitteeIds = await _context.ExamSchedules
                .Include(es => es.Exam)
                .Where(es => es.Exam.ExamDate.Date == selectedExam.ExamDate.Date
                          && selectedExam.StartTime < es.Exam.EndTime
                          && selectedExam.EndTime > es.Exam.StartTime)
                .Select(es => es.CommitteeId)
                .ToListAsync();

            var availableCommittees = await _context.Committees
                .Where(c => !busyCommitteeIds.Contains(c.CommitteeId))
                .Select(c => new
                {
                    id = c.CommitteeId,
                    number = "لجنة " + c.CommitteeNumber
                })
                .ToListAsync();

            return Json(availableCommittees);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("ExamScheduleId,ExamId,CommitteeId")] ExamSchedule examSchedule)
        {
            if (ModelState.IsValid)
            {
                var currentExam = await _context.Exams.FindAsync(examSchedule.ExamId);

                bool isBusy = await _context.ExamSchedules
                    .Include(es => es.Exam)
                    .AnyAsync(es => es.CommitteeId == examSchedule.CommitteeId
                                 && es.Exam.ExamDate.Date == currentExam.ExamDate.Date 
                                 && (
                                     (currentExam.StartTime < es.Exam.EndTime && currentExam.EndTime > es.Exam.StartTime)
                                 ));

                if (isBusy)
                {
                    ModelState.AddModelError("", "هذه اللجنة محجوزة بالفعل في هذا الوقت لامتحان آخر.");
                }
                else
                {
                    _context.Add(examSchedule);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تخصيص اللجنة بنجاح.";
                    return RedirectToAction(nameof(Index));
                }
            }

            PopulateDropdowns(examSchedule.ExamId, examSchedule.CommitteeId);
            return View(examSchedule);
        }

        // =====================================  
        // EDIT
        // =====================================
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
        [Authorize(Roles = "Admin")]
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
                    Name = $"{e.Subject.SubjectName} - {e.ExamDate.ToShortDateString()} ({e.Subject.AcademicYear})"
                }).ToList();

            var committeesQuery = _context.Committees
                .Select(c => new
                {
                    Id = c.CommitteeId,
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