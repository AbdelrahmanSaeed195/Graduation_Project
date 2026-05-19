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

            modelBuilder.Entity<Role>().HasData(
                new Role { RoleID = 1, RoleName = StaffPosition.HallManager, RoleDescription = "رئيس صالة (أستاذ)" },
                new Role { RoleID = 2, RoleName = StaffPosition.HallManager, RoleDescription = "رئيس صالة (أستاذ مساعد)" },
                new Role { RoleID = 3, RoleName = StaffPosition.HallManager, RoleDescription = "رئيس صالة (أستاذ متفرغ)" },
                new Role { RoleID = 4, RoleName = StaffPosition.BlockGroupLeader, RoleDescription = "مراقب (مدرس)" },
                new Role { RoleID = 5, RoleName = StaffPosition.CommitteeObserver, RoleDescription = "ملاحظ (معيد)" },
                new Role { RoleID = 6, RoleName = StaffPosition.CommitteeObserver, RoleDescription = "ملاحظ (مدرس مساعد)" },
                new Role { RoleID = 7, RoleName = StaffPosition.CommitteeObserver, RoleDescription = "ملاحظ (موظف)" },
                new Role { RoleID = 8, RoleName = StaffPosition.Doctor, RoleDescription = "طبيب" },
                new Role { RoleID = 9, RoleName = StaffPosition.Nurse, RoleDescription = "ممرض" }
            );

            modelBuilder.Entity<Exam>()
                .HasOne(e => e.Subject)
                .WithMany(s => s.Exams)
                .HasForeignKey(e => e.SubjectID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CommitteesAssignment>()
                .HasKey(ca => ca.AssignmentId);

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

            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Hall)
                .WithMany()
                .HasForeignKey(ca => ca.HallId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Block)
                .WithMany()
                .HasForeignKey(ca => ca.BlockId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Committee)
                .WithMany(c => c.CommitteesAssignments)
                .HasForeignKey(ca => ca.CommitteeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

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

            modelBuilder.Entity<ExamSchedule>()
                .HasOne(es => es.Exam)
                .WithMany(e => e.ExamSchedules)
                .HasForeignKey(es => es.ExamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExamSchedule>()
                .HasOne(es => es.Block)
                .WithMany()
                .HasForeignKey(es => es.BlockId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ExamSchedule>()
                .HasIndex(es => new { es.ExamId, es.BlockId })
                .IsUnique();

            modelBuilder.Entity<Report>()
                .HasOne(r => r.ExamSchedule)
                .WithMany(es => es.Reports)
                .HasForeignKey(r => r.ScheduleId)
                .OnDelete(DeleteBehavior.Cascade);

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