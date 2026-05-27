namespace projectweb.Models.ViewModels
{
    public class ExamControlSheetViewModel
    {
        public string AcademicYear { get; set; } = "2025 / 2026";
        public string Semester { get; set; } = "الفصل الدراسي الثاني";
        public string SubjectName { get; set; }
        public string TargetYear { get; set; }
        public string ExamTime { get; set; }
        public string ExamDate { get; set; }
        public string ExamDay { get; set; }
        public string HallName { get; set; } // اسم الصالة الرئيسية (مثلاً: ل)

        // --- طاقم رئيس الصالة ---
        public string MainHead1 { get; set; }
        public string ReserveHead1 { get; set; }
        public string ReserveObserver1 { get; set; }
        public List<string> ReserveNotes1 { get; set; } = new List<string>();

        public string MainHead2 { get; set; }
        public string ReserveHead2 { get; set; }
        public string ReserveObserver2 { get; set; }
        public List<string> ReserveNotes2 { get; set; } = new List<string>();

        // --- الهيكل التنظيمي المجمع لكل البلوكات التابعة للصالة ---
        public List<BlockGroupItem> Blocks { get; set; } = new List<BlockGroupItem>();

        // --- الطاقم الطبي ---
        public string DoctorName { get; set; } 
        public string NurseName { get; set; } 

        // --- طاقم الاحتياطي العام للجنة (الأسفل) ---
        public List<string> GeneralReserveObservers { get; set; } = new List<string>();
    }

    public class BlockGroupItem
    {
        public string BlockName { get; set; } // اسم البلوك 
        public string BlockObserverName { get; set; } // المراقب المسؤول عن البلوك
        public List<ObserverRowItem> CommitteeObservers { get; set; } = new List<ObserverRowItem>();
    }

    public class ObserverRowItem
    {
        public string ObserverName { get; set; }
        public string CommitteeNumber { get; set; } // رقم اللجنة
        public int BookletsCount { get; set; }   // عدد كراسات
    }
}