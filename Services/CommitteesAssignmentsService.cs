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
            // 1. جلب بيانات الجلسة الحالية وتحديد الامتحان والصالة
            var currentSchedule = await _context.ExamSchedules
                .Include(s => s.Exam)
                .Include(s => s.Committee).ThenInclude(c => c.Block)
                .FirstOrDefaultAsync(s => s.ExamScheduleId == examScheduleId);

            if (currentSchedule == null || currentSchedule.Exam == null) return false;

            var hallId = currentSchedule.Committee.Block.HallId;
            var examDate = currentSchedule.Exam.ExamDate.Date;
            var examId = currentSchedule.ExamId; // معرف الامتحان الحالي لفلترة اللجان

            // --- تنظيف التوزيعات التلقائية السابقة لنفس الصالة واليوم لمنع التكرار ---
            var oldAssignments = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .Where(a => a.ExamSchedule.Exam.ExamDate.Date == examDate &&
                            ((a.HallId == hallId) || (a.Block.HallId == hallId) || (a.Committee.Block.HallId == hallId)) &&
                            a.AssignmentType == "Auto")
                .ToListAsync();

            _context.CommitteesAssignments.RemoveRange(oldAssignments);
            await _context.SaveChangesAsync();

            // 2. التعديل الجوهري: جلب اللجان المحجوزة لهذا الامتحان فقط من جدول ExamSchedules
            // هذا يمنع التوزيع على لجنة 6 إذا كانت مادة البرمجة تشغل لجان 1-5 فقط
            var scheduledCommittees = await _context.ExamSchedules
                .Include(es => es.Committee)
                    .ThenInclude(c => c.Block)
                .Where(es => es.ExamId == examId && es.Committee.Block.HallId == hallId)
                .ToListAsync();

            if (!scheduledCommittees.Any()) return false;

            // تحديد البلوكات "النشطة" التي تحتوي على لجان لهذا الامتحان فقط
            var activeBlocks = scheduledCommittees
                .Select(es => es.Committee.Block)
                .GroupBy(b => b.BlockID)
                .Select(g => g.First())
                .ToList();

            // 3. جلب الموظفين المتاحين (غير المشغولين في لجان أخرى في نفس اليوم)
            var busyPersonIds = await _context.CommitteesAssignments
                .Include(a => a.ExamSchedule).ThenInclude(es => es.Exam)
                .Where(a => a.ExamSchedule.Exam.ExamDate.Date == examDate)
                .Select(a => a.PersonID).ToListAsync();

            var availableStaff = await _context.Persons
                .Include(p => p.Role)
                .Where(p => !busyPersonIds.Contains(p.PersonId) && p.IsActiveForAssignment)
                .ToListAsync();

            var finalAssignments = new List<CommitteesAssignment>();
            var random = new Random();

            // --- أ- توزيع رؤساء الصالة (أساسي + احتياطي) ---
            var hallManagersPool = availableStaff
                .Where(p => p.JobRole == JobTitle.ProfessorEmeritus || p.JobRole == JobTitle.AssistantProfessor)
                .OrderBy(p => random.Next())
                .Take(3)
                .ToList();

            if (hallManagersPool.Count >= 2)
            {
                int totalBlocks = activeBlocks.Count;
                int half = totalBlocks / 2;

                for (int i = 0; i < Math.Min(hallManagersPool.Count, 3); i++)
                {
                    var manager = hallManagersPool[i];
                    string roleTitle = "رئيس صالة احتياطي";

                    if (i == 0) roleTitle = $"رئيس صالة أساسي (أول {half} بلوك)";
                    else if (i == 1) roleTitle = $"رئيس صالة أساسي ({totalBlocks - half} بلوك المتبقية)";

                    finalAssignments.Add(new CommitteesAssignment
                    {
                        PersonID = manager.PersonId,
                        ExamScheduleId = examScheduleId,
                        RoleID = manager.RoleID,
                        HallId = hallId,
                        AssignmentType = "Auto",
                        RoleType = roleTitle
                    });
                    availableStaff.Remove(manager);
                }
            }

            // --- ب- توزيع مراقبي البلوكات وملاحظي اللجان بناءً على اللجان المجدولة فعلياً ---
            foreach (var block in activeBlocks)
            {
                // تعيين مراقب للبلوك (فقط إذا كان البلوك يحتوي على لجان لهذا الامتحان)
                var blockLeader = availableStaff
                    .Where(p => p.JobRole == JobTitle.StaffObserver || p.JobRole == JobTitle.Assistant)
                    .OrderBy(p => random.Next()).FirstOrDefault();

                if (blockLeader != null)
                {
                    finalAssignments.Add(new CommitteesAssignment
                    {
                        PersonID = blockLeader.PersonId,
                        ExamScheduleId = examScheduleId,
                        RoleID = blockLeader.RoleID,
                        BlockId = block.BlockID,
                        AssignmentType = "Auto",
                        RoleType = "مراقب"
                    });
                    availableStaff.Remove(blockLeader);

                    // توزيع ملاحظي اللجان (فقط للجان الموجودة في ExamSchedules لهذا الامتحان)
                    var committeesInThisBlock = scheduledCommittees
                        .Where(es => es.Committee.BlockID == block.BlockID)
                        .Select(es => es.Committee)
                        .ToList();

                    foreach (var committee in committeesInThisBlock)
                    {
                        var observer = availableStaff
                            .Where(p => p.JobRole == JobTitle.Employee)
                            .OrderBy(p => random.Next()).FirstOrDefault();

                        if (observer != null)
                        {
                            finalAssignments.Add(new CommitteesAssignment
                            {
                                PersonID = observer.PersonId,
                                ExamScheduleId = examScheduleId,
                                RoleID = observer.RoleID,
                                CommitteeID = committee.CommitteeID,
                                AssignmentType = "Auto",
                                RoleType = "ملاحظ لجنة"
                            });
                            availableStaff.Remove(observer);
                        }
                    }
                }
            }

            // 4. الحفظ النهائي في قاعدة البيانات
            if (finalAssignments.Any())
            {
                _context.CommitteesAssignments.AddRange(finalAssignments);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }
    }
}