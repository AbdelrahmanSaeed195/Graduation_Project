using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;

namespace projectweb.Controllers
{
    [AllowAnonymous]
    public class RelativesController : Controller
    {
        private readonly ApplicationDbContext _db;

        public RelativesController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(int? personId)
        {
            var query = _db.Relatives
                .Include(r => r.Person)
                .Include(r => r.Student)
                .AsQueryable();

            if (personId.HasValue)
            {
                query = query.Where(r => r.PersonId == personId.Value);
                ViewBag.SelectedPerson = personId;
            }

            var result = await query.ToListAsync();

            ViewBag.PersonsList = await _db.Persons
                .Select(p => new SelectListItem { Value = p.PersonId.ToString(), Text = p.FullName })
                .ToListAsync();

            ViewBag.StudentsList = await _db.Students
                .Select(s => new SelectListItem { Value = s.StudentId.ToString(), Text = s.FullName })
                .ToListAsync();

            return View("Index", result);
        }

        
        public async Task<IActionResult> Create()
        {
            ViewBag.PersonsList = await _db.Persons
                .Select(p => new SelectListItem { Value = p.PersonId.ToString(), Text = p.FullName })
                .ToListAsync();

            ViewBag.StudentsList = await _db.Students
                .Select(s => new SelectListItem { Value = s.StudentId.ToString(), Text = s.FullName })
                .ToListAsync();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Relative relative)
        {
            ModelState.Remove("Person");
            ModelState.Remove("Student");

            if (ModelState.IsValid)
            {
                _db.Add(relative);
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { personId = relative.PersonId });
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var relative = await _db.Relatives.FindAsync(id);
            if (relative == null) return NotFound();

            ViewBag.PersonsList = new SelectList(_db.Persons, "PersonId", "FullName", relative.PersonId);
            ViewBag.StudentsList = new SelectList(_db.Students, "StudentId", "FullName", relative.StudentId);
            return View(relative);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Relative relative)
        {
            if (id != relative.RelativeId) return NotFound();

            ModelState.Remove("Person");
            ModelState.Remove("Student");

            if (ModelState.IsValid)
            {
                try
                {
                    _db.Update(relative);
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RelativeExists(relative.RelativeId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index), new { personId = relative.PersonId });
            }
            ViewBag.PersonsList = new SelectList(_db.Persons, "PersonId", "FullName", relative.PersonId);
            ViewBag.StudentsList = new SelectList(_db.Students, "StudentId", "FullName", relative.StudentId);
            return View(relative);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var relative = await _db.Relatives
                .Include(r => r.Person)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(m => m.RelativeId == id);
            if (relative == null) return NotFound();
            return View(relative);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var relative = await _db.Relatives
                .Include(r => r.Person)
                .Include(r => r.Student)
                .FirstOrDefaultAsync(m => m.RelativeId == id);
            if (relative == null) return NotFound();
            return View(relative);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var relative = await _db.Relatives.FindAsync(id);
            int? pId = null;
            if (relative != null)
            {
                pId = relative.PersonId;
                _db.Relatives.Remove(relative);
                await _db.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index), new { personId = pId });
        }

        private bool RelativeExists(int id) => _db.Relatives.Any(e => e.RelativeId == id);
    }
}