using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClaimSystem.Data;
using ClaimSystem.Models;

namespace ClaimSystem.Controllers
{
    public class ClaimsController : Controller
    {   
        private readonly ClaimDbContext _db;
        private readonly IWebHostEnvironment _env;
        //for unit tests
        private static RedirectToActionResult GoToDetails(int id) =>
            new RedirectToActionResult(nameof(Details), "Claims", new { id });

        public ClaimsController(ClaimDbContext db, IWebHostEnvironment env)
        {
            _db = db; _env = env;
        }

        // 1) LIST / Dashboard
        public async Task<IActionResult> Index()
        {
            var claims = await _db.Claims
                .AsNoTracking()
                .Include(c => c.LineItems)
                .OrderByDescending(c => c.Year).ThenByDescending(c => c.Month)
                .ToListAsync();

            return View(claims);
        }

        // 2) CREATE (GET/POST)
        public IActionResult Create() => View(new Claim {Year = DateTime.Now.Year, Month = DateTime.Now.Month, Status = ClaimStatus.Submitted});

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("LecturerName,ModuleCode,Year,Month,Status")] Claim model)
        {
            if (!ModelState.IsValid) return View(model);
            _db.Claims.Add(model);
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Claim created.";
            return GoToDetails(model.Id);
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
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> AddLineItem(int claimId, [Bind("Date,Hours,RatePerHour,Notes")] ClaimLineItem item)
        {
            var claim = await _db.Claims.AsNoTracking().FirstOrDefaultAsync(c => c.Id == claimId);
            if (claim == null) return NotFound();
            if (!ModelState.IsValid || !TryValidateModel(item))
            {
                TempData["Err"] = "Line item invalid. Please check date/hours/rate.";
                return GoToDetails(claimId);
            }
            item.ClaimId = claimId;
            _db.ClaimLineItems.Add(item);
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Line item added.";
            return GoToDetails(claimId);
        }

        // Upload document
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(int claimId, IFormFile? file)
        {
            var claim = await _db.Claims.AsNoTracking().FirstOrDefaultAsync(c => c.Id == claimId);
            if (claim == null) return NotFound();
            if (file == null || file.Length == 0)
            {
                TempData["Err"] = "Please choose a file.";
                return GoToDetails(claimId);
            }

            // config with safe fallbacks
            var cfg = HttpContext?.RequestServices?.GetService<IConfiguration>();
            var allowed = cfg?.GetSection("Upload:Allowed").Get<string[]>() ?? new[] { ".pdf", ".docx", ".xlsx" };
            var maxBytes = cfg?.GetValue<long>("Upload:MaxBytes") ?? 10 * 1024 * 1024;

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext) || file.Length > maxBytes)
            {
                TempData["Err"] = "Invalid file. Allowed: .pdf, .docx, .xlsx and ≤ 10 MB.";
                return GoToDetails(claimId);
            }

            var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsDir);
            var safeName = $"{Guid.NewGuid()}{ext}";
            using (var fs = System.IO.File.Create(Path.Combine(uploadsDir, safeName)))
                await file.CopyToAsync(fs);

            _db.SupportingDocuments.Add(new SupportingDocument
            {
                ClaimId = claimId,
                FileName = Path.GetFileName(file.FileName),
                ContentType = file.ContentType,
                SizeBytes = file.Length,
                StoragePath = $"/uploads/{safeName}",
                UploadedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Document uploaded.";
            return GoToDetails(claimId);
        }

        // Change status
        private static bool IsValidTransition(ClaimStatus from, ClaimStatus to) => // prevents jumping from draft to approved
            (from, to) switch
            {
                (ClaimStatus.Draft, ClaimStatus.Submitted) => true,
                (ClaimStatus.Submitted, ClaimStatus.UnderReview) => true,
                (ClaimStatus.Submitted, ClaimStatus.Approved) => true,
                (ClaimStatus.Submitted, ClaimStatus.Rejected) => true,
                (ClaimStatus.UnderReview, ClaimStatus.Approved) => true,
                (ClaimStatus.UnderReview, ClaimStatus.Rejected) => true,
                _ => false
            };

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetStatus(int id, ClaimStatus status)
        {
            var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == id);
            if (claim == null) return NotFound();

            if (IsValidTransition(claim.Status, status))
            {
                claim.Status = status;
                if (status is ClaimStatus.Approved or ClaimStatus.Rejected)
                    claim.ReviewedAtUtc = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                TempData["Ok"] = $"Status set to {claim.Status}.";
            }
            else
            {
                TempData["Err"] = $"Invalid transition from {claim.Status} to {status}.";
            }

            return GoToDetails(id);
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
