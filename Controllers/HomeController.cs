using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MedicalBooking.Data;
using MedicalBooking.Models;

namespace MedicalBooking.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, IMemoryCache cache)
    {
        _logger = logger;
        _context = context;
        _cache = cache;
    }

    public IActionResult Index()
    {
        // Cache department count (1 hour)
        var departmentCount = _cache.GetOrCreate("DepartmentCount", entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            return _context.Departments.Count();
        });

        // Cache active doctors count (15 minutes)
        var doctorCount = _cache.GetOrCreate("DoctorCount", entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(15);
            return _context.Users.OfType<Doctor>().Count();
        });

        // Cache recent appointments (1 minute)
        var recentAppointments = _cache.GetOrCreate("RecentAppointments", entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(1);
            return _context.Appointments
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .ToList();
        });

        ViewBag.DepartmentCount = departmentCount;
        ViewBag.DoctorCount = doctorCount;
        ViewBag.RecentAppointments = recentAppointments;

        return View();
    }

    [Authorize(Roles = "Admin")]
    public IActionResult Statistics()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
