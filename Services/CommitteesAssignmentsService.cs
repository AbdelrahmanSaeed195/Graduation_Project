using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace projectweb.Services
{
    public class _CommitteesAssignmentsService : ICommitteesAssignmentsService
    {
        private readonly ApplicationDbContext _context;

        public _CommitteesAssignmentsService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> RunAssignmentAsync(int examScheduleId)
        {
            // 1. جلب بيانات الجلسة مع الهيكل المكاني كامل (صالة -> بلوك -> لجنة)
            var currentSchedule = await _context.ExamSchedules
                .Include(s => s.Committee)
                    .ThenInclude(c => c.Block)
                        .ThenInclude(b => b.Hall)
                .FirstOrDefaultAsync(s => s.ExamScheduleId == examScheduleId);

            if (currentSchedule == null) return false;

            var startTime = currentSchedule.StartTime;
            var endTime = currentSchedule.EndTime;
            var currentDate = currentSchedule.ScheduledDate.Date;
            var yesterday = currentDate.AddDays(-1);
            var hallId = currentSchedule.Committee.Block.HallId;

            // 2. تحديد الموظفين المشغولين في نفس الوقت لتجنب التضارب
            var busyPersonIds = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule)
                .Where(a => a.ExamSchedule.ScheduledDate.Date == currentDate &&
                            a.ExamSchedule.StartTime < endTime &&
                            a.ExamSchedule.EndTime > startTime)
                .Select(a => a.PersonID)
                .Distinct()
                .ToListAsync();

            // 3. جلب الموظفين المتاحين والنشطين
            var availableStaff = await _context.Persons
                .Include(p => p.Role)
                .Where(p => !busyPersonIds.Contains(p.PersonId) && p.IsActiveForAssignment)
                .ToListAsync();

            var finalAssignments = new List<CommitteesAssignment>();
            var random = new Random();

            // --- أ- توزيع رؤساء الصالة (الأولوية لمن عمل أمس) ---
            var workedYesterdayIds = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule)
                .Where(a => a.ExamSchedule.ScheduledDate.Date == yesterday &&
                            a.RoleID == (int)StaffPosition.HallManager &&
                            a.HallId == hallId)
                .Select(a => a.PersonID)
                .ToListAsync();

            var managers = availableStaff
                .Where(p => p.Role?.RoleName == StaffPosition.HallManager)
                .OrderByDescending(m => workedYesterdayIds.Contains(m.PersonId)) // الأولوية للي كان موجود امبارح
                .ThenBy(p => random.Next())
                .Take(2)
                .ToList();

            foreach (var m in managers)
            {
                finalAssignments.Add(new CommitteesAssignment
                {
                    PersonID = m.PersonId,
                    ExamScheduleId = examScheduleId,
                    RoleID = m.RoleID,
                    HallId = hallId, // التكليف على مستوى الصالة
                    AssignmentType = "Auto",
                    RoleType = "رئيس صالة"
                });
                availableStaff.Remove(m);
            }

            // --- ب- توزيع الطاقم الطبي (طبيب وممرض واحد لكل صالة) ---
            var doctor = availableStaff.FirstOrDefault(p => p.Role?.RoleName == StaffPosition.Doctor);
            if (doctor != null)
            {
                finalAssignments.Add(new CommitteesAssignment
                {
                    PersonID = doctor.PersonId,
                    ExamScheduleId = examScheduleId,
                    RoleID = doctor.RoleID,
                    HallId = hallId,
                    AssignmentType = "Auto",
                    RoleType = "طبيب الصالة"
                });
                availableStaff.Remove(doctor);
            }

            var nurse = availableStaff.FirstOrDefault(p => p.Role?.RoleName == StaffPosition.Nurse);
            if (nurse != null)
            {
                finalAssignments.Add(new CommitteesAssignment
                {
                    PersonID = nurse.PersonId,
                    ExamScheduleId = examScheduleId,
                    RoleID = nurse.RoleID,
                    HallId = hallId,
                    AssignmentType = "Auto",
                    RoleType = "ممرض الصالة"
                });
                availableStaff.Remove(nurse);
            }

            // --- ج- توزيع مراقب البلوك (رئيس مجموعة لجان) ---
            var blockId = currentSchedule.Committee.BlockID;
            var leader = availableStaff
                .OrderBy(p => random.Next())
                .FirstOrDefault(p => p.Role?.RoleName == StaffPosition.BlockGroupLeader);

            if (leader != null)
            {
                finalAssignments.Add(new CommitteesAssignment
                {
                    PersonID = leader.PersonId,
                    ExamScheduleId = examScheduleId,
                    RoleID = leader.RoleID,
                    BlockId = blockId, // التكليف على مستوى البلوك
                    AssignmentType = "Auto",
                    RoleType = "مراقب بلوك"
                });
                availableStaff.Remove(leader);
            }

            // --- د- توزيع ملاحظ اللجنة ---
            var observer = availableStaff
                .OrderBy(p => random.Next())
                .FirstOrDefault(p => p.Role?.RoleName == StaffPosition.CommitteeObserver);

            if (observer != null)
            {
                finalAssignments.Add(new CommitteesAssignment
                {
                    PersonID = observer.PersonId,
                    ExamScheduleId = examScheduleId,
                    RoleID = observer.RoleID,
                    CommitteeID = currentSchedule.CommitteeId, // التكليف على مستوى اللجنة
                    AssignmentType = "Auto",
                    RoleType = "ملاحظ لجنة"
                });
            }

            // 4. الحفظ النهائي
            if (finalAssignments.Any())
            {
                _context.CommitteesAssignments.AddRange(finalAssignments);
                try
                {
                    await _context.SaveChangesAsync();
                    return true;
                }
                catch { return false; }
            }

            return false;
        }
    }
}