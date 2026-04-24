using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace projectweb.Models
{
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

        [Display(Name = "مشرف الصالة")]
        public int? HallSupervisorID { get; set; }

        [ForeignKey("HallSupervisorID")]
        [Display(Name = "بيانات المشرف")]
        public virtual Person? HallSupervisor { get; set; }

        [Display(Name = "البلوكات التابعة")]
        public virtual ICollection<Block>? Blocks { get; set; }

      
    }
}