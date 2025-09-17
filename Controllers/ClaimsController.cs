using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClaimSystem.Data;
using ClaimSystem.Models;

namespace ClaimSystem.Controllers
{
    public class ClaimsController : Controller
    {   
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public ClaimsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db; _env = env;
        }

        // 1) LIST / Dashboard
        public async Task<IActionResult> Index()
        {
            var claims = await _db.Claims
                .Include(c => c.LineItems)
                .AsNoTracking()
                .OrderByDescending(c => c.Year).ThenByDescending(c => c.Month)
                .ToListAsync();
            return View(claims);
        }

        // 2) CREATE (GET/POST)
        public IActionResult Create() => View(new Claim { Year = DateTime.Now.Year, Month = DateTime.Now.Month });

        [HttpPost]
        public async Task<IActionResult> Create(Claim model)
        {
            if (!ModelState.IsValid) return View(model);
            _db.Claims.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = model.Id });
        }

        // 3) DETAILS (shows line items + documents + status actions)
        public async Task<IActionResult> Details(int id)
        {
            var claim = await _db.Claims
                .Include(c => c.LineItems)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (claim == null) return NotFound();
            return View(claim);
        }

        // Add line item
        [HttpPost]
        public async Task<IActionResult> AddLineItem(int claimId, ClaimLineItem item)
        {
            var claim = await _db.Claims.FindAsync(claimId);
            if (claim == null) return NotFound();
            item.ClaimId = claimId;
            _db.ClaimLineItems.Add(item);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = claimId });
        }

        // Upload document
        [HttpPost]
        public async Task<IActionResult> Upload(int claimId, IFormFile file)
        {
            var claim = await _db.Claims.FindAsync(claimId);
            if (claim == null || file == null || file.Length == 0) return RedirectToAction(nameof(Details), new { id = claimId });

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsDir);
            var savedName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var savePath = Path.Combine(uploadsDir, savedName);
            using (var fs = System.IO.File.Create(savePath)) { await file.CopyToAsync(fs); }

            _db.SupportingDocuments.Add(new SupportingDocument
            {
                ClaimId = claimId,
                FileName = file.FileName,
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                StoragePath = $"/uploads/{savedName}"
            });
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id = claimId });
        }

        // Change status (for screenshots; no auth)
        [HttpPost]
        public async Task<IActionResult> SetStatus(int id, ClaimStatus status)
        {
            var claim = await _db.Claims.FindAsync(id);
            if (claim == null) return NotFound();
            claim.Status = status;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }

        // 4) Review queue
        public async Task<IActionResult> Review()
        {
            var toReview = await _db.Claims
                .Include(c => c.LineItems)
                .Where(c => c.Status == ClaimStatus.Submitted || c.Status == ClaimStatus.UnderReview)
                .ToListAsync();
            return View(toReview);
        }
    }
}
