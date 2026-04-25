using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    public class Exam
    {
        [Key]
        [Display(Name = "كود الامتحان")]
        public int ExamId { get; set; }

        [Required(ErrorMessage = "تاريخ الامتحان مطلوب")]
        [DataType(DataType.Date)]
        [Display(Name = "تاريخ الامتحان")]
        public DateTime ExamDate { get; set; }

        [Required(ErrorMessage = "وقت البداية مطلوب")]
        [DataType(DataType.Time)]
        [Display(Name = "وقت البدء")]
        public DateTime StartTime { get; set; }

        [Required(ErrorMessage = "وقت النهاية مطلوب")]
        [DataType(DataType.Time)]
        [Display(Name = "وقت الانتهاء")]
        public DateTime EndTime { get; set; }

        [Required(ErrorMessage = "السنة الدراسية مطلوبة")]
        [Display(Name = "السنة الدراسية المستهدفة")]
        public string TargetAcademicYear { get; set; }

        [Required(ErrorMessage = "يجب اختيار المادة")]
        [Display(Name = "المادة الدراسية")]
        public int SubjectID { get; set; }
        [ValidateNever]
        [ForeignKey("SubjectID")]
        [Display(Name = "بيانات المادة")]
        public virtual Subject Subject { get; set; }
        [ValidateNever]
        [Display(Name = "جدول توزيع اللجان")]
        public virtual ICollection<ExamSchedule> ExamSchedules { get; set; }

        
    }
}