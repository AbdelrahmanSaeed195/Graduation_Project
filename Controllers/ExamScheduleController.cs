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
        // 1. القائمة الرئيسية - INDEX
        // =====================================
        public async Task<IActionResult> Index()
        {
            var examSchedules = _context.ExamSchedules
                .Include(e => e.ExamLocation)
                .Include(e => e.Exam).ThenInclude(ex => ex.Subject)
                .OrderBy(e => e.Exam.ExamDate)
                .ThenBy(e => e.Exam.StartTime);

            return View(await examSchedules.ToListAsync());
        }

        // =====================================
        // 2. التفاصيل - DETAILS
        // =====================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var examSchedule = await _context.ExamSchedules
                .Include(e => e.ExamLocation)
                .Include(e => e.Exam).ThenInclude(ex => ex.Subject)
                .FirstOrDefaultAsync(m => m.ExamScheduleId == id);

            if (examSchedule == null) return NotFound();

            return View(examSchedule);
        }

        // =====================================
        // 3. شاشة الإضافة - CREATE (GET)
        // =====================================
        public async Task<IActionResult> Create()
        {
            await PopulateDropdownsAsync();
            return View();
        }

        // =====================================
        // 4. دالة تفاعلية: جلب الصالات المتاحة وقت الامتحان
        // =====================================
        [HttpGet]
        public async Task<IActionResult> GetAvailableLocations(int examId)
        {
            var selectedExam = await _context.Exams.FindAsync(examId);
            if (selectedExam == null) return Json(new List<object>());

            var busyLocationIds = await _context.ExamSchedules
                .Include(es => es.Exam)
                .Where(es => es.Exam.ExamDate.Date == selectedExam.ExamDate.Date
                          && selectedExam.StartTime < es.Exam.EndTime
                          && selectedExam.EndTime > es.Exam.StartTime)
                .Select(es => es.LocationId)
                .ToListAsync();

            var availableLocations = await _context.ExamLocations
                .Include(l => l.ParentLocation)
                .Where(l => l.Type == LocationType.Block && !busyLocationIds.Contains(l.LocationId))
                .Select(l => new
                {
                    id = l.LocationId,
                    name = "صالة: " + l.LocationName + " (جراش: " + (l.ParentLocation != null ? l.ParentLocation.LocationName : "غير محدد") + ")"
                })
                .OrderBy(l => l.name)
                .ToListAsync();

            return Json(availableLocations);
        }

        // =====================================
        // 5. حفظ الجلسة الجديدة - CREATE (POST)
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ExamScheduleId,ExamId,LocationId,ScheduledDate")] ExamSchedule examSchedule)
        {
            if (ModelState.IsValid)
            {
                var currentExam = await _context.Exams.FindAsync(examSchedule.ExamId);

                bool isBusy = await _context.ExamSchedules
                    .Include(es => es.Exam)
                    .AnyAsync(es => es.LocationId == examSchedule.LocationId
                                 && es.Exam.ExamDate.Date == currentExam.ExamDate.Date
                                 && (currentExam.StartTime < es.Exam.EndTime && currentExam.EndTime > es.Exam.StartTime));

                if (isBusy)
                {
                    ModelState.AddModelError("", "هذه الصالة محجوزة بالكامل في هذا الوقت لامتحان آخر.");
                }
                else
                {
                    _context.Add(examSchedule);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تخصيص الصالة للجلسة بنجاح.";
                    return RedirectToAction(nameof(Index));
                }
            }

            await PopulateDropdownsAsync(examSchedule.ExamId, examSchedule.LocationId);
            return View(examSchedule);
        }

        // =====================================  
        // 6. شاشة التعديل - EDIT (GET)
        // =====================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var examSchedule = await _context.ExamSchedules
                .Include(es => es.Exam)
                    .ThenInclude(e => e.Subject)
                .FirstOrDefaultAsync(es => es.ExamScheduleId == id);

            if (examSchedule == null) return NotFound();

            await PopulateDropdownsAsync(examSchedule.ExamId, examSchedule.LocationId);
            return View(examSchedule);
        }

        // =====================================
        // 7. حفظ التعديلات - EDIT (POST)
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ExamScheduleId,ExamId,LocationId,ScheduledDate")] ExamSchedule examSchedule)
        {
            if (id != examSchedule.ExamScheduleId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var currentExam = await _context.Exams.FindAsync(examSchedule.ExamId);

                    bool isBusy = await _context.ExamSchedules
                        .Include(es => es.Exam)
                        .AnyAsync(es => es.LocationId == examSchedule.LocationId
                                     && es.ExamScheduleId != examSchedule.ExamScheduleId
                                     && es.Exam.ExamDate.Date == currentExam.ExamDate.Date
                                     && (currentExam.StartTime < es.Exam.EndTime && currentExam.EndTime > es.Exam.StartTime));

                    if (isBusy)
                    {
                        ModelState.AddModelError("", "عفواً، هذه الصالة مشغولة حالياً في هذا التوقيت.");
                        await PopulateDropdownsAsync(examSchedule.ExamId, examSchedule.LocationId);
                        return View(examSchedule);
                    }

                    _context.Update(examSchedule);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث توزيع الصالة بنجاح.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ExamScheduleExists(examSchedule.ExamScheduleId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdownsAsync(examSchedule.ExamId, examSchedule.LocationId);
            return View(examSchedule);
        }

        // =====================================
        // 8. شاشة الحذف - DELETE
        // =====================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var examSchedule = await _context.ExamSchedules
                .Include(e => e.ExamLocation)
                .Include(e => e.Exam).ThenInclude(ex => ex.Subject)
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
                TempData["SuccessMessage"] = "تم حذف جلسة توزيع الصالة بنجاح.";
            }
            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // 9. دالة تعبئة القوائم المنسدلة الموحدة 
        // =====================================
        private async Task PopulateDropdownsAsync(object selectedExam = null, object selectedLocation = null)
        {
            // جلب الامتحانات
            var exams = await _context.Exams
                .Include(e => e.Subject)
                .AsNoTracking()
                .ToListAsync();

            var examsQuery = exams.Select(e => new
            {
                Id = e.ExamId,
                Name = e.Subject != null
                    ? $"{e.Subject.SubjectName} - {e.ExamDate.ToString("yyyy/MM/dd")}"
                    : $"امتحان #{e.ExamId} - {e.ExamDate.ToString("yyyy/MM/dd")}"
            }).ToList();

            var locationsList = await _context.ExamLocations
                .Include(l => l.ParentLocation) 
                .Where(l => l.Type == LocationType.Block)
                .AsNoTracking()
                .ToListAsync();

            var locationsQuery = locationsList.Select(l => new
            {
                Id = l.LocationId,
                Name = $"صالة: {l.LocationName} (جراش: {l.ParentLocation?.LocationName ?? "غير محدد"})"
            }).ToList();

            ViewData["ExamId"] = new SelectList(examsQuery, "Id", "Name", selectedExam);
            ViewData["LocationId"] = new SelectList(locationsQuery, "Id", "Name", selectedLocation);
        }

        private bool ExamScheduleExists(int id) => _context.ExamSchedules.Any(e => e.ExamScheduleId == id);
    }
}