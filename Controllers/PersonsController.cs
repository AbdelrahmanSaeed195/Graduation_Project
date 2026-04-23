using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;

namespace projectweb.Controllers
{
    public class PersonsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PersonsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string search)
        {
            var query = _context.Persons.Include(p => p.Role).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(p => p.FullName.ToLower().Contains(search)
                                      || p.NationalId.Contains(search)
                                      || p.Phone.Contains(search));
            }

            var result = await query.ToListAsync();
            ViewBag.Search = search;
            return View(result);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var person = await _context.Persons.Include(p => p.Role).FirstOrDefaultAsync(m => m.PersonId == id);
            if (person == null) return NotFound();
            return View(person);
        }

        public IActionResult Create()
        {
            ViewBag.Roles = new SelectList(_context.Roles, "RoleID", "RoleName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Person person)
        {
            ModelState.Remove("Role");
            ModelState.Remove("Accounts");
            ModelState.Remove("CommitteesAssignments");
            ModelState.Remove("ReportPersons");
            ModelState.Remove("Relatives");

            if (ModelState.IsValid)
            {
                _context.Add(person);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Roles = new SelectList(_context.Roles, "RoleID", "RoleName", person.RoleID);
            return View(person);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var person = await _context.Persons.FindAsync(id);
            if (person == null) return NotFound();
            ViewBag.Roles = new SelectList(_context.Roles, "RoleID", "RoleName", person.RoleID);
            return View(person);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Person person)
        {
            if (id != person.PersonId) return NotFound();

            ModelState.Remove("Role");
            ModelState.Remove("Accounts");
            ModelState.Remove("CommitteesAssignments");
            ModelState.Remove("ReportPersons");
            ModelState.Remove("Relatives");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(person);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PersonExists(person.PersonId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Roles = new SelectList(_context.Roles, "RoleID", "RoleName", person.RoleID);
            return View(person);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var person = await _context.Persons.Include(p => p.Role).FirstOrDefaultAsync(m => m.PersonId == id);
            if (person == null) return NotFound();
            return View(person);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var person = await _context.Persons.FindAsync(id);
            if (person != null)
            {
                _context.Persons.Remove(person);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool PersonExists(int id) => _context.Persons.Any(e => e.PersonId == id);
    }
}