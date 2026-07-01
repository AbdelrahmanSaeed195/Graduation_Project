using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace projectweb.Controllers
{
    public class AssignmentSettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AssignmentSettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // شاشة الإعدادات الموحدة (GET)
        // ============================================================
        public async Task<IActionResult> Manage()
        {
            string currentYearCode = "2025 / 2026"; // السنة الحالية الافتراضية
            ViewBag.AcademicYearCode = currentYearCode;

            // جلب الإعدادات الحالية للسنة المحددة
            var settings = await _context.AssignmentSettings
                .Where(s => s.AcademicYearCode == currentYearCode)
                .ToListAsync();

            // تجهيز قائمة بالوظائف الثلاثة المستهدفة لضمان وجود سطر لكل وظيفة في الشاشة
            var targetRoles = new List<JobTitle> { JobTitle.Professor, JobTitle.AssistantProfessor, JobTitle.ProfessorEmeritus };
            var viewModel = new List<AssignmentSettings>();

            foreach (var role in targetRoles)
            {
                var existingSetting = settings.FirstOrDefault(s => s.JobRole == role);
                if (existingSetting != null)
                {
                    viewModel.Add(existingSetting);
                }
                else
                {
                    // إذا لم تكن موجودة ننشئ كائن مؤقت للعرض بقيمة افتراضية (4)
                    viewModel.Add(new AssignmentSettings
                    {
                        AcademicYearCode = currentYearCode,
                        JobRole = role,
                        MaxAssignmentsLimit = 4
                    });
                }
            }

            return View(viewModel);
        }

        // ============================================================
        // حفظ الإعدادات المجمعة ديناميكياً (POST)
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(List<AssignmentSettings> settingsList)
        {
            if (settingsList == null || !settingsList.Any())
            {
                TempData["Error"] = "عفواً، لا توجد بيانات لحفظها.";
                return RedirectToAction(nameof(Manage));
            }

            foreach (var setting in settingsList)
            {
                // البحث في قاعدة البيانات للتأكد من حالة السجل (جديد أم تعديل)
                var dbSetting = await _context.AssignmentSettings
                    .FirstOrDefaultAsync(s => s.AcademicYearCode == setting.AcademicYearCode && s.JobRole == setting.JobRole);

                if (dbSetting != null)
                {
                    // تعديل السقف الحالي
                    dbSetting.MaxAssignmentsLimit = setting.MaxAssignmentsLimit;
                    _context.Entry(dbSetting).State = EntityState.Modified;
                }
                else
                {
                    // إضافة سجل جديد تماماً للسنة الحالية
                    _context.AssignmentSettings.Add(setting);
                }
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم حفظ سقوف التكليفات الديناميكية لأعضاء هيئة التدريس بنجاح.";

            return RedirectToAction(nameof(Manage));
        }
    }
}