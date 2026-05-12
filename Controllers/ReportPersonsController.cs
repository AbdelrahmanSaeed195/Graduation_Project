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
                .Include(r => r.Report)
                    .ThenInclude(rep => rep.ExamSchedule)
                        .ThenInclude(s => s.Exam)
                            .ThenInclude(e => e.Subject)
                .Include(r => r.Role)
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
                .Include(r => r.Role)
                .Include(r => r.Report)
                    .ThenInclude(rep => rep.ExamSchedule)
                        .ThenInclude(s => s.Exam)
                            .ThenInclude(e => e.Subject)
                .FirstOrDefaultAsync(m => m.ReportID == reportId && m.PersonID == personId);

            if (reportPerson == null) return NotFound();

            return View(reportPerson);
        }

        // =====================================
        // 3. إنشاء ارتباط جديد - CREATE
        // =====================================
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            PopulateDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("ReportID,PersonID,RoleID,Signature,SignedAt")] ReportPerson reportPerson)
        {
            if (ModelState.IsValid)
            {
                bool exists = await _context.ReportPersons.AnyAsync(rp => rp.ReportID == reportPerson.ReportID && rp.PersonID == reportPerson.PersonID);
                if (exists)
                {
                    ModelState.AddModelError("", "هذا الشخص مرتبط بالفعل بهذا المحضر");
                }
                else
                {
                    _context.Add(reportPerson);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم ربط الشخص بالمحضر بنجاح";
                    return RedirectToAction(nameof(Index));
                }
            }
            PopulateDropdowns(reportPerson);
            return View(reportPerson);
        }

        // =====================================
        // 4. تعديل الارتباط - EDIT
        // =====================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? reportId, int? personId)
        {
            if (reportId == null || personId == null) return NotFound();

            var reportPerson = await _context.ReportPersons.FindAsync(reportId, personId);
            if (reportPerson == null) return NotFound();

            PopulateDropdowns(reportPerson);
            return View(reportPerson);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int reportId, int personId, [Bind("ReportID,PersonID,RoleID,Signature,SignedAt")] ReportPerson reportPerson)
        {
            if (reportId != reportPerson.ReportID || personId != reportPerson.PersonID) return NotFound();

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
                    if (!ReportPersonExists(reportPerson.ReportID, reportPerson.PersonID)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            PopulateDropdowns(reportPerson);
            return View(reportPerson);
        }

        // =====================================
        // 5. الحذف - DELETE
        // =====================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? reportId, int? personId)
        {
            if (reportId == null || personId == null) return NotFound();

            var reportPerson = await _context.ReportPersons
                .Include(r => r.Person)
                .Include(r => r.Report)
                .FirstOrDefaultAsync(m => m.ReportID == reportId && m.PersonID == personId);

            if (reportPerson == null) return NotFound();

            return View(reportPerson);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
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
        // HELPERS (دوال مساعدة لتحسين تجربة المستخدم)
        // ==================================================

        private void PopulateDropdowns(ReportPerson rp = null)
        {
            var persons = _context.Persons.AsNoTracking().ToList().Select(p => new
            {
                ID = p.PersonId,
                Name = $"{p.FullName} ({GetEnumDisplayName(p.JobRole)})"
            });

            var reports = _context.Reports
                .Include(r => r.ExamSchedule).ThenInclude(s => s.Exam).ThenInclude(e => e.Subject)
                .AsNoTracking().ToList().Select(r => new
                {
                    ID = r.ReportID,
                    Text = $"محضر #{r.ReportID} - {r.ExamSchedule?.Exam?.Subject?.SubjectName} ({r.CreatedDate:yyyy/MM/dd})"
                });

            ViewData["PersonID"] = new SelectList(persons, "ID", "Name", rp?.PersonID);
            ViewData["ReportID"] = new SelectList(reports, "ID", "Text", rp?.ReportID);
            ViewData["RoleID"] = new SelectList(_context.Roles, "RoleID", "RoleName", rp?.RoleID);
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
            return _context.ReportPersons.Any(e => e.ReportID == reportId && e.PersonID == personId);
        }
    }
}