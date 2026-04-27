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
        public string? AssignmentType { get; set; } // Auto / Manual

        [Display(Name = "مسمى الدور")]
        public string? RoleType { get; set; }

        // Navigation Properties
        [ValidateNever][ForeignKey("PersonID")] public virtual Person Person { get; set; }
        [ValidateNever][ForeignKey("HallId")] public virtual Hall? Hall { get; set; }
        [ValidateNever][ForeignKey("BlockId")] public virtual Block? Block { get; set; }
        [ValidateNever][ForeignKey("CommitteeID")] public virtual Committee? Committee { get; set; }
        [ValidateNever][ForeignKey("RoleID")] public virtual Role Role { get; set; }
        [ValidateNever][ForeignKey("ExamScheduleId")] public virtual ExamSchedule ExamSchedule { get; set; }
    }
}