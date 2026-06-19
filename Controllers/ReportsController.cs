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
                .Include(r => r.Committee)
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Exam).ThenInclude(e => e.Subject)
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Block)
                .AsQueryable();

            if (reportType.HasValue && reportType.Value > 0)
            {
                var status = (ReportStatus)reportType.Value;
                reportsQuery = reportsQuery.Where(r => r.Status == status);
            }

            var reports = await reportsQuery.OrderByDescending(r => r.CreatedDate).ToListAsync();

            ViewBag.ReportStatusList = GetArabicEnumList<ReportStatus>();

            ViewBag.SelectedReportType = reportType;

            return View(reports);
        }

        // =====================================
        // 2. DETAILS - تفاصيل المحضر
        // =====================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var report = await _context.Reports
                .Include(r => r.Committee) 
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Exam).ThenInclude(e => e.Subject)
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Block)
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
        public async Task<IActionResult> Create()
        {
            await PopulateExamsDropDownListAsync();
            ViewBag.StatusList = GetArabicEnumList<ReportStatus>();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ReportId,Status,Notes,ScheduleId,CommitteeId")] Report report, int[] SelectedStaffIds, int? SelectedStudentId)
        {
            if (SelectedStudentId.HasValue)
            {
                var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == SelectedStudentId.Value);
                if (student != null)
                {
                    report.CommitteeId = student.CommitteeId ?? 0;
                }
            }
            else
            {
                var examSchedule = await _context.ExamSchedules.AsNoTracking().FirstOrDefaultAsync(es => es.ExamScheduleId == report.ScheduleId);
                if (examSchedule != null)
                {
                    var firstCommitteeInBlock = await _context.Committees.AsNoTracking().FirstOrDefaultAsync(c => c.BlockId == examSchedule.BlockId);
                    report.CommitteeId = firstCommitteeInBlock?.CommitteeId ?? 0;
                }
            }

            if (ModelState.IsValid)
            {
                report.CreatedDate = DateTime.Now;
                _context.Add(report);
                await _context.SaveChangesAsync();

                int fallbackPersonId = (SelectedStaffIds != null && SelectedStaffIds.Length > 0)
                    ? SelectedStaffIds[0]
                    : _context.Persons.Select(p => p.PersonId).FirstOrDefault();
                if (SelectedStudentId.HasValue && fallbackPersonId > 0)
                {
                    _context.ReportPersons.Add(new ReportPerson
                    {
                        ReportId = report.ReportId,
                        PersonId = fallbackPersonId,
                        StudentId = SelectedStudentId.Value,
                        RoleId = 1, 
                        SignedAt = DateTime.Now,
                        Signature = "Waiting"
                    });
                }

                if (SelectedStaffIds != null)
                {
                    foreach (var staffId in SelectedStaffIds)
                    {
                        if (SelectedStudentId.HasValue && staffId == fallbackPersonId)
                        {
                            continue;
                        }

                        var assignment = await _context.CommitteesAssignments
                            .FirstOrDefaultAsync(a => a.PersonId == staffId && a.ExamScheduleId == report.ScheduleId);

                        _context.ReportPersons.Add(new ReportPerson
                        {
                            ReportId = report.ReportId,
                            PersonId = staffId,
                            StudentId = null, 
                            RoleId = assignment?.RoleId ?? 1,
                            SignedAt = DateTime.Now,
                            Signature = "Pending"
                        });
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إنشاء المحضر الأكاديمي بنجاح";
                return RedirectToAction(nameof(Index));
            }

            await PopulateExamsDropDownListAsync();
            ViewBag.StatusList = GetArabicEnumList<ReportStatus>();
            return View(report);
        }

        // =====================================
        // 4. EDIT - تعديل المحضر
        // =====================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var report = await _context.Reports
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Exam)
                .Include(r => r.ReportPersons)
                .FirstOrDefaultAsync(r => r.ReportId == id);

            if (report == null) return NotFound();

            var studentParty = report.ReportPersons.FirstOrDefault(rp => rp.StudentId != null);
            ViewBag.SelectedStudentId = studentParty?.StudentId;

            ViewBag.CurrentStaffIds = report.ReportPersons
                .Where(rp => rp.PersonId != 0)
                .Select(rp => rp.PersonId).Distinct().ToArray();

            int currentExamId = report.ExamSchedule?.ExamId ?? 0;
            await PopulateExamsDropDownListAsync(currentExamId);
            ViewBag.StatusList = GetArabicEnumList<ReportStatus>();
            ViewBag.SelectedExamId = currentExamId;
            ViewBag.SelectedScheduleId = report.ScheduleId;

            return View(report);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ReportId,CreatedDate,Status,Notes,ScheduleId,CommitteeId")] Report report, int[] SelectedStaffIds, int? SelectedStudentId)
        {
            if (id != report.ReportId) return NotFound();

            if (SelectedStudentId.HasValue)
            {
                var student = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == SelectedStudentId.Value);
                if (student != null)
                {
                    report.CommitteeId = student.CommitteeId ?? 0;
                }
            }
            else
            {
                var examSchedule = await _context.ExamSchedules.AsNoTracking().FirstOrDefaultAsync(es => es.ExamScheduleId == report.ScheduleId);
                if (examSchedule != null)
                {
                    var firstCommitteeInBlock = await _context.Committees.AsNoTracking().FirstOrDefaultAsync(c => c.BlockId == examSchedule.BlockId);
                    report.CommitteeId = firstCommitteeInBlock?.CommitteeId ?? 0;
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(report);

                    var existingPersons = _context.ReportPersons.Where(rp => rp.ReportId == id);
                    _context.ReportPersons.RemoveRange(existingPersons);
                    await _context.SaveChangesAsync();

                    int fallbackPersonId = (SelectedStaffIds != null && SelectedStaffIds.Length > 0)
                        ? SelectedStaffIds[0]
                        : _context.Persons.Select(p => p.PersonId).FirstOrDefault();

                    if (SelectedStudentId.HasValue && fallbackPersonId > 0)
                    {
                        _context.ReportPersons.Add(new ReportPerson
                        {
                            ReportId = report.ReportId,
                            PersonId = fallbackPersonId,
                            StudentId = SelectedStudentId.Value,
                            RoleId = 1,
                            SignedAt = DateTime.Now,
                            Signature = "Waiting"
                        });
                    }

                    if (SelectedStaffIds != null)
                    {
                        foreach (var staffId in SelectedStaffIds)
                        {
                            if (SelectedStudentId.HasValue && staffId == fallbackPersonId)
                            {
                                continue;
                            }

                            var assignment = await _context.CommitteesAssignments
                                .FirstOrDefaultAsync(a => a.PersonId == staffId && a.ExamScheduleId == report.ScheduleId);

                            _context.ReportPersons.Add(new ReportPerson
                            {
                                ReportId = report.ReportId,
                                PersonId = staffId,
                                StudentId = null,
                                RoleId = assignment?.RoleId ?? 1,
                                SignedAt = DateTime.Now,
                                Signature = "Pending"
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث بيانات المحضر بنجاح";
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
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var report = await _context.Reports
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Exam).ThenInclude(e => e.Subject)
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Block)
                .Include(r => r.ReportPersons)
                .FirstOrDefaultAsync(m => m.ReportId == id);

            if (report == null) return NotFound();
            return View(report);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
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

        // ========================================================================
        // 6. AJAX METHODS
        // ========================================================================
        [HttpGet]
        public async Task<JsonResult> GetCommitteeParties(int scheduleId)
        {
            var examSchedule = await _context.ExamSchedules
                .Include(es => es.Block)
                .FirstOrDefaultAsync(es => es.ExamScheduleId == scheduleId);

            var students = new List<object>();

            if (examSchedule != null)
            {
                var committeeIdsInBlock = await _context.Committees
                    .Where(c => c.BlockId == examSchedule.BlockId)
                    .Select(c => c.CommitteeId)
                    .ToListAsync();

                var dbStudents = await _context.Students
                    .Where(s => s.CommitteeId.HasValue && committeeIdsInBlock.Contains(s.CommitteeId.Value))
                    .Select(s => new {
                        s.StudentId,
                        s.FullName,
                        s.SeatNumber
                    })
                    .ToListAsync();

                foreach (var s in dbStudents)
                {
                    students.Add(new
                    {
                        id = s.StudentId,
                        name = s.FullName + " (جلوس: " + s.SeatNumber.ToString() + ")"
                    });
                }
            }

            var staff = await _context.CommitteesAssignments
                .Include(a => a.Person)
                .Where(a => a.ExamScheduleId == scheduleId && a.Person != null)
                .Select(a => new { id = a.PersonId, name = a.Person.FullName + " [" + (a.RoleType ?? "ملاحظ") + "]" })
                .ToListAsync();

            return Json(new { students = students, staff = staff });
        }

        [HttpGet]
        public async Task<JsonResult> GetCommitteesByExam(int examId)
        {
            var committees = await _context.ExamSchedules
                .Include(s => s.Block)
                .Where(s => s.ExamId == examId && s.Block != null)
                .Select(s => new { id = s.ExamScheduleId, name = " صالة: " + s.Block.BlockName })
                .ToListAsync();

            return Json(committees);
        }

        // =====================================
        // 7. HELPERS
        // =====================================
        private string GetEnumDisplayName(Enum enumValue) =>
            enumValue.GetType().GetMember(enumValue.ToString()).First().GetCustomAttribute<DisplayAttribute>()?.Name ?? enumValue.ToString();

        private List<SelectListItem> GetArabicEnumList<T>() where T : Enum =>
            Enum.GetValues(typeof(T)).Cast<T>().Select(e => new SelectListItem { Value = Convert.ToInt32(e).ToString(), Text = GetEnumDisplayName(e) }).ToList();

        private async Task PopulateExamsDropDownListAsync(object selectedExam = null)
        {
            var examsList = await _context.Exams.Include(e => e.Subject).OrderByDescending(e => e.ExamDate).ToListAsync();

            var exams = examsList.Select(e => {
                string arabicLevel = "غير محدد";
                if (e.Subject != null)
                {
                    arabicLevel = e.Subject.AcademicYear switch
                    {
                        AcademicLevel.FirstYear => "المستوى الأول",
                        AcademicLevel.SecondYear => "المستوى الثاني",
                        AcademicLevel.ThirdYear => "المستوى الثالث",
                        AcademicLevel.FourthYear => "المستوى الرابع",
                        _ => "غير محدد"
                    };
                }
                return new
                {
                    ID = e.ExamId,
                    Text = e.Subject != null
                        ? $"{e.Subject.SubjectName} - ({arabicLevel}) | {e.ExamDate:yyyy/MM/dd}"
                        : $"امتحان #{e.ExamId} | {e.ExamDate:yyyy/MM/dd}"
                };
            }).ToList();

            ViewData["ExamID"] = new SelectList(exams, "ID", "Text", selectedExam);
        }

        private bool ReportExists(int id) => _context.Reports.Any(e => e.ReportId == id);
    }
}