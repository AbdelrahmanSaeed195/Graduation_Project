using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
    [Index(nameof(HallName), IsUnique = true)]

    public class Hall
    {
        [Key]
        [Display(Name = "كود الصالة")]
        public int HallId { get; set; }

        [Required(ErrorMessage = "اسم الصالة/القاعة مطلوب")]
        [StringLength(100)]
        [Display(Name = "اسم الصالة")]
        public string HallName { get; set; }

        [Display(Name = "رقم الدور")]
        public int Floor { get; set; }
        [Required(ErrorMessage = "يرجى تحديد عدد البلوكات المسموح بها")]
        [Range(1, 20, ErrorMessage = "عدد البلوكات يجب أن يكون بين 1 و 20")]
        [Display(Name = "عدد البلوكات المتاحة")]
        public int MaxBlocks { get; set; }

        [Display(Name = "مشرف الصالة")]
        public int? HallSupervisorID { get; set; }
        [ValidateNever]
        [ForeignKey("HallSupervisorID")]
        [Display(Name = "بيانات المشرف")]
        public virtual Person HallSupervisor { get; set; }
        [ValidateNever]
        [Display(Name = "البلوكات التابعة")]
        public virtual ICollection<Block> Blocks { get; set; }

      
    }
}