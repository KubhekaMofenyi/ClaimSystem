using ClaimSystem.Data;
using ClaimSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace ClaimSystem.Controllers
{
    [Authorize(Roles = "AcademicManager")]   // only manager/HR
    public class HrController : Controller
    {
        private readonly ClaimDbContext _db;

        public HrController(ClaimDbContext db)
        {
            _db = db;
        }

        // HR dashboard: summary by lecturer & month
        public async Task<IActionResult> Index(int? year, int? month)
        {
            // STEP 1 — Load from DB FIRST (no aggregates)
            var claims = await _db.Claims
                .Include(c => c.LineItems)
                .Where(c => c.Status == ClaimStatus.Approved)
                .ToListAsync();

            // STEP 2 — Apply filters in-memory
            if (year.HasValue)
                claims = claims.Where(c => c.Year == year.Value).ToList();

            if (month.HasValue)
                claims = claims.Where(c => c.Month == month.Value).ToList();

            // STEP 3 — Perform grouping + aggregates IN MEMORY
            var summaries = claims
                .GroupBy(c => new { c.LecturerName, c.Year, c.Month })
                .Select(g => new HrClaimSummaryVm
                {
                    LecturerName = g.Key.LecturerName,
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    ClaimCount = g.Count(),
                    TotalHours = g.SelectMany(c => c.LineItems).Sum(li => li.Hours),
                    TotalAmount = g.Sum(c => c.TotalAmount)
                })
                .OrderBy(s => s.LecturerName)
                .ThenBy(s => s.Year)
                .ThenBy(s => s.Month)
                .ToList();

            // Filter dropdowns
            ViewBag.SelectedYear = year;
            ViewBag.SelectedMonth = month;

            ViewBag.Years = await _db.Claims
                .Select(c => c.Year)
                .Distinct()
                .OrderBy(y => y)
                .ToListAsync();

            ViewBag.Months = Enumerable.Range(1, 12).ToList();

            return View(summaries);
        }

        // Detailed invoice-style view for one lecturer & period
        public async Task<IActionResult> Details(string lecturer, int year, int month)
        {
            if (string.IsNullOrWhiteSpace(lecturer))
                return NotFound();

            var claims = await _db.Claims
                .Include(c => c.LineItems)
                .Where(c =>
                    c.Status == ClaimStatus.Approved &&
                    c.LecturerName == lecturer &&
                    c.Year == year &&
                    c.Month == month)
                .ToListAsync();

            if (!claims.Any())
                return NotFound();

            ViewBag.Lecturer = lecturer;
            ViewBag.Year = year;
            ViewBag.Month = month;
            ViewBag.TotalAmount = claims.Sum(c => c.TotalAmount);
            ViewBag.TotalHours = claims.SelectMany(c => c.LineItems).Sum(li => li.Hours);

            return View(claims);
        }
    }
}
