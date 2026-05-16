using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

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
        // 1. INDEX - عرض المحاضر
        // =====================================
        public async Task<IActionResult> Index(int? reportType)
        {
            var reportsQuery = _context.Reports
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Exam).ThenInclude(e => e.Subject)
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Committee)
                .AsQueryable();

            if (reportType.HasValue && reportType.Value > 0)
            {
                var status = (ReportStatus)reportType.Value;
                reportsQuery = reportsQuery.Where(r => r.Status == status);
            }

            var reports = await reportsQuery.OrderByDescending(r => r.CreatedDate).ToListAsync();
            ViewBag.ReportStatusList = GetArabicEnumList<ReportStatus>();
            return View(reports);
        }

        // =====================================
        // 2. DETAILS - تفاصيل المحضر
        // =====================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var report = await _context.Reports
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Exam).ThenInclude(e => e.Subject)
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Committee)
                .Include(r => r.ReportPersons).ThenInclude(rp => rp.Person)
                .Include(r => r.ReportPersons).ThenInclude(rp => rp.Student)
                .Include(r => r.ReportPersons).ThenInclude(rp => rp.Role)
                .FirstOrDefaultAsync(m => m.ReportId == id);

            if (report == null) return NotFound();
            return View(report);
        }

        // =====================================
        // 3. CREATE - شاشة إنشاء محضر جديد
        // =====================================
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            PopulateExamsDropDownList();
            ViewBag.StatusList = GetArabicEnumList<ReportStatus>();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("ReportId,Status,Notes,ScheduleId")] Report report, int[] SelectedStaffIds, int? SelectedStudentId)
        {
            if (ModelState.IsValid)
            {
                report.CreatedDate = DateTime.Now;
                _context.Add(report);
                await _context.SaveChangesAsync();

                if (SelectedStudentId.HasValue)
                {
                    _context.ReportPersons.Add(new ReportPerson
                    {
                        ReportId = report.ReportId,
                        PersonId = 0, 
                        StudentId = SelectedStudentId.Value,
                        RoleId = 1, // دور افتراطي
                        SignedAt = DateTime.Now,
                        Signature = "Waiting"
                    });
                }

                // حفظ الموظفين والملاحظين الذين رصدوا الواقعة
                if (SelectedStaffIds != null)
                {
                    foreach (var staffId in SelectedStaffIds)
                    {
                        var assignment = await _context.CommitteesAssignments
                            .FirstOrDefaultAsync(a => a.PersonId == staffId && a.ExamScheduleId == report.ScheduleId);

                        _context.ReportPersons.Add(new ReportPerson
                        {
                            ReportId = report.ReportId,
                            PersonId = staffId,
                            RoleId = assignment?.RoleId ?? 1,
                            SignedAt = DateTime.Now,
                            Signature = "Pending"
                        });
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إنشاء المحضر بنجاح";
                return RedirectToAction(nameof(Index));
            }

            PopulateExamsDropDownList();
            ViewBag.StatusList = GetArabicEnumList<ReportStatus>();
            return View(report);
        }

        // =====================================
        // 4. EDIT - تعديل المحضر
        // =====================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var report = await _context.Reports
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Exam)
                .Include(r => r.ReportPersons)
                .FirstOrDefaultAsync(r => r.ReportId == id);

            if (report == null) return NotFound();

            // استرجاع الطالب المختار سابقاً
            var studentParty = report.ReportPersons.FirstOrDefault(rp => rp.StudentId != null);
            ViewBag.SelectedStudentId = studentParty?.StudentId;

            // استرجاع الموظفين المختارين سابقاً
            ViewBag.CurrentStaffIds = report.ReportPersons
                .Where(rp => rp.PersonId != 0)
                .Select(rp => rp.PersonId).ToArray();

            int currentExamId = report.ExamSchedule?.ExamId ?? 0;
            PopulateExamsDropDownList(currentExamId);
            ViewBag.StatusList = GetArabicEnumList<ReportStatus>();
            ViewBag.SelectedExamId = currentExamId;
            ViewBag.SelectedScheduleId = report.ScheduleId;

            return View(report);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("ReportId,CreatedDate,Status,Notes,ScheduleId")] Report report, int[] SelectedStaffIds, int? SelectedStudentId)
        {
            if (id != report.ReportId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(report);

                    // حذف الموقّعين القدامى لإعادة بنائهم من جديد بشكل نظيف
                    var existingPersons = _context.ReportPersons.Where(rp => rp.ReportId == id);
                    _context.ReportPersons.RemoveRange(existingPersons);
                    await _context.SaveChangesAsync();

                    // إعادة حفظ الطالب
                    if (SelectedStudentId.HasValue)
                    {
                        _context.ReportPersons.Add(new ReportPerson
                        {
                            ReportId = report.ReportId,
                            PersonId = 0,
                            StudentId = SelectedStudentId.Value,
                            RoleId = 1,
                            SignedAt = DateTime.Now,
                            Signature = "Waiting"
                        });
                    }

                    // إعادة حفظ الموظفين والملاحظين
                    if (SelectedStaffIds != null)
                    {
                        foreach (var staffId in SelectedStaffIds)
                        {
                            var assignment = await _context.CommitteesAssignments
                                .FirstOrDefaultAsync(a => a.PersonId == staffId && a.ExamScheduleId == report.ScheduleId);

                            _context.ReportPersons.Add(new ReportPerson
                            {
                                ReportId = report.ReportId,
                                PersonId = staffId,
                                RoleId = assignment?.RoleId ?? 1,
                                SignedAt = DateTime.Now,
                                Signature = "Pending"
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث المحضر بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReportExists(report.ReportId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(report);
        }

        // =====================================
        // 5. DELETE - حذف المحضر
        // =====================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var report = await _context.Reports
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Exam).ThenInclude(e => e.Subject)
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Committee)
                .Include(r => r.ReportPersons)
                .FirstOrDefaultAsync(m => m.ReportId == id);

            if (report == null) return NotFound();
            return View(report);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var associatedPersons = _context.ReportPersons.Where(rp => rp.ReportId == id);
            _context.ReportPersons.RemoveRange(associatedPersons);

            var report = await _context.Reports.FindAsync(id);
            if (report != null)
            {
                _context.Reports.Remove(report);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // 6. AJAX METHODS - دوال تفاعلية للشاشة
        // =====================================
        [HttpGet]
        public async Task<JsonResult> GetCommitteeParties(int scheduleId)
        {
            var students = await _context.Students
                .Where(s => s.ExamScheduleId == scheduleId)
                .Select(s => new { id = s.StudentId, name = s.FullName + " (جلوس: " + s.SeatNumber + ")" })
                .ToListAsync();
            var staff = await _context.CommitteesAssignments
                .Include(a => a.Person)
                .Where(a => a.ExamScheduleId == scheduleId)
                .Select(a => new { id = a.PersonId, name = a.Person.FullName + " [" + GetEnumDisplayName(a.Person.JobRole) + "]" })
                .ToListAsync();
            return Json(new { students = students, staff = staff });
        }

        [HttpGet]
        public async Task<JsonResult> GetCommitteesByExam(int examId)
        {
            var committees = await _context.ExamSchedules
                .Include(s => s.Committee)
                .Where(s => s.ExamId == examId && s.Committee != null)
                .Select(s => new { id = s.ExamScheduleId, name = "لجنة رقم: " + s.Committee.CommitteeNumber })
                .ToListAsync();
            return Json(committees);
        }

        // =====================================
        // 7. HELPERS - دوال مساعدة للـ Enums والقوائم
        // =====================================
        private string GetEnumDisplayName(Enum enumValue) =>
            enumValue.GetType().GetMember(enumValue.ToString()).First().GetCustomAttribute<DisplayAttribute>()?.Name ?? enumValue.ToString();

        private List<SelectListItem> GetArabicEnumList<T>() where T : Enum =>
            Enum.GetValues(typeof(T)).Cast<T>().Select(e => new SelectListItem { Value = Convert.ToInt32(e).ToString(), Text = GetEnumDisplayName(e) }).ToList();

        private void PopulateExamsDropDownList(object selectedExam = null)
        {
            var exams = _context.Exams.Include(e => e.Subject).OrderByDescending(e => e.ExamDate)
                .Select(e => new { ID = e.ExamId, Text = $"{e.Subject.SubjectName} | {e.ExamDate:yyyy/MM/dd}" }).ToList();
            ViewData["ExamId"] = new SelectList(exams, "ID", "Text", selectedExam);
        }

        private bool ReportExists(int id) => _context.Reports.Any(e => e.ReportId == id);
    }
}