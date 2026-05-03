using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace projectweb.Controllers
{
    public class CommitteesController : Controller
    {
        private readonly ApplicationDbContext db;

        public CommitteesController(ApplicationDbContext context)
        {
            db = context;
        }

        // =====================================
        // عرض قائمة اللجان (Index)
        // =====================================
        public async Task<IActionResult> Index()
        {
            // جلب اللجان مع تضمين بيانات البلوك والقاعة لعرض الأسماء النصية
            var committees = await db.Committees
                .Include(c => c.Block)
                .ThenInclude(b => b.Hall)
                .OrderBy(c => c.CommitteeNumber)
                .ToListAsync();

            return View(committees);
        }

        // =====================================
        // تفاصيل اللجنة (Details)
        // =====================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return BadRequest();

            var committee = await db.Committees
                .Include(c => c.Block).ThenInclude(b => b.Hall)
                .Include(c => c.Students)
                .Include(c => c.CommitteesAssignments).ThenInclude(a => a.Person)
                .FirstOrDefaultAsync(c => c.CommitteeID == id);

            if (committee == null) return NotFound();

            return View(committee);
        }

        // =====================================
        // إضافة لجنة جديدة - GET
        // =====================================
        public IActionResult Create()
        {
            ViewBag.BlockID = GetBlockSelectList();
            ViewBag.StatusList = GetStatusList();
            return View();
        }

        // =====================================
        // إضافة لجنة جديدة - POST
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Committee committee)
        {
            if (ModelState.IsValid)
            {
                // 1. التحقق من عدم تكرار رقم اللجنة
                bool exists = await db.Committees.AnyAsync(c => c.CommitteeNumber == committee.CommitteeNumber);
                if (exists)
                {
                    ModelState.AddModelError("CommitteeNumber", "رقم اللجنة هذا موجود بالفعل.");
                }

                // 2. التحقق من سعة البلوك القصوى (لا يتخطى العدد المحدد)
                var targetBlock = await db.Blocks.Include(b => b.Committees).FirstOrDefaultAsync(b => b.BlockID == committee.BlockID);
                if (targetBlock != null && targetBlock.Committees.Count >= targetBlock.MaxCommittees)
                {
                    ModelState.AddModelError("BlockID", $"عفواً، هذا البلوك اكتمل بسعة {targetBlock.MaxCommittees} لجان ولا يمكن إضافة المزيد.");
                }

                if (ModelState.IsValid)
                {
                    db.Committees.Add(committee);
                    await db.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم إضافة اللجنة بنجاح.";
                    return RedirectToAction(nameof(Index));
                }
            }

            ViewBag.BlockID = GetBlockSelectList(committee.BlockID);
            ViewBag.StatusList = GetStatusList(committee.StatusOfCommittee);
            return View(committee);
        }

        // =====================================
        // تعديل لجنة - GET
        // =====================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return BadRequest();
            var committee = await db.Committees.FindAsync(id);
            if (committee == null) return NotFound();

            ViewBag.BlockID = GetBlockSelectList(committee.BlockID);
            ViewBag.StatusList = GetStatusList(committee.StatusOfCommittee);
            return View(committee);
        }

        // =====================================
        // تعديل لجنة - POST
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Committee committee)
        {
            if (id != committee.CommitteeID) return NotFound();

            if (ModelState.IsValid)
            {
                // 1. التحقق من تكرار الرقم مع استثناء اللجنة الحالية
                bool exists = await db.Committees.AnyAsync(c => c.CommitteeNumber == committee.CommitteeNumber && c.CommitteeID != id);
                if (exists)
                {
                    ModelState.AddModelError("CommitteeNumber", "رقم اللجنة هذا مسجل للجنة أخرى.");
                }

                // 2. التحقق من سعة البلوك عند النقل لبلوك آخر
                var targetBlock = await db.Blocks.Include(b => b.Committees).FirstOrDefaultAsync(b => b.BlockID == committee.BlockID);
                if (targetBlock != null)
                {
                    int currentCount = targetBlock.Committees.Count(c => c.CommitteeID != id);
                    if (currentCount >= targetBlock.MaxCommittees)
                    {
                        ModelState.AddModelError("BlockID", "لا يمكن نقل اللجنة لهذا البلوك لأنه مكتمل.");
                    }
                }

                if (ModelState.IsValid)
                {
                    db.Update(committee);
                    await db.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث بيانات اللجنة بنجاح.";
                    return RedirectToAction(nameof(Index));
                }
            }

            ViewBag.BlockID = GetBlockSelectList(committee.BlockID);
            ViewBag.StatusList = GetStatusList(committee.StatusOfCommittee);
            return View(committee);
        }

        // =====================================
        // حذف لجنة - POST
        // =====================================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var committee = await db.Committees.FindAsync(id);
            if (committee == null) return NotFound();

            // فحص الارتباطات قبل الحذف
            bool hasDependencies = await db.Students.AnyAsync(s => s.ExamSchedule.Committee.CommitteeID == id) ||
                                   await db.CommitteesAssignments.AnyAsync(a => a.CommitteeID == id);

            if (hasDependencies)
            {
                TempData["ErrorMessage"] = "لا يمكن حذف اللجنة لارتباطها بطلاب أو ملاحظين.";
                return RedirectToAction(nameof(Index));
            }

            db.Committees.Remove(committee);
            await db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف اللجنة بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // دوال مساعدة (Helpers)
        // =====================================
        private SelectList GetBlockSelectList(int? selected = null)
        {
            // عرض اسم القاعة واسم البلوك بدلاً من الأرقام لسهولة الاختيار
            var blocks = db.Blocks
                .Include(b => b.Hall)
                .Select(b => new
                {
                    b.BlockID,
                    DisplayName = "قاعة " + (b.Hall != null ? b.Hall.HallName : "---") + " / بلوك " + b.BlockName
                })
                .OrderBy(b => b.DisplayName)
                .ToList();

            return new SelectList(blocks, "BlockID", "DisplayName", selected);
        }

        private SelectList GetStatusList(string selected = null)
        {
            var list = new List<SelectListItem>
            {
                new SelectListItem { Value = "نشطة", Text = "نشطة" },
                new SelectListItem { Value = "مغلقة", Text = "مغلقة" },
                new SelectListItem { Value = "تحت الإعداد", Text = "تحت الإعداد" }
            };
            return new SelectList(list, "Value", "Text", selected);
        }
    }
}