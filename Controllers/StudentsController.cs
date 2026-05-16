using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using projectweb.ViewModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace projectweb.Controllers
{
    public class StudentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================
        // 1. القائمة الرئيسية - INDEX
        // =====================================
        public async Task<IActionResult> Index()
        {
            var students = await _context.Students
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Committee)
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Exam)
                .OrderBy(s => s.AcademicYear)
                .ThenBy(s => s.FullName)
                .ToListAsync();

            return View(students);
        }

        // =====================================
        // 2. البحث - SEARCH
        // =====================================
        [HttpGet]
        public async Task<IActionResult> Search(string searchTerm)
        {
            var query = _context.Students
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Committee)
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Exam)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.ToLower().Trim();
                query = query.Where(s => s.FullName.ToLower().Contains(searchTerm)
                                      || s.NationalId.Contains(searchTerm));
            }

            var students = await query
                .OrderBy(s => s.AcademicYear)
                .ThenBy(s => s.FullName)
                .ToListAsync();

            ViewBag.Search = searchTerm;
            return View("Index", students);
        }

        // =====================================
        // 3. التفاصيل - DETAILS
        // =====================================
        public async Task<IActionResult> Details(int id)
        {
            var student = await _context.Students
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Committee)
                .Include(s => s.Relatives)
                .FirstOrDefaultAsync(s => s.StudentId == id);

            if (student == null)
                return NotFound();

            return View(student);
        }

        // =====================================
        // 4. إضافة طالب - CREATE
        // =====================================
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(StudentCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            model.NationalId = model.NationalId?.Replace(" ", "").Trim();

            bool exists = await _context.Students.AnyAsync(s => s.NationalId == model.NationalId);
            if (exists)
            {
                ModelState.AddModelError("NationalId", "هذا الرقم القومي مسجل بالفعل");
                return View(model);
            }

            var student = new Student
            {
                FullName = model.FullName,
                NationalId = model.NationalId,
                AcademicYear = model.AcademicYear,
                SeatNumber = 0,
                ExamScheduleId = null
            };

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // 5. تعديل طالب - EDIT
        // =====================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            var model = new StudentCreateViewModel
            {
                FullName = student.FullName,
                NationalId = student.NationalId,
                AcademicYear = student.AcademicYear
            };

            ViewBag.StudentId = student.StudentId;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(int id, StudentCreateViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            bool exists = await _context.Students
                .AnyAsync(s => s.NationalId == model.NationalId && s.StudentId != id);

            if (exists)
            {
                ModelState.AddModelError("NationalId", "هذا الرقم القومي مستخدم لطالب آخر");
                return View(model);
            }

            student.FullName = model.FullName;
            student.NationalId = model.NationalId?.Replace(" ", "").Trim();
            student.AcademicYear = model.AcademicYear;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // 6. الحذف - DELETE (بدون حذف أي سطر)
        // =====================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return BadRequest();

            var student = await _context.Students
                .Include(s => s.Relatives)
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Committee)
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null)
                return NotFound();

            return View(student);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _context.Students
                .Include(s => s.Relatives)
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null)
                return NotFound();

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // 7. توزيع الطلاب تلقائياً - DISTRIBUTE
        // =====================================
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DistributeStudents()
        {
            // 1. جلب الطلاب غير الموزعين
            var students = await _context.Students
                .Where(s => s.ExamScheduleId == null)
                .OrderBy(s => s.AcademicYear)
                .ThenBy(s => s.FullName)
                .ToListAsync();

            if (!students.Any())
            {
                TempData["InfoMessage"] = "لا يوجد طلاب بانتظار التوزيع.";
                return RedirectToAction(nameof(Index));
            }

            // 2. جلب الجلسات مع اللجان والمواد
            var schedules = await _context.ExamSchedules
                .Include(es => es.Committee)
                .Include(es => es.Exam)
                    .ThenInclude(ex => ex.Subject)
                .OrderBy(es => es.Exam.ExamDate)
                .ThenBy(es => es.Exam.StartTime)
                .ToListAsync();

            // 3. حساب عدد الطلاب الحاليين في كل لجنة
            var scheduleCounts = await _context.Students
                .Where(s => s.ExamScheduleId != null)
                .GroupBy(s => s.ExamScheduleId)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Id!.Value, x => x.Count);

            var yearMapping = new Dictionary<string, string>
    {
        { "1", "الأولى" },
        { "2", "الثانية" },
        { "3", "الثالثة" },
        { "4", "الرابعة" }
    };

            int totalAssigned = 0;

            // 5. عملية التوزيع
            var groupedStudents = students.GroupBy(s => s.AcademicYear).ToList();

            foreach (var group in groupedStudents)
            {
                string rawYear = group.Key?.Trim() ?? "";

                // محاولة تحويل الرقم إلى مسمى (مثلاً من "1" إلى "الأولى")
                // إذا لم يجد تحويل، سيستخدم النص الأصلي كما هو
                string studentYear = yearMapping.ContainsKey(rawYear) ? yearMapping[rawYear] : rawYear;

                var relevantSchedules = schedules
                    .Where(s => s.Exam?.Subject != null &&
                                (s.Exam.Subject.AcademicYear.Trim() == studentYear ||
                                 s.Exam.Subject.AcademicYear.Contains(studentYear)))
                    .ToList();

                if (!relevantSchedules.Any()) continue;

                int scheduleIndex = 0;
                foreach (var student in group)
                {
                    bool distributed = false;
                    while (scheduleIndex < relevantSchedules.Count)
                    {
                        var currentSchedule = relevantSchedules[scheduleIndex];
                        var currentCount = scheduleCounts.GetValueOrDefault(currentSchedule.ExamScheduleId, 0);

                        // التحقق من السعة
                        if (currentCount < currentSchedule.Committee.NumberOfStudent)
                        {
                            student.ExamScheduleId = currentSchedule.ExamScheduleId;

                            // تحديث العداد في الذاكرة
                            scheduleCounts[currentSchedule.ExamScheduleId] = currentCount + 1;
                            totalAssigned++;
                            distributed = true;
                            break;
                        }
                        scheduleIndex++;
                    }
                    if (!distributed) break;
                }
            }

            // 6. الحفظ وإعادة ترقيم الجلوس
            if (totalAssigned > 0)
            {
                await _context.SaveChangesAsync();

                var affectedIds = students.Where(s => s.ExamScheduleId != null)
                    .Select(s => s.ExamScheduleId!.Value).Distinct();

                foreach (var id in affectedIds)
                    await RecalculateSeatNumbers(id);

                TempData["SuccessMessage"] = $"تم توزيع {totalAssigned} طالب بنجاح.";
            }
            else
            {
                TempData["ErrorMessage"] = "فشل التوزيع: لم يتم العثور على جلسات تطابق السنة الدراسية للطلاب. تأكد من كتابة السنة في المادة (الأولى، الثانية...) وفي الطالب (1، 2...).";
            }

            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // عرض لجنة الطالب - COMMITTEE
        // =====================================
        public async Task<IActionResult> Committee(int id)
        {
            var student = await _context.Students
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Committee)
                .Include(s => s.ExamSchedule)
                    .ThenInclude(e => e.Exam)
                .FirstOrDefaultAsync(s => s.StudentId == id);

            if (student == null) return NotFound();

            if (student.ExamScheduleId == null || student.ExamSchedule == null)
            {
                ViewBag.Message = $"عفواً، الطالب ({student.FullName}) لم يتم توزيعه على أي لجنة امتحانية حتى الآن.";
                return View("NoCommittee");
            }

            // 3. إذا كان موزعاً، نقوم بملء الـ ViewModel
            var model = new StudentCommitteeViewModel
            {
                StudentId = student.StudentId,
                FullName = student.FullName,
                AcademicYear = student.AcademicYear,
                SeatNumber = student.SeatNumber,
                CommitteeId = student.ExamSchedule.Committee.CommitteeId,
                CommitteeNumber = student.ExamSchedule.Committee.CommitteeNumber,
                NumberOfStudent = student.ExamSchedule.Committee.NumberOfStudent
            };

            return View(model);
        }
        // =====================================
        // 9. مساعد: إعادة ترقيم الجلوس
        // =====================================
        private async Task RecalculateSeatNumbers(int examScheduleId)
        {
            var students = await _context.Students
                .Where(s => s.ExamScheduleId == examScheduleId)
                .OrderBy(s => s.FullName)
                .ToListAsync();

            for (int i = 0; i < students.Count; i++)
                students[i].SeatNumber = i + 1;

            await _context.SaveChangesAsync();
        }
    }
}