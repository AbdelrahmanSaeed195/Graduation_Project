using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    public class Block
    {
        [Key]
        [Display(Name = "كود البلوك")]
        public int BlockID { get; set; }

        [Required(ErrorMessage = "اسم البلوك أو الجزء مطلوب")]
        [Display(Name = "اسم البلوك (الجزء)")]
        public string BlockName { get; set; }

        [Required(ErrorMessage = "يرجى تحديد عدد اللجان المسموح بها في هذا البلوك")]
        [Range(1, 50, ErrorMessage = "عدد اللجان يجب أن يكون بين 1 و 50")]
        [Display(Name = "عدد اللجان المتاحة")]
        public int MaxCommittees { get; set; }

        [Required(ErrorMessage = "يجب تحديد الصالة التابع لها")]
        [Display(Name = "الصالة / القاعة")]
        public int HallId { get; set; }

        [ValidateNever]
        [ForeignKey("HallId")]
        [Display(Name = "بيانات الصالة")]
        public virtual Hall Hall { get; set; }
        [ValidateNever]
        [Display(Name = "اللجان التابعة")]
        public virtual ICollection<Committee> Committees { get; set; }

        
    }
}