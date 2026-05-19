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
    public class ReportPersonsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportPersonsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================
        // 1. القائمة الرئيسية - INDEX
        // =====================================
        public async Task<IActionResult> Index()
        {
            var reportPersons = await _context.ReportPersons
                .Include(r => r.Person)
                .Include(r => r.Student)
                .Include(r => r.Role)
                .Include(r => r.Report).ThenInclude(rep => rep.ExamSchedule).ThenInclude(s => s.Exam).ThenInclude(e => e.Subject)
                .OrderByDescending(rp => rp.SignedAt)
                .ToListAsync();

            return View(reportPersons);
        }

        // =====================================
        // 2. التفاصيل - DETAILS
        // =====================================
        public async Task<IActionResult> Details(int? reportId, int? personId)
        {
            if (reportId == null || personId == null) return NotFound();

            var reportPerson = await _context.ReportPersons
                .Include(r => r.Person)
                .Include(r => r.Student)
                .Include(r => r.Role)
                .Include(r => r.Report).ThenInclude(rep => rep.ExamSchedule).ThenInclude(s => s.Exam).ThenInclude(e => e.Subject)
                .FirstOrDefaultAsync(m => m.ReportId == reportId && m.PersonId == personId);

            if (reportPerson == null) return NotFound();

            return View(reportPerson);
        }

        // =====================================
        // 3. إنشاء ارتباط جديد - CREATE
        // =====================================
        public async Task<IActionResult> Create()
        {
            await PopulateDropdownsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("ReportId,PersonId,StudentId,RoleId,Signature,SignedAt")] ReportPerson reportPerson)
        {
            if (ModelState.IsValid)
            {
                bool exists = await _context.ReportPersons.AnyAsync(rp => rp.ReportId == reportPerson.ReportId && rp.PersonId == reportPerson.PersonId && rp.StudentId == reportPerson.StudentId);
                if (exists)
                {
                    ModelState.AddModelError("", "هذا الشخص أو الطالب مرتبط بالفعل بهذا المحضر الدراسية");
                }
                else
                {
                    _context.Add(reportPerson);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم ربط الشخص والموقع بالمحضر بنجاح";
                    return RedirectToAction(nameof(Index));
                }
            }
            await PopulateDropdownsAsync(reportPerson);
            return View(reportPerson);
        }

        // =====================================
        // 4. تعديل الارتباط - EDIT
        // =====================================
        public async Task<IActionResult> Edit(int? reportId, int? personId)
        {
            if (reportId == null || personId == null) return NotFound();

            // 🌟 تم التعديل هنا لعمل Include كامل لبيانات السجل لمنع ظهور الطلاسم أو الحقول الفارغة
            var reportPerson = await _context.ReportPersons
                .Include(r => r.Person)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(m => m.ReportId == reportId && m.PersonId == personId);

            if (reportPerson == null) return NotFound();

            await PopulateDropdownsAsync(reportPerson);
            return View(reportPerson);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int reportId, int personId, [Bind("ReportId,PersonId,StudentId,RoleId,Signature,SignedAt")] ReportPerson reportPerson)
        {
            if (reportId != reportPerson.ReportId || personId != reportPerson.PersonId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(reportPerson);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث البيانات بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReportPersonExists(reportPerson.ReportId, reportPerson.PersonId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            await PopulateDropdownsAsync(reportPerson);
            return View(reportPerson);
        }

        // =====================================
        // 5. الحذف - DELETE
        // =====================================
        public async Task<IActionResult> Delete(int? reportId, int? personId)
        {
            if (reportId == null || personId == null) return NotFound();

            var reportPerson = await _context.ReportPersons
                .Include(r => r.Person)
                .Include(r => r.Student)
                .Include(r => r.Report)
                .FirstOrDefaultAsync(m => m.ReportId == reportId && m.PersonId == personId);

            if (reportPerson == null) return NotFound();

            return View(reportPerson);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int reportId, int personId)
        {
            var reportPerson = await _context.ReportPersons.FindAsync(reportId, personId);
            if (reportPerson != null)
            {
                _context.ReportPersons.Remove(reportPerson);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إزالة الشخص من المحضر";
            }
            return RedirectToAction(nameof(Index));
        }

        // ==================================================
        // HELPERS (🌟 تحويل كامل لـ Async لمنع مشاكل الـ HTML Encoding والرموز المشوهة)
        // ==================================================
        private async Task PopulateDropdownsAsync(ReportPerson rp = null)
        {
            var dbPersons = await _context.Persons.AsNoTracking().ToListAsync();
            var persons = dbPersons.Select(p => new
            {
                ID = p.PersonId,
                Name = $"{p.FullName} ({GetEnumDisplayName(p.JobRole)})"
            }).ToList();

            var dbStudents = await _context.Students.AsNoTracking().ToListAsync();
            var students = dbStudents.Select(s => new
            {
                ID = s.StudentId,
                Name = $"{s.FullName} (رقم جلوس: {s.SeatNumber})"
            }).ToList();

            var dbReports = await _context.Reports
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Exam).ThenInclude(e => e.Subject)
                .AsNoTracking()
                .ToListAsync();

            var reports = dbReports.Select(r => new
            {
                ID = r.ReportId,
                Text = r.ExamSchedule?.Exam?.Subject != null
                    ? $"محضر #{r.ReportId} - {r.ExamSchedule.Exam.Subject.SubjectName} ({r.CreatedDate:yyyy/MM/dd})"
                    : $"محضر #{r.ReportId} ({r.CreatedDate:yyyy/MM/dd})"
            }).ToList();

            var roles = await _context.Roles.AsNoTracking().ToListAsync();

            ViewData["PersonId"] = new SelectList(persons, "ID", "Name", rp?.PersonId);
            ViewData["StudentId"] = new SelectList(students, "ID", "Name", rp?.StudentId);
            ViewData["ReportId"] = new SelectList(reports, "ID", "Text", rp?.ReportId);
            ViewData["RoleId"] = new SelectList(roles, "RoleID", "RoleDescription", rp?.RoleId);
        }

        private string GetEnumDisplayName(Enum enumValue)
        {
            return enumValue.GetType()
                             .GetMember(enumValue.ToString())
                             .First()
                             .GetCustomAttribute<DisplayAttribute>()
                             ?.Name ?? enumValue.ToString();
        }

        private bool ReportPersonExists(int reportId, int personId)
        {
            return _context.ReportPersons.Any(e => e.ReportId == reportId && e.PersonId == personId);
        }
    }
}