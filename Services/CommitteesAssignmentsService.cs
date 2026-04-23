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
            // 1. جلب بيانات الجلسة الحالية (اللجنة)
            var currentSchedule = await _context.ExamSchedules
                .Include(s => s.Committee)
                .FirstOrDefaultAsync(s => s.ExamScheduleId == examScheduleId);

            if (currentSchedule == null) return false;

            var startTime = currentSchedule.StartTime;
            var endTime = currentSchedule.EndTime;
            var currentDate = currentSchedule.ScheduledDate.Date;
            var yesterday = currentDate.AddDays(-1);

            // 2. فحص الأشخاص المشغولين (تجنب التداخل في نفس الوقت)
            var busyPersonIds = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule)
                .Where(a => a.ExamSchedule.ScheduledDate.Date == currentDate &&
                            a.ExamSchedule.StartTime < endTime &&
                            a.ExamSchedule.EndTime > startTime)
                .Select(a => a.PersonID)
                .Distinct()
                .ToListAsync();

            // 3. جلب الموظفين المتاحين مع تضمين أدوارهم (Enum)
            // جلب الموظفين المتاحين والنشطين فقط
            var availableStaff = await _context.Persons
                .Include(p => p.Role)
                .Where(p => !busyPersonIds.Contains(p.PersonId) && p.IsActiveForAssignment)
                .ToListAsync();

            var random = new Random();
            var finalAssignments = new List<CommitteesAssignment>();

            // --- أ- توزيع ملاحظ اللجنة (شخص واحد لكل لجنة فرعية) ---
            var observer = availableStaff
                .FirstOrDefault(p => p.Role?.RoleName == StaffPosition.CommitteeObserver);

            if (observer != null)
            {
                finalAssignments.Add(CreateAssignment(currentSchedule, observer, "ملاحظ لجنة"));
                availableStaff.Remove(observer);
            }

            // --- ب- توزيع رئيس مجموعة البلوكات (شخص واحد للبلوك) ---
            // نتأكد أن البلوك لم يأخذ رئيساً في لجان أخرى بنفس الموعد
            bool blockHasLeader = await _context.CommitteesAssignments
                .AnyAsync(a => a.ExamScheduleId == examScheduleId &&
                               a.RoleID == (int)StaffPosition.BlockGroupLeader);

            if (!blockHasLeader)
            {
                var leader = availableStaff
                    .FirstOrDefault(p => p.Role?.RoleName == StaffPosition.BlockGroupLeader);
                if (leader != null)
                {
                    finalAssignments.Add(CreateAssignment(currentSchedule, leader, "رئيس مجموعة بلوكات"));
                    availableStaff.Remove(leader);
                }
            }

            // --- ج- توزيع رئيس الصالة والمساعد (مع شرط الأيام المتتالية) ---
            bool hallHasManagement = await _context.CommitteesAssignments
                .AnyAsync(a => a.ExamScheduleId == examScheduleId &&
                               a.RoleID == (int)StaffPosition.HallManager);

            if (!hallHasManagement)
            {
                // تحديد من عمل "أمس" كرئيس صالة لإعطائه الأولوية
                var workedYesterdayIds = await _context.CommitteesAssignments
                    .Include(a => a.ExamSchedule)
                    .Where(a => a.ExamSchedule.ScheduledDate.Date == yesterday &&
                                a.RoleID == (int)StaffPosition.HallManager)
                    .Select(a => a.PersonID)
                    .ToListAsync();

                var managers = availableStaff
                    .Where(p => p.Role?.RoleName == StaffPosition.HallManager)
                    .OrderByDescending(m => workedYesterdayIds.Contains(m.PersonId)) // الأولوية لمن عمل أمس
                    .ThenBy(p => random.Next()) // ثم توزيع عشوائي
                    .Take(2) // نأخذ 2 (واحد أساسي وواحد مساعد)
                    .ToList();

                if (managers.Count > 0)
                    finalAssignments.Add(CreateAssignment(currentSchedule, managers[0], "رئيس صالة"));

                if (managers.Count > 1)
                    finalAssignments.Add(CreateAssignment(currentSchedule, managers[1], "مساعد رئيس صالة (احتياطي)"));

                foreach (var m in managers) availableStaff.Remove(m);
            }

            // --- د- توزيع الطاقم الطبي (طبيب وممرض للصالة) ---
            bool hallHasMedical = await _context.CommitteesAssignments
                .AnyAsync(a => a.ExamScheduleId == examScheduleId &&
                               a.RoleID == (int)StaffPosition.Doctor);

            if (!hallHasMedical)
            {
                var doctor = availableStaff.FirstOrDefault(p => p.Role?.RoleName == StaffPosition.Doctor);
                var nurse = availableStaff.FirstOrDefault(p => p.Role?.RoleName == StaffPosition.Nurse);

                if (doctor != null) finalAssignments.Add(CreateAssignment(currentSchedule, doctor, "طبيب الصالة"));
                if (nurse != null) finalAssignments.Add(CreateAssignment(currentSchedule, nurse, "ممرض الصالة"));
            }

            // 4. الحفظ النهائي في قاعدة البيانات
            if (finalAssignments.Any())
            {
                _context.CommitteesAssignments.AddRange(finalAssignments);
                try
                {
                    await _context.SaveChangesAsync();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return false;
        }

        // دالة مساعدة لبناء غرض التكليف (Mapping)
        private CommitteesAssignment CreateAssignment(ExamSchedule schedule, Person person, string typeLabel)
        {
            return new CommitteesAssignment
            {
                CommitteeID = schedule.CommitteeId,
                PersonID = person.PersonId,
                RoleID = person.RoleID,
                ExamScheduleId = schedule.ExamScheduleId,
                AssignmentType = "Auto",
                RoleType = typeLabel,
                isReserve = typeLabel.Contains("احتياطي")
            };
        }
    }
}