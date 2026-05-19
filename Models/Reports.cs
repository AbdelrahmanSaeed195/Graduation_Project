using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
   
    public class Report
    {
        [Key]
        [Display(Name = "كود المحضر")]
        public int ReportId { get; set; }

        [Required]
        [Display(Name = "تاريخ تحرير المحضر")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "يجب تحديد نوع وحالة المحضر")]
        [Display(Name = "نوع/حالة المحضر")]
        public ReportStatus Status { get; set; } = ReportStatus.Normal;

        [Display(Name = "ملاحظات وتفاصيل")]
        [DataType(DataType.MultilineText)]
        public string? Notes { get; set; }

        [Required(ErrorMessage = "يجب ربط المحضر بجلسة امتحان")]
        [Display(Name = "جلسة الامتحان")]
        public int ScheduleId { get; set; }
        [ValidateNever]
        [ForeignKey("ScheduleId")]
        [Display(Name = "بيانات جلسة الامتحان")]
        public virtual ExamSchedule ExamSchedule { get; set; }
        [ValidateNever]
        [Display(Name = "اللجنة")]
        public int? CommitteeId { get; set; }

        [ValidateNever]
        [ForeignKey("CommitteeId")]
        [Display(Name = "بيانات اللجنة")]
        public virtual Committee Committee { get; set; }
        [ValidateNever]
        [Display(Name = "الموقعون على المحضر")]
        public virtual ICollection<ReportPerson> ReportPersons { get; set; }

       
    }
}