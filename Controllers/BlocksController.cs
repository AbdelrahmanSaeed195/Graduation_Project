using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace projectweb.Controllers
{
    public class BlocksController : Controller
    {
        private readonly ApplicationDbContext db;

        public BlocksController(ApplicationDbContext context)
        {
            db = context;
        }

        // =========================
        // INDEX
        // =========================
        public async Task<IActionResult> Index()
        {
            var blocks = await db.Blocks
                .Include(b => b.Hall)
                .Include(b => b.Committees)
                .OrderBy(b => b.Hall.HallName)
                .ThenBy(b => b.BlockName)
                .ToListAsync();

            return View(blocks);
        }
        // =========================
        // DETAILS
        // =========================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return BadRequest();
            var block = await db.Blocks
                .Include(b => b.Hall)
                .Include(b => b.Committees)
                    .ThenInclude(c => c.CommitteesAssignments)
                        .ThenInclude(a => a.Person)
                .FirstOrDefaultAsync(b => b.BlockID == id);

            if (block == null) return NotFound();
            return View(block);
        }

        // =========================
        // CREATE - GET
        // =========================
        public IActionResult Create()
        {
            // عرض أسماء الصالات بدلاً من الأكواد
            ViewBag.HallId = new SelectList(db.Halls.OrderBy(h => h.HallName), "HallId", "HallName");
            return View();
        }

        // =========================
        // CREATE - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Block block)
        {
            // 1️⃣ التحقق من سعة الصالة (MaxBlocks)
            var hall = await db.Halls.Include(h => h.Blocks).FirstOrDefaultAsync(h => h.HallId == block.HallId);
            if (hall != null)
            {
                if (hall.Blocks.Count >= hall.MaxBlocks)
                {
                    ModelState.AddModelError("HallId", $"عفواً، هذه الصالة اكتملت برصيد {hall.MaxBlocks} بلوكات ولا يمكن إضافة المزيد.");
                }
            }

            // 2️⃣ التحقق من تكرار اسم البلوك داخل نفس الصالة
            bool isNameExist = await db.Blocks.AnyAsync(b => b.BlockName == block.BlockName && b.HallId == block.HallId);
            if (isNameExist)
            {
                ModelState.AddModelError("BlockName", "هذا البلوك موجود بالفعل داخل هذه الصالة.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.HallId = new SelectList(db.Halls.OrderBy(h => h.HallName), "HallId", "HallName", block.HallId);
                return View(block);
            }

            try
            {
                db.Blocks.Add(block);
                await db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إنشاء البلوك بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "حدث خطأ غير متوقع أثناء الحفظ.";
                ViewBag.HallId = new SelectList(db.Halls.OrderBy(h => h.HallName), "HallId", "HallName", block.HallId);
                return View(block);
            }
        }

        // =========================
        // EDIT - GET
        // =========================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return BadRequest();
            var block = await db.Blocks.FindAsync(id);
            if (block == null) return NotFound();

            ViewBag.HallId = new SelectList(db.Halls.OrderBy(h => h.HallName), "HallId", "HallName", block.HallId);
            return View(block);
        }

        // =========================
        // EDIT - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Block block)
        {
            // التحقق من تكرار الاسم مع استبعاد البلوك الحالي
            bool isNameExist = await db.Blocks.AnyAsync(b => b.BlockName == block.BlockName
                                                            && b.HallId == block.HallId
                                                            && b.BlockID != block.BlockID);
            if (isNameExist)
            {
                ModelState.AddModelError("BlockName", "اسم البلوك مستخدم بالفعل في هذه الصالة.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.HallId = new SelectList(db.Halls.OrderBy(h => h.HallName), "HallId", "HallName", block.HallId);
                return View(block);
            }

            try
            {
                db.Update(block);
                await db.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم تعديل بيانات البلوك بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] = "حدث خطأ أثناء التعديل.";
                ViewBag.HallId = new SelectList(db.Halls.OrderBy(h => h.HallName), "HallId", "HallName", block.HallId);
                return View(block);
            }
        }

        // =========================
        // DELETE - POST
        // =========================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var block = await db.Blocks.Include(b => b.Committees).FirstOrDefaultAsync(b => b.BlockID == id);
            if (block == null) return NotFound();

            if (block.Committees.Any())
            {
                TempData["ErrorMessage"] = "لا يمكن حذف البلوك لأنه يحتوي على لجان فعالة.";
                return RedirectToAction(nameof(Index));
            }

            db.Blocks.Remove(block);
            await db.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم حذف البلوك بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        
    }
}