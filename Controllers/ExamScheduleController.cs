using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace projectweb.Controllers
{
    public class ExamSchedulesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ExamSchedulesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================
        // 1. القائمة الرئيسية - INDEX
        // =====================================
        public async Task<IActionResult> Index()
        {
            var examSchedules = _context.ExamSchedules
                .Include(e => e.Block)
                .Include(e => e.Exam).ThenInclude(ex => ex.Subject)
                .OrderBy(e => e.Exam.ExamDate)
                .ThenBy(e => e.Exam.StartTime);

            return View(await examSchedules.ToListAsync());
        }

        // =====================================
        // 2. التفاصيل - DETAILS
        // =====================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var examSchedule = await _context.ExamSchedules
                .Include(e => e.Block)
                .Include(e => e.Exam).ThenInclude(ex => ex.Subject)
                .FirstOrDefaultAsync(m => m.ExamScheduleId == id);

            if (examSchedule == null) return NotFound();

            return View(examSchedule);
        }

        // =====================================
        // 3. شاشة الإضافة - CREATE (GET)
        // =====================================
        public async Task<IActionResult> Create()
        {
            var exams = await _context.Exams
                .Include(e => e.Subject)
                .AsNoTracking()
                .ToListAsync();

            var examList = exams.Select(e =>
            {
                string arabicLevel = "غير محدد";
                if (e.Subject != null)
                {
                    arabicLevel = e.Subject.AcademicYear switch
                    {
                        AcademicLevel.FirstYear => "المستوى الأول",
                        AcademicLevel.SecondYear => "المستوى الثاني",
                        AcademicLevel.ThirdYear => "المستوى الثالث",
                        AcademicLevel.FourthYear => "المستوى الرابع",
                        _ => "غير محدد"
                    };
                }

                return new
                {
                    Id = e.ExamId,
                    Name = e.Subject != null
                        ? $"{e.Subject.SubjectName} - {e.ExamDate.ToString("yyyy/MM/dd")} ({arabicLevel})"
                        : $"امتحان رقم #{e.ExamId} - {e.ExamDate.ToString("yyyy/MM/dd")}"
                };
            }).ToList();

            ViewData["ExamId"] = new SelectList(examList, "Id", "Name");
            ViewData["BlockId"] = new SelectList(Enumerable.Empty<SelectListItem>(), "Value", "Text");

            return View();
        }

        // =====================================
        // 4. دالة تفاعلية: جلب البلوكات المتاحة وقت الامتحان
        // =====================================
        [HttpGet]
        public async Task<IActionResult> GetAvailableBlocks(int examId)
        {
            var selectedExam = await _context.Exams.FindAsync(examId);
            if (selectedExam == null) return Json(new List<object>());

            var busyBlockIds = await _context.ExamSchedules
                .Include(es => es.Exam)
                .Where(es => es.Exam.ExamDate.Date == selectedExam.ExamDate.Date
                          && selectedExam.StartTime < es.Exam.EndTime
                          && selectedExam.EndTime > es.Exam.StartTime)
                .Select(es => es.BlockId)
                .ToListAsync();

            var availableBlocks = await _context.Blocks
                .Where(b => !busyBlockIds.Contains(b.BlockId))
                .Select(b => new
                {
                    id = b.BlockId,
                    name = b.BlockName
                })
                .ToListAsync();

            return Json(availableBlocks);
        }

        // =====================================
        // 5. حفظ الجلسة الجديدة - CREATE (POST)
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ExamScheduleId,ExamId,BlockId,ScheduledDate")] ExamSchedule examSchedule)
        {
            if (ModelState.IsValid)
            {
                var currentExam = await _context.Exams.FindAsync(examSchedule.ExamId);

                bool isBusy = await _context.ExamSchedules
                    .Include(es => es.Exam)
                    .AnyAsync(es => es.BlockId == examSchedule.BlockId
                                 && es.Exam.ExamDate.Date == currentExam.ExamDate.Date
                                 && (currentExam.StartTime < es.Exam.EndTime && currentExam.EndTime > es.Exam.StartTime));

                if (isBusy)
                {
                    ModelState.AddModelError("", "هذا البلوك (المبنى) محجوز بالكامل في هذا الوقت لامتحان آخر.");
                }
                else
                {
                    _context.Add(examSchedule);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تخصيص البلوك للجلسة بنجاح.";
                    return RedirectToAction(nameof(Index));
                }
            }

            await PopulateDropdownsAsync(examSchedule.ExamId, examSchedule.BlockId);
            return View(examSchedule);
        }

        // =====================================  
        // 6. شاشة التعديل - EDIT (GET)
        // =====================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var examSchedule = await _context.ExamSchedules
                .Include(es => es.Exam)
                    .ThenInclude(e => e.Subject)
                .FirstOrDefaultAsync(es => es.ExamScheduleId == id);

            if (examSchedule == null) return NotFound();

            await PopulateDropdownsAsync(examSchedule.ExamId, examSchedule.BlockId);
            return View(examSchedule);
        }

        // =====================================
        // 7. حفظ التعديلات - EDIT (POST)
        // =====================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ExamScheduleId,ExamId,BlockId,ScheduledDate")] ExamSchedule examSchedule)
        {
            if (id != examSchedule.ExamScheduleId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var currentExam = await _context.Exams.FindAsync(examSchedule.ExamId);

                    bool isBusy = await _context.ExamSchedules
                        .Include(es => es.Exam)
                        .AnyAsync(es => es.BlockId == examSchedule.BlockId
                                     && es.ExamScheduleId != examSchedule.ExamScheduleId
                                     && es.Exam.ExamDate.Date == currentExam.ExamDate.Date
                                     && (currentExam.StartTime < es.Exam.EndTime && currentExam.EndTime > es.Exam.StartTime));

                    if (isBusy)
                    {
                        ModelState.AddModelError("", "عفواً، تم تعديل الجدول مسبقاً وهذا البلوك مشغول حالياً.");
                        await PopulateDropdownsAsync(examSchedule.ExamId, examSchedule.BlockId);
                        return View(examSchedule);
                    }

                    _context.Update(examSchedule);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث توزيع البلوك بنجاح.";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ExamScheduleExists(examSchedule.ExamScheduleId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdownsAsync(examSchedule.ExamId, examSchedule.BlockId);
            return View(examSchedule);
        }

        // =====================================
        // 8. شاشة الحذف - DELETE (GET)
        // =====================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var examSchedule = await _context.ExamSchedules
                .Include(e => e.Block)
                .Include(e => e.Exam).ThenInclude(ex => ex.Subject)
                .FirstOrDefaultAsync(m => m.ExamScheduleId == id);

            if (examSchedule == null) return NotFound();

            string arabicLevel = "غير محدد";
            if (examSchedule.Exam?.Subject != null)
            {
                arabicLevel = examSchedule.Exam.Subject.AcademicYear switch
                {
                    AcademicLevel.FirstYear => "المستوى الأول",
                    AcademicLevel.SecondYear => "المستوى الثاني",
                    AcademicLevel.ThirdYear => "المستوى الثالث",
                    AcademicLevel.FourthYear => "المستوى الرابع",
                    _ => "غير محدد"
                };
            }
            ViewBag.ArabicAcademicYear = arabicLevel;

            return View(examSchedule);
        }

        // =====================================
        // 9. تأكيد الحذف - DELETE (POST)
        // =====================================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var examSchedule = await _context.ExamSchedules.FindAsync(id);
            if (examSchedule != null)
            {
                _context.ExamSchedules.Remove(examSchedule);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف جلسة توزيع البلوك بنجاح.";
            }
            return RedirectToAction(nameof(Index));
        }

        // =====================================
        // 10. دالة تعبئة القوائم المنسدلة الموحدة (محدثة بالكامل لـ Async والـ Enum)
        // =====================================
        private async Task PopulateDropdownsAsync(object selectedExam = null, object selectedBlock = null)
        {
            var exams = await _context.Exams
                .Include(e => e.Subject)
                .AsNoTracking()
                .ToListAsync();

            var examsQuery = exams.Select(e =>
            {
                string arabicLevel = "غير محدد";
                if (e.Subject != null)
                {
                    arabicLevel = e.Subject.AcademicYear switch
                    {
                        AcademicLevel.FirstYear => "المستوى الأول",
                        AcademicLevel.SecondYear => "المستوى الثاني",
                        AcademicLevel.ThirdYear => "المستوى الثالث",
                        AcademicLevel.FourthYear => "المستوى الرابع",
                        _ => "غير محدد"
                    };
                }

                return new
                {
                    Id = e.ExamId,
                    Name = e.Subject != null
                        ? $"{e.Subject.SubjectName} - {e.ExamDate.ToString("yyyy/MM/dd")} ({arabicLevel})"
                        : $"امتحان #{e.ExamId} - {e.ExamDate.ToString("yyyy/MM/dd")}"
                };
            }).ToList();

            var blocksList = await _context.Blocks
                .AsNoTracking()
                .ToListAsync();

            var blocksQuery = blocksList.Select(b => new
            {
                Id = b.BlockId,
                Name = b.BlockName
            }).ToList();

            ViewData["ExamId"] = new SelectList(examsQuery, "Id", "Name", selectedExam);
            ViewData["BlockId"] = new SelectList(blocksQuery, "Id", "Name", selectedBlock);
        }

        private bool ExamScheduleExists(int id)
        {
            return _context.ExamSchedules.Any(e => e.ExamScheduleId == id);
        }
    }
}