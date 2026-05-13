using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MedicalBooking.Models
{
    public class Department
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        // Relationship: One Department has many Doctors
        public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
    }
}
