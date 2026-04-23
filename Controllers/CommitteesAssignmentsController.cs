using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using projectweb.Models;
using projectweb.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace projectweb.Controllers
{
    public class CommitteesAssignmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICommitteesAssignmentsService _committeesAssignmentsService;

        public CommitteesAssignmentsController(ApplicationDbContext context, ICommitteesAssignmentsService committeesAssignmentsService)
        {
            _context = context;
            _committeesAssignmentsService = committeesAssignmentsService;
        }
        // =====================================
        // INDEX
        // =====================================
        public async Task<IActionResult> Index()
        {
            var committeesAssignments = _context.CommitteesAssignments
                .Include(c => c.Committee)
                .Include(c => c.Person)
                .Include(c => c.Role)
                .Include(c => c.ExamSchedule)
                .OrderByDescending(c => c.ExamSchedule.ScheduledDate); 

            return View(await committeesAssignments.ToListAsync());
        }
        // =====================================
        // DETAILS
        // =====================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var committeesAssignment = await _context.CommitteesAssignments
                .Include(c => c.Committee)
                .Include(c => c.Person)
                .Include(c => c.Role)
                .FirstOrDefaultAsync(m => m.AssignmentID == id);
            if (committeesAssignment == null)
            {
                return NotFound();
            }

            return View(committeesAssignment);
        }
        // =====================================
        // CREATE
        // =====================================
        public IActionResult Create()
        {
            ViewData["CommitteeID"] = new SelectList(_context.Committees, "CommitteeID", "CommitteeNumber");
            ViewData["PersonID"] = new SelectList(_context.Persons, "PersonId", "FullName");
            ViewData["RoleID"] = new SelectList(_context.Roles, "RoleID", "RoleName");

            
            var schedules = _context.ExamSchedules
                .Include(s => s.Exam)
                .Select(s => new {
                    ID = s.ExamScheduleId,
                    Text = s.Exam.ExamId + " - " + s.ScheduledDate.ToShortDateString()
                }).ToList();

            ViewData["ExamScheduleId"] = new SelectList(schedules, "ID", "Text");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AssignmentID,PersonID,CommitteeID,RoleID,ExamScheduleId,isReserve,AssignmentType,RoleType")] CommitteesAssignment committeesAssignment)
        {
          
            if (ModelState.IsValid)
            {
                _context.Add(committeesAssignment);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم إنشاء التكليف بنجاح";
                return RedirectToAction(nameof(Index));
            }

          
            ViewData["CommitteeID"] = new SelectList(_context.Committees, "CommitteeID", "CommitteeNumber", committeesAssignment.CommitteeID);
            ViewData["PersonID"] = new SelectList(_context.Persons, "PersonId", "FullName", committeesAssignment.PersonID);
            ViewData["RoleID"] = new SelectList(_context.Roles, "RoleID", "RoleName", committeesAssignment.RoleID);
            ViewData["ExamScheduleId"] = new SelectList(_context.ExamSchedules, "ExamScheduleId", "ScheduledDate", committeesAssignment.ExamScheduleId);

            return View(committeesAssignment);
        }
        // =====================================
        // EDIT
        // =====================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var committeesAssignment = await _context.CommitteesAssignments.FindAsync(id);
            if (committeesAssignment == null)
            {
                return NotFound();
            }

            ViewData["CommitteeID"] = new SelectList(_context.Committees, "CommitteeID", "CommitteeNumber", committeesAssignment.CommitteeID);
            ViewData["PersonID"] = new SelectList(_context.Persons, "PersonId", "FullName", committeesAssignment.PersonID);
            ViewData["RoleID"] = new SelectList(_context.Roles, "RoleID", "RoleName", committeesAssignment.RoleID);
    
            var schedules = _context.ExamSchedules
                .Include(s => s.Exam)
                .Select(s => new {
                    ID = s.ExamScheduleId,
                    Text = s.Exam.ExamId + " - " + s.ScheduledDate.ToShortDateString()
                }).ToList();
            ViewData["ExamScheduleId"] = new SelectList(schedules, "ID", "Text", committeesAssignment.ExamScheduleId);

            return View(committeesAssignment);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AssignmentID,PersonID,CommitteeID,RoleID,ExamScheduleId,isReserve,AssignmentType,RoleType")] CommitteesAssignment committeesAssignment)
        {
            if (id != committeesAssignment.AssignmentID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(committeesAssignment);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "تم تحديث التكليف بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CommitteesAssignmentExists(committeesAssignment.AssignmentID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

           
            ViewData["CommitteeID"] = new SelectList(_context.Committees, "CommitteeID", "CommitteeNumber", committeesAssignment.CommitteeID);
            ViewData["PersonID"] = new SelectList(_context.Persons, "PersonId", "FullName", committeesAssignment.PersonID);
            ViewData["RoleID"] = new SelectList(_context.Roles, "RoleID", "RoleName", committeesAssignment.RoleID);
            ViewData["ExamScheduleId"] = new SelectList(_context.ExamSchedules, "ExamScheduleId", "ScheduledDate", committeesAssignment.ExamScheduleId);

            return View(committeesAssignment);
        }
        // =====================================
        //  DELETE
        // =====================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var committeesAssignment = await _context.CommitteesAssignments
                .Include(c => c.Committee)
                .Include(c => c.Person) 
                .Include(c => c.Role)
                .FirstOrDefaultAsync(m => m.AssignmentID == id);

            if (committeesAssignment == null)
            {
                return NotFound();
            }

            return View(committeesAssignment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var committeesAssignment = await _context.CommitteesAssignments.FindAsync(id);
            if (committeesAssignment != null)
            {
                _context.CommitteesAssignments.Remove(committeesAssignment);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "تم حذف تكليف اللجنة بنجاح";

            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> AutoAssign()
        {
            
            var schedules = _context.ExamSchedules
                .Include(s => s.Exam)
                    .ThenInclude(e => e.Subject)
                .Include(s => s.Committee)
                .OrderBy(s => s.ScheduledDate)
                .Select(s => new {
                    ID = s.ExamScheduleId,
                    Text = $"{s.Exam.Subject.SubjectName} - Committee {s.Committee.CommitteeNumber} ({s.ScheduledDate:dd/MM})"
                }).ToList();

            ViewData["ExamScheduleId"] = new SelectList(schedules, "ID", "Text");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AutoAssign(int examScheduleId)
        {
    
            var success = await _committeesAssignmentsService.RunAssignmentAsync(examScheduleId);

            if (success)
            {
                TempData["SuccessMessage"] = "تم إنشاء تكليفات اللجنة بنجاح";
            }
            else
            {
                TempData["ErrorMessage"] = "فشل في إنشاء التكليفات. الأسباب المحتملة: جميع الموظفين مشغولون في هذا الوقت أو لم يتم العثور على جدول الجلسة.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool CommitteesAssignmentExists(int id)
        {
            return _context.CommitteesAssignments.Any(e => e.AssignmentID == id);
        }
    }
}