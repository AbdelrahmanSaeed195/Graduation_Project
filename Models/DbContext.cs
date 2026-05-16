using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;

namespace projectweb.Models
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // جداول النظام الأساسية
        public DbSet<Person> Persons { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Relative> Relatives { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Committee> Committees { get; set; }
        public DbSet<CommitteesAssignment> CommitteesAssignments { get; set; }
        public DbSet<Hall> Halls { get; set; }
        public DbSet<Block> Blocks { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<ExamSchedule> ExamSchedules { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<ReportPerson> ReportPersons { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            /* =======================================================
               1. Seed Data - تغذية الجداول بالبيانات الأساسية للأدوار
            ======================================================= */
            modelBuilder.Entity<Role>().HasData(

                 new Role { RoleID = 1, RoleName = StaffPosition.HallManager, RoleDescription = "رئيس صالة" },

                 new Role { RoleID = 2, RoleName = StaffPosition.BlockGroupLeader, RoleDescription = "مراقب" },

                 new Role { RoleID = 3, RoleName = StaffPosition.CommitteeObserver, RoleDescription = "ملاحظ" },

                 new Role { RoleID = 4, RoleName = StaffPosition.Doctor, RoleDescription = "طبيب" },

                 new Role { RoleID = 5, RoleName = StaffPosition.Nurse, RoleDescription = "ممرض" }

             );

            /* =======================================================
               2. العلاقات المعقدة (Fluent API)
            ======================================================= */

            // علاقة المادة بالامتحانات
            modelBuilder.Entity<Exam>()
                .HasOne(e => e.Subject)
                .WithMany(s => s.Exams)
                .HasForeignKey(e => e.SubjectID)
                .OnDelete(DeleteBehavior.Restrict);

            // إعدادات جدول التكليفات (CommitteesAssignment)
            modelBuilder.Entity<CommitteesAssignment>()
                .HasKey(ca => ca.AssignmentId);

            // منع تكرار تكليف نفس الشخص في نفس الجلسة الامتحانية
            modelBuilder.Entity<CommitteesAssignment>()
                .HasIndex(ca => new { ca.PersonId, ca.ExamScheduleId })
                .IsUnique();

            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Person)
                .WithMany(p => p.CommitteesAssignments)
                .HasForeignKey(ca => ca.PersonId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.ExamSchedule)
                .WithMany()
                .HasForeignKey(ca => ca.ExamScheduleId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Role)
                .WithMany(r => r.CommitteesAssignments)
                .HasForeignKey(ca => ca.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // ربط التكليف بالصالة أو البلوك أو اللجنة (اختياري حسب نوع الدور)
            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Hall)
                .WithMany()
                .HasForeignKey(ca => ca.HallId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Block)
                .WithMany() // تم التعديل للسماح للبلوك برؤية تكليفاته مباشرة
                .HasForeignKey(ca => ca.BlockId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Committee)
                .WithMany(c => c.CommitteesAssignments)
                .HasForeignKey(ca => ca.CommitteeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            // إعدادات الأقارب (Relatives)
            modelBuilder.Entity<Relative>()
                .HasOne(r => r.Student)
                .WithMany(s => s.Relatives)
                .HasForeignKey(r => r.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Relative>()
                .HasOne(r => r.Person)
                .WithMany(p => p.Relatives)
                .HasForeignKey(r => r.PersonId)
                .OnDelete(DeleteBehavior.Restrict);

            // إعدادات القاعات والبلوكات واللجان (Hall -> Block -> Committee)
            modelBuilder.Entity<Hall>()
                .HasOne(h => h.HallSupervisor)
                .WithMany()
                .HasForeignKey(h => h.HallSupervisorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Block>()
                .HasOne(b => b.Hall)
                .WithMany(h => h.Blocks)
                .HasForeignKey(b => b.HallId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Committee>()
                .HasOne(c => c.Block)
                .WithMany(b => b.Committees)
                .HasForeignKey(c => c.BlockId)
                .OnDelete(DeleteBehavior.Restrict);

            // إعدادات جدول مواعيد الامتحانات (Exam Schedule)
            modelBuilder.Entity<ExamSchedule>()
                .HasOne(es => es.Exam)
                .WithMany(e => e.ExamSchedules)
                .HasForeignKey(es => es.ExamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExamSchedule>()
                .HasOne(es => es.Committee)
                .WithMany(c => c.ExamSchedules)
                .HasForeignKey(es => es.CommitteeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExamSchedule>()
            .HasIndex(es => new { es.ExamId, es.CommitteeId })
            .IsUnique();

            // التقارير (Reports)
            modelBuilder.Entity<Report>()
                .HasOne(r => r.ExamSchedule)
                .WithMany(es => es.Reports)
                .HasForeignKey(r => r.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);

            // الربط المتعدد للأشخاص في التقارير (ReportPerson)
            modelBuilder.Entity<ReportPerson>()
                .HasKey(rp => new { rp.ReportId, rp.PersonId });

            modelBuilder.Entity<ReportPerson>()
                .HasOne(rp => rp.Report)
                .WithMany(r => r.ReportPersons)
                .HasForeignKey(rp => rp.ReportId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReportPerson>()
                .HasOne(rp => rp.Person)
                .WithMany(p => p.ReportPersons)
                .HasForeignKey(rp => rp.PersonId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReportPerson>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.ReportPersons)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}