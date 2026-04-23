using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;

namespace projectweb.Controllers
{
    public class HallsController : Controller
    {
        private readonly ApplicationDbContext db;

        public HallsController(ApplicationDbContext context)
        {
            db = context;
        }

        // =========================
        // INDEX
        // =========================
        public async Task<IActionResult> Index()
        {
            var halls = await db.Halls
                .Include(h => h.HallSupervisor)
                .Include(h => h.Blocks)
                .ToListAsync();

            return View(halls);
        }

        // =========================
        // DETAILS
        // =========================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return BadRequest();

            var hall = await db.Halls
                .Include(h => h.HallSupervisor)
                .Include(h => h.Blocks)
                    .ThenInclude(b => b.Committees)
                .FirstOrDefaultAsync(h => h.HallId == id);

            if (hall == null)
                return NotFound();

            return View(hall);
        }

        // =========================
        // CREATE - GET
        // =========================
        public async Task<IActionResult> Create()
        {
            await LoadSupervisors();

            return View();
        }

        // =========================
        // CREATE - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Hall hall)
        {
            if (!ModelState.IsValid)
            {
                await LoadSupervisors(hall.HallSupervisorID);
                return View(hall);
            }

            try
            {
                db.Halls.Add(hall);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "تم إضافة الصالة بنجاح.";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ErrorMessage"] =
                    "حدث خطأ أثناء إضافة الصالة.";

                await LoadSupervisors(hall.HallSupervisorID);
                return View(hall);
            }
        }

        // =========================
        // EDIT - GET
        // =========================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return BadRequest();

            var hall = await db.Halls.FindAsync(id);

            if (hall == null)
                return NotFound();

            await LoadSupervisors(hall.HallSupervisorID);

            return View(hall);
        }

        // =========================
        // EDIT - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Hall hall)
        {
            if (!ModelState.IsValid)
            {
                await LoadSupervisors(hall.HallSupervisorID);
                return View(hall);
            }

            try
            {
                db.Update(hall);
                await db.SaveChangesAsync();

                TempData["SuccessMessage"] =
                    "تم تعديل الصالة بنجاح.";

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["ErrorMessage"] =
                    "حدث خطأ أثناء تعديل الصالة.";

                await LoadSupervisors(hall.HallSupervisorID);
                return View(hall);
            }
        }

        // =========================
        // DELETE - GET
        // =========================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return BadRequest();

            var hall = await db.Halls
                .Include(h => h.Blocks)
                .Include(h => h.HallSupervisor)
                .FirstOrDefaultAsync(h => h.HallId == id);

            if (hall == null)
                return NotFound();

            return View(hall);
        }

        // =========================
        // DELETE - POST
        // =========================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
            DeleteConfirmed(int id)
        {
            var hall = await db.Halls
                .Include(h => h.Blocks)
                .FirstOrDefaultAsync(h => h.HallId == id);

            if (hall == null)
                return NotFound();

            bool hasBlocks =
                await db.Blocks
                .AnyAsync(b => b.HallId == id);

            if (hasBlocks)
            {
                TempData["ErrorMessage"] =
                    "لا يمكن حذف الصالة لأنها تحتوي على بلوكات.";

                return RedirectToAction(nameof(Index));
            }

            db.Halls.Remove(hall);
            await db.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "تم حذف الصالة بنجاح.";

            return RedirectToAction(nameof(Index));
        }

        // =========================
        // تحميل المشرفين
        // =========================
        private async Task LoadSupervisors(int? selectedId = null)
        {
            var supervisors = await db.Persons
                .Include(p => p.Role)
                .Where(p =>
                    p.Role.RoleName ==
                    StaffPosition.HallManager)
                .ToListAsync();

            ViewBag.HallSupervisorID =
                new SelectList(
                    supervisors,
                    "PersonId",
                    "FullName",
                    selectedId);
        }
    }
}