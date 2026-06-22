using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using projectweb.Models;
using projectweb.ViewModel;
using System;
using System.Collections.Generic;
using System.IO;
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
        public async Task<IActionResult> Index(string searchTerm)
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
                .ThenBy(s => s.Specialization)
                .ThenBy(s => s.FullName)
                .ToListAsync();

            ViewBag.Search = searchTerm;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_StudentTablePartial", students);

            return View(students);
        }

        //// =====================================
        //// 2. البحث - SEARCH
        //// =====================================
        //[HttpGet]
        //public async Task<IActionResult> Search(string searchTerm)
        //{
        //    var query = _context.Students
        //        .Include(s => s.Committee)
        //        .Include(s => s.ExamSchedule)
        //        .AsQueryable();

        //    if (!string.IsNullOrEmpty(searchTerm))
        //    {
        //        searchTerm = searchTerm.ToLower().Trim();
        //        query = query.Where(s => s.FullName.ToLower().Contains(searchTerm)
        //                              || s.NationalId.Contains(searchTerm));
        //    }

        //    var students = await query
        //        .OrderBy(s => s.AcademicYear)
        //        .ThenBy(s => s.FullName)
        //        .ToListAsync();

        //    ViewBag.Search = searchTerm;
        //    return View("Index", students);
        //}

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
                        await RecalculateSeatNumbersByCommittee(oldCommitteeId.Value);
                    if (student.CommitteeId.HasValue)
                        await RecalculateSeatNumbersByCommittee(student.CommitteeId.Value);
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

            if (committeeId.HasValue)
                await RecalculateSeatNumbersByCommittee(committeeId.Value);

            return RedirectToAction(nameof(Index));
        }

        // ======================================================================
        // 7. التوزيع التلقائي للطلاب على اللجان المتاحة بالترتيب الأبجدي
        // ======================================================================
        public async Task<IActionResult> DistributeStudents()
        {
            // 1. جلب كافة الطلاب
            var allStudents = await _context.Students.ToListAsync();
            if (!allStudents.Any())
            {
                TempData["ErrorMessage"] = "عفواً، لا يوجد طلاب مسجلين في النظام حالياً.";
                return RedirectToAction(nameof(Index));
            }

            // 2. جلب كافة اللجان
            var allCommittees = await _context.Committees.OrderBy(c => c.CommitteeNumber).ToListAsync();
            if (!allCommittees.Any())
            {
                TempData["ErrorMessage"] = "فشل التوزيع: لا توجد لجان امتحانية.";
                return RedirectToAction(nameof(Index));
            }

            
            foreach (var student in allStudents)
            {
                student.CommitteeId = null;
            }

            // 3. بناء مصفوفة التعارض (Conflict Map)
            var studentConflictMap = new Dictionary<int, HashSet<int>>();

            // جلب كل علاقات القرابة
            var allRelatives = await _context.Relatives.ToListAsync();

            // جلب كل الملاحظين/الموظفين المعينين فعلياً في اللجان (تصفية آمنة)
            var committeeStaff = await _context.CommitteesAssignments
                .Where(ca => ca.CommitteeId != null)
                .Select(ca => new { ca.PersonId, CommitteeId = ca.CommitteeId.Value })
                .Distinct() // منع التكرار لتحسين الأداء
                .ToListAsync();

            foreach (var rel in allRelatives)
            {
                // البحث عن اللجان التي تم تعيين هذا الموظف القريب فيها
                var forbiddenCommittees = committeeStaff
                    .Where(cs => cs.PersonId == rel.PersonId)
                    .Select(cs => cs.CommitteeId)
                    .ToList();

                if (forbiddenCommittees.Any())
                {
                    if (!studentConflictMap.ContainsKey(rel.StudentId))
                        studentConflictMap[rel.StudentId] = new HashSet<int>();

                    foreach (var comId in forbiddenCommittees)
                        studentConflictMap[rel.StudentId].Add(comId);
                }
            }

            // 4. خوارزمية توزيع الطلاب
            var orderedStudents = allStudents.OrderBy(s => s.AcademicYear).ThenBy(s => s.FullName).ToList();
            var committeeOccupancy = allCommittees.ToDictionary(c => c.CommitteeId, c => 0);

            int totalAssigned = 0;

            foreach (var student in orderedStudents)
            {
                bool distributed = false;

                foreach (var targetCommittee in allCommittees)
                {
                    int currentCount = committeeOccupancy[targetCommittee.CommitteeId];

                    // هل يوجد تعارض قرابة؟
                    bool hasConflict = studentConflictMap.ContainsKey(student.StudentId) &&
                                       studentConflictMap[student.StudentId].Contains(targetCommittee.CommitteeId);

                    // التحقق من السعة الاستيعابية وعدم وجود تعارض
                    if (currentCount < targetCommittee.NumberOfStudent && !hasConflict)
                    {
                        student.CommitteeId = targetCommittee.CommitteeId;
                        committeeOccupancy[targetCommittee.CommitteeId] = currentCount + 1;
                        totalAssigned++;
                        distributed = true;
                        break; // الانتقال للطالب التالي بعد التسكين بنجاح
                    }
                }
            }

            // 5. حفظ التعديلات وإعادة حساب أرقام الجلوس
            if (totalAssigned > 0)
            {
                await _context.SaveChangesAsync();

                // جلب معرفات اللجان التي تم تسكين طلاب بها فعلياً لتحديث أرقام جلوسها
                var affectedCommitteeIds = allStudents
                    .Where(s => s.CommitteeId != null)
                    .Select(s => s.CommitteeId.Value)
                    .Distinct()
                    .ToList();

                foreach (var committeeId in affectedCommitteeIds)
                {
                    await RecalculateSeatNumbersByCommittee(committeeId);
                }

                TempData["SuccessMessage"] = $"تم التوزيع لـ {totalAssigned} طالب بنجاح مع تفعيل فحص منع تعارض صلة القرابة.";
            }
            else
            {
                TempData["ErrorMessage"] = "فشل التوزيع: سعة اللجان ممتلئة بالكامل أو توجد تعارضات قرابة تمنع تسكين الطلاب.";
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
        // 9. استيراد Excel - IMPORT
        // =====================================
        public IActionResult ImportExcel()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportExcel(IFormFile excelfile)
        {
            if (excelfile == null || excelfile.Length == 0)
            {
                TempData["ErrorMessage"] = "برجاء اختيار ملف Excel أولاً.";
                return View();
            }

            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            int added = 0, skipped = 0;

            using var stream = new MemoryStream();
            await excelfile.CopyToAsync(stream);

            using var package = new ExcelPackage(stream);
            var sheet = package.Workbook.Worksheets[0];
            int rowCount = sheet.Dimension?.Rows ?? 0;

            for (int i = 2; i <= rowCount; i++)
            {
                var fullName = sheet.Cells[i, 1].Text?.Trim();
                var nationalId = sheet.Cells[i, 2].Text?.Trim();
                var yearText = sheet.Cells[i, 3].Text?.Trim();
                var specText = sheet.Cells[i, 4].Text?.Trim();
                var seatText = sheet.Cells[i, 5].Text?.Trim();

                if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(nationalId))
                { skipped++; continue; }

                if (_context.Students.Any(s => s.NationalId == nationalId))
                { skipped++; continue; }

                var academicYear = yearText switch
                {
                    "المستوى الأول" => AcademicLevel.FirstYear,
                    "المستوى الثاني" => AcademicLevel.SecondYear,
                    "المستوى الثالث" => AcademicLevel.ThirdYear,
                    "المستوى الرابع" => AcademicLevel.FourthYear,
                    _ => AcademicLevel.FirstYear
                };

                var specialization = specText switch
                {
                    "عام" => StudentSpecialization.General,
                    "إدارة" => StudentSpecialization.Management,
                    "تدريس" => StudentSpecialization.Teaching,
                    "تدريب" => StudentSpecialization.Training,
                    _ => StudentSpecialization.General
                };

                int seatNumber = 0;
                if (!string.IsNullOrEmpty(seatText))
                    int.TryParse(seatText, out seatNumber);

                _context.Students.Add(new Student
                {
                    FullName = fullName,
                    NationalId = nationalId,
                    AcademicYear = academicYear,
                    Specialization = specialization,
                    SeatNumber = seatNumber,
                    CommitteeId = null,
                    ExamScheduleId = null
                });

                added++;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"تم الاستيراد بنجاح! تمت إضافة {added} طالب، وتم تجاهل {skipped} مكرر/فارغ.";
            return RedirectToAction(nameof(Index));
        }
        // =====================================
        // 10. مساعد: إعادة ترقيم المقاعد
        // =====================================
        private async Task RecalculateSeatNumbersByCommittee(int committeeId)
        {
            var studentsInCommittee = await _context.Students
                .Where(s => s.CommitteeId == committeeId)
                .OrderBy(s => s.FullName)
                .ToListAsync();

            for (int i = 0; i < studentsInCommittee.Count; i++)
                studentsInCommittee[i].SeatNumber = i + 1;

            await _context.SaveChangesAsync();
        }
    }
}