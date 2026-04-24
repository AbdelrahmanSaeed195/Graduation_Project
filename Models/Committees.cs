using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    public class Committee
    {
        [Key]
        [Display(Name = "كود اللجنة")]
        public int CommitteeID { get; set; }

        [Required(ErrorMessage = "رقم اللجنة مطلوب")]
        [Display(Name = "رقم اللجنة")]
        public int CommitteeNumber { get; set; }

        [Display(Name = "عدد المراقبين المطلوب")]
        public int RequiredObservers { get; set; }

        [Display(Name = "عدد المعاونين المطلوب")]
        public int RequiredMentors { get; set; }

        [Display(Name = "عدد رؤساء اللجان")]
        public int RequiredHeads { get; set; }

        [Required(ErrorMessage = "سعة اللجنة من الطلاب مطلوبة")]
        [Display(Name = "سعة الطلاب")]
        public int NumberOfStudent { get; set; }

        [Display(Name = "حالة اللجنة")]
        public string StatusOfCommittee { get; set; }

        [Required(ErrorMessage = "يجب تحديد البلوك التابع له")]
        [Display(Name = "البلوك (الجزء)")]
        public int BlockID { get; set; }
        [ValidateNever]
        [ForeignKey("BlockID")]
        [Display(Name = "بيانات البلوك")]
        public virtual Block Block { get; set; }
        [ValidateNever]
        [Display(Name = "قائمة الطلاب")]
        public virtual ICollection<Student> Students { get; set; }
        [ValidateNever]
        [Display(Name = "توزيعات المراقبين")]
        public virtual ICollection<CommitteesAssignment> CommitteesAssignments { get; set; }
        [ValidateNever]
        [Display(Name = "جدول الامتحانات")]
        public virtual ICollection<ExamSchedule> ExamSchedules { get; set; }

       
    }
}