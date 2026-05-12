using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using projectweb.Models.ViewModels;
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
        // =====================================
        // INDEX
        //  =====================================
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
                                     .OrderBy(p => p.JobRole)
                                     .ThenBy(p => p.FullName)
                                     .ToList();

            ViewBag.Search = search;
            return View(uniqueResult);
        }
        //  =====================================
        // DETAILS
        //  =====================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var person = await _context.Persons.FirstOrDefaultAsync(m => m.PersonId == id);
            if (person == null) return NotFound();
            return View(person);
        }
        //  =====================================
        // CREATE
        //  =====================================
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Person person)
        {
          
            if (_context.Persons.Any(p => p.NationalId == person.NationalId))
            {
                ModelState.AddModelError("NationalId", "عفواً، هذا الرقم القومي مسجل مسبقاً.");
            }

            if (ModelState.IsValid)
            {
                try
                {
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
        //  =====================================
        // EDIT
        //  =====================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var person = await _context.Persons.FindAsync(id);
            if (person == null) return NotFound();

            return View(person);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
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
        //  =====================================
        // DELETE
        //  =====================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var person = await _context.Persons.FirstOrDefaultAsync(m => m.PersonId == id);
            if (person == null) return NotFound();
            return View(person);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
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

        // =====================================
        // PRINT ASSIGNMENTS (توزيع المراقبين)
        // =====================================
        public async Task<IActionResult> PrintAssignments(int id)
        {
            // 1. جلب بيانات الشخص (للتأكد من وجوده وللحصول على اسمه الكامل والوظيفة)
            var person = await _context.Persons
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PersonId == id);

            if (person == null) return Content($"خطأ: لم يتم العثور على الموظف رقم {id}");

            // 2. جلب جميع التكليفات المتعلقة بهذا الشخص مع التفاصيل اللازمة (المادة، السنة المستهدفة، المواعيد)
            var rawAssignments = await _context.CommitteesAssignments
                .AsNoTracking()
                .Include(a => a.Role)
                .Include(a => a.ExamSchedule)
                    .ThenInclude(es => es.Exam)
                        .ThenInclude(e => e.Subject)
                .Where(a => a.PersonID == id)
                .ToListAsync();

            // 3. إعداد خريطة لتخزين مواعيد كل سنة دراسية (للهيدر)
            var yearTimesMap = new Dictionary<string, string> {
            { "1", "---" }, { "2", "---" }, { "3", "---" }, { "4", "---" }
        };

            // 4. تجميع التكليفات حسب التاريخ لعرضها في التقرير
            var groupedRows = new List<AssignmentRowGroup>();

            if (rawAssignments != null && rawAssignments.Any())
            {
                groupedRows = rawAssignments
                    .Where(a => a.ExamSchedule?.Exam != null)
                    .GroupBy(a => a.ExamSchedule.Exam.ExamDate.Date)
                    .Select(g => new AssignmentRowGroup
                    {
                        Date = g.Key,
                        Day = g.Key.ToString("dddd", new System.Globalization.CultureInfo("ar-EG")),
                        DailyItems = g.Select(a => {
                            var exam = a.ExamSchedule.Exam;
                            string yearText = exam.TargetAcademicYear ?? "";
                            string yearNum = yearText.Contains("الأولى") ? "1" :
                                             yearText.Contains("الثانية") ? "2" :
                                             yearText.Contains("الثالثة") ? "3" :
                                             yearText.Contains("الرابعة") ? "4" : "";

                            // تحديث خريطة مواعيد السنوات إذا كانت السنة موجودة ولم يتم تعيين موعد لها بعد
                            if (yearNum != "" && yearTimesMap[yearNum] == "---")
                            {
                                yearTimesMap[yearNum] = $"{DateTime.Today.Add(exam.StartTime):hh:mm tt} - {DateTime.Today.Add(exam.EndTime):hh:mm tt}";
                            }

                            return new AssignmentReportItem
                            {
                                SubjectName = exam.Subject?.SubjectName ?? "مادة غير محددة",
                                TargetYear = yearNum,
                                PersonFullName = person.FullName
                            };
                        }).ToList()
                    })
                    .OrderBy(x => x.Date)
                    .ToList();
            }

            // 5. إعداد الـ ViewModel لتمريره إلى العرض
            var model = new PrintReportViewModel
            {
                PersonFullName = person.FullName,
                PersonRoleInReport = GetArabicJobTitle(person.JobRole),
                Rows = groupedRows, 
                YearTimes = yearTimesMap,
                AcademicYear = "2025/2026",
                CollegeName = "كلية علوم الرياضة"
            };

            return View(model);
        }

        // دالة مساعدة لتحويل الوظيفة إلى نص عربي لعرضه في التقرير
        private string GetArabicJobTitle(JobTitle job)
        {
            return job switch
            {
                JobTitle.ProfessorEmeritus => "أستاذ متفرغ",
                JobTitle.AssistantProfessor => "أستاذ مساعد",
                JobTitle.Professor => "أستاذ",
                JobTitle.Dean => "عميد الكلية",
                JobTitle.StaffObserver => "مدرس",
                JobTitle.AssistantStaff => "مدرس مساعد",
                JobTitle.Assistant => "معيد",
                JobTitle.Employee => "موظف",
                JobTitle.Doctor => "دكتور",
                JobTitle.Nurse => "ممرض",
                _ => "عضو هيئة تدريس"
            };
        }
        private bool PersonExists(int id) => _context.Persons.Any(e => e.PersonId == id);
    }
}