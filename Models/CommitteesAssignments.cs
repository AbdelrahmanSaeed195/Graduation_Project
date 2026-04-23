using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    public class CommitteesAssignment
    {
        [Key]
        [Display(Name = "كود التكليف")]
        public int AssignmentID { get; set; }

        [Required(ErrorMessage = "يجب اختيار الموظف")]
        [Display(Name = "الموظف / المراقب")]
        public int PersonID { get; set; }

        [Required(ErrorMessage = "يجب اختيار اللجنة")]
        [Display(Name = "اللجنة")]
        public int CommitteeID { get; set; }

        [Required(ErrorMessage = "يجب تحديد الدور")]
        [Display(Name = "الدور الوظيفي")]
        public int RoleID { get; set; }

        [Display(Name = "نوع التكليف")]
        public string AssignmentType { get; set; }

        [Display(Name = "هل هو احتياطي؟")]
        public bool isReserve { get; set; } = false;

        [Display(Name = "مسمى الدور")]
        public string RoleType { get; set; }
    
        [ForeignKey("PersonID")]
        [Display(Name = "بيانات الموظف")]
        public virtual Person Person { get; set; }

        [ForeignKey("CommitteeID")]
        [Display(Name = "بيانات اللجنة")]
        public virtual Committee Committee { get; set; }

        [Required(ErrorMessage = "يجب اختيار موعد الجلسة")]
        [Display(Name = "موعد الجلسة الامتحانية")]
        public int ExamScheduleId { get; set; }

        [ForeignKey("ExamScheduleId")]
        [Display(Name = "بيانات الجلسة")]
        public virtual ExamSchedule ExamSchedule { get; set; }

        [ForeignKey("RoleID")]
        [Display(Name = "بيانات الدور")]
        public virtual Role Role { get; set; }
    }
}