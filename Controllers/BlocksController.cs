using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;

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
                .ThenInclude(c => c.CommitteesAssignments)
                .ToListAsync();

            return View(blocks);
        }

        // =========================
        // DETAILS
        // =========================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return BadRequest();

            var block = await db.Blocks
                .Include(b => b.Hall)
                .Include(b => b.Committees)
                    .ThenInclude(c => c.CommitteesAssignments)
                        .ThenInclude(a => a.Person)
                .FirstOrDefaultAsync(b => b.BlockID == id);

            if (block == null)
                return NotFound();

            return View(block);
        }

        // =========================
        // CREATE - GET
        // =========================
        public IActionResult Create()
        {
            ViewBag.HallId =
                new SelectList(db.Halls, "HallId", "HallId");

            return View();
        }

        // =========================
        // CREATE - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Block block)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.HallId =
                    new SelectList(db.Halls,
                        "HallId",
                        "HallName",
                        block.HallId);

                return View(block);
            }

            try
            {
                // إنشاء البلوك فقط
                db.Blocks.Add(block);

                await db.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "تم إنشاء البلوك بنجاح.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                TempData["ErrorMessage"] =
                    "حدث خطأ أثناء إنشاء البلوك.";

                ViewBag.HallId =
                    new SelectList(db.Halls,
                        "HallId",
                        "HallName",
                        block.HallId);

                return View(block);
            }
        }

        // =========================
        // EDIT - GET
        // =========================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return BadRequest();

            var block =
                await db.Blocks.FindAsync(id);

            if (block == null)
                return NotFound();

            ViewBag.HallId =
                new SelectList(db.Halls,
                    "HallId",
                    "HallId",
                    block.HallId);

            return View(block);
        }

        // =========================
        // EDIT - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Block block)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.HallId =
                    new SelectList(db.Halls,
                        "HallId",
                        "HallId",
                        block.HallId);

                return View(block);
            }

            try
            {
                db.Update(block);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "تم تعديل البلوك بنجاح.";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ErrorMessage"] =
                    "حدث خطأ أثناء التعديل.";

                return View(block);
            }
        }

        // =========================
        // DELETE - GET
        // =========================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return BadRequest();

            var block =
                await db.Blocks
                .Include(b => b.Committees)
                .FirstOrDefaultAsync(
                    b => b.BlockID == id);

            if (block == null)
                return NotFound();

            return View(block);
        }

        // =========================
        // DELETE - POST
        // =========================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            DeleteConfirmed(int id)
        {
            var block =
                await db.Blocks
                .Include(b => b.Committees)
                .FirstOrDefaultAsync(
                    b => b.BlockID == id);

            if (block == null)
                return NotFound();

            bool hasCommittees =
                await db.Committees
                .AnyAsync(c =>
                    c.BlockID == id);

            if (hasCommittees)
            {
                TempData["ErrorMessage"] =
                    "لا يمكن حذف البلوك لأنه يحتوي على لجان مرتبطة به.";

                return RedirectToAction(nameof(Index));
            }

            db.Blocks.Remove(block);
            await db.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "تم حذف البلوك بنجاح.";

            return RedirectToAction(nameof(Index));
        }
        // =========================
        // ASSIGN COMMITTEES TO BLOCK
        // =========================
        public async Task<IActionResult> AssignCommittees(int blockId)
        {
            using var transaction =
                await db.Database.BeginTransactionAsync();

            try
            {
                // 1️⃣ التأكد من وجود البلوك
                var block = await db.Blocks
                    .FirstOrDefaultAsync(b => b.BlockID == blockId);

                if (block == null)
                {
                    TempData["ErrorMessage"] =
                        "البلوك غير موجود.";

                    return RedirectToAction(nameof(Index));
                }

                // 2️⃣ جلب اللجان غير المرتبطة
                var availableCommittees =
                    await db.Committees
                    .Where(c => c.BlockID == null)
                    .ToListAsync();

                Random rnd =
                    new Random(Guid.NewGuid().GetHashCode());

                int count = rnd.Next(4, 6); // 4 أو 5 لجان

                if (availableCommittees.Count < count)
                {
                    TempData["ErrorMessage"] =
                        "عدد اللجان غير كافي لتكوين بلوك.";

                    await transaction.RollbackAsync();

                    return RedirectToAction(nameof(Index));
                }

                // 3️⃣ اختيار لجان عشوائي
                var selectedCommittees =
                    availableCommittees
                    .OrderBy(x => Guid.NewGuid())
                    .Take(count)
                    .ToList();

                // 4️⃣ جلب الملاحظين غير المرتبطين
                var observers =
                    await db.Persons
                    .Include(p => p.Role)
                    .Where(p =>
                        p.Role.RoleName ==
                        StaffPosition.CommitteeObserver

                        &&

                        !db.CommitteesAssignments
                        .Any(a =>
                            a.PersonID ==
                            p.PersonId)
                    )
                    .ToListAsync();

                if (observers.Count < count)
                {
                    TempData["ErrorMessage"] =
                        "عدد الملاحظين غير كافي.";

                    await transaction.RollbackAsync();

                    return RedirectToAction(nameof(Index));
                }

                var selectedObservers =
                    observers
                    .OrderBy(x => Guid.NewGuid())
                    .Take(count)
                    .ToList();

                // 5️⃣ ربط اللجان بالبلوك
                for (int i = 0; i < count; i++)
                {
                    selectedCommittees[i].BlockID =
                        block.BlockID;

                    db.Committees
                        .Update(selectedCommittees[i]);

                    // توزيع الملاحظين
                    db.CommitteesAssignments.Add(
                        new CommitteesAssignment
                        {
                            CommitteeID =
                                selectedCommittees[i].CommitteeID,

                            PersonID =
                                selectedObservers[i].PersonId
                        });
                }

                await db.SaveChangesAsync();

                await transaction.CommitAsync();

                TempData["SuccessMessage"] =
                    "تم توزيع اللجان والملاحظين على البلوك بنجاح.";

                return RedirectToAction(nameof(Index));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                TempData["ErrorMessage"] =
                    "حدث خطأ أثناء توزيع اللجان.";

                return RedirectToAction(nameof(Index));
            }
        }
    }
}