using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ContosoUniversity.Data;
using ContosoUniversity.Models;
using ContosoUniversity.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ContosoUniversity.Controllers
{
    public class StudentsController : Controller
    {
        private readonly SchoolContext _context;
        private readonly UserManager<Person> _userManager;

        public StudentsController(SchoolContext context, UserManager<Person> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Students
        [Authorize(Roles = "Admin, Student")]
        public async Task<IActionResult> Index(
            string sortOrder,
            string currentFilter,
            string searchString,
            int? pageNumber)
        {
            ViewData["CurrentSort"] = sortOrder;
            ViewData["NameSortParm"] = String.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";

            if (searchString != null)
            {
                pageNumber = 1;
            }
            else
            {
                searchString = currentFilter;
            }

            ViewData["CurrentFilter"] = searchString;

            if (User.IsInRole("Admin"))
            {
                var  students = from s in _context.Students
                               select s;
                if (!String.IsNullOrEmpty(searchString))
                {
                    students = students.Where(s => s.LastName.Contains(searchString)
                                           || s.FirstMidName.Contains(searchString));
                }
                switch (sortOrder)
                {
                    case "name_desc":
                        students = students.OrderByDescending(s => s.LastName);
                        break;
                    case "Date":
                        students = students.OrderBy(s => s.EnrollmentDate);
                        break;
                    case "date_desc":
                        students = students.OrderByDescending(s => s.EnrollmentDate);
                        break;
                    default:
                        students = students.OrderBy(s => s.LastName);
                        break;
                }

                int pageSize = 3;
                return View(await PaginatedList<Student>.CreateAsync(students.AsNoTracking(), pageNumber ?? 1, pageSize));
            }
            else if (User.IsInRole("Student"))
            {
                string userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

                int userId = int.Parse(userIdStr); // because your Identity uses int IDs

                var user = await _userManager.FindByIdAsync(userId.ToString());

                return RedirectToAction("Details", "Students", new { id = user.Id });
            }
            else
            {
                return RedirectToAction("Login", "Account");
            }


        }

        // GET: Students/Details/5
        [Authorize(Roles = "Admin, Student")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            string userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            int userId = int.Parse(userIdStr); // because your Identity uses int IDs

            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (User.IsInRole("Student") && user.Id != id)
            {
                return RedirectToAction("Details", "Students", new { id = user.Id });
            }
            var student = await _context.Students
                .Include(s => s.Enrollments)
                    .ThenInclude(e => e.Course)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }

        //[Authorize(Roles = "Admin")]
        // GET: Students/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Students/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("LastName,FirstMidName,EnrollmentDate")] Student student)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    _context.Add(student);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException /* ex */)
            {
                //Log the error (uncomment ex variable name and write a log.
                ModelState.AddModelError("", "Unable to save changes. " +
                    "Try again, and if the problem persists " +
                    "see your system administrator.");
            }
            return View(student);
        }

        // GET: Students/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context.Students.Include(s => s.Enrollments).FirstOrDefaultAsync(m => m.Id == id);

            PopulateAssignedCourseData(student);
            if (student == null)
            {
                return NotFound();
            }
            return View(student);
        }

        private void PopulateAssignedCourseData(Student student)
        {
            var allCourses = _context.Courses;
            var studentCourses = new HashSet<int>(student.Enrollments.Select(c => c.CourseID));
            var viewModel = new List<AssignedCourseData>();
            foreach (var course in allCourses)
            {
                viewModel.Add(new AssignedCourseData
                {
                    CourseID = course.CourseID,
                    Title = course.Title,
                    Assigned = studentCourses.Contains(course.CourseID)
                });
            }
            ViewData["Courses"] = viewModel;
        }

        // POST: Students/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,LastName,FirstMidName,EnrollmentDate")] Student student, string[] selectedCourses)
        {
            if (id != student.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingStudent = _context.Students.Include(s => s.Enrollments).ThenInclude(e => e.Course).FirstOrDefault(m => m.Id == id);
                    if (existingStudent != null)
                    {
                        _context.Entry(existingStudent).CurrentValues.SetValues(student);
                        UpdateStudentCourses(selectedCourses, existingStudent);
                        await _context.SaveChangesAsync();
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StudentExists(student.Id))
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
            return View(student);
        }

        private void UpdateStudentCourses(string[] selectedCourses, Student student)
        {
            var allCourses = _context.Courses;
            var selectedCoursesHS = new HashSet<string>(selectedCourses);
            var StudentCourses = new HashSet<int>
                (student.Enrollments.Select(c => c.Course.CourseID));
            foreach (var course in allCourses)
            {
                if (selectedCoursesHS.Contains(course.CourseID.ToString()))
                {
                    if (!StudentCourses.Contains(course.CourseID))
                    {
                        student.Enrollments.Add(new Enrollment { StudentID = student.Id, CourseID = course.CourseID, Grade = Grade.F});
                    }
                }
                else
                {
                     if (StudentCourses.Contains(course.CourseID))
                    {
                        Enrollment courseToRemove = student.Enrollments.FirstOrDefault(i => i.CourseID == course.CourseID);
                        _context.Remove(courseToRemove);
                    }
                }
            }
        }

        // GET: Students/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context.Students
                .FirstOrDefaultAsync(m => m.Id == id);
            if (student == null)
            {
                return NotFound();
            }

            return View(student);
        }

        // POST: Students/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student != null)
            {
                _context.Students.Remove(student);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool StudentExists(int id)
        {
            return _context.Students.Any(e => e.Id == id);
        }
    }
}
