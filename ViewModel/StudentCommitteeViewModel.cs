using System.ComponentModel.DataAnnotations;
using projectweb.Models;

namespace projectweb.ViewModel
{
    public class StudentCommitteeViewModel
    {
        public int StudentId { get; set; }

        [Display(Name = "اسم الطالب")]
        public string FullName { get; set; }

        [Display(Name = "السنة الدراسية")]
        public AcademicLevel AcademicYear { get; set; }

        [Display(Name = "رقم الجلوس")]
        public int SeatNumber { get; set; }

        // ========================================================
        // Navigation properties 
        // ========================================================
        [Display(Name = "كود مكان الامتحان (اللجنة)")]
        public int LocationId { get; set; }

        [Display(Name = "اسم / رقم اللجنة")]
        public string LocationName { get; set; } 

        [Display(Name = "الاستيعاب الأقصى للجنة")]
        public int? StudentCapacity { get; set; }
        // ========================================================
    }
}