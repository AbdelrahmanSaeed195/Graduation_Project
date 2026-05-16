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
        public string HallName { get; set; }

        // --- طاقم رئيس الصالة الأول ---
        public string MainHead1 { get; set; }
        public string ReserveHead1 { get; set; } // رئيس احتياطي لـ 1
        public string ReserveObserver1 { get; set; }
        public List<string> ReserveNotes1 { get; set; } = new List<string>();

        // --- طاقم رئيس الصالة الثاني ---
        public string MainHead2 { get; set; }
        public string ReserveHead2 { get; set; } // رئيس احتياطي لـ 2
        public string ReserveObserver2 { get; set; }
        public List<string> ReserveNotes2 { get; set; } = new List<string>();

        // --- الهيكل التنظيمي للبلوكات واللجان ---
        public List<BlockGroupItem> Blocks { get; set; } = new List<BlockGroupItem>();

        public string DoctorName { get; set; } = "................";
        public string NurseName { get; set; } = "................";
    }

    public class BlockGroupItem
    {
        public string BlockName { get; set; }
        public string BlockObserverName { get; set; } // المراقب المسؤول عن البلوك
        public List<ObserverRowItem> CommitteeObservers { get; set; } = new List<ObserverRowItem>();
    }

    public class ObserverRowItem
    {
        public string ObserverName { get; set; }
        public string CommitteeNumber { get; set; }
    }
}