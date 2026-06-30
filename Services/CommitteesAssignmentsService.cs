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

        // ========================================================================
        // 1. CheckTimeConflictAsync: فحص تعارضات الوقت في المقر الموحد
        // ========================================================================
        public async Task<string> CheckTimeConflictAsync(int examScheduleId)
        {
            var current = await _context.ExamSchedules
                .Include(s => s.Exam).ThenInclude(e => e.Subject)
                .Include(s => s.ExamLocation)
                .FirstOrDefaultAsync(s => s.ExamScheduleId == examScheduleId);

            if (current == null || current.Exam == null || current.ExamLocation == null)
                return "الجلسة المطلوبة غير موجودة.";

            var currentLocId = current.LocationId;
            var parentLocId = current.ExamLocation.ParentLocationId;
            var examDate = current.Exam.ExamDate.Date;
            var startTime = current.Exam.StartTime;
            var endTime = current.Exam.EndTime;

            var conflict = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam).ThenInclude(e => e.Subject)
                .Include(a => a.ExamLocation)
                .Where(a => a.ExamSchedule.ExamId != current.ExamId &&
                            (a.LocationId == currentLocId ||
                             a.LocationId == parentLocId ||
                             a.ExamLocation.ParentLocationId == currentLocId ||
                             a.ExamLocation.ParentLocation.ParentLocationId == parentLocId) &&
                            a.ExamSchedule.Exam.ExamDate.Date == examDate &&
                            ((startTime >= a.ExamSchedule.Exam.StartTime && startTime < a.ExamSchedule.Exam.EndTime) ||
                             (endTime > a.ExamSchedule.Exam.StartTime && endTime <= a.ExamSchedule.Exam.EndTime)))
                .Select(a => a.ExamSchedule.Exam.Subject.SubjectName)
                .Distinct()
                .FirstOrDefaultAsync();

            if (conflict != null)
            {
                return $"عفواً، لا يمكن إجراء التوزيع. هذا المقر مشغول حالياً بامتحان مادة ({conflict}) في نفس الفترة الزمنية.";
            }

            return null;
        }

        // ========================================================================
        // 2. RunAssignmentAsync: تشغيل ماكينة التوزيع التلقائي وهندسة المقاعد والأقارب
        // ========================================================================
        public async Task<bool> RunAssignmentAsync(int examScheduleId)
        {
            var currentSchedule = await _context.ExamSchedules
                .Include(s => s.Exam)
                .Include(s => s.ExamLocation)
                .FirstOrDefaultAsync(s => s.ExamScheduleId == examScheduleId);

            if (currentSchedule == null || currentSchedule.Exam == null || currentSchedule.ExamLocation == null)
                return false;

            var currentLocId = currentSchedule.LocationId;
            var examId = currentSchedule.ExamId;
            var examDate = currentSchedule.Exam.ExamDate.Date;

            // 1. تنظيف التوزيع التلقائي القديم
            var oldAutoAssignments = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule)
                .Include(a => a.ExamLocation)
                .Where(a => a.ExamSchedule.ExamId == examId &&
                            (a.LocationId == currentLocId || a.ExamLocation.ParentLocationId == currentLocId || a.ExamLocation.ParentLocation.ParentLocationId == currentLocId) &&
                            a.AssignmentType == "Auto")
                .ToListAsync();

            if (oldAutoAssignments.Any())
            {
                _context.CommitteesAssignments.RemoveRange(oldAutoAssignments);
                await _context.SaveChangesAsync();
            }

            var scheduledSchedules = await _context.ExamSchedules
                .Include(es => es.ExamLocation)
                .Where(es => es.ExamId == examId && (es.LocationId == currentLocId || es.ExamLocation.ParentLocationId == currentLocId))
                .ToListAsync();

            if (!scheduledSchedules.Any()) return false;

            var activeBlocks = scheduledSchedules
                .Select(es => es.ExamLocation.Type == LocationType.Block ? es.ExamLocation : es.ExamLocation.ParentLocation)
                .Where(l => l != null && l.Type == LocationType.Block)
                .GroupBy(l => l.LocationId)
                .Select(g => g.First())
                .OrderBy(l => l.LocationName)
                .ToList();

            if (!activeBlocks.Any()) return false;

            var activeBlockIds = activeBlocks.Select(b => b.LocationId).ToList();
            var activeCommitteeIds = await _context.ExamLocations
                .Where(l => l.ParentLocationId != null && activeBlockIds.Contains(l.ParentLocationId.Value) && l.Type == LocationType.Committee)
                .Select(l => l.LocationId)
                .ToListAsync();

            var studentIdsInHall = await _context.Students
                .Where(s => (s.LocationId != null && activeCommitteeIds.Contains(s.LocationId.Value)) || s.ExamScheduleId == examScheduleId)
                .Select(s => s.StudentId)
                .ToListAsync();

            var excludedPersonIdsDueToRelatives = await _context.Relatives
                .Where(r => studentIdsInHall.Contains(r.StudentId))
                .Select(r => r.PersonId)
                .Distinct()
                .ToListAsync();

            var busyPersonIds = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule)
                .Where(a => a.ExamSchedule.ExamId == examId)
                .Select(a => a.PersonId).ToListAsync();

            var allAvailableStaff = await _context.Persons
                .Include(p => p.Role)
                .Where(p => !busyPersonIds.Contains(p.PersonId)
                            && !excludedPersonIdsDueToRelatives.Contains(p.PersonId)
                            && p.IsActiveForAssignment)
                .ToListAsync();

            var staffWhoWorkedInThisHallToday = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .Include(a => a.ExamLocation)
                .Where(a => a.ExamSchedule.Exam.ExamDate.Date == examDate &&
                            (a.LocationId == currentLocId || a.ExamLocation.ParentLocationId == currentLocId || a.ExamLocation.ParentLocation.ParentLocationId == currentLocId))
                .Select(a => a.PersonId)
                .Distinct()
                .ToListAsync();

            var yesterday = examDate.AddDays(-1).Date;
            var yesterdayAssignments = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .Where(a => a.ExamSchedule.Exam.ExamDate.Date == yesterday && a.LocationId != null)
                .Select(a => new { a.PersonId, LocationId = a.LocationId.Value })
                .ToListAsync();

            var finalAssignments = new List<CommitteesAssignment>();
            var random = new Random();

            var currentDayHallPriorityStaff = new List<Person>();
            var historyPriorityStaff = new List<Person>();
            var normalStaff = new List<Person>();
            var lowPriorityStaff = new List<Person>();

            foreach (var staff in allAvailableStaff)
            {
                if (staffWhoWorkedInThisHallToday.Contains(staff.PersonId))
                {
                    currentDayHallPriorityStaff.Add(staff);
                }
                else
                {
                    int weight = await GetStaffPriorityWeightAsync(staff.PersonId, examDate);
                    if (weight == 2) historyPriorityStaff.Add(staff);
                    else if (weight == 1) normalStaff.Add(staff);
                    else lowPriorityStaff.Add(staff);
                }
            }

            var poolManagers = new List<Person>();
            var managerRoles = new HashSet<JobTitle> { JobTitle.Professor, JobTitle.AssistantProfessor, JobTitle.ProfessorEmeritus };
            poolManagers.AddRange(currentDayHallPriorityStaff.Where(p => managerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));
            poolManagers.AddRange(historyPriorityStaff.Where(p => managerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));
            poolManagers.AddRange(normalStaff.Where(p => managerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));
            poolManagers.AddRange(lowPriorityStaff.Where(p => managerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));

            var poolLeaders = new List<Person>();
            poolLeaders.AddRange(currentDayHallPriorityStaff.Where(p => p.JobRole == JobTitle.StaffObserver).OrderBy(p => random.Next()));
            poolLeaders.AddRange(historyPriorityStaff.Where(p => p.JobRole == JobTitle.StaffObserver).OrderBy(p => random.Next()));
            poolLeaders.AddRange(normalStaff.Where(p => p.JobRole == JobTitle.StaffObserver).OrderBy(p => random.Next()));
            poolLeaders.AddRange(lowPriorityStaff.Where(p => p.JobRole == JobTitle.StaffObserver).OrderBy(p => random.Next()));

            var poolObservers = new List<Person>();
            var observerRoles = new HashSet<JobTitle> { JobTitle.Assistant, JobTitle.AssistantStaff, JobTitle.Employee };
            poolObservers.AddRange(currentDayHallPriorityStaff.Where(p => observerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));
            poolObservers.AddRange(historyPriorityStaff.Where(p => observerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));
            poolObservers.AddRange(normalStaff.Where(p => observerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));
            poolObservers.AddRange(lowPriorityStaff.Where(p => observerRoles.Contains(p.JobRole)).OrderBy(p => random.Next()));

            var poolDoctors = allAvailableStaff.Where(p => p.JobRole == JobTitle.Doctor).OrderBy(p => random.Next()).ToList();
            var poolNurses = allAvailableStaff.Where(p => p.JobRole == JobTitle.Nurse).OrderBy(p => random.Next()).ToList();

            int mainHallId = currentSchedule.ExamLocation.Type == LocationType.Hall
                 ? currentSchedule.ExamLocation.LocationId
                 : (currentSchedule.ExamLocation.ParentLocation?.ParentLocationId
                    ?? currentSchedule.ExamLocation.ParentLocationId
                    ?? currentLocId);

            // 2. جلب الكيان الرئيسي بالكامل من قاعدة البيانات لفحص حقله الفعلي
            var mainHallEntity = await _context.ExamLocations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LocationId == mainHallId);

            // 3. الفحص المركب: هل الكيان الرئيسي دورين؟ أو هل هناك صالات فرعية تابعة له مسجلة بالدور الثاني؟
            bool hasTwoFloors = (mainHallEntity?.Floor == 2) ||
                                await _context.ExamLocations.AnyAsync(l => (l.LocationId == mainHallId && l.Floor == 2) ||
                                                                           (l.ParentLocationId == mainHallId && l.Floor == 2));

            // 4. تحديد إجمالي القادة المطلوبين بناءً على الحسابات الدقيقة
            int totalManagersNeeded = hasTwoFloors ? 5 : 3;

            var selectedManagers = poolManagers.Take(totalManagersNeeded).ToList();
            if (selectedManagers.Count < totalManagersNeeded) return false;

            for (int i = 0; i < selectedManagers.Count; i++)
            {
                var manager = selectedManagers[i];
                string roleType = "رئيس جراش"; // القيمة الافتراضية
                string subRole = "";

                if (hasTwoFloors)
                {
                    subRole = i switch
                    {
                        0 => "قطاع أول",
                        1 => "قطاع ثاني",
                        2 => "قطاع ثالث",
                        3 => "احتياطي دور أول",
                        4 => "احتياطي دور ثاني",
                        _ => "رئيس"
                    };

                    // تغيير المسمى الوظيفي في اللوحة للاحتياطيين لكي يظهر بوضوح في شاشتك
                    if (i >= 3) roleType = "رئيس جراش احتياطي";
                }
                else
                {
                    subRole = i switch
                    {
                        0 => "قطاع أول",
                        1 => "قطاع ثاني",
                        2 => "احتياطي",
                        _ => "رئيس"
                    };

                    if (i == 2) roleType = "رئيس جراش احتياطي";
                }

                finalAssignments.Add(new CommitteesAssignment
                {
                    PersonId = manager.PersonId,
                    ExamScheduleId = examScheduleId,
                    RoleId = manager.RoleId,
                    LocationId = currentLocId,
                    AssignmentType = "Auto",
                    RoleType = roleType,      // المسمى الوظيفي يحدد رئيس جراش أو رئيس جراش احتياطي
                    SubRoleType = subRole
                });
            }

            // 3. توزيع المراقبين الاحتياطيين
            int reserveCount = hasTwoFloors ? 2 : 1;
            var reserveLeaders = poolLeaders.Take(reserveCount).ToList();

            foreach (var res in reserveLeaders)
            {
                int index = reserveLeaders.IndexOf(res);
                string roleType = "مراقب احتياطي";
                string subRole = hasTwoFloors ? (index == 0 ? "الدور الأول" : "الدور الثاني") : "لجراش";

                finalAssignments.Add(new CommitteesAssignment
                {
                    PersonId = res.PersonId,
                    ExamScheduleId = examScheduleId,
                    RoleId = res.RoleId,
                    LocationId = currentLocId,
                    AssignmentType = "Auto",
                    RoleType = roleType,
                    SubRoleType = subRole
                });
                poolLeaders.Remove(res);
            }

            // حساب الـ 5% ملاحظين احتياطيين
            int totalCommitteesCount = await _context.ExamSchedules
                .Include(es => es.ExamLocation)
                .Where(es => es.ExamId == examId && (es.LocationId == currentLocId || es.ExamLocation.ParentLocationId == currentLocId))
                .CountAsync();

            int requiredReserveObserversCount = (int)Math.Ceiling(totalCommitteesCount * 0.05);
            if (requiredReserveObserversCount < 1) requiredReserveObserversCount = 1;

            // توزيع الطاقم الطبي
            var doctor = poolDoctors.FirstOrDefault();
            if (doctor != null)
            {
                finalAssignments.Add(new CommitteesAssignment { PersonId = doctor.PersonId, ExamScheduleId = examScheduleId, LocationId = currentLocId, AssignmentType = "Auto", RoleType = "دكتور", RoleId = doctor.RoleId });
            }

            var nurse = poolNurses.FirstOrDefault();
            if (nurse != null)
            {
                finalAssignments.Add(new CommitteesAssignment { PersonId = nurse.PersonId, ExamScheduleId = examScheduleId, LocationId = currentLocId, AssignmentType = "Auto", RoleType = "مساعد دكتور", RoleId = nurse.RoleId });
            }

            // 4. توزيع المراقبين الأساسيين والملاحظين
            foreach (var block in activeBlocks)
            {
                var blockLeader = poolLeaders.FirstOrDefault();
                if (blockLeader != null)
                {
                    finalAssignments.Add(new CommitteesAssignment
                    {
                        PersonId = blockLeader.PersonId,
                        ExamScheduleId = examScheduleId,
                        LocationId = block.LocationId,
                        AssignmentType = "Auto",
                        RoleType = "مراقب",
                        RoleId = blockLeader.RoleId
                    });
                    poolLeaders.Remove(blockLeader);
                }

                var activeCommitteesInBlock = await _context.ExamLocations
                    .Where(c => c.ParentLocationId == block.LocationId && c.Type == LocationType.Committee)
                    .ToListAsync();

                foreach (var com in activeCommitteesInBlock)
                {
                    var obs = poolObservers
                        .FirstOrDefault(p => !yesterdayAssignments.Any(y => y.PersonId == p.PersonId && y.LocationId == com.LocationId));

                    if (obs == null)
                    {
                        obs = poolObservers.FirstOrDefault();
                    }

                    if (obs != null)
                    {
                        finalAssignments.Add(new CommitteesAssignment
                        {
                            PersonId = obs.PersonId,
                            ExamScheduleId = examScheduleId,
                            LocationId = com.LocationId,
                            AssignmentType = "Auto",
                            RoleType = "ملاحظ لجنة",
                            RoleId = obs.RoleId
                        });
                        poolObservers.Remove(obs);
                    }
                }
            }

            // 5. توزيع الملاحظين الاحتياطيين بنسبة 5%
            int distributedReserves = 0;
            while (distributedReserves < requiredReserveObserversCount && poolObservers.Any())
            {
                foreach (var block in activeBlocks)
                {
                    if (distributedReserves >= requiredReserveObserversCount || !poolObservers.Any()) break;

                    var reserveNote = poolObservers.FirstOrDefault();
                    if (reserveNote != null)
                    {
                        finalAssignments.Add(new CommitteesAssignment
                        {
                            PersonId = reserveNote.PersonId,
                            ExamScheduleId = examScheduleId,
                            RoleId = reserveNote.RoleId,
                            LocationId = block.LocationId,
                            AssignmentType = "Auto",
                            RoleType = "ملاحظ احتياطي",
                            SubRoleType = "احتياطي صالة"
                        });
                        poolObservers.Remove(reserveNote);
                        distributedReserves++;
                    }
                }
            }

            if (finalAssignments.Any())
            {
                _context.CommitteesAssignments.AddRange(finalAssignments);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }

        private async Task<int> GetStaffPriorityWeightAsync(int personId, DateTime currentExamDate)
        {
            var yesterday = currentExamDate.AddDays(-1).Date;
            var dayBeforeYesterday = currentExamDate.AddDays(-2).Date;

            bool workedYesterday = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .AnyAsync(a => a.PersonId == personId && a.ExamSchedule.Exam.ExamDate.Date == yesterday);

            if (workedYesterday) return 0;

            bool workedDayBefore = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .AnyAsync(a => a.PersonId == personId && a.ExamSchedule.Exam.ExamDate.Date == dayBeforeYesterday);

            if (workedDayBefore) return 2;

            return 1;
        }
    }
}