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
                .Include(s => s.ExamLocation) // التحديث للمقر الموحد
                .FirstOrDefaultAsync(s => s.ExamScheduleId == examScheduleId);

            if (current == null || current.Exam == null || current.ExamLocation == null)
                return "الجلسة المطلوبة غير موجودة.";

            // جلب معرف الموقع الأعلى (الموقع الأب أو الحالي إذا كان هو الأب)
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
                             a.ExamLocation.ParentLocationId == parentLocId) &&
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

            // 1. تنظيف التوزيع التلقائي القديم لهذه المادة في هذا المقر الموحد اليوم
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

            // جلب جداول التوزيع المرتبطة بالجلسة والموقع الحالي (أو المواقع الفرعية التابعة له)
            var scheduledSchedules = await _context.ExamSchedules
                .Include(es => es.ExamLocation)
                .Where(es => es.ExamId == examId && (es.LocationId == currentLocId || es.ExamLocation.ParentLocationId == currentLocId))
                .ToListAsync();

            if (!scheduledSchedules.Any()) return false;

            // فلترة المقرات المصنفة كـ صالات/بلوكات نشطة (Block) داخل نطاق التوزيع الحالي
            var activeBlocks = scheduledSchedules
                .Select(es => es.ExamLocation.Type == LocationType.Block ? es.ExamLocation : es.ExamLocation.ParentLocation)
                .Where(l => l != null && l.Type == LocationType.Block)
                .GroupBy(l => l.LocationId)
                .Select(g => g.First())
                .OrderBy(l => l.LocationName)
                .ToList();

            if (!activeBlocks.Any()) return false;

            // جلب معرفات اللجان النشطة (Committees) التابعة لهذه الصالات
            var activeBlockIds = activeBlocks.Select(b => b.LocationId).ToList();
            var activeCommitteeIds = await _context.ExamLocations
                .Where(l => l.ParentLocationId != null && activeBlockIds.Contains(l.ParentLocationId.Value) && l.Type == LocationType.Committee)
                .Select(l => l.LocationId)
                .ToListAsync();

            // جلب معرفات الطلاب المتواجدين في نطاق هذه المقرات
            var studentIdsInHall = await _context.Students
                .Where(s => (s.LocationId != null && activeCommitteeIds.Contains(s.LocationId.Value)) || s.ExamScheduleId == examScheduleId)
                .Select(s => s.StudentId)
                .ToListAsync();

            // تحديد ومنع تعارض صلات القرابة بدقة متناهية لحماية أرشيف الامتحانات
            var excludedPersonIdsDueToRelatives = await _context.Relatives
                .Where(r => studentIdsInHall.Contains(r.StudentId))
                .Select(r => r.PersonId)
                .Distinct()
                .ToListAsync();

            // جلب المنشغلين الفعليين في نفس الجلسة
            var busyPersonIds = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule)
                .Where(a => a.ExamSchedule.ExamId == examId)
                .Select(a => a.PersonId).ToListAsync();

            // جلب القوى البشرية الفعالة والمتاحة للفرز
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

            // تجميع وتوزيع الخزانات البشرية (Pools) حسب المسمى الوظيفي والوزن التراكمي
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

            int totalActiveBlocks = activeBlocks.Count;
            int half = (int)Math.Ceiling((double)totalActiveBlocks / 2);

            var firstSectionBlocks = activeBlocks.Take(half).ToList();
            var secondSectionBlocks = activeBlocks.Skip(half).ToList();

            // 2. توزيع طاقم الإشراف ورؤساء القطاعات (الأساتذة)
            var selectedManagers = poolManagers.Take(3).ToList();
            if (selectedManagers.Count < 2) return false;

            for (int i = 0; i < selectedManagers.Count; i++)
            {
                var manager = selectedManagers[i];
                if (i == 0)
                {
                    var firstBlock = firstSectionBlocks.FirstOrDefault();
                    finalAssignments.Add(new CommitteesAssignment
                    {
                        PersonId = manager.PersonId,
                        ExamScheduleId = examScheduleId,
                        RoleId = manager.RoleId,
                        LocationId = currentLocId, 
                        AssignmentType = "Auto",
                        RoleType = "رئيس جراش أساسي (القطاع الأول)"
                    });
                }
                else if (i == 1)
                {
                    var firstBlockOfSecondSection = secondSectionBlocks.FirstOrDefault();
                    finalAssignments.Add(new CommitteesAssignment
                    {
                        PersonId = manager.PersonId,
                        ExamScheduleId = examScheduleId,
                        RoleId = manager.RoleId,
                        LocationId = currentLocId,
                        AssignmentType = "Auto",
                        RoleType = "رئيس جراش أساسي (القطاع الثاني)"
                    });
                }
                else if (i == 2)
                {
                    finalAssignments.Add(new CommitteesAssignment
                    {
                        PersonId = manager.PersonId,
                        ExamScheduleId = examScheduleId,
                        RoleId = manager.RoleId,
                        LocationId = currentLocId,
                        AssignmentType = "Auto",
                        RoleType = "رئيس جراش احتياطي"
                    });
                }
            }

            // 3. توزيع مراقب احتياطي واحد للمقر الرئيسي
            var reserveLeader = poolLeaders.FirstOrDefault();
            if (reserveLeader != null)
            {
                finalAssignments.Add(new CommitteesAssignment
                {
                    PersonId = reserveLeader.PersonId,
                    ExamScheduleId = examScheduleId,
                    RoleId = reserveLeader.RoleId,
                    LocationId = currentLocId,
                    AssignmentType = "Auto",
                    RoleType = "مراقب احتياطي للجراش"
                });
                poolLeaders.Remove(reserveLeader);
            }

            // حساب الـ 5% ملاحظين احتياطيين بناءً على إجمالي لجان التوزيع الفعلي
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

            // 4. توزيع المراقبين الأساسيين والملاحظين على الصالات (Blocks) واللجان (Committees)
            foreach (var block in activeBlocks)
            {
                var blockLeader = poolLeaders.FirstOrDefault();
                if (blockLeader != null)
                {
                    finalAssignments.Add(new CommitteesAssignment
                    {
                        PersonId = blockLeader.PersonId,
                        ExamScheduleId = examScheduleId,
                        LocationId = block.LocationId, // ربط مباشر بالصالة الفرعية
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
                    // تطبيق شرط عدم تكرار مراقبة نفس اللجنة ليومين متتاليين لضمان النزاهة والأمان الدراسي
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
                            LocationId = com.LocationId, // التسكين المباشر بكود اللجنة الموحد
                            AssignmentType = "Auto",
                            RoleType = "ملاحظ لجنة",
                            RoleId = obs.RoleId
                        });
                        poolObservers.Remove(obs);
                    }
                }
            }

            // 5. توزيع الملاحظين الاحتياطيين بنسبة 5% على مستوى الصالات الفرعية النشطة
            int distributedReserves = 0;
            while (distributedReserves < requiredReserveObserversCount && poolObservers.Any())
            {
                foreach (var block in activeBlocks)
                {
                    if (distributedReserves >= requiredReserveObserversCount || !poolObservers.Any())
                        break;

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
                            RoleType = "ملاحظ احتياطي للصالة (تحت إدارة المراقب)"
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

        // ========================================================================
        // 3. GetStaffPriorityWeightAsync: حساب الأوزان التراكمية لتوزيع ميزان العبء الدوري
        // ========================================================================
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