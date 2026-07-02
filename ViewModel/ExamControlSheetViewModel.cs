using System.Collections.Generic;

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
        public string HallName { get; set; } // سيعرض اسم الجراش متبوعاً بالصف الحالي (مثال: جراش أ - صف 1)

        // --- طاقم إدارة الموقع (دعم ديناميكي مبني على عدد الصفوف والأدوار) ---

        // ✅ جديد: رئيس القطاع المسؤول عن هذا الصف بالتحديد فقط (وليس كل القطاعات مع بعض)
        // صف 1 -> قطاع أول | صف 2 -> قطاع ثاني | صف 3 -> قطاع ثالث ... إلخ
        public string RowMainHead { get; set; }

        // الحقول القديمة (لسه موجودة لو محتاجينها في أي مكان تاني بالنظام)
        public string MainHead1 { get; set; }
        public string MainHead2 { get; set; }
        public string? MainHead3 { get; set; }

        // طاقم الاحتياطي الإداري (يُستخدم لتعويض غيابات رؤساء القطاعات)
        public string ReserveHead1 { get; set; }
        public string ReserveObserver1 { get; set; }

        // الحقول الخاصة بحالة "الدورين" (تُملأ فقط إذا كان الجراش ذو دورين، وتظل null في الدور الواحد)
        public string? ReserveHead2 { get; set; }
        public string? ReserveObserver2 { get; set; }

        // --- الهيكل التنظيمي للصالات واللجان ---
        public List<BlockGroupItem> Blocks { get; set; } = new List<BlockGroupItem>();

        // --- الطاقم الطبي بمقر اللجان ---
        public string DoctorName { get; set; }
        public string NurseName { get; set; }

        // --- طاقم الاحتياطي العام للملاحظين ---
        public List<string> GeneralReserveObservers { get; set; } = new List<string>();
    }

    public class BlockGroupItem
    {
        public string BlockName { get; set; }
        public string BlockObserverName { get; set; } // اسم المراقب المعين على الصالة
        public int? FloorNumber { get; set; } // لتمييز الدور التابع له المقر
        public List<ObserverRowItem> CommitteeObservers { get; set; } = new List<ObserverRowItem>();

        public int TotalStudentsInBlock { get; set; }
    }

    public class ObserverRowItem
    {
        public string ObserverName { get; set; }
        public string CommitteeNumber { get; set; }

        public int BookletsCount { get; set; }
    }
}
