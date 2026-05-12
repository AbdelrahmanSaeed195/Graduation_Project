using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace projectweb.Models
{
    public class CommitteesAssignment
    {
        [Key]
        [Display(Name = "كود التكليف")]
        public int AssignmentID { get; set; }

        [Required(ErrorMessage = "يجب اختيار الموظف")]
        [Display(Name = "الموظف")]
        public int PersonID { get; set; }

        [Display(Name = "الصالة")]
        public int? HallId { get; set; }

        [Display(Name = "البلوك")]
        public int? BlockId { get; set; }

        [Display(Name = "اللجنة")]
        public int? CommitteeID { get; set; }

        [Required(ErrorMessage = "يجب تحديد الدور")]
        [Display(Name = "الدور الوظيفي")]
        public int RoleID { get; set; }

        [Required(ErrorMessage = "يجب اختيار الجلسة")]
        [Display(Name = "جلسة الامتحان")]
        public int ExamScheduleId { get; set; }

        [Display(Name = "نوع التوزيع")]
        public string? AssignmentType { get; set; } // تلقائي او يدوي

        [Display(Name = "مسمى الدور")]
        public string? RoleType { get; set; }

        [ValidateNever][ForeignKey("PersonID")] public virtual Person Person { get; set; }
        [ValidateNever][ForeignKey("HallId")] public virtual Hall? Hall { get; set; }
        [ValidateNever][ForeignKey("BlockId")] public virtual Block? Block { get; set; }
        [ValidateNever][ForeignKey("CommitteeID")] public virtual Committee? Committee { get; set; }
        [ValidateNever][ForeignKey("RoleID")] public virtual Role Role { get; set; }
        [ValidateNever][ForeignKey("ExamScheduleId")] public virtual ExamSchedule ExamSchedule { get; set; }
    }
}


// =====================================
// PRINT CONTROL SHEET (استمارة اللجنة)
// =====================================
//[Authorize(Roles = "Admin")]
//public async Task<IActionResult> PrintControlSheet(int id, int headId)
//{
//    var exam = await _context.Exams.Include(e => e.Subject).FirstOrDefaultAsync(e => e.ExamId == id);
//    if (exam == null) return NotFound();

//    var allAssignments = await _context.CommitteesAssignments
//        .Include(a => a.Person)
//        .Where(a => a.ExamSchedule.ExamId == id)
//        .ToListAsync();

//    var targetHead = allAssignments.FirstOrDefault(a => a.PersonID == headId && a.RoleType.Contains("رئيس صالة أساسي"));
//    if (targetHead == null) return Content("هذا الموظف ليس رئيس صالة أساسي في هذا الامتحان.");

//    string headNumber = targetHead.RoleType.Contains("أول") ? "1" : "2";

//    var viewModel = new ExamControlSheetViewModel
//    {
//        SubjectName = exam.Subject?.SubjectName,
//        ExamDate = exam.ExamDate.ToString("yyyy/MM/dd"),
//        MainHead = targetHead.Person.FullName,

//        ReserveObserver = allAssignments.FirstOrDefault(a => a.RoleType == $"مراقب احتياطي - تبع رئيس {headNumber}")?.Person?.FullName ?? "................",
//        ReserveObserversList = allAssignments.Where(a => a.RoleType == $"ملاحظ احتياطي - تبع رئيس {headNumber}").Select(a => a.Person.FullName).ToList(),

//        DoctorName = allAssignments.FirstOrDefault(a => a.RoleType.Contains("دكتور"))?.Person?.FullName,
//        ObserverRows = allAssignments.Where(a => a.RoleType == "ملاحظ لجنة").Select(a => new ObserverRowItem { ObserverName = a.Person.FullName }).ToList()
//    };

//    return View(viewModel);
//}