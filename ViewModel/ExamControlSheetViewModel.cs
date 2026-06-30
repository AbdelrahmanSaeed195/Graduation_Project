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

        // --- طاقم إدارة الموقع (دعم ديناميكي) ---

        // الحقول الأساسية (تعمل في كل الحالات)
        public string MainHead1 { get; set; }
        public string MainHead2 { get; set; }
        public string? MainHead3 { get; set; } // يستخدم في الدورين (تظل فارغة في حالة الدور الواحد

        // احتياطي عام (يتم استخدامه في حالة الدورين أو دور  )
        public string ReserveHead1 { get; set; }
        public string ReserveObserver1 { get; set; }

        // الحقول الخاصة بحالة "الدورين" (تظل فارغة في حالة الدور الواحد)
        public string? ReserveHead2 { get; set; }
        public string? ReserveObserver2 { get; set; }

        // --- الهيكل التنظيمي للمقرات ---
        public List<BlockGroupItem> Blocks { get; set; } = new List<BlockGroupItem>();

        // --- الطاقم الطبي ---
        public string DoctorName { get; set; }
        public string NurseName { get; set; }

        // --- طاقم الاحتياطي العام ---
        public List<string> GeneralReserveObservers { get; set; } = new List<string>();
    }

    public class BlockGroupItem
    {
        public string BlockName { get; set; }
        public string BlockObserverName { get; set; }
        public int? FloorNumber { get; set; } // لتمييز الدور في حالة الدورين
        public List<ObserverRowItem> CommitteeObservers { get; set; } = new List<ObserverRowItem>();
    }

    public class ObserverRowItem
    {
        public string ObserverName { get; set; }
        public string CommitteeNumber { get; set; }
        public int BookletsCount { get; set; }
    }
}