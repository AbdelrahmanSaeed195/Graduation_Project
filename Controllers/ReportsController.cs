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
            var reportsQuery = _context.Reports
                .Include(r => r.ExamSchedule)
                    .ThenInclude(s => s.Exam)
                        .ThenInclude(e => e.Subject)
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
                        .ThenInclude(e => e.Subject) // أضفنا Subject هنا للوضوح
                .Include(r => r.ExamSchedule)
                    .ThenInclude(s => s.Committee)
                .Include(r => r.ReportPersons)
                    .ThenInclude(rp => rp.Person)
                .FirstOrDefaultAsync(m => m.ReportID == id);

            if (report == null) return NotFound();

            return View(report);
        }

        // =====================================
        // PRINT ASSIGNMENTS (توزيع المراقبين)
        // =====================================
        public async Task<IActionResult> PrintAssignments(int id)
        {
            // 1. جلب البيانات مع التأكد من Inclusion للموديلات الجديدة
            var rawAssignments = await _context.CommitteesAssignments
                .Include(a => a.Person)
                .Include(a => a.Role)
                .Include(a => a.ExamSchedule)
                    .ThenInclude(es => es.Exam) // الوصول لجدول الامتحان الأساسي
                        .ThenInclude(e => e.Subject) // الوصول لاسم المادة
                .Where(a => a.PersonID == id)
                .ToListAsync();

            // 2. التحقق من وجود بيانات لمنع انهيار البرنامج
            if (rawAssignments == null || !rawAssignments.Any())
                return Content("لا توجد بيانات لهذا الشخص");

            // 3. بناء قاموس المواعيد من موديل Exam (المنطق الجديد)
            var yearTimesMap = rawAssignments
                .Where(a => a.ExamSchedule?.Exam != null)
                .GroupBy(a => a.ExamSchedule.Exam.TargetAcademicYear ?? "غير محدد")
                .ToDictionary(
                    g => g.Key,
                    g => {
                        var exam = g.First().ExamSchedule.Exam;
                        // تحويل TimeSpan لتنسيق 12 ساعة احترافي
                        return $"{DateTime.Today.Add(exam.StartTime):hh:mm tt} - {DateTime.Today.Add(exam.EndTime):hh:mm tt}";
                    }
                );

            // 4. تجميع البيانات للعرض في الجدول
            var groupedRows = rawAssignments
                .GroupBy(a => a.ExamSchedule.ScheduledDate.Date)
                .Select(g => new AssignmentRowGroup
                {
                    Date = g.Key,
                    Day = g.Key.ToString("dddd", new System.Globalization.CultureInfo("ar-EG")),
                    DailyItems = g.Select(a => new AssignmentReportItem
                    {
                        SubjectName = a.ExamSchedule?.Exam?.Subject?.SubjectName ?? "مادة غير محددة",
                        TargetYear = a.ExamSchedule?.Exam?.TargetAcademicYear ?? "",
                        PersonFullName = a.Person?.FullName ?? ""
                    }).ToList()
                }).OrderBy(x => x.Date).ToList();

            // 5. استخدام ! لإخبار الفيزوال ستوديو أن القائمة ليست فارغة (يحل الخط الأحمر)
            var firstEntry = rawAssignments.FirstOrDefault()!;

            var model = new PrintReportViewModel
            {
                Rows = groupedRows,
                PersonRoleInReport = GetArabicRoleName(firstEntry?.Role?.RoleName.ToString() ?? ""),
                YearTimes = yearTimesMap // تمرير القاموس للـ ViewModel
            };

            return View(model);
        }

        // =====================================
        // PRINT CONTROL SHEET (استمارة اللجنة)
        // =====================================
        public async Task<IActionResult> PrintControlSheet(int? scheduleId)
        {
            if (scheduleId == null)
            {
                return View(new ExamControlSheetViewModel { ObserverRows = new List<ObserverRowItem>() });
            }

            var schedule = await _context.ExamSchedules
                .Include(s => s.Exam)
                    .ThenInclude(e => e.Subject)
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
                // تعديل هنا: سحب الوقت من schedule.Exam وليس من schedule مباشرة
                ExamTime = schedule.Exam != null
                    ? $"{DateTime.Today.Add(schedule.Exam.StartTime):hh:mm tt} - {DateTime.Today.Add(schedule.Exam.EndTime):hh:mm tt}"
                    : "................",
                ObserverRows = observers
            };

            return View(viewModel);
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
                // سحب اسم المادة من Exam المرتبط
                Text = $"{(s.Exam?.Subject?.SubjectName ?? "مادة غير محددة")} | اللجنة: {(s.Committee?.CommitteeNumber ?? 0)} | التاريخ: {s.ScheduledDate.ToString("dd/MM/yyyy")}"
            }).ToList();

            ViewData["ScheduleID"] = new SelectList(dropdownList, "ID", "Text", selectedSchedule);
        }

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

        private bool ReportExists(int id)
        {
            return _context.Reports.Any(e => e.ReportID == id);
        }
    }
}