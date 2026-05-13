using System.Collections.Generic;

namespace MedicalBooking.Models
{
    public class Patient : ApplicationUser
    {
        // Relationship: One Patient has many Appointments
        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

        // Relationship: One Patient has many MedicalRecords
        public ICollection<MedicalRecord> MedicalRecords { get; set; } = new List<MedicalRecord>();
    }
}
