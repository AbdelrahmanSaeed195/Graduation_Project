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