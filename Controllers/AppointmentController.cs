using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedicalBooking.Data;
using MedicalBooking.Models;

namespace MedicalBooking.Controllers
{
    [Authorize]
    public class AppointmentController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public AppointmentController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: /Appointment
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var isAdmin = User.IsInRole("Admin");
            var isDoctor = User.IsInRole("Doctor");
            var isPatient = User.IsInRole("Patient");

            ViewBag.CurrentUserId = user?.Id;

            if (isAdmin)
            {
                var allAppointments = await _context.Appointments
                    .Include(a => a.Patient)
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d!.Department)
                    .OrderByDescending(a => a.AppointmentDate)
                    .ToListAsync();
                return View(allAppointments);
            }
            else if (isDoctor && user is Doctor doctor)
            {
                var doctorAppointments = await _context.Appointments
                    .Where(a => a.DoctorId == doctor.Id)
                    .Include(a => a.Patient)
                    .OrderByDescending(a => a.AppointmentDate)
                    .ToListAsync();
                return View(doctorAppointments);
            }
            else if (isPatient && user is Patient patient)
            {
                var patientAppointments = await _context.Appointments
                    .Where(a => a.PatientId == patient.Id)
                    .Include(a => a.Doctor)
                        .ThenInclude(d => d!.Department)
                    .OrderByDescending(a => a.AppointmentDate)
                    .ToListAsync();
                return View(patientAppointments);
            }

            return Forbid();
        }

        // GET: /Appointment/Book
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> Book()
        {
            ViewBag.Doctors = await _context.Users
                .OfType<Doctor>()
                .Include(d => d.Department)
                .ToListAsync();
            return View();
        }

        // POST: /Appointment/Book
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Patient")]
        public async Task<IActionResult> Book(Appointment appointment)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is not Patient patient)
            {
                return Forbid();
            }

            ViewBag.Doctors = await _context.Users
                .OfType<Doctor>()
                .Include(d => d.Department)
                .ToListAsync();

            if (!ModelState.IsValid)
            {
                return View(appointment);
            }

            // Validate: appointment date must be in future
            if (appointment.AppointmentDate <= DateTime.UtcNow)
            {
                ModelState.AddModelError("AppointmentDate", "Appointment date must be in the future");
                return View(appointment);
            }

            appointment.PatientId = patient.Id;
            appointment.Status = "Pending";
            appointment.CreatedAt = DateTime.UtcNow;

            // Check for conflicts: existing appointment with same doctor at same time
            var conflict = await _context.Appointments
                .AnyAsync(a => a.DoctorId == appointment.DoctorId && 
                              a.AppointmentDate == appointment.AppointmentDate && 
                              a.Status != "Cancelled");
            if (conflict)
            {
                ModelState.AddModelError("AppointmentDate", "This time slot is already booked");
                return View(appointment);
            }

            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: /Appointment/Confirm/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Doctor")]
        public async Task<IActionResult> Confirm(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user is not Doctor doctor || appointment.DoctorId != doctor.Id)
            {
                return Forbid();
            }

            appointment.Status = "Confirmed";
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: /Appointment/Complete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Doctor")]
        public async Task<IActionResult> Complete(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var isAdmin = User.IsInRole("Admin");
            var isDoctor = User.IsInRole("Doctor");

            // Admin can complete any, Doctor can complete only their own
            if (!isAdmin && (!isDoctor || !(user is Doctor d) || appointment.DoctorId != d.Id))
            {
                return Forbid();
            }

            // Only allow completing Confirmed or Pending appointments
            if (appointment.Status != "Confirmed" && appointment.Status != "Pending")
            {
                return BadRequest("Cannot complete an appointment that is not Confirmed or Pending");
            }

            appointment.Status = "Completed";
            await _context.SaveChangesAsync();

            // If AJAX request, return JSON
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true });
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Appointment/Cancel/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Cancel(int id)
        {
            var appointment = await _context.Appointments.FindAsync(id);
            if (appointment == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var isAdmin = User.IsInRole("Admin");
            var isDoctor = User.IsInRole("Doctor");
            var isPatient = User.IsInRole("Patient");

            if (isAdmin || (isDoctor && user is Doctor d && appointment.DoctorId == d.Id) || (isPatient && user is Patient p && appointment.PatientId == p.Id))
            {
                appointment.Status = "Cancelled";
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return Forbid();
        }

        // GET: /Appointment/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                    .ThenInclude(d => d!.Department)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var isAdmin = User.IsInRole("Admin");
            var isDoctor = User.IsInRole("Doctor");
            var isPatient = User.IsInRole("Patient");

            if (isAdmin || (isDoctor && user is Doctor d && appointment.DoctorId == d.Id) || (isPatient && user is Patient p && appointment.PatientId == p.Id))
            {
                return View(appointment);
            }

            return Forbid();
        }
    }
}
