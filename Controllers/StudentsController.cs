using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
                .Include(s => s.ExamLocation) // تحديث للموقع الموحد
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

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return PartialView("_StudentTablePartial", students);

            return View(students);
        }

        // =====================================
        // 3. التفاصيل - DETAILS
        // =====================================
        public async Task<IActionResult> Details(int id)
        {
            var student = await _context.Students
                .Include(s => s.ExamLocation)
                .Include(s => s.Relatives)
                    .ThenInclude(r => r.Person)  // ← دي اللي ناقصة
                .FirstOrDefaultAsync(s => s.StudentId == id);

            if (student == null) return NotFound();

            return View(student);
        }

        // =====================================
        // 4. إضافة طالب - CREATE
        // =====================================
        public async Task<IActionResult> Create()
        {
            // جلب الأماكن التي تمثل لجان امتحانية فقط وتمريرها للشاشة
            var committees = await _context.ExamLocations
                .Where(l => l.Type == LocationType.Committee)
                .OrderBy(l => l.LocationName)
                .ToListAsync();

            ViewData["LocationId"] = new SelectList(committees, "LocationId", "LocationName");
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
                    var committeesList = await _context.ExamLocations.Where(l => l.Type == LocationType.Committee).OrderBy(l => l.LocationName).ToListAsync();
                    ViewData["LocationId"] = new SelectList(committeesList, "LocationId", "LocationName", model.LocationId);
                    return View(model);
                }

                var student = new Student
                {
                    FullName = model.FullName,
                    NationalId = model.NationalId,
                    AcademicYear = model.AcademicYear,
                    
                    LocationId = model.LocationId, // التغيير هنا لـ LocationId
                    
                    ExamScheduleId = null
                };

                _context.Add(student);
                await _context.SaveChangesAsync();

               

                return RedirectToAction(nameof(Index));
            }

            var committeesRollback = await _context.ExamLocations.Where(l => l.Type == LocationType.Committee).OrderBy(l => l.LocationName).ToListAsync();
            ViewData["LocationId"] = new SelectList(committeesRollback, "LocationId", "LocationName", model.LocationId);
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
               
                LocationId = student.LocationId // التغيير هنا لـ LocationId
            };

            ViewBag.StudentId = student.StudentId;
            var committees = await _context.ExamLocations.Where(l => l.Type == LocationType.Committee).OrderBy(l => l.LocationName).ToListAsync();
            ViewData["LocationId"] = new SelectList(committees, "LocationId", "LocationName", student.LocationId);
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
                    var committeesList = await _context.ExamLocations.Where(l => l.Type == LocationType.Committee).OrderBy(l => l.LocationName).ToListAsync();
                    ViewData["LocationId"] = new SelectList(committeesList, "LocationId", "LocationName", model.LocationId);
                    return View(model);
                }

                var student = await _context.Students.FindAsync(id);
                if (student == null) return NotFound();

                int? oldLocationId = student.LocationId;

                student.FullName = model.FullName;
                student.NationalId = model.NationalId;
                student.AcademicYear = model.AcademicYear;
                
                student.LocationId = model.LocationId; 

                _context.Update(student);
                await _context.SaveChangesAsync();

              

                return RedirectToAction(nameof(Index));
            }

            var committeesRollback = await _context.ExamLocations.Where(l => l.Type == LocationType.Committee).OrderBy(l => l.LocationName).ToListAsync();
            ViewData["LocationId"] = new SelectList(committeesRollback, "LocationId", "LocationName", model.LocationId);
            return View(model);
        }

        // =====================================
        // 6. الحذف - DELETE
        // =====================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return BadRequest();

            var student = await _context.Students
                .Include(s => s.ExamLocation)
                .Include(s => s.Relatives)
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

            int? locationId = student.LocationId;

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

          

            return RedirectToAction(nameof(Index));
        }

        // ======================================================================
        // 7. التوزيع التلقائي للطلاب مع فحص منع تعارض صلة القرابة (محدث للأداء العالي)
        // ======================================================================
        public async Task<IActionResult> DistributeStudents()
        {
            // 1. جلب البيانات الأساسية دفعة واحدة
            var allStudents = await _context.Students.ToListAsync();
            var allCommittees = await _context.ExamLocations
                .Where(l => l.Type == LocationType.Committee)
                .ToListAsync();

            // 2. جلب عدد الطلاب الموزعين مسبقاً في كل لجنة (في الذاكرة) لتجنب استعلامات متكررة
            var committeeOccupancy = await _context.Students
                .Where(s => s.LocationId != null)
                .GroupBy(s => s.LocationId)
                .Select(g => new { LocId = g.Key.Value, Count = g.Count() })
                .ToDictionaryAsync(x => x.LocId, x => x.Count);

            // 3. جلب التعارضات (القرابة)
            var studentConflictMap = await GetStudentConflictMap();

            // 4. جلب الجداول المتاحة
            var schedules = await _context.ExamSchedules
                .Include(es => es.Exam).ThenInclude(e => e.Subject)
                .ToListAsync();

            // 5. التوزيع
            var studentsByYear = allStudents.GroupBy(s => s.AcademicYear);

            foreach (var yearGroup in studentsByYear)
            {
                // اللجان المتاحة فقط للسنة الدراسية الحالية
                var relevantLocIds = schedules
                    .Where(s => s.Exam.Subject.AcademicYear == yearGroup.Key)
                    .Select(s => s.LocationId)
                    .Distinct()
                    .ToList();

                foreach (var student in yearGroup)
                {
                    foreach (var locId in relevantLocIds)
                    {
                        var committee = allCommittees.FirstOrDefault(c => c.LocationId == locId);
                        if (committee == null) continue;

                        int capacity = committee.StudentCapacity ?? 0;
                        int currentCount = committeeOccupancy.ContainsKey(locId) ? committeeOccupancy[locId] : 0;

                        if (currentCount < capacity)
                        {
                            // فحص تعارض القرابة
                            if (studentConflictMap.TryGetValue(student.StudentId, out var forbiddenSet) && forbiddenSet.Contains(locId))
                                continue;

                            student.LocationId = locId;
                            committeeOccupancy[locId] = currentCount + 1; // تحديث العداد في الذاكرة
                            break;
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"تم توزيع {allStudents.Count} طالب بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        // دالة مساعدة لجلب تعارضات القرابة بكفاءة
        private async Task<Dictionary<int, HashSet<int>>> GetStudentConflictMap()
        {
            var map = new Dictionary<int, HashSet<int>>();
            var committeeStaff = await _context.CommitteesAssignments
                .Where(ca => ca.LocationId != null)
                .Select(ca => new { ca.PersonId, LocationId = ca.LocationId.Value })
                .ToListAsync();

            var relatives = await _context.Relatives.ToListAsync();

            foreach (var rel in relatives)
            {
                var forbiddenLocs = committeeStaff
                    .Where(cs => cs.PersonId == rel.PersonId)
                    .Select(cs => cs.LocationId);

                if (!map.ContainsKey(rel.StudentId)) map[rel.StudentId] = new HashSet<int>();
                foreach (var locId in forbiddenLocs) map[rel.StudentId].Add(locId);
            }
            return map;
        }

        // =====================================
        // 8. عرض لجنة الطالب - COMMITTEE (معدل)
        // =====================================
        public async Task<IActionResult> Committee(int id)
        {
            var student = await _context.Students
                .Include(s => s.ExamLocation) 
                .FirstOrDefaultAsync(s => s.StudentId == id);

            if (student == null) return NotFound();

            if (student.LocationId == null || student.LocationId == 0 || student.ExamLocation == null)
            {
                ViewBag.Message = $"عفواً، الطالب ({student.FullName}) لم يتم تسكينه في أي لجنة ثابتة حتى الآن.";
                return View("NoCommittee");
            }


            var viewModel = new StudentCommitteeViewModel
            {
                StudentId = student.StudentId,
                FullName = student.FullName,
                AcademicYear = student.AcademicYear,
               
                LocationId = student.ExamLocation?.LocationId ?? 0,
                LocationName = student.ExamLocation?.LocationName ?? "غير محدد",
                StudentCapacity = student.ExamLocation?.StudentCapacity ?? 0
            };

            return View(viewModel);
        }
        // =====================================
        // 9. استيراد Excel - IMPORT (معدل ومصحح)
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

                // التحقق من عدم تكرار الرقم القومي
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
                    LocationId = null, 
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
       

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClearStudents()
        {
            var students = await _context.Students.ToListAsync();

            if (students.Any())
            {
                _context.Students.RemoveRange(students);
                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = "تم حذف جميع الطلاب بنجاح.";

            return RedirectToAction(nameof(Index));
        }
    }
}