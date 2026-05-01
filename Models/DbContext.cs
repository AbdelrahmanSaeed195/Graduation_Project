using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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

            /* =========================
               Composite Primary Keys
            ========================== */


            modelBuilder.Entity<Role>().HasData(
new Role { RoleID = 1, RoleName = StaffPosition.HallManager, RoleDescription = "رئيس صالة" },
new Role { RoleID = 2, RoleName = StaffPosition.BlockGroupLeader, RoleDescription = "مراقب" },
new Role { RoleID = 3, RoleName = StaffPosition.CommitteeObserver, RoleDescription = "ملاحظ" },
new Role { RoleID = 4, RoleName = StaffPosition.Doctor, RoleDescription = "طبيب" },
new Role { RoleID = 5, RoleName = StaffPosition.Nurse, RoleDescription = "ممرض" }
        );


            modelBuilder.Entity<ReportPerson>()
                .HasKey(rp => new { rp.ReportID, rp.PersonID });

            modelBuilder.Entity<CommitteesAssignment>()
                .HasKey(ca => new { ca.PersonID, ca.CommitteeID, ca.RoleID });

            /* =========================
               Relationships
            ========================== */

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

            //modelBuilder.Entity<Person>()
            //    .HasOne(p => p.Role)
            //    .WithMany(p => p.Roles)
            //    .HasForeignKey(r => r.PersonID)
            //    .OnDelete(DeleteBehavior.Restrict);


            // 1. اجعل الـ AssignmentID هو المفتاح الأساسي الوحيد
            modelBuilder.Entity<CommitteesAssignment>()
     .HasKey(ca => ca.AssignmentID);

            // 2. منع تكرار الموظف في نفس الجلسة (لضمان عدم التضارب)
            modelBuilder.Entity<CommitteesAssignment>()
                .HasIndex(ca => new { ca.PersonID, ca.ExamScheduleId })
                .IsUnique();

            // 3. علاقة التكليف بالموظف
            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Person)
                .WithMany(p => p.CommitteesAssignments)
                .HasForeignKey(ca => ca.PersonID)
                .OnDelete(DeleteBehavior.Restrict);

            // 4. علاقة التكليف بجلسة الامتحان
            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.ExamSchedule)
                .WithMany()
                .HasForeignKey(ca => ca.ExamScheduleId)
                .OnDelete(DeleteBehavior.Cascade);

            // 5. علاقة التكليف بالدور (Role)
            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Role)
                .WithMany(r => r.CommitteesAssignments)
                .HasForeignKey(ca => ca.RoleID)
                .OnDelete(DeleteBehavior.Restrict);

            // 6. العلاقات الجديدة (نطاق المسئولية)
            // علاقة اختيارية بالصالة (لرؤساء الصالات)
            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Hall)
                .WithMany()
                .HasForeignKey(ca => ca.HallId)
                .OnDelete(DeleteBehavior.Restrict);

            // علاقة اختيارية بالبلوك (للمراقبين)
            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Block)
                .WithMany()
                .HasForeignKey(ca => ca.BlockId)
                .OnDelete(DeleteBehavior.Restrict);

            // علاقة اختيارية باللجنة (للملاحظين)
            modelBuilder.Entity<CommitteesAssignment>()
                .HasOne(ca => ca.Committee)
                .WithMany(c => c.CommitteesAssignments)
                .HasForeignKey(ca => ca.CommitteeID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Hall>()
                .HasOne(h => h.HallSupervisor)
                .WithMany()
                .HasForeignKey(h => h.HallSupervisorID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Block>()
                .HasOne(b => b.Hall)
                .WithMany(h => h.Blocks)
                .HasForeignKey(b => b.HallId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Committee>()
                .HasOne(c => c.Block)
                .WithMany(b => b.Committees)
                .HasForeignKey(c => c.BlockID)
                .OnDelete(DeleteBehavior.Restrict);

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
                .HasIndex(e => new { e.ScheduledDate, e.CommitteeId,})
                .IsUnique();

            modelBuilder.Entity<Report>()
                .HasOne(r => r.ExamSchedule)
                .WithMany(es => es.Reports)
                .HasForeignKey(r => r.ScheduleID)
                .OnDelete(DeleteBehavior.Cascade);


            modelBuilder.Entity<ReportPerson>()
                .HasOne(rp => rp.Report)
                .WithMany(r => r.ReportPersons)
                .HasForeignKey(rp => rp.ReportID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReportPerson>()
                .HasOne(rp => rp.Person)
                .WithMany(p => p.ReportPersons)
                .HasForeignKey(rp => rp.PersonID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ReportPerson>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.ReportPersons)
                .HasForeignKey(rp => rp.RoleID)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
