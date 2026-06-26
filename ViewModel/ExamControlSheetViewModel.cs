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
        public string HallName { get; set; } // اسم الموقع الرئيسي الموحد (الموقع الأب الأعلى)

        // --- طاقم إدارة الموقع ---
        public string MainHead1 { get; set; }
        public string ReserveHead1 { get; set; }
        public string ReserveObserver1 { get; set; }
        public List<string> ReserveNotes1 { get; set; } = new List<string>();

        public string MainHead2 { get; set; }
        public string ReserveHead2 { get; set; }
        public string ReserveObserver2 { get; set; }
        public List<string> ReserveNotes2 { get; set; } = new List<string>();

        // --- الهيكل التنظيمي المجمع للمقرات التابعة للموقع ---
        public List<BlockGroupItem> Blocks { get; set; } = new List<BlockGroupItem>();

        // --- الطاقم الطبي ---
        public string DoctorName { get; set; }
        public string NurseName { get; set; }

        // --- طاقم الاحتياطي العام للجنة (الأسفل) ---
        public List<string> GeneralReserveObservers { get; set; } = new List<string>();
    }

    public class BlockGroupItem
    {
        public string BlockName { get; set; } // اسم الصالة الموحد 
        public string BlockObserverName { get; set; } // المراقب المسؤول
        public List<ObserverRowItem> CommitteeObservers { get; set; } = new List<ObserverRowItem>();
    }

    public class ObserverRowItem
    {
        public string ObserverName { get; set; }
        public string CommitteeNumber { get; set; } // اسم / رقم اللجنة الامتحانية الموحدة
        public int BookletsCount { get; set; }   // عدد الكراسات الفعلي بناءً على فرقة المادة
    }
}