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


        [Required(ErrorMessage = "يجب اختيار الامتحان")]
        [Display(Name = "المادة")]
        public int ExamId { get; set; }

        [ValidateNever]
        [ForeignKey("ExamId")]
        public virtual Exam Exam { get; set; }

        [Required(ErrorMessage = "يجب اختيار الصالة")]
        [Display(Name = "الصالة")]
        public int BlockId { get; set; }

        [ValidateNever]
        [ForeignKey("BlockId")]
        [Display(Name = "بيانات الصالة")]
        public virtual Block Block { get; set; }

        [ValidateNever]
        public virtual ICollection<Student> Students { get; set; }

        [ValidateNever]
        [Display(Name = "المحاضر والتقارير")]
        public virtual ICollection<Report> Reports { get; set; }
    }
}