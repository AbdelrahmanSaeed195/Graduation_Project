using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using projectweb.Models;
using projectweb.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
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
        // =====================================
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

            // إذا كان الطلب AJAX أرسل الجدول فقط
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("_PersonTablePartial", uniqueResult);
            }

            return View(uniqueResult);
        }

        // =====================================
        // DETAILS
        // =====================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var person = await _context.Persons.FirstOrDefaultAsync(m => m.PersonId == id);
            if (person == null) return NotFound();
            return View(person);
        }

        // =====================================
        // CREATE
        // =====================================
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
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
                    if (person.RoleId == 0) person.RoleId = 1;

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

        // =====================================
        // EDIT
        // =====================================
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

        // =====================================
        // DELETE
        // =====================================
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

        // =====================================
        // PRINT ASSIGNMENTS
        // =====================================
        public async Task<IActionResult> PrintAssignments(int id)
        {
            var person = await _context.Persons
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PersonId == id);

            if (person == null) return Content($"خطأ: لم يتم العثور على الموظف رقم {id}");

            var rawAssignments = await _context.CommitteesAssignments
                .AsNoTracking()
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam).ThenInclude(e => e.Subject)
                .Where(a => a.PersonId == id)
                .ToListAsync();

            var yearTimesMap = new Dictionary<string, string>
    {
        { "1", "" }, { "2", "" }, { "3", "" }, { "4", "" }
    };

            var groupedRows = new List<AssignmentRowGroup>();

            string assignedRoleInCommittee = "عضو لجنة امتحانات";

            if (rawAssignments != null && rawAssignments.Any())
            {
                var firstValidAssignment = rawAssignments.FirstOrDefault(a => !string.IsNullOrEmpty(a.RoleType));
                if (firstValidAssignment != null)
                {
                    assignedRoleInCommittee = firstValidAssignment.RoleType;

                    if (assignedRoleInCommittee.Contains("رئيس صالة")) assignedRoleInCommittee = "رئيس صالة";
                    else if (assignedRoleInCommittee.Contains("مراقب")) assignedRoleInCommittee = "مراقب";
                    else if (assignedRoleInCommittee.Contains("ملاحظ")) assignedRoleInCommittee = "ملاحظ";
                }

                foreach (var assignment in rawAssignments.Where(a => a.ExamSchedule?.Exam != null))
                {
                    var exam = assignment.ExamSchedule.Exam;
                    AcademicLevel? level = exam.Subject?.AcademicYear;

                    string yearNum = level switch
                    {
                        AcademicLevel.FirstYear => "1",
                        AcademicLevel.SecondYear => "2",
                        AcademicLevel.ThirdYear => "3",
                        AcademicLevel.FourthYear => "4",
                        _ => ""
                    };

                    if (!string.IsNullOrEmpty(yearNum) && string.IsNullOrEmpty(yearTimesMap[yearNum]))
                    {
                        yearTimesMap[yearNum] = $"{DateTime.Today.Add(exam.StartTime):hh:mm tt} - {DateTime.Today.Add(exam.EndTime):hh:mm tt}";
                    }
                }

                groupedRows = rawAssignments
                    .Where(a => a.ExamSchedule?.Exam != null)
                    .GroupBy(a => a.ExamSchedule.Exam.ExamDate.Date)
                    .Select(g => new AssignmentRowGroup
                    {
                        Date = g.Key,
                        Day = g.Key.ToString("dddd", new System.Globalization.CultureInfo("ar-EG")),
                        DailyItems = g.Select(a =>
                        {
                            var exam = a.ExamSchedule.Exam;
                            AcademicLevel? level = exam.Subject?.AcademicYear;

                            string yearNum = level switch
                            {
                                AcademicLevel.FirstYear => "1",
                                AcademicLevel.SecondYear => "2",
                                AcademicLevel.ThirdYear => "3",
                                AcademicLevel.FourthYear => "4",
                                _ => ""
                            };

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

            var model = new PrintReportViewModel
            {
                PersonFullName = person.FullName,
                PersonRoleInReport = assignedRoleInCommittee,
                Rows = groupedRows,
                YearTimes = yearTimesMap,
                AcademicYear = "2025/2026",
                CollegeName = "كلية علوم الرياضة"
            };

            return View(model);
        }

        public string GetArabicJobTitle(JobTitle job)
        {
            return job switch
            {
                JobTitle.ProfessorEmeritus => "أستاذ متفرغ",
                JobTitle.AssistantProfessor => "أستاذ مساعد",
                JobTitle.Professor => "أستاذ",
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

        // =====================================
        // EXPORT TO EXCEL
        // =====================================
        public async Task<IActionResult> ExportToExcel()
        {
            var persons = await _context.Persons
                .OrderBy(p => p.JobRole)
                .ThenBy(p => p.FullName)
                .ToListAsync();

            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var sheet = package.Workbook.Worksheets.Add("الموظفين");

            // الهيدر
            sheet.Cells[1, 1].Value = "الاسم الكامل";
            sheet.Cells[1, 2].Value = "الرقم القومي";
            sheet.Cells[1, 3].Value = "رقم الهاتف";
            sheet.Cells[1, 4].Value = "البريد الإلكتروني";
            sheet.Cells[1, 5].Value = "الوظيفة";
            sheet.Cells[1, 6].Value = "الحالة";

            // تنسيق الهيدر
            using (var range = sheet.Cells[1, 1, 1, 6])
            {
                range.Style.Font.Bold = true;
                range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
            }

            // البيانات
            int row = 2;
            foreach (var p in persons)
            {
                sheet.Cells[row, 1].Value = p.FullName;
                sheet.Cells[row, 2].Value = p.NationalId;
                sheet.Cells[row, 3].Value = p.Phone;
                sheet.Cells[row, 4].Value = p.Email;
                sheet.Cells[row, 5].Value = GetArabicJobTitle(p.JobRole);
                sheet.Cells[row, 6].Value = p.IsActiveForAssignment ? "نشط" : "متوقف";
                row++;
            }

            sheet.Cells.AutoFitColumns();

            var fileBytes = package.GetAsByteArray();
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Persons_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        // =====================================
        // IMPORT EXCEL - GET
        // =====================================
        public IActionResult ImportExcel()
        {
            return View();
        }

        // =====================================
        // IMPORT EXCEL - POST
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportExcel(IFormFile excelfile)
        {
            if (excelfile == null || excelfile.Length == 0)
            {
                TempData["Error"] = "برجاء اختيار ملف Excel أولاً.";
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
                var phone = sheet.Cells[i, 3].Text?.Trim();
                var email = sheet.Cells[i, 5].Text?.Trim();
                var jobText = sheet.Cells[i, 4].Text?.Trim();

                if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(nationalId))
                {
                    skipped++;
                    continue;
                }

                if (_context.Persons.Any(p => p.NationalId == nationalId))
                {
                    skipped++;
                    continue;
                }

                var jobRole = jobText switch
                {
                    "أستاذ متفرغ" => JobTitle.ProfessorEmeritus,
                    "أستاذ مساعد" => JobTitle.AssistantProfessor,
                    "أستاذ" => JobTitle.Professor,
                    "مدرس" => JobTitle.StaffObserver,
                    "مدرس مساعد" => JobTitle.AssistantStaff,
                    "معيد" => JobTitle.Assistant,
                    "موظف" => JobTitle.Employee,
                    "دكتور" => JobTitle.Doctor,
                    "ممرض" => JobTitle.Nurse,
                    _ => JobTitle.StaffObserver
                };

                _context.Persons.Add(new Person
                {
                    FullName = fullName,
                    NationalId = nationalId,
                    Phone = phone ?? "",
                    Email = email ?? "",
                    JobRole = jobRole,
                    RoleId = 1,
                    IsActiveForAssignment = true
                });

                added++;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم الاستيراد بنجاح! تمت إضافة {added} شخص، وتم تجاهل {skipped} مكرر/فارغ.";
            return RedirectToAction(nameof(Index));
        }

    }
}
