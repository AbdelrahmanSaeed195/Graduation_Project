//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.Mvc.Rendering;
//using Microsoft.EntityFrameworkCore;
//using projectweb.Models;

//namespace projectweb.Controllers
//{
//    public class PersonsController : Controller
//    {
//        private readonly ApplicationDbContext _context;

//        public PersonsController(ApplicationDbContext context)
//        {
//            _context = context;
//        }

//        // 1. عرض الأشخاص بدون تكرار في الجدول
//        public async Task<IActionResult> Index(string search)
//        {
//            var query = _context.Persons.Include(p => p.Role).AsQueryable();

//            if (!string.IsNullOrEmpty(search))
//            {
//                search = search.ToLower();
//                query = query.Where(p => p.FullName.ToLower().Contains(search)
//                                      || p.NationalId.Contains(search)
//                                      || p.Phone.Contains(search));
//            }

//            var result = await query.ToListAsync();

//            // تجميع بالرقم القومي لضمان عدم ظهور الشخص أكثر من مرة
//            var uniqueResult = result.GroupBy(p => p.NationalId)
//                                     .Select(g => g.First())
//                                     .ToList();

//            ViewBag.Search = search;
//            return View(uniqueResult);
//        }

//        public async Task<IActionResult> Details(int? id)
//        {
//            if (id == null) return NotFound();
//            var person = await _context.Persons.Include(p => p.Role).FirstOrDefaultAsync(m => m.PersonId == id);
//            if (person == null) return NotFound();
//            return View(person);
//        }

//        public IActionResult Create()
//        {
//            // سحب الوصف العربي من الداتابيز وترتيبه
//            ViewBag.Roles = new SelectList(_context.Roles.OrderBy(r => r.RoleDescription), "RoleID", "RoleDescription");
//            return View();
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Create(Person person)
//        {
//            // التحقق من عدم تكرار الرقم القومي
//            if (_context.Persons.Any(p => p.NationalId == person.NationalId))
//            {
//                ModelState.AddModelError("NationalId", "عفواً، هذا الرقم القومي مسجل مسبقاً.");
//            }

//            // التحقق من عدم تكرار الاسم
//            if (_context.Persons.Any(p => p.FullName == person.FullName))
//            {
//                ModelState.AddModelError("FullName", "عفواً، هذا الاسم موجود بالفعل في النظام.");
//            }

//            ModelState.Remove("Role");
//            ModelState.Remove("Accounts");
//            ModelState.Remove("CommitteesAssignments");
//            ModelState.Remove("ReportPersons");
//            ModelState.Remove("Relatives");

//            if (ModelState.IsValid)
//            {
//                _context.Add(person);
//                await _context.SaveChangesAsync();
//                return RedirectToAction(nameof(Index));
//            }

//            ViewBag.Roles = new SelectList(_context.Roles.OrderBy(r => r.RoleDescription), "RoleID", "RoleDescription", person.RoleID);
//            return View(person);
//        }

//        public async Task<IActionResult> Edit(int? id)
//        {
//            if (id == null) return NotFound();
//            var person = await _context.Persons.FindAsync(id);
//            if (person == null) return NotFound();

//            ViewBag.Roles = new SelectList(_context.Roles.OrderBy(r => r.RoleDescription), "RoleID", "RoleDescription", person.RoleID);
//            return View(person);
//        }

//        [HttpPost]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> Edit(int id, Person person)
//        {
//            if (id != person.PersonId) return NotFound();

//            // التحقق من عدم التكرار مع استثناء الشخص الحالي
//            if (_context.Persons.Any(p => p.NationalId == person.NationalId && p.PersonId != id))
//            {
//                ModelState.AddModelError("NationalId", "هذا الرقم القومي مستخدم مع شخص آخر.");
//            }

//            if (_context.Persons.Any(p => p.FullName == person.FullName && p.PersonId != id))
//            {
//                ModelState.AddModelError("FullName", "هذا الاسم مستخدم بالفعل لشخص آخر.");
//            }

//            ModelState.Remove("Role");
//            ModelState.Remove("Accounts");
//            ModelState.Remove("CommitteesAssignments");
//            ModelState.Remove("ReportPersons");
//            ModelState.Remove("Relatives");

//            if (ModelState.IsValid)
//            {
//                try
//                {
//                    _context.Update(person);
//                    await _context.SaveChangesAsync();
//                }
//                catch (DbUpdateConcurrencyException)
//                {
//                    if (!PersonExists(person.PersonId)) return NotFound();
//                    else throw;
//                }
//                return RedirectToAction(nameof(Index));
//            }

//            ViewBag.Roles = new SelectList(_context.Roles.OrderBy(r => r.RoleDescription), "RoleID", "RoleDescription", person.RoleID);
//            return View(person);
//        }

//        public async Task<IActionResult> Delete(int? id)
//        {
//            if (id == null) return NotFound();
//            var person = await _context.Persons.Include(p => p.Role).FirstOrDefaultAsync(m => m.PersonId == id);
//            if (person == null) return NotFound();
//            return View(person);
//        }

//        [HttpPost, ActionName("Delete")]
//        [ValidateAntiForgeryToken]
//        public async Task<IActionResult> DeleteConfirmed(int id)
//        {
//            var person = await _context.Persons.FindAsync(id);
//            if (person != null)
//            {
//                _context.Persons.Remove(person);
//                await _context.SaveChangesAsync();
//            }
//            return RedirectToAction(nameof(Index));
//        }

//        private bool PersonExists(int id) => _context.Persons.Any(e => e.PersonId == id);
//    }
//}
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

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
            var query = _context.Persons.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(p => p.FullName.ToLower().Contains(search)
                                      || p.NationalId.Contains(search)
                                      || p.Phone.Contains(search));
            }

            var result = await query.ToListAsync();

            var uniqueResult = result.GroupBy(p => p.NationalId)
                                     .Select(g => g.First())
                                     .ToList();

            ViewBag.Search = search;
            return View(uniqueResult);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var person = await _context.Persons.FirstOrDefaultAsync(m => m.PersonId == id);
            if (person == null) return NotFound();
            return View(person);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Person person)
        {
            // التحقق من التكرار
            if (_context.Persons.Any(p => p.NationalId == person.NationalId))
            {
                ModelState.AddModelError("NationalId", "عفواً، هذا الرقم القومي مسجل مسبقاً.");
            }

            // إزالة التحقق من العلاقات لتجنب الـ Validation Errors
            ModelState.Remove("Role");
            ModelState.Remove("CommitteesAssignments");
            ModelState.Remove("ReportPersons");
            ModelState.Remove("Relatives");

            if (ModelState.IsValid)
            {
                try
                {
                    // نضمن إن الـ RoleID واخد قيمة موجودة فعلياً في جدول الـ Roles
                    if (person.RoleID == 0) person.RoleID = 1;

                    _context.Add(person);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    var inner = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    ModelState.AddModelError("", "حدث خطأ أثناء الحفظ: " + inner);
                }
            }
            return View(person);
        }
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var person = await _context.Persons.FindAsync(id);
            if (person == null) return NotFound();

            return View(person);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Person person)
        {
            if (id != person.PersonId) return NotFound();

            if (_context.Persons.Any(p => p.NationalId == person.NationalId && p.PersonId != id))
            {
                ModelState.AddModelError("NationalId", "هذا الرقم القومي مستخدم مع شخص آخر.");
            }

            if (_context.Persons.Any(p => p.FullName == person.FullName && p.PersonId != id))
            {
                ModelState.AddModelError("FullName", "هذا الاسم مستخدم بالفعل لشخص آخر.");
            }

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

            return View(person);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var person = await _context.Persons.FirstOrDefaultAsync(m => m.PersonId == id);
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