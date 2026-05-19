using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using projectweb.Models.ViewModels;
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

            // تم تعديل الـ Include الذاهب للـ ExamSchedules ليتوافق مع الموديل الجديد
            var committee = await db.Committees
                .Include(c => c.Block).ThenInclude(b => b.Hall)
                .Include(c => c.Students)
                .Include(c => c.ExamSchedules)
                    .ThenInclude(es => es.Exam)
                        .ThenInclude(e => e.Subject)
                .FirstOrDefaultAsync(c => c.CommitteeId == id);

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
                bool exists = await db.Committees.AnyAsync(c => c.CommitteeNumber == committee.CommitteeNumber);
                if (exists)
                {
                    ModelState.AddModelError("CommitteeNumber", "رقم اللجنة هذا موجود بالفعل.");
                }

                var targetBlock = await db.Blocks.Include(b => b.Committees).FirstOrDefaultAsync(b => b.BlockId == committee.BlockId);
                if (targetBlock != null && targetBlock.Committees.Count >= targetBlock.MaxCommittees)
                {
                    ModelState.AddModelError("BlockId", $"عفواً، هذا البلوك اكتمل بسعة {targetBlock.MaxCommittees} لجان ولا يمكن إضافة المزيد.");
                }

                if (ModelState.IsValid)
                {
                    db.Committees.Add(committee);
                    await db.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم إضافة اللجنة بنجاح.";
                    return RedirectToAction(nameof(Index));
                }
            }

            ViewBag.BlockID = GetBlockSelectList(committee.BlockId);
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

            ViewBag.BlockID = GetBlockSelectList(committee.BlockId);
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
            if (id != committee.CommitteeId) return NotFound();

            if (ModelState.IsValid)
            {
                bool exists = await db.Committees.AnyAsync(c => c.CommitteeNumber == committee.CommitteeNumber && c.CommitteeId != id);
                if (exists)
                {
                    ModelState.AddModelError("CommitteeNumber", "رقم اللجنة هذا مسجل للجنة أخرى.");
                }

                var targetBlock = await db.Blocks.Include(b => b.Committees).FirstOrDefaultAsync(b => b.BlockId == committee.BlockId);
                if (targetBlock != null)
                {
                    int currentCount = targetBlock.Committees.Count(c => c.CommitteeId != id);
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

            ViewBag.BlockID = GetBlockSelectList(committee.BlockId);
            ViewBag.StatusList = GetStatusList(committee.StatusOfCommittee);
            return View(committee);
        }

        // =====================================
        // Delete Get
        // =====================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var committee = await db.Committees
                .Include(c => c.Block)
                    .ThenInclude(b => b.Hall)
                .FirstOrDefaultAsync(m => m.CommitteeId == id);

            if (committee == null)
            {
                return NotFound();
            }

            // تم تعديل فحص الطلاب ليعتمد على حقل الـ CommitteeId المباشر في جدول الطلاب
            bool hasStudents = await db.Students.AnyAsync(s => s.CommitteeId == id);
            bool hasAssignments = await db.CommitteesAssignments.AnyAsync(a => a.CommitteeId == id);

            if (hasStudents || hasAssignments)
            {
                ViewBag.CanDelete = false;
                ViewBag.Message = "تنبيه: هذه اللجنة مرتبطة بطلاب موزعين أو تكليفات ملاحظين. يجب إلغاء التوزيعات أولاً لتتمكن من حذفها.";
            }
            else
            {
                ViewBag.CanDelete = true;
            }

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

            // تم التحديث هنا أيضاً ليعتمد الفحص على حقل الربط المباشر لحماية تكامل البيانات
            bool hasDependencies = await db.Students.AnyAsync(s => s.CommitteeId == id) ||
                                   await db.CommitteesAssignments.AnyAsync(a => a.CommitteeId == id);

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
            var blocks = db.Blocks
                .Include(b => b.Hall)
                .Select(b => new
                {
                    b.BlockId,
                    DisplayName = "قاعة " + (b.Hall != null ? b.Hall.HallName : "---") + " / بلوك " + b.BlockName
                })
                .OrderBy(b => b.DisplayName)
                .ToList();

            return new SelectList(blocks, "BlockId", "DisplayName", selected);
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