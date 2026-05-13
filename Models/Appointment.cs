using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedicalBooking.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        // Foreign Key: Patient
        [Required]
        public string PatientId { get; set; } = default!;
        public Patient? Patient { get; set; }

        // Foreign Key: Doctor
        [Required]
        public string DoctorId { get; set; } = default!;
        public Doctor? Doctor { get; set; }

        [Required]
        public DateTime AppointmentDate { get; set; }

        [Required]
        [StringLength(20)]
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Cancelled, Completed

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Relationship: One Appointment has one MedicalRecord (optional)
        public MedicalRecord? MedicalRecord { get; set; }
    }
}
