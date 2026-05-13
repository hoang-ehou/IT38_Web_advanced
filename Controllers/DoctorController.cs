using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedicalBooking.Data;
using MedicalBooking.Models;

namespace MedicalBooking.Controllers
{
    [Authorize(Roles = "Admin,Doctor")]
    public class DoctorController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public DoctorController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: /Doctor
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var doctors = await _context.Users
                .OfType<Doctor>()
                .Include(d => d.Department)
                .Include(d => d.Appointments)
                .ToListAsync();
            return View(doctors);
        }

        // GET: /Doctor/Create
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Departments = await _context.Departments.ToListAsync();
            return View();
        }

        // POST: /Doctor/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Doctor model)
        {
            ViewBag.Departments = await _context.Departments.ToListAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Check if email already exists
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("Email", "Email already exists");
                return View(model);
            }

            var doctor = new Doctor
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                DateOfBirth = model.DateOfBirth,
                DepartmentId = model.DepartmentId,
                Specialization = model.Specialization,
                ConsultationFee = model.ConsultationFee
            };

            var result = await _userManager.CreateAsync(doctor, "Pass123!");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(doctor, "Doctor");
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // GET: /Doctor/Profile
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !(user is Doctor))
            {
                return NotFound();
            }
            var doctor = user as Doctor;
            return View(doctor);
        }

        // GET: /Doctor/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !(user is Doctor))
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (!User.IsInRole("Admin") && currentUser.Id != user.Id)
            {
                return Forbid();
            }

            var doctor = user as Doctor;
            ViewBag.Departments = await _context.Departments.ToListAsync();
            return View(doctor);
        }

        // POST: /Doctor/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Doctor model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null || !(user is Doctor))
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (!User.IsInRole("Admin") && currentUser.Id != user.Id)
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    user.FullName = model.FullName;
                    user.PhoneNumber = model.PhoneNumber;
                    user.DateOfBirth = model.DateOfBirth;
                    
                    if (user is Doctor doctor)
                    {
                        doctor.Specialization = model.Specialization;
                        doctor.ConsultationFee = model.ConsultationFee;
                        doctor.DepartmentId = model.DepartmentId;
                    }

                    var result = await _userManager.UpdateAsync(user);
                    if (result.Succeeded)
                    {
                        if (User.IsInRole("Admin"))
                            return RedirectToAction(nameof(Index));
                        return RedirectToAction(nameof(Profile));
                    }

                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(model.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                ViewBag.Departments = await _context.Departments.ToListAsync();
                return View(model);
            }
            ViewBag.Departments = await _context.Departments.ToListAsync();
            return View(model);
        }

        // POST: /Doctor/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null && user is Doctor doctor)
            {
                // Delete associated appointments first
                var appointments = _context.Appointments.Where(a => a.DoctorId == doctor.Id);
                _context.Appointments.RemoveRange(appointments);
                await _context.SaveChangesAsync();

                // Delete associated medical records
                var medicalRecords = _context.MedicalRecords.Where(m => m.DoctorId == doctor.Id);
                _context.MedicalRecords.RemoveRange(medicalRecords);
                await _context.SaveChangesAsync();

                var result = await _userManager.DeleteAsync(user);
            }
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(string id)
        {
            return _context.Users.Any(u => u.Id == id);
        }
    }
}
