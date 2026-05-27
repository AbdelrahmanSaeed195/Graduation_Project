using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace projectweb.Models
{
    public class CommitteesAssignment
    {
        [Key]
        [Display(Name = "كود التكليف")]
        public int AssignmentId { get; set; } 

        [Required(ErrorMessage = "يجب اختيار الموظف")]
        [Display(Name = "الموظف")]
        public int PersonId { get; set; }

        [Display(Name = "الصالة")]
        public int? HallId { get; set; } 

        [Display(Name = "البلوك")]
        public int? BlockId { get; set; }

        [Display(Name = "اللجنة")]
        public int? CommitteeId { get; set; }

        [Required(ErrorMessage = "يجب تحديد الدور")]
        [Display(Name = "الدور الوظيفي")]
        public int RoleId { get; set; }

        [Required(ErrorMessage = "يجب اختيار الجلسة")]
        [Display(Name = "جلسة الامتحان")]
        public int ExamScheduleId { get; set; }

        [Display(Name = "نوع التوزيع")]
        public string? AssignmentType { get; set; }

        [Display(Name = "مسمى الدور")]
        public string? RoleType { get; set; }

        // --- العلاقات (Navigation Properties) ---

        [ValidateNever]
        [ForeignKey("PersonId")]
        public virtual Person Person { get; set; }

        [ValidateNever]
        [ForeignKey("HallId")]
        public virtual Hall? Hall { get; set; }

        [ValidateNever]
        [ForeignKey("BlockId")]
        public virtual Block? Block { get; set; }

        [ValidateNever]
        [ForeignKey("CommitteeId")]
        public virtual Committee? Committee { get; set; }

        [ValidateNever]
        [ForeignKey("RoleId")]
        public virtual Role Role { get; set; }

        [ValidateNever]
        [ForeignKey("ExamScheduleId")]
        public virtual ExamSchedule ExamSchedule { get; set; }
    }
}

// ============================================================
// 8. شاشة التحضير لطباعة المحاضر الرسمية (تعتمد على المادة والمبنى/البلوك) 🌟
// ============================================================
//[HttpGet]
//public async Task<IActionResult> PreparePrintSheet()
//{
//    var exams = await _context.Exams
//        .Include(e => e.Subject)
//        .OrderByDescending(e => e.ExamDate)
//        .ToListAsync();

//    var blocks = await _context.Blocks
//        .Include(b => b.Hall)
//        .OrderBy(b => b.Hall.HallName)
//        .ThenBy(b => b.BlockName)
//        .Select(b => new {
//            BlockId = b.BlockId,
//            DisplayName = $"{b.Hall.HallName} - {b.BlockName}"
//        })
//        .ToListAsync();

//    ViewBag.Exams = new SelectList(exams, "ExamId", "Subject.SubjectName");
//    ViewBag.Blocks = new SelectList(blocks, "BlockId", "DisplayName");

//    return View();
//}

//// ============================================================
//// 9. توليد بيانات محضر التوزيع الفعلي للمبنى المختار - PrintControlSheet 🌟
//// ============================================================
//[HttpPost]
//public async Task<IActionResult> PrintControlSheet(int examId, int blockId)
//{
//    var exam = await _context.Exams
//        .Include(e => e.Subject)
//        .FirstOrDefaultAsync(e => e.ExamId == examId);

//    var block = await _context.Blocks
//        .Include(b => b.Hall)
//        .FirstOrDefaultAsync(b => b.BlockId == blockId);

//    if (exam == null || block == null) return NotFound();

//    var allAssignments = await _context.CommitteesAssignments
//        .Include(a => a.Person)
//        .Include(a => a.Block)
//        .Include(a => a.Committee).ThenInclude(c => c.Block)
//        .Include(a => a.ExamSchedule)
//        .Where(a => a.ExamSchedule.ExamId == examId &&
//                   (a.BlockId == blockId || (a.Committee != null && a.Committee.BlockId == blockId)))
//        .ToListAsync();

//    if (!allAssignments.Any())
//    {
//        TempData["ErrorMessage"] = $"عفواً، لا توجد تكليفات أو طواقم مراقبة مسجلة لمبنى ({block.BlockName}) في مادة ({exam.Subject.SubjectName}) حتى الآن.";
//        return RedirectToAction(nameof(PreparePrintSheet));
//    }

//    var viewModel = new ExamControlSheetViewModel
//    {
//        SubjectName = exam.Subject?.SubjectName ?? "---",
//        TargetYear = exam.Subject?.AcademicYear.ToString() ?? "---",
//        ExamDate = exam.ExamDate.ToString("yyyy/MM/dd"),
//        ExamDay = exam.ExamDate.ToString("dddd", new System.Globalization.CultureInfo("ar-EG")),
//        ExamTime = $"{DateTime.Today.Add(exam.StartTime):hh:mm tt} - {DateTime.Today.Add(exam.EndTime):hh:mm tt}",
//        HallName = $"{block.Hall?.HallName ?? "---"} - {block.BlockName}",

//        // طاقم قطاع 1
//        MainHead1 = allAssignments.FirstOrDefault(a => a.RoleType == "رئيس صالة أساسي (القطاع الأول)")?.Person?.FullName ?? "................",
//        ReserveHead1 = allAssignments.FirstOrDefault(a => a.RoleType == "رئيس صالة احتياطي")?.Person?.FullName ?? "................",
//        ReserveObserver1 = allAssignments.FirstOrDefault(a => a.RoleType == "مراقب احتياطي للصالة (تحت إدارة رئيس الصالة)")?.Person?.FullName ?? "................",

//        // طاقم قطاع 2
//        MainHead2 = allAssignments.FirstOrDefault(a => a.RoleType == "رئيس صالة أساسي (القطاع الثاني)")?.Person?.FullName ?? "................",
//        ReserveHead2 = "................",
//        ReserveObserver2 = "................",

//        // الطواقم الطبية العامة
//        DoctorName = allAssignments.FirstOrDefault(a => a.RoleType == "دكتور الصالة")?.Person?.FullName ?? "................",
//        NurseName = allAssignments.FirstOrDefault(a => a.RoleType == "ممرض الصالة")?.Person?.FullName ?? "................",

//        Blocks = new List<BlockGroupItem>()
//    };

//    var blockItem = new BlockGroupItem
//    {
//        BlockName = block.BlockName,
//        BlockObserverName = allAssignments.FirstOrDefault(a => a.BlockId == blockId && a.RoleType == "مراقب")?.Person?.FullName ?? "................",
//        CommitteeObservers = new List<ObserverRowItem>()
//    };

//    // جلب الملاحظين الفعليين داخل اللجان التابعة للبلوك المختار
//    var activeObservers = allAssignments
//        .Where(a => a.Committee != null && a.Committee.BlockId == blockId && a.RoleType == "ملاحظ لجنة" && a.Person != null)
//        .OrderBy(a => a.Committee.CommitteeNumber)
//        .Select(a => new ObserverRowItem
//        {
//            ObserverName = a.Person.FullName,
//            CommitteeNumber = "لجنة " + a.Committee.CommitteeNumber.ToString()
//        })
//        .ToList();

//    blockItem.CommitteeObservers.AddRange(activeObservers);

//    // جلب الملاحظين الاحتياطيين الـ 5% المسجلين في هذا المبنى
//    var reserveObservers = allAssignments
//        .Where(a => a.BlockId == blockId && a.RoleType == "ملاحظ احتياطي للكلية (تحت إدارة المراقب)" && a.Person != null)
//        .Select(a => new ObserverRowItem
//        {
//            ObserverName = a.Person.FullName,
//            CommitteeNumber = "ملاحظ احتياطي بالمبنى"
//        })
//        .ToList();

//    blockItem.CommitteeObservers.AddRange(reserveObservers);
//    viewModel.Blocks.Add(blockItem);

//    viewModel.ReserveNotes1.Add("طاقم القطاع الأول ملتزم بالخطة الزمنية لتوزيع الأسئلة.");
//    viewModel.ReserveNotes2.Add("طاقم القطاع الثاني مستعد لأي حالات طارئة أو غياب.");

//    return View(viewModel);
//}