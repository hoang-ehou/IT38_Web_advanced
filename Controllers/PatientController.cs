using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MedicalBooking.Data;
using MedicalBooking.Models;

namespace MedicalBooking.Controllers
{
    [Authorize(Roles = "Admin,Patient")]
    public class PatientController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public PatientController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        // GET: /Patient
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var patients = await _context.Users
                .OfType<Patient>()
                .Include(p => p.Appointments)
                .ToListAsync();
            return View(patients);
        }

        // GET: /Patient/Create
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: /Patient/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(Patient model)
        {
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

            var patient = new Patient
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                DateOfBirth = model.DateOfBirth
            };

            var result = await _userManager.CreateAsync(patient, "Pass123!");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(patient, "Patient");
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // GET: /Patient/Profile
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !(user is Patient))
            {
                return NotFound();
            }
            var patient = user as Patient;
            return View(patient);
        }

        // GET: /Patient/Edit/5
        public async Task<IActionResult> Edit(string? id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return NotFound();

            var userId = string.IsNullOrEmpty(id) ? currentUser.Id : id;

            if (!User.IsInRole("Admin") && currentUser.Id != userId)
            {
                return Forbid();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || !(user is Patient))
            {
                return NotFound();
            }

            return View(user as Patient);
        }

        // POST: /Patient/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string? id, Patient model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return NotFound();

            var userId = string.IsNullOrEmpty(id) ? currentUser.Id : id;

            if (userId != model.Id)
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null || !(user is Patient))
            {
                return NotFound();
            }

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
                return View(model);
            }
            return View(model);
        }

        // POST: /Patient/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null && user is Patient patient)
            {
                // Delete associated appointments first due to Restrict constraint
                var appointments = _context.Appointments.Where(a => a.PatientId == patient.Id);
                _context.Appointments.RemoveRange(appointments);
                await _context.SaveChangesAsync();

                // Delete associated medical records
                var medicalRecords = _context.MedicalRecords.Where(m => m.PatientId == patient.Id);
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
