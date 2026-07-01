using System.Threading.Tasks;

namespace projectweb.Services
{
    public interface ICommitteesAssignmentsService
    {

        Task<bool> RunAssignmentAsync(int examScheduleId);

        Task<string> CheckTimeConflictAsync(int examScheduleId);

        // ========================================================================
        // ✅ جديد: فحص تضارب صلة القرابة - يمنع تعيين الشخص في أي مكان بنفس فرقة قريبه بالكامل
        // ========================================================================
        Task<string> CheckRelativeConflictAsync(int personId, int examScheduleId);
    }
}
