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
                .ThenBy(s => s.Specialization)
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
                    Specialization = model.Specialization,
                    LocationId = model.LocationId, // التغيير هنا لـ LocationId
                    SeatNumber = 0,
                    ExamScheduleId = null
                };

                _context.Add(student);
                await _context.SaveChangesAsync();

                if (student.LocationId.HasValue)
                {
                    await RecalculateSeatNumbersByCommittee(student.LocationId.Value);
                }

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
                Specialization = student.Specialization,
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
                student.Specialization = model.Specialization;
                student.LocationId = model.LocationId; 

                _context.Update(student);
                await _context.SaveChangesAsync();

                if (oldLocationId != student.LocationId)
                {
                    if (oldLocationId.HasValue)
                        await RecalculateSeatNumbersByCommittee(oldLocationId.Value);
                    if (student.LocationId.HasValue)
                        await RecalculateSeatNumbersByCommittee(student.LocationId.Value);
                }

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

            if (locationId.HasValue)
                await RecalculateSeatNumbersByCommittee(locationId.Value);

            return RedirectToAction(nameof(Index));
        }

        // ======================================================================
        // 7. التوزيع التلقائي للطلاب مع فحص منع تعارض صلة القرابة (محدث بالكامل)
        // ======================================================================
        public async Task<IActionResult> DistributeStudents()
        {
            var allStudents = await _context.Students.ToListAsync();

            if (!allStudents.Any())
            {
                TempData["ErrorMessage"] = "عفواً، لا يوجد طلاب مسجلين في النظام حالياً.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var student in allStudents)
            {
                student.LocationId = null;
            }

            // الصالات اللي محدد لها سنة دراسية
            var blocksByYear = await _context.ExamLocations
                .Where(l => l.Type == LocationType.Block && l.AcademicYear != null)
                .ToListAsync();

            // مصفوفة فحص التعارضات (بدون تغيير)
            var studentConflictMap = new Dictionary<int, HashSet<int>>();
            var allRelatives = await _context.Relatives.ToListAsync();

            var committeeStaff = await _context.CommitteesAssignments
                .Where(ca => ca.LocationId != null)
                .Select(ca => new { ca.PersonId, LocationId = ca.LocationId.Value })
                .Distinct()
                .ToListAsync();

            foreach (var rel in allRelatives)
            {
                var forbiddenCommittees = committeeStaff
                    .Where(cs => cs.PersonId == rel.PersonId)
                    .Select(cs => cs.LocationId)
                    .ToList();

                if (forbiddenCommittees.Any())
                {
                    if (!studentConflictMap.ContainsKey(rel.StudentId))
                        studentConflictMap[rel.StudentId] = new HashSet<int>();

                    foreach (var locId in forbiddenCommittees)
                        studentConflictMap[rel.StudentId].Add(locId);
                }
            }

            // كاش للجان كل صالة (مش جراش دلوقتي)
            var blockCommitteesMap = new Dictionary<int, List<ExamLocation>>();
            var committeeOccupancy = new Dictionary<int, int>();

            async Task<List<ExamLocation>> GetCommitteesForBlockAsync(int blockId)
            {
                if (blockCommitteesMap.TryGetValue(blockId, out var cachedCommittees))
                    return cachedCommittees;

                // اللجان التابعة لهذه الصالة مباشرة فقط
                var committeesOfBlock = await _context.ExamLocations
                    .Where(l => l.Type == LocationType.Committee && l.ParentLocationId == blockId)
                    .OrderBy(l => l.LocationName)
                    .ToListAsync();

                blockCommitteesMap[blockId] = committeesOfBlock;

                foreach (var committee in committeesOfBlock)
                {
                    if (!committeeOccupancy.ContainsKey(committee.LocationId))
                        committeeOccupancy[committee.LocationId] = 0;
                }

                return committeesOfBlock;
            }

            var orderedStudents = allStudents.OrderBy(s => s.AcademicYear).ThenBy(s => s.FullName).ToList();

            int totalAssigned = 0;
            int skippedNoBlockForYear = 0;
            int skippedNoCommitteesInBlock = 0;

            foreach (var student in orderedStudents)
            {
                // الصالة بتتحدد مباشرة من سنة الطالب
                // ملحوظة: لو فيه أكتر من صالة بنفس السنة، التوزيع هيستخدم أول واحدة بس
                // (لو عايز توزيع على أكتر من صالة لنفس السنة قولي وأضيف منطق التنقل بينهم)
                var block = blocksByYear.FirstOrDefault(b => b.AcademicYear == student.AcademicYear);

                if (block == null)
                {
                    skippedNoBlockForYear++;
                    continue;
                }

                var committeesOfBlock = await GetCommitteesForBlockAsync(block.LocationId);

                if (!committeesOfBlock.Any())
                {
                    skippedNoCommitteesInBlock++;
                    continue;
                }

                foreach (var targetCommittee in committeesOfBlock)
                {
                    int currentCount = committeeOccupancy[targetCommittee.LocationId];

                    bool hasConflict = studentConflictMap.ContainsKey(student.StudentId) &&
                                       studentConflictMap[student.StudentId].Contains(targetCommittee.LocationId);

                    if (currentCount < (targetCommittee.StudentCapacity ?? 0) && !hasConflict)
                    {
                        student.LocationId = targetCommittee.LocationId;
                        committeeOccupancy[targetCommittee.LocationId] = currentCount + 1;
                        totalAssigned++;
                        break;
                    }
                }
            }

            if (totalAssigned > 0)
            {
                await _context.SaveChangesAsync();

                var affectedLocationIds = allStudents
                    .Where(s => s.LocationId != null)
                    .Select(s => s.LocationId.Value)
                    .Distinct()
                    .ToList();

                foreach (var locId in affectedLocationIds)
                {
                    await RecalculateSeatNumbersByCommittee(locId);
                }

                TempData["SuccessMessage"] = $"تم التوزيع لـ {totalAssigned} طالب بنجاح، كل سنة دراسية التزمت بصالتها المخصصة، مع فحص تعارض صلة القرابة.";
            }
            else
            {
                TempData["ErrorMessage"] = $"فشل التوزيع: {skippedNoBlockForYear} طالب بلا صالة مخصصة لسنتهم الدراسية، {skippedNoCommitteesInBlock} صالة بلا لجان تابعة لها، أو سعة اللجان ممتلئة/تعارضات قرابة تمنع التسكين.";
            }

            return RedirectToAction(nameof(Index));
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
                SeatNumber = student.SeatNumber,
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
                    Specialization = specialization,
                    SeatNumber = seatNumber,
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
        private async Task RecalculateSeatNumbersByCommittee(int locationId)
        {
            var studentsInCommittee = await _context.Students
                .Where(s => s.LocationId == locationId)
                .OrderBy(s => s.FullName)
                .ToListAsync();

            for (int i = 0; i < studentsInCommittee.Count; i++)
                studentsInCommittee[i].SeatNumber = i + 1;

            await _context.SaveChangesAsync();
        }

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