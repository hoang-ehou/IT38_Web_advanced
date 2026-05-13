using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedicalBooking.Data;
using MedicalBooking.Models;

namespace MedicalBooking.Controllers
{
    [Route("Api")]
    [Authorize]
    public class ApiController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /Api/Doctors/GetAvailableDoctors
        [HttpGet("Doctors/GetAvailableDoctors")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableDoctors(int? departmentId)
        {
            var query = _context.Users
                .OfType<Doctor>()
                .Include(d => d.Department)
                .AsQueryable();

            if (departmentId.HasValue)
            {
                query = query.Where(d => d.DepartmentId == departmentId.Value);
            }

            var doctors = await query.ToListAsync();

            var dto = doctors.Select(d => new
            {
                id = d.Id,
                name = d.FullName,
                specialization = d.Specialization,
                department = d.Department != null ? d.Department.Name : null,
                fee = d.ConsultationFee
            });

            return Json(dto);
        }

        // GET: /Api/Appointment/GetAvailableSlots
        [HttpGet("Appointment/GetAvailableSlots")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailableSlots(string doctorId, DateTime? date)
        {
            if (string.IsNullOrEmpty(doctorId) || !date.HasValue)
            {
                return Json(new List<string>());
            }

            // Define available hours (9:00 - 17:00)
            var slots = new List<string>();
            var selectedDate = date.Value.Date;

            for (int hour = 9; hour <= 17; hour++)
            {
                var slotTime = selectedDate.AddHours(hour);
                var isBooked = await _context.Appointments
                    .AnyAsync(a => a.DoctorId == doctorId &&
                                  a.AppointmentDate.Date == selectedDate &&
                                  a.AppointmentDate.Hour == hour &&
                                  a.Status != "Cancelled");

                if (!isBooked)
                {
                    slots.Add($"{hour:00}:00");
                }
            }

            return Json(slots);
        }

        // POST: /Api/Appointment/Create
        [HttpPost("Appointment/Create")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> Create([FromBody] CreateAppointmentRequest request)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is not Patient patient)
            {
                return Json(new { success = false, message = "Only patients can book appointments" });
            }

            if (request.DoctorId == null || request.AppointmentDate == null)
            {
                return Json(new { success = false, message = "Missing required fields" });
            }

            // Validate appointment date is in the future
            if (request.AppointmentDate.Value <= DateTime.UtcNow)
            {
                return Json(new { success = false, message = "Appointment date must be in the future" });
            }

            // Check for conflicts
            var conflict = await _context.Appointments
                .AnyAsync(a => a.DoctorId == request.DoctorId &&
                              a.AppointmentDate == request.AppointmentDate &&
                              a.Status != "Cancelled");

            if (conflict)
            {
                return Json(new { success = false, message = "This time slot is already booked" });
            }

            var appointment = new Appointment
            {
                PatientId = patient.Id,
                DoctorId = request.DoctorId,
                AppointmentDate = request.AppointmentDate.Value,
                Notes = request.Notes,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            return Json(new {
                success = true,
                message = "Appointment booked successfully",
                appointmentId = appointment.Id
            });
        }

        // GET: /Api/Department/GetAll
        [HttpGet("Department/GetAll")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDepartments(string? search = null)
        {
            var query = _context.Departments
                .Include(d => d.Doctors)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(d => d.Name.Contains(search) || d.Description.Contains(search));
            }

            var departments = await query.Select(d => new
            {
                id = d.Id,
                name = d.Name,
                description = d.Description,
                doctorCount = d.Doctors.Count
            }).ToListAsync();

            return Json(departments);
        }

        // GET: /Api/Doctors
        [HttpGet("Doctors")]
        [AllowAnonymous]
        public async Task<IActionResult> GetDoctors(int? departmentId, string? search = null)
        {
            var query = _context.Users
                .OfType<Doctor>()
                .Include(d => d.Department)
                .AsQueryable();

            if (departmentId.HasValue)
            {
                query = query.Where(d => d.DepartmentId == departmentId.Value);
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(d => d.FullName.Contains(search) ||
                                         d.Specialization.Contains(search) ||
                                         d.Email.Contains(search));
            }

            var doctors = await query.Select(d => new
            {
                id = d.Id,
                fullName = d.FullName,
                email = d.Email,
                phoneNumber = d.PhoneNumber,
                dateOfBirth = d.DateOfBirth,
                specialization = d.Specialization,
                consultationFee = d.ConsultationFee,
                departmentId = d.DepartmentId,
                departmentName = d.Department != null ? d.Department.Name : null
            }).ToListAsync();

            return Json(doctors);
        }

        // GET: /Api/Patients
        [HttpGet("Patients")]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> GetPatients(string? search = null)
        {
            var query = _context.Users
                .OfType<Patient>()
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.FullName.Contains(search) ||
                                         p.Email.Contains(search) ||
                                         p.PhoneNumber.Contains(search));
            }

            var patients = await query.Select(p => new
            {
                id = p.Id,
                fullName = p.FullName,
                email = p.Email,
                phoneNumber = p.PhoneNumber,
                dateOfBirth = p.DateOfBirth
            }).ToListAsync();

            return Json(patients);
        }

        // GET: /Api/Appointments
        [HttpGet("Appointments")]
        [Authorize]
        public async Task<IActionResult> GetAppointments(string? status = null, string? search = null)
        {
            var user = await _userManager.GetUserAsync(User);
            IQueryable<Appointment> query = _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .ThenInclude(d => (d as Doctor)!.Department)
                .OrderByDescending(a => a.AppointmentDate);

            // Filter by role
            if (User.IsInRole("Admin"))
            {
                // Admin sees all
            }
            else if (User.IsInRole("Doctor"))
            {
                if (user is Doctor doctor)
                {
                    query = query.Where(a => a.DoctorId == doctor.Id);
                }
            }
            else if (User.IsInRole("Patient"))
            {
                if (user is Patient patient)
                {
                    query = query.Where(a => a.PatientId == patient.Id);
                }
            }

            // Filter by status if provided
            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(a => a.Status == status);
            }

            // Filter by search if provided
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(a => a.Patient != null && a.Patient.FullName.Contains(search) ||
                                         a.Doctor != null && a.Doctor.FullName.Contains(search) ||
                                         a.Notes.Contains(search));
            }

            var appointments = await query.Select(a => new
            {
                id = a.Id,
                patientId = a.PatientId,
                patientName = a.Patient != null ? a.Patient.FullName : null,
                doctorId = a.DoctorId,
                doctorName = a.Doctor != null ? a.Doctor.FullName : null,
                doctorSpecialization = a.Doctor != null ? a.Doctor.Specialization : null,
                appointmentDate = a.AppointmentDate,
                status = a.Status,
                notes = a.Notes,
                createdAt = a.CreatedAt
            }).ToListAsync();

            return Json(appointments);
        }

        // GET: /Api/Statistics/Overview
        [HttpGet("Statistics/Overview")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetOverviewStats()
        {
            var totalDoctors = await _context.Users.OfType<Doctor>().CountAsync();
            var totalPatients = await _context.Users.OfType<Patient>().CountAsync();
            var totalAppointments = await _context.Appointments.CountAsync();
            var totalDepartments = await _context.Departments.CountAsync();

            var pendingAppointments = await _context.Appointments.CountAsync(a => a.Status == "Pending");
            var confirmedAppointments = await _context.Appointments.CountAsync(a => a.Status == "Confirmed");
            var completedAppointments = await _context.Appointments.CountAsync(a => a.Status == "Completed");
            var cancelledAppointments = await _context.Appointments.CountAsync(a => a.Status == "Cancelled");

            var todayAppointments = await _context.Appointments
                .CountAsync(a => a.AppointmentDate.Date == DateTime.Today);

            var weeklyRevenue = await _context.Appointments
                .Where(a => a.Status == "Completed" && a.AppointmentDate >= DateTime.Today.AddDays(-7))
                .Join(_context.Users.OfType<Doctor>(),
                      a => a.DoctorId,
                      d => d.Id,
                      (a, d) => d.ConsultationFee)
                .SumAsync(fee => (decimal?)fee) ?? 0;

            var data = new
            {
                totalDoctors,
                totalPatients,
                totalAppointments,
                totalDepartments,
                appointmentStatus = new
                {
                    pending = pendingAppointments,
                    confirmed = confirmedAppointments,
                    completed = completedAppointments,
                    cancelled = cancelledAppointments
                },
                todayAppointments,
                weeklyRevenue
            };

            return Json(data);
        }

        // GET: /Api/Statistics/MonthlyAppointments
        [HttpGet("Statistics/MonthlyAppointments")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetMonthlyAppointments(int? year = null)
        {
            var targetYear = year ?? DateTime.Today.Year;

            var monthlyData = await _context.Appointments
                .Where(a => a.AppointmentDate.Year == targetYear)
                .GroupBy(a => a.AppointmentDate.Month)
                .Select(g => new
                {
                    month = g.Key,
                    count = g.Count()
                })
                .OrderBy(x => x.month)
                .ToListAsync();

            // Ensure all 12 months are represented
            var result = new List<object>();
            for (int month = 1; month <= 12; month++)
            {
                var data = monthlyData.FirstOrDefault(m => m.month == month);
                result.Add(new
                {
                    month = month,
                    monthName = new DateTime(targetYear, month, 1).ToString("MMM"),
                    count = data?.count ?? 0
                });
            }

            return Json(result);
        }

        // GET: /Api/Statistics/DoctorsByDepartment
        [HttpGet("Statistics/DoctorsByDepartment")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetDoctorsByDepartment()
        {
            var data = await _context.Departments
                .Include(d => d.Doctors)
                .Select(d => new
                {
                    department = d.Name,
                    doctorCount = d.Doctors.Count
                })
                .OrderByDescending(x => x.doctorCount)
                .ToListAsync();

            return Json(data);
        }

        // GET: /Api/Statistics/TopDoctors
        [HttpGet("Statistics/TopDoctors")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetTopDoctors(int limit = 5)
        {
            var data = await _context.Users
                .OfType<Doctor>()
                .Select(d => new
                {
                    id = d.Id,
                    name = d.FullName,
                    specialization = d.Specialization,
                    department = d.Department != null ? d.Department.Name : null,
                    appointmentCount = _context.Appointments.Count(a => a.DoctorId == d.Id && (a.Status == "Completed" || a.Status == "Confirmed")),
                    totalRevenue = _context.Appointments
                        .Where(a => a.DoctorId == d.Id && a.Status == "Completed")
                        .Sum(a => (decimal?)d.ConsultationFee) ?? 0
                })
                .OrderByDescending(x => x.appointmentCount)
                .Take(limit)
                .ToListAsync();

            return Json(data);
        }

        // GET: /Api/Statistics/UpcomingAppointments
        [HttpGet("Statistics/UpcomingAppointments")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetUpcomingAppointments(int days = 7)
        {
            var startDate = DateTime.Today;
            var endDate = startDate.AddDays(days);

            var data = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Where(a => a.AppointmentDate >= startDate && a.AppointmentDate <= endDate && a.Status != "Cancelled")
                .OrderBy(a => a.AppointmentDate)
                .Select(a => new
                {
                    id = a.Id,
                    patientName = a.Patient != null ? a.Patient.FullName : null,
                    doctorName = a.Doctor != null ? a.Doctor.FullName : null,
                    appointmentDate = a.AppointmentDate,
                    status = a.Status
                })
                .ToListAsync();

            return Json(data);
        }

        // POST: /Api/Appointment/Confirm
        [HttpPost("Appointment/Confirm")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Confirm(int appointmentId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is not Doctor doctor)
            {
                return Json(new { success = false, message = "Only doctors can confirm appointments" });
            }

            var appointment = await _context.Appointments.FindAsync(appointmentId);
            if (appointment == null || appointment.DoctorId != doctor.Id)
            {
                return Json(new { success = false, message = "Appointment not found" });
            }

            appointment.Status = "Confirmed";
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Appointment confirmed" });
        }

        // POST: /Api/Appointment/Cancel
        [HttpPost("Appointment/Cancel")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Cancel(int appointmentId)
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = User.IsInRole("Admin");
            var isDoctor = User.IsInRole("Doctor");
            var isPatient = User.IsInRole("Patient");

            var appointment = await _context.Appointments.FindAsync(appointmentId);
            if (appointment == null)
            {
                return Json(new { success = false, message = "Appointment not found" });
            }

            if (isAdmin ||
                (isDoctor && user is Doctor d && appointment.DoctorId == d.Id) ||
                (isPatient && user is Patient p && appointment.PatientId == p.Id))
            {
                appointment.Status = "Cancelled";
                await _context.SaveChangesAsync();
                return Json(new { success = true, message = "Appointment cancelled" });
            }

            return Json(new { success = false, message = "Unauthorized" });
        }
    }

    public class CreateAppointmentRequest
    {
        public string? DoctorId { get; set; }
        public DateTime? AppointmentDate { get; set; }
        public string? Notes { get; set; }
    }
}