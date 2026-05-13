using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalBooking.Models
{
    public class Doctor : ApplicationUser
    {
        [Required]
        [StringLength(100)]
        public string Specialization { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ConsultationFee { get; set; }

        // Foreign Key: Doctor belongs to a Department
        public int DepartmentId { get; set; }

        // Relationship: Doctor belongs to one Department
        public Department? Department { get; set; }

        // Relationship: One Doctor has many Appointments
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

        // Relationship: One Doctor creates many MedicalRecords
        public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
    }
}
