using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace projectweb.Controllers
{
    public class ExamLocationsController : Controller
    {
        private readonly ApplicationDbContext db;

        public ExamLocationsController(ApplicationDbContext context)
        {
            db = context;
        }

        private string GetLocationTypeDisplayName(LocationType type)
        {
            return type.GetType()
                .GetField(type.ToString())?
                .GetCustomAttributes(typeof(DisplayAttribute), false)
                .FirstOrDefault() is DisplayAttribute attr ? attr.Name : type.ToString();
        }

        private SelectList GetStatusListByType(LocationType type, string? selectedValue = null)
        {
            List<string> statuses;
            switch (type)
            {
                case LocationType.Committee:
                    statuses = new List<string> { "نشطة", "مغلقة", "تحت الإعداد", "محجوزة" };
                    break;
                case LocationType.Hall:
                case LocationType.Block:
                default:
                    statuses = new List<string> { "نشطة", "مغلقة", "تحت الإعداد" };
                    break;
            }
            return new SelectList(statuses, selectedValue);
        }

        // ========================
        // Index
        // ========================
        public async Task<IActionResult> Index(LocationType? type)
        {
            var query = db.ExamLocations
                .Include(l => l.ParentLocation)
                .Include(l => l.SubLocations)
                .AsNoTracking();

            if (type.HasValue)
                query = query.Where(l => l.Type == type.Value);

            var locations = await query.OrderBy(l => l.Type).ThenBy(l => l.LocationName).ToListAsync();
            return View(locations);
        }

        // ========================
        // Details
        // ========================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return BadRequest();

            var location = await db.ExamLocations
                .Include(l => l.ParentLocation)
                .Include(l => l.SubLocations)
                .FirstOrDefaultAsync(l => l.LocationId == id);

            if (location == null) return NotFound();
            return View(location);
        }

        // ========================
        // GET: Create
        // ========================
        public async Task<IActionResult> Create()
        {
            await LoadDropdownLists();
            return View();
        }

        // ========================
        // POST: Create
        // ========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ExamLocation location)
        {
            // التحقق من تكرار الاسم
            bool isNameExist = await db.ExamLocations.AnyAsync(l =>
                l.LocationName == location.LocationName &&
                l.ParentLocationId == location.ParentLocationId &&
                l.Type == location.Type);

            if (isNameExist)
                ModelState.AddModelError("LocationName", "هذا الاسم موجود بالفعل في هذا المستوى.");

            // التحقق من السعة الفرعية للأب
            if (location.ParentLocationId != null)
            {
                var parent = await db.ExamLocations
                    .Include(p => p.SubLocations)
                    .FirstOrDefaultAsync(p => p.LocationId == location.ParentLocationId);

                if (parent == null)
                {
                    // ✅ key فاضي عشان يظهر في validation-summary
                    ModelState.AddModelError("", "المكان الرئيسي غير موجود.");
                }
                else if (parent.SubLocations != null && parent.MaxSubLocations > 0
                         && parent.SubLocations.Count >= parent.MaxSubLocations)
                {
                    // ✅ key فاضي عشان يظهر في validation-summary
                    ModelState.AddModelError("",
                        $"عفواً، المكان الرئيسي ({parent.LocationName}) استوفى الحد الأقصى ({parent.MaxSubLocations}) ولا يمكن إضافة المزيد.");
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadDropdownLists(location.ParentLocationId);
                return View(location);
            }

            db.ExamLocations.Add(location);
            await db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم إضافة المكان بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // ========================
        // GET: Edit
        // ========================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var location = await db.ExamLocations.FindAsync(id);
            if (location == null) return NotFound();

            ViewBag.TypeDisplayName = GetLocationTypeDisplayName(location.Type);
            ViewBag.StatusList = GetStatusListByType(location.Type, location.Status);
            await LoadDropdownLists(location.ParentLocationId);
            return View(location);
        }

        // ========================
        // POST: Edit
        // ========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ExamLocation location)
        {
            if (id != location.LocationId) return NotFound();

            var existingLocation = await db.ExamLocations.FindAsync(id);
            if (existingLocation == null) return NotFound();

            bool isNameExist = await db.ExamLocations.AnyAsync(l =>
                l.LocationName == location.LocationName &&
                l.ParentLocationId == location.ParentLocationId &&
                l.Type == location.Type &&
                l.LocationId != id);

            if (isNameExist)
                ModelState.AddModelError("LocationName", "هذا الاسم موجود بالفعل في هذا المستوى.");

            if (ModelState.IsValid)
            {
                try
                {
                    existingLocation.LocationName = location.LocationName;
                    existingLocation.Floor = location.Floor;
                    existingLocation.MaxSubLocations = location.MaxSubLocations;
                    existingLocation.StudentCapacity = location.StudentCapacity;
                    existingLocation.Status = location.Status;
                    existingLocation.ParentLocationId = location.ParentLocationId;
                    

                    await db.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث البيانات بنجاح.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "حدث خطأ أثناء الحفظ.");
                }
            }

            ViewBag.TypeDisplayName = GetLocationTypeDisplayName(existingLocation.Type);
            ViewBag.StatusList = GetStatusListByType(existingLocation.Type, location.Status);
            await LoadDropdownLists(location.ParentLocationId);
            return View(location);
        }

        // ========================
        // GET: Delete
        // ========================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return BadRequest();

            var location = await db.ExamLocations
                .Include(l => l.SubLocations)
                .FirstOrDefaultAsync(l => l.LocationId == id);

            if (location == null) return NotFound();

            if (location.SubLocations.Any())
            {
                ViewBag.Message = "لا يمكن الحذف: هذا المكان يحتوي على تفرعات (أماكن تابعة).";
                ViewBag.CanDelete = false;
            }
            else
            {
                ViewBag.CanDelete = true;
            }

            return View(location);
        }

        // ========================
        // POST: Delete
        // ========================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var location = await db.ExamLocations.FindAsync(id);
            if (location != null)
            {
                db.ExamLocations.Remove(location);
                await db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم الحذف بنجاح.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ========================
        // Helper: LoadDropdownLists
        // ========================
        private async Task LoadDropdownLists(int? selectedParentId = null)
        {
            var allLocations = await db.ExamLocations
                .Select(l => new {
                    l.LocationId,
                    l.Type,
                    l.LocationName,
                    l.ParentLocationId,
                    l.MaxSubLocations,
                    // ✅ بنجيب العدد الحالي عشان الـ JS يقدر يعرض تحذير
                    CurrentSubCount = db.ExamLocations.Count(s => s.ParentLocationId == l.LocationId)
                })
                .OrderBy(l => l.LocationName)
                .ToListAsync();

            ViewBag.AllLocationsJson = Newtonsoft.Json.JsonConvert.SerializeObject(allLocations);

            var statusList = new List<SelectListItem>
            {
                new SelectListItem { Value = "نشطة",        Text = "نشطة" },
                new SelectListItem { Value = "مغلقة",       Text = "مغلقة" },
                new SelectListItem { Value = "تحت الإعداد", Text = "تحت الإعداد" }
            };
            ViewBag.StatusList = new SelectList(statusList, "Value", "Text");
        }
    }
}