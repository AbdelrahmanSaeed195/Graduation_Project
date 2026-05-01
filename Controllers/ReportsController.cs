using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using projectweb.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        public async Task<IActionResult> Index(int? reportType)
        {
            // 1. نبدأ بالاستعلام الأساسي مع جلب كل الجداول المرتبطة (اسم المادة واللجنة)
            var reportsQuery = _context.Reports
                .Include(r => r.ExamSchedule)
                    .ThenInclude(s => s.Exam)
                        .ThenInclude(e => e.Subject) // تأكد من جلب اسم المادة
                .Include(r => r.ExamSchedule)
                    .ThenInclude(s => s.Committee)
                .AsQueryable();


            if (reportType.HasValue && reportType.Value > 0)
            {

                var status = (ReportStatus)reportType.Value;


                reportsQuery = reportsQuery.Where(r => r.Status == status);


                ViewBag.SelectedReportType = reportType.Value;
            }


            var reports = await reportsQuery
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();

            return View(reports);
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
                    .ThenInclude(e => e.Subject)
                .Include(s => s.Committee)
                .OrderBy(s => s.ScheduledDate)
                .ToList();

            var dropdownList = schedules.Select(s => new
            {
                ID = s.ExamScheduleId,

                Text = $"{(s.Exam?.Subject?.SubjectName ?? "مادة غير محددة")} | اللجنة: {(s.Committee?.CommitteeNumber ?? 0)} | التاريخ: {s.ScheduledDate.ToString("dd/MM/yyyy")}"
            }).ToList();

            ViewData["ScheduleID"] = new SelectList(dropdownList, "ID", "Text", selectedSchedule);
        }
        // =====================================
        // PRINT ASSIGNMENTS (توزيع المراقبين)
        // ====================================
        // أضف id هنا لاستلام رقم الشخص المراد طباعة تقريره
        public async Task<IActionResult> PrintAssignments(int id)
        {
            var rawAssignments = await _context.CommitteesAssignments
                .Include(a => a.Person).Include(a => a.Role)
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam).ThenInclude(e => e.Subject)
                .Where(a => a.PersonID == id)
                .ToListAsync();

            if (!rawAssignments.Any()) return Content("لا توجد بيانات");

            // استخراج المواعيد تلقائياً: بنشوف كل فرقة ميعادها إيه في السيستم
            var yearTimes = rawAssignments
                .Where(a => a.ExamSchedule != null)
                .GroupBy(a => a.ExamSchedule.Exam.TargetAcademicYear)
                .ToDictionary(
                    g => g.Key,
                    g => {
                        var first = g.First().ExamSchedule;
                        return $"{DateTime.Today.Add(first.StartTime):hh:mm} - {DateTime.Today.Add(first.EndTime):hh:mm}";
                    }
                );

            var groupedRows = rawAssignments
                .GroupBy(a => a.ExamSchedule.ScheduledDate.Date)
                .Select(g => new AssignmentRowGroup
                {
                    Date = g.Key,
                    Day = g.Key.ToString("dddd", new System.Globalization.CultureInfo("ar-EG")),
                    DailyItems = g.Select(a => new AssignmentReportItem
                    {
                        SubjectName = a.ExamSchedule?.Exam?.Subject?.SubjectName ?? "",
                        TargetYear = a.ExamSchedule?.Exam?.TargetAcademicYear ?? "",
                        // نترك الـ TimeRange فارغاً هنا لأننا سنعرضه في الهيدر
                        PersonFullName = a.Person?.FullName ?? ""
                    }).ToList()
                }).OrderBy(x => x.Date).ToList();

            var model = new PrintReportViewModel
            {
                Rows = groupedRows,
                PersonRoleInReport = GetArabicRoleName(rawAssignments.First().Role?.RoleName.ToString() ?? ""),
                // سنحتاج إضافة خاصية جديدة في الـ ViewModel باسم YearTimes من نوع Dictionary
                YearTimes = yearTimes
            };

            return View(model);
        }

        // ميثود تحويل الرتب للعربي
        private string GetArabicRoleName(string englishName)
        {
            return englishName switch
            {
                "HallManager" => "رئيس صالة",
                "BlockGroupLeader" => "مراقب",
                "CommitteeObserver" => "ملاحظ",
                _ => englishName
            };
        }

        // أضف علامة الاستفهام بجانب int
        public async Task<IActionResult> PrintControlSheet(int? scheduleId)
        {
            // 1. إذا لم يتم إرسال ID، نعرض صفحة فارغة أو نرجع للـ Index
            if (scheduleId == null)
            {
                // يمكنك إرجاع View فارغ لملئه يدوياً كما في الورقة الأصلية
                return View(new ExamControlSheetViewModel { ObserverRows = new List<ObserverRowItem>() });
            }

            var schedule = await _context.ExamSchedules
                .Include(s => s.Exam).ThenInclude(e => e.Subject)
                .Include(s => s.Committee)
                .FirstOrDefaultAsync(s => s.ExamScheduleId == scheduleId);

            if (schedule == null) return NotFound();

            var observers = await _context.CommitteesAssignments
                .Where(a => a.ExamScheduleId == scheduleId)
                .Include(a => a.Person)
                .Select(a => new ObserverRowItem
                {
                    ObserverName = a.Person.FullName
                }).ToListAsync();

            var viewModel = new ExamControlSheetViewModel
            {
                SubjectName = schedule.Exam?.Subject?.SubjectName ?? "................",
                TargetYear = schedule.Exam?.TargetAcademicYear ?? "................",
                ExamTime = $"{schedule.StartTime:hh\\:mm} - {schedule.EndTime:hh\\:mm}",
                ObserverRows = observers
            };

            return View(viewModel);
        }
        private bool ReportExists(int id)
        {
            return _context.Reports.Any(e => e.ReportID == id);
        }
    }
}