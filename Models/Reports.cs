using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    public enum ReportStatus
    {
        [Display(Name = "تقرير طبيعي/دوري")]
        Normal = 1,

        [Display(Name = "حالة غش")]
        Cheating = 2,

        [Display(Name = "شغب وضوضاء")]
        Disturbance = 3,

        [Display(Name = "حالة طوارئ")]
        Emergency = 4,

        [Display(Name = "مشكلة في الغياب")]
        AbsenceIssue = 5
    }

    public class Report
    {
        [Key]
        [Display(Name = "كود المحضر")]
        public int ReportID { get; set; }

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
        public int ScheduleID { get; set; }
        [ValidateNever]
        [ForeignKey("ScheduleID")]
        [Display(Name = "بيانات جلسة الامتحان")]
        public virtual ExamSchedule ExamSchedule { get; set; }
        [ValidateNever]
        [Display(Name = "الموقعون على المحضر")]
        public virtual ICollection<ReportPerson> ReportPersons { get; set; }

       
    }
}