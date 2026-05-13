using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MedicalBooking.Models;

namespace MedicalBooking.Data
{
    public static class SeedData
    {
        public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            string[] roleNames = { "Admin", "Doctor", "Patient" };

            foreach (var roleName in roleNames)
            {
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }
        }

        public static async Task SeedTestUsersAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // Ensure at least one department exists
            if (!await dbContext.Departments.AnyAsync())
            {
                dbContext.Departments.AddRange(
                    new Department { Name = "Cardiology", Description = "Heart and cardiovascular diseases" },
                    new Department { Name = "Neurology", Description = "Brain and nervous system disorders" },
                    new Department { Name = "Orthopedics", Description = "Bones, joints, and muscles" },
                    new Department { Name = "Dermatology", Description = "Skin, hair, and nails" },
                    new Department { Name = "Pediatrics", Description = "Children's health care" },
                    new Department { Name = "General Medicine", Description = "General medical services" }
                );
                await dbContext.SaveChangesAsync();
            }

            var departments = await dbContext.Departments.ToListAsync();

            // Seed Admin
            var adminEmail = "admin@demo.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(adminUser, "Pass123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }

            // Seed 10 Doctors
            for (int i = 1; i <= 10; i++)
            {
                var doctorEmail = $"doctor{i}@demo.com";
                var existingDoctor = await userManager.FindByEmailAsync(doctorEmail);
                if (existingDoctor == null)
                {
                    var department = departments[(i - 1) % departments.Count];
                    var doctor = new Doctor
                    {
                        UserName = doctorEmail,
                        Email = doctorEmail,
                        FullName = $"Dr. {GetFirstName(i)} {GetLastName(i)}",
                        EmailConfirmed = true,
                        DepartmentId = department.Id,
                        Specialization = GetSpecialization(department.Name),
                        ConsultationFee = 50m + (i * 10),
                        PhoneNumber = $"090{i}123456",
                        DateOfBirth = new DateTime(1980 - i, 5, 15)
                    };
                    var result = await userManager.CreateAsync(doctor, "Pass123!");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(doctor, "Doctor");
                    }
                }
            }

            // Seed 10 Patients
            for (int i = 1; i <= 10; i++)
            {
                var patientEmail = $"patient{i}@demo.com";
                var existingPatient = await userManager.FindByEmailAsync(patientEmail);
                if (existingPatient == null)
                {
                    var patient = new Patient
                    {
                        UserName = patientEmail,
                        Email = patientEmail,
                        FullName = $"{GetPatientFirstName(i)} {GetPatientLastName(i)}",
                        EmailConfirmed = true,
                        PhoneNumber = $"091{i}654321",
                        DateOfBirth = new DateTime(1990 - i, 8, 20)
                    };
                    var result = await userManager.CreateAsync(patient, "Pass123!");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(patient, "Patient");
                    }
                }
            }

            // Seed 10 Appointments
            var doctors = await userManager.Users.OfType<Doctor>().ToListAsync();
            var patients = await userManager.Users.OfType<Patient>().ToListAsync();

            if (await dbContext.Appointments.CountAsync() == 0 && doctors.Any() && patients.Any())
            {
                var now = DateTime.UtcNow;
                for (int i = 1; i <= 10; i++)
                {
                    var status = i <= 3 ? "Confirmed" : (i <= 6 ? "Pending" : (i <= 8 ? "Completed" : "Cancelled"));
                    var appointment = new Appointment
                    {
                        PatientId = patients[i % patients.Count].Id,
                        DoctorId = doctors[i % doctors.Count].Id,
                        AppointmentDate = now.AddDays(i).AddHours(9 + (i % 8)),
                        Status = status,
                        Notes = i <= 5 ? $"Regular checkup for patient {i}" : "Follow-up consultation",
                        CreatedAt = now.AddDays(-i)
                    };
                    dbContext.Appointments.Add(appointment);
                }
                await dbContext.SaveChangesAsync();
            }

            // Seed 10 Medical Records (for Completed appointments)
            if (await dbContext.MedicalRecords.CountAsync() == 0)
            {
                var completedAppointments = await dbContext.Appointments
                    .Where(a => a.Status == "Completed")
                    .ToListAsync();

                if (completedAppointments.Any())
                {
                    int recordNum = 1;
                    foreach (var appt in completedAppointments)
                    {
                        var medicalRecord = new MedicalRecord
                        {
                            PatientId = appt.PatientId,
                            DoctorId = appt.DoctorId,
                            AppointmentId = appt.Id,
                            Diagnosis = GetDiagnosis(recordNum),
                            Prescription = GetPrescription(recordNum),
                            Notes = $"Medical record created on {DateTime.UtcNow:yyyy-MM-dd}",
                            CreatedAt = appt.AppointmentDate.AddDays(1)
                        };
                        dbContext.MedicalRecords.Add(medicalRecord);
                        recordNum++;
                    }
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        private static string GetFirstName(int i) =>
            i switch
            {
                1 => "John", 2 => "Sarah", 3 => "Michael", 4 => "Emily", 5 => "David",
                6 => "Jessica", 7 => "James", 8 => "Jennifer", 9 => "Robert", _ => "Lisa"
            };

        private static string GetLastName(int i) =>
            i switch
            {
                1 => "Smith", 2 => "Johnson", 3 => "Williams", 4 => "Brown", 5 => "Jones",
                6 => "Garcia", 7 => "Miller", 8 => "Davis", 9 => "Rodriguez", _ => "Martinez"
            };

        private static string GetPatientFirstName(int i) =>
            i switch
            {
                1 => "Emma", 2 => "Liam", 3 => "Olivia", 4 => "Noah", 5 => "Ava",
                6 => "William", 7 => "Sophia", 8 => "Benjamin", 9 => "Isabella", _ => "Lucas"
            };

        private static string GetPatientLastName(int i) =>
            i switch
            {
                1 => "Anderson", 2 => "Thomas", 3 => "Jackson", 4 => "White", 5 => "Harris",
                6 => "Martin", 7 => "Thompson", 8 => "Garcia", 9 => "Martinez", _ => "Robinson"
            };

        private static string GetSpecialization(string department) =>
            department switch
            {
                "Cardiology" => "Heart Specialist",
                "Neurology" => "Brain & Nervous System",
                "Orthopedics" => "Bone & Joint Specialist",
                "Dermatology" => "Skin Care Expert",
                "Pediatrics" => "Child Healthcare",
                _ => "General Practice"
            };

        private static string GetSymptoms(int i) =>
            i switch
            {
                1 => "Chest pain, shortness of breath",
                2 => "Headache, dizziness, blurred vision",
                3 => "Joint pain, swelling in knees",
                4 => "Rash, itching, red spots",
                5 => "Fever, cough, sore throat",
                6 => "Abdominal pain, nausea",
                7 => "Back pain, numbness in legs",
                8 => "Fatigue, weight loss",
                9 => "Anxiety, insomnia",
                _ => "General weakness, fever"
            };

        private static string GetDiagnosis(int i) =>
            i switch
            {
                1 => "Hypertension, early stage heart disease - Prescribed: Amlodipine 5mg daily",
                2 => "Migraine, tension headache - Prescribed: Ibuprofen 400mg PRN",
                3 => "Osteoarthritis, mild joint inflammation - Prescribed: Naproxen 500mg BID",
                4 => "Eczema, allergic dermatitis - Prescribed: Hydrocortisone cream topically",
                5 => "Upper respiratory infection - Prescribed: Rest, fluids, symptom management",
                6 => "Gastritis, acid reflux - Prescribed: Omeprazole 20mg daily",
                7 => "Lumbar strain, sciatica - Prescribed: Physical therapy, pain management",
                8 => "Hypothyroidism, vitamin deficiency - Prescribed: Levothyroxine 50mcg daily",
                9 => "Generalized anxiety disorder - Prescribed: Counseling, SSRI evaluation",
                _ => "Viral infection, mild fever - Prescribed: Rest, fluids, acetaminophen PRN"
            };

        private static string GetPrescription(int i) =>
            i switch
            {
                1 => "Amlodipine 5mg once daily, Lisinopril 10mg once daily, Aspirin 81mg daily",
                2 => "Ibuprofen 400mg every 6 hours PRN, Triptan for severe episodes",
                3 => "Naproxen 500mg twice daily, Glucosamine 1500mg daily, Ice/heat therapy",
                4 => "Hydrocortisone 1% cream BID, Cetirizine 10mg daily, Moisturizer",
                5 => "Acetaminophen 500mg PRN, Saline nasal spray, Throat lozenges",
                6 => "Omeprazole 20mg daily before breakfast, Antacids PRN",
                7 => "Naproxen 500mg BID, Cyclobenzaprine 10mg at bedtime, PT referral",
                8 => "Levothyroxine 50mcg daily, Vitamin D 2000 IU, B12 1000mcg",
                9 => "SSRI evaluation, Cognitive behavioral therapy referral, Sleep hygiene",
                _ => "Acetaminophen 500mg Q6H PRN, Fluids 2-3L daily, Rest"
            };
    }
}