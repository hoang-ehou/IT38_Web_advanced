using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalBooking.Models
{
    public class MedicalRecord
    {
        public int Id { get; set; }

        // Foreign Key: Appointment (one-to-one relationship)
        public int AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }

        // Foreign Key: Doctor who created this record
        [Required]
        public string DoctorId { get; set; } = default!;
        public Doctor? Doctor { get; set; }

        // Foreign Key: Patient (redundant but convenient)
        [Required]
        public string PatientId { get; set; } = default!;
        public Patient? Patient { get; set; }

        [StringLength(1000)]
        public string? Diagnosis { get; set; }

        [StringLength(2000)]
        public string? Prescription { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
