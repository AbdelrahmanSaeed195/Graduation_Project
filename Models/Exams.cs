using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    public class Exam : IValidatableObject
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
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "وقت النهاية مطلوب")]
        [DataType(DataType.Time)]
        [Display(Name = "وقت الانتهاء")]
        public TimeSpan EndTime { get; set; }       

        [Required(ErrorMessage = "يجب اختيار المادة")]
        [Display(Name = "المادة الدراسية")]
        public int SubjectID { get; set; }

        [ValidateNever]
        [ForeignKey("SubjectID")]
        [Display(Name = "بيانات المادة")]
        public virtual Subject? Subject { get; set; }
        [BindNever]
        [NotMapped]
        [Display(Name = "السنة الدراسية")]
        public AcademicLevel? AcademicYear => Subject?.AcademicYear;

        [ValidateNever]
        [Display(Name = "جدول توزيع اللجان")]
        public virtual ICollection<ExamSchedule> ExamSchedules { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var durationMinutes = (EndTime - StartTime).TotalMinutes;

            if (EndTime <= StartTime)
            {
                yield return new ValidationResult(
                    "وقت الانتهاء يجب أن يكون بعد وقت البدء.",
                    new[] { nameof(EndTime) }
                );
            }
            else if (durationMinutes > 180)
            {
                yield return new ValidationResult(
                    "عفواً، لا يمكن أن تتجاوز مدة الامتحان 3 ساعات (180 دقيقة).",
                    new[] { nameof(EndTime) }
                );
            }

            
        }
    }
}