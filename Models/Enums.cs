using System.ComponentModel.DataAnnotations;

namespace projectweb.Models
{
    public enum AcademicLevel
    {
        [Display(Name = "المستوى الأول")]
        FirstYear = 1,

        [Display(Name = "المستوى الثاني")]
        SecondYear = 2,

        [Display(Name = "المستوى الثالث")]
        ThirdYear = 3,

        [Display(Name = "المستوى الرابع")]
        FourthYear = 4
    }

    public enum StudentSpecialization
    {
        [Display(Name = "عام / غير متخصص")]
        General = 1,

        [Display(Name = "إدارة")]
        Management = 2,

        [Display(Name = "تدريس")]
        Teaching = 3,

        [Display(Name = "تدريب")]
        Training = 4
    }
    public enum StaffPosition
    {
        [Display(Name = "رئيس لجنة")]
        HallManager = 1,

        [Display(Name = "مراقب")]
        BlockGroupLeader = 2,

        [Display(Name = "ملاحظ")]
        CommitteeObserver = 3,

        [Display(Name = "دكتور")]
        Doctor = 4,

        [Display(Name = "مساعد دكتور")]
        Nurse = 5,

    }
    public enum ReportStatus
    {
        [Display(Name = "تقرير طبيعي/دوري")]
        Normal = 1,

        [Display(Name = "حالة غش")]
        Cheating = 2,

        [Display(Name = "شغب وضوضاء")]
        Disturbance = 3,

        [Display(Name = "حالة طوارئ")]
        Emergency = 4,

        [Display(Name = "مشكلة في الغياب")]
        AbsenceIssue = 5
    }
    public enum JobTitle
    {
        [Display(Name = "أستاذ متفرغ")]
        ProfessorEmeritus = 1,
        [Display(Name = "أستاذ مساعد")]
        AssistantProfessor = 2,
        [Display(Name = "أستاذ")]
        Professor = 3,
        [Display(Name = "مدرس")]
        StaffObserver = 4,
        [Display(Name = "مدرس مساعد ")]
        AssistantStaff = 5,
        [Display(Name = "معيد")]
        Assistant = 6,
        [Display(Name = "موظف")]
        Employee = 7,
        [Display(Name = "دكتور")]
        Doctor = 8,
        [Display(Name = "مساعد دكتور")]
        Nurse = 9
    }
    public enum LocationType
    {
        [Display(Name = "جراش")]
        Hall = 1,

        [Display(Name = "صف")]
        Row = 2,

       [Display(Name = "صالة")]
        Block = 3,

        [Display(Name = "لجنة")]
        Committee = 4
    }
}