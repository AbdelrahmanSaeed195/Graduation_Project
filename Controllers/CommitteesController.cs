using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;

namespace projectweb.Controllers
{
    public class CommitteesController : Controller
    {
        private readonly ApplicationDbContext db;

        public CommitteesController(ApplicationDbContext context)
        {
            db = context;
        }

        // ============================================================
        // INDEX
        // ============================================================
        public async Task<IActionResult> Index()
        {
            var committees = await db.Committees
                .Include(c => c.Block)
                .ThenInclude(b => b.Hall)
                .Where(c => c.BlockID != null) // اللجان المربوطة فقط
                .ToListAsync();

            return View(committees);
        }

        // ============================================================
        // DETAILS
        // ============================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return BadRequest();

            var committee = await db.Committees
                .Include(c => c.Block)
                .ThenInclude(b => b.Hall)
                .Include(c => c.Students)
                .Include(c => c.CommitteesAssignments)
                    .ThenInclude(a => a.Person)
                .Include(c => c.ExamSchedules)
                .FirstOrDefaultAsync(c => c.CommitteeID == id);

            if (committee == null)
                return NotFound();

            return View(committee);
        }

        // ============================================================
        // CREATE - GET
        // ============================================================
        public IActionResult Create()
        {
            ViewBag.BlockID = GetBlockSelectList();
            ViewBag.StatusList = GetStatusList();

            return View();
        }

        // ============================================================
        // CREATE - POST
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Committee committee)
        {
            if (ModelState.IsValid)
            {
                // منع التكرار
                bool exists = await db.Committees.AnyAsync(c =>
                    c.CommitteeNumber == committee.CommitteeNumber &&
                    c.CommitteeID != committee.CommitteeID);

                if (exists)
                {
                    ModelState.AddModelError("", "رقم اللجنة موجود بالفعل.");

                    ViewBag.BlockID = GetBlockSelectList(committee.BlockID);
                    ViewBag.StatusList = GetStatusList(committee.StatusOfCommittee);

                    return View(committee);
                }

                db.Committees.Add(committee);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = "تم إضافة اللجنة بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.BlockID = GetBlockSelectList(committee.BlockID);
            ViewBag.StatusList = GetStatusList(committee.StatusOfCommittee);

            return View(committee);
        }

        // ============================================================
        // EDIT - GET
        // ============================================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return BadRequest();

            var committee = await db.Committees.FindAsync(id);

            if (committee == null)
                return NotFound();

            ViewBag.BlockID = GetBlockSelectList(committee.BlockID);
            ViewBag.StatusList = GetStatusList(committee.StatusOfCommittee);

            return View(committee);
        }

        // ============================================================
        // EDIT - POST
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Committee committee)
        {
            if (ModelState.IsValid)
            {
                bool exists = await db.Committees.AnyAsync(c =>
                    c.CommitteeNumber == committee.CommitteeNumber &&
                    c.CommitteeID != committee.CommitteeID);

                if (exists)
                {
                    ModelState.AddModelError("", "رقم اللجنة موجود بالفعل.");

                    ViewBag.BlockID = GetBlockSelectList(committee.BlockID);
                    ViewBag.StatusList = GetStatusList(committee.StatusOfCommittee);

                    return View(committee);
                }

                db.Update(committee);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] = "تم تعديل اللجنة بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.BlockID = GetBlockSelectList(committee.BlockID);
            ViewBag.StatusList = GetStatusList(committee.StatusOfCommittee);

            return View(committee);
        }

        // ============================================================
        // DELETE - GET
        // ============================================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return BadRequest();

            var committee = await db.Committees
                .Include(c => c.Block)
                .ThenInclude(b => b.Hall)
                .FirstOrDefaultAsync(c => c.CommitteeID == id);

            if (committee == null)
                return NotFound();

            return View(committee);
        }

        // ============================================================
        // DELETE - POST
        // ============================================================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var committee = await db.Committees.FindAsync(id);

            if (committee == null)
                return NotFound();

            bool hasDependencies =
                await db.Students.AnyAsync(s => s.ExamSchedule.Committee.CommitteeID == id) ||
                await db.CommitteesAssignments.AnyAsync(a => a.CommitteeID == id) ||
                await db.ExamSchedules.AnyAsync(e => e.CommitteeId == id);

            if (hasDependencies)
            {
                TempData["ErrorMessage"] =
                    "لا يمكن حذف اللجنة لأنها مرتبطة ببيانات أخرى.";

                return RedirectToAction(nameof(Index));
            }

            db.Committees.Remove(committee);
            await db.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف اللجنة بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // HELPERS
        // ============================================================
        private SelectList GetBlockSelectList(int? selected = null)
        {
            var blocks = db.Blocks
                .Include(b => b.Hall)
                .Select(b => new
                {
                    b.BlockID,
                    DisplayName = "قاعة " + b.HallId + " / بلوك " + b.BlockID
                })
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