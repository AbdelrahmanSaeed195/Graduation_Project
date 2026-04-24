using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    public class ExamSchedule
    {
        [Key]
        [Display(Name = "كود الجلسة")]
        public int ExamScheduleId { get; set; }

        [Required(ErrorMessage = "تاريخ الجلسة مطلوب")]
        [DataType(DataType.Date)]
        [Display(Name = "التاريخ المحدد")]
        public DateTime ScheduledDate { get; set; }

        [Required(ErrorMessage = "وقت البداية مطلوب")]
        [DataType(DataType.Time)]
        [Display(Name = "وقت البدء")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "وقت النهاية مطلوب")]
        [DataType(DataType.Time)]
        [Display(Name = "وقت الانتهاء")]
        public TimeSpan EndTime { get; set; }

        [Required(ErrorMessage = "يجب اختيار الامتحان")]
        [Display(Name = "الامتحان / المادة")]
        public int ExamId { get; set; }

        [ForeignKey("ExamId")]
        [Display(Name = "بيانات الامتحان")]
        public virtual Exam? Exam { get; set; }

        [Required(ErrorMessage = "يجب اختيار اللجنة")]
        [Display(Name = "اللجنة")]
        public int CommitteeId { get; set; }

        [ForeignKey("CommitteeId")]
        [Display(Name = "بيانات اللجنة")]
        public virtual Committee? Committee { get; set; }

        [Display(Name = "المحاضر والتقارير")]
        public virtual ICollection<Report>? Reports { get; set; }

       
    }
}