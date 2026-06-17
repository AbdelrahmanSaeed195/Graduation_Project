using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
                .Include(s => s.Committee)
                .Include(s => s.ExamSchedule)
                .OrderBy(s => s.AcademicYear)
                .ThenBy(s => s.Specialization)
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
                .Include(s => s.Committee)
                .Include(s => s.ExamSchedule)
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
                .Include(s => s.Committee)
                .Include(s => s.Relatives)
                .FirstOrDefaultAsync(s => s.StudentId == id);

            if (student == null) return NotFound();

            return View(student);
        }

        // =====================================
        // 4. إضافة طالب - CREATE
        // =====================================
        public async Task<IActionResult> Create()
        {
            ViewData["CommitteeId"] = new SelectList(await _context.Committees.OrderBy(c => c.CommitteeNumber).ToListAsync(), "CommitteeId", "CommitteeNumber");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StudentCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                model.NationalId = model.NationalId?.Replace(" ", "").Trim();

                bool exists = await _context.Students.AnyAsync(s => s.NationalId == model.NationalId);
                if (exists)
                {
                    ModelState.AddModelError("NationalId", "هذا الرقم القومي مسجل بالفعل");
                    ViewData["CommitteeId"] = new SelectList(await _context.Committees.OrderBy(c => c.CommitteeNumber).ToListAsync(), "CommitteeId", "CommitteeNumber", model.CommitteeId);
                    return View(model);
                }

                var student = new Student
                {
                    FullName = model.FullName,
                    NationalId = model.NationalId,
                    AcademicYear = model.AcademicYear,
                    Specialization = model.Specialization,
                    CommitteeId = model.CommitteeId, 
                    SeatNumber = 0,
                    ExamScheduleId = null
                };

                _context.Add(student);
                await _context.SaveChangesAsync();

                if (student.CommitteeId.HasValue)
                {
                    await RecalculateSeatNumbersByCommittee(student.CommitteeId.Value);
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["CommitteeId"] = new SelectList(await _context.Committees.OrderBy(c => c.CommitteeNumber).ToListAsync(), "CommitteeId", "CommitteeNumber", model.CommitteeId);
            return View(model);
        }

        // =====================================
        // 5. تعديل طالب - EDIT
        // =====================================
        public async Task<IActionResult> Edit(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            var model = new StudentCreateViewModel
            {
                FullName = student.FullName,
                NationalId = student.NationalId,
                AcademicYear = student.AcademicYear,
                Specialization = student.Specialization,
                CommitteeId = student.CommitteeId
            };

            ViewBag.StudentId = student.StudentId;
            ViewData["CommitteeId"] = new SelectList(await _context.Committees.OrderBy(c => c.CommitteeNumber).ToListAsync(), "CommitteeId", "CommitteeNumber", student.CommitteeId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StudentCreateViewModel model)
        {
            if (ModelState.IsValid)
            {
                model.NationalId = model.NationalId?.Replace(" ", "").Trim();

                bool exists = await _context.Students
                    .AnyAsync(s => s.NationalId == model.NationalId && s.StudentId != id);

                if (exists)
                {
                    ModelState.AddModelError("NationalId", "هذا الرقم القومي مستخدم لطالب آخر");
                    ViewData["CommitteeId"] = new SelectList(await _context.Committees.OrderBy(c => c.CommitteeNumber).ToListAsync(), "CommitteeId", "CommitteeNumber", model.CommitteeId);
                    return View(model);
                }

                var student = await _context.Students.FindAsync(id);
                if (student == null) return NotFound();

                int? oldCommitteeId = student.CommitteeId;

                student.FullName = model.FullName;
                student.NationalId = model.NationalId;
                student.AcademicYear = model.AcademicYear;
                student.Specialization = model.Specialization;
                student.CommitteeId = model.CommitteeId;

                _context.Update(student);
                await _context.SaveChangesAsync();

                if (oldCommitteeId != student.CommitteeId)
                {
                    if (oldCommitteeId.HasValue)
                    {
                        await RecalculateSeatNumbersByCommittee(oldCommitteeId.Value);
                    }
                    if (student.CommitteeId.HasValue)
                    {
                        await RecalculateSeatNumbersByCommittee(student.CommitteeId.Value);
                    }
                }

                return RedirectToAction(nameof(Index));
            }

            ViewData["CommitteeId"] = new SelectList(await _context.Committees.OrderBy(c => c.CommitteeNumber).ToListAsync(), "CommitteeId", "CommitteeNumber", model.CommitteeId);
            return View(model);
        }

        // =====================================
        // 6. الحذف - DELETE
        // =====================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return BadRequest();

            var student = await _context.Students
                .Include(s => s.Committee)
                .FirstOrDefaultAsync(m => m.StudentId == id);

            if (student == null) return NotFound();

            return View(student);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();

            int? committeeId = student.CommitteeId;

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            // إعادة ترقيم مقاعد اللجنة التي حذف منها الطالب لحماية التسلسل الأبجدي
            if (committeeId.HasValue)
            {
                await RecalculateSeatNumbersByCommittee(committeeId.Value);
            }

            return RedirectToAction(nameof(Index));
        }

        // ======================================================================
        // 7. التوزيع التلقائي الشامل والذكي للطلاب على اللجان المتاحة بالترتيب الأبجدي
        // ======================================================================
        public async Task<IActionResult> DistributeStudents()
        {
            // 1. جلب كافة الطلاب المسجلين بالسيستم (سواء مرتبطين بلجنة أو لسه)
            var allStudents = await _context.Students.ToListAsync();

            if (!allStudents.Any())
            {
                TempData["ErrorMessage"] = "عفواً، لا يوجد طلاب مسجلين في النظام حالياً لإجراء التوزيع.";
                return RedirectToAction(nameof(Index));
            }

            // 2. جلب كافة اللجان المتاحة في قاعدة البيانات
            var allCommittees = await _context.Committees.OrderBy(c => c.CommitteeNumber).ToListAsync();

            if (!allCommittees.Any())
            {
                TempData["ErrorMessage"] = "فشل التوزيع: لا توجد لجان امتحانية منشأة في قاعدة البيانات لتوزيع الطلاب عليها.";
                return RedirectToAction(nameof(Index));
            }

            // 3. خوارزمية توزيع الطلاب الذكية بناءً على توزيع اللجان الفعلي والسعة الاستيعابية
            // نقوم بترتيب الطلاب حسب المستوى الدراسي ثم أبجدياً بالاسم كاملاً لضمان تسلسل أرقام الجلوس
            var orderedStudents = allStudents.OrderBy(s => s.AcademicYear).ThenBy(s => s.FullName).ToList();
            var committeeOccupancy = allCommittees.ToDictionary(c => c.CommitteeId, c => 0);

            int totalAssigned = 0;
            int committeeIndex = 0;

            foreach (var student in orderedStudents)
            {
                bool distributed = false;

                // التوزيع يتم بالتتابع بناءً على ترتيب اللجان المعتمد وسعتها الاستيعابية القصوى
                while (committeeIndex < allCommittees.Count)
                {
                    var targetCommittee = allCommittees[committeeIndex];
                    int currentCount = committeeOccupancy[targetCommittee.CommitteeId];

                    // التأكد من أن اللجنة الحالية لم تمتزج أو تتخطى الحد الأقصى لسعتها من الطلاب
                    if (currentCount < targetCommittee.NumberOfStudent)
                    {
                        student.CommitteeId = targetCommittee.CommitteeId;
                        committeeOccupancy[targetCommittee.CommitteeId] = currentCount + 1;
                        totalAssigned++;
                        distributed = true;
                        break;
                    }

                    // إذا امتلأت اللجنة الحالية، ننتقل تلقائياً للجنة التالية في خطة التوزيع
                    committeeIndex++;
                }

                if (!distributed) break;
            }

            // 4. حفظ التعديلات وإعادة توليد أرقام الجلوس أبجدياً لكل لجنة تأثرت بالتوزيع
            if (totalAssigned > 0)
            {
                await _context.SaveChangesAsync();

                // تحديث وإعادة توليد أرقام المقاعد (أرقام الجلوس) بشكل تسلسلي صحيح من 1 إلى N لكل لجنة على حدة
                var affectedCommitteeIds = allCommittees.Select(c => c.CommitteeId).Distinct().ToList();
                foreach (var committeeId in affectedCommitteeIds)
                {
                    await RecalculateSeatNumbersByCommittee(committeeId);
                }

                if (totalAssigned < allStudents.Count)
                {
                    TempData["InfoMessage"] = $"تم تسكين عدد {totalAssigned} طالب بنجاح وفقاً لتوزيع اللجان، ولكن اللجان امتلأت بالكامل ويوجد عدد {allStudents.Count - totalAssigned} طالب بحاجة لإنشاء لجان إضافية.";
                }
                else
                {
                    TempData["SuccessMessage"] = $"نجاح التوزيع الشامل: تم تسكين وتوزيع جميع الطلاب البالغ عددهم ({totalAssigned}) طالب على لجانهم الامتحانية وتوليد أرقام الجلوس أبجدياً بنجاح 🚀.";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "فشل التوزيع التلقائي: سعة اللجان الحالية ممتلئة بالكامل، يرجى زيادة سعة المقاعد للجان القائمة.";
            }

            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // 8. عرض لجنة الطالب - COMMITTEE
        // =====================================
        public async Task<IActionResult> Committee(int id)
        {
            var student = await _context.Students
                .Include(s => s.Committee)
                .FirstOrDefaultAsync(s => s.StudentId == id);

            if (student == null) return NotFound();

            if (student.CommitteeId == null || student.CommitteeId == 0 || student.Committee == null)
            {
                ViewBag.Message = $"عفواً، الطالب ({student.FullName}) لم يتم تسكينه في أي لجنة ثابتة حتى الآن.";
                return View("NoCommittee");
            }

            var viewModel = new StudentCommitteeViewModel
            {
                StudentId = student.StudentId,
                FullName = student.FullName,
                AcademicYear = student.AcademicYear,
                SeatNumber = student.SeatNumber,
                CommitteeId = student.Committee.CommitteeId,
                CommitteeNumber = student.Committee.CommitteeNumber,
                NumberOfStudent = student.Committee.NumberOfStudent
            };

            return View(viewModel);
        }

        // =====================================
        // 9. مساعد: إعادة ترقيم مقاعد الجلوس أبجدياً داخل اللجنة
        // =====================================
        private async Task RecalculateSeatNumbersByCommittee(int committeeId)
        {
            var studentsInCommittee = await _context.Students
                .Where(s => s.CommitteeId == committeeId)
                .OrderBy(s => s.FullName)
                .ToListAsync();

            for (int i = 0; i < studentsInCommittee.Count; i++)
            {
                studentsInCommittee[i].SeatNumber = i + 1;
            }

            await _context.SaveChangesAsync();
        }
    }
}