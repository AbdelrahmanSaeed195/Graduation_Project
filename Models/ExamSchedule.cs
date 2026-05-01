using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
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
        [Display(Name = "التاريخ")]
        public DateTime ScheduledDate { get; set; }

        [Required(ErrorMessage = "وقت البداية مطلوب")]
        [DataType(DataType.Time)]
        [Display(Name = "من")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "وقت النهاية مطلوب")]
        [DataType(DataType.Time)]
        [Display(Name = "إلى")]
        public TimeSpan EndTime { get; set; }

        [Required(ErrorMessage = "يجب اختيار الامتحان")]
        [Display(Name = "المادة")]
        public int ExamId { get; set; }

        [ValidateNever]
        [ForeignKey("ExamId")]
        public virtual Exam Exam { get; set; }

        [Required(ErrorMessage = "يجب اختيار اللجنة")]
        [Display(Name = "اللجنة")]
        public int CommitteeId { get; set; }

        [ValidateNever]
        [ForeignKey("CommitteeId")]
        public virtual Committee Committee { get; set; }

        // 🔥 الطلاب المرتبطين بالجلسة
        [ValidateNever]
        public virtual ICollection<Student> Students { get; set; }

        [ValidateNever]
        [Display(Name = "المحاضر والتقارير")]
        public virtual ICollection<Report> Reports { get; set; }
    }
}