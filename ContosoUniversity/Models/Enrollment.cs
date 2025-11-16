using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ContosoUniversity.Models
{
    public enum Grade
    {
        A, B, C, D, F
    }

    public class Enrollment
    {
        public int EnrollmentID { get; set; }
        public int CourseID { get; set; }
        public int StudentID { get; set; }

        [DisplayFormat(NullDisplayText = "No Grade")]
        public Grade Grade { get; set; } = Grade.F;

        public Course Course { get; set; } = default!;
        public Student Student { get; set; } = default!;
    }
}