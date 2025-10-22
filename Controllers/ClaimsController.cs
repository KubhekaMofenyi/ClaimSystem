using ClaimSystem.Data;
using ClaimSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;


namespace ClaimSystem.Controllers
{
    public class ClaimsController : Controller
    {   
        private readonly ClaimDbContext _db;
        private readonly IWebHostEnvironment _env;
        //logger mainly for upload errors
        private readonly ILogger<ClaimsController> _logger;
        //login
        private readonly UserManager<IdentityUser> _userManager;

        public ClaimsController(ClaimDbContext db, IWebHostEnvironment env, ILogger<ClaimsController> logger, UserManager<IdentityUser> userManager)
        {
            _db = db;
            _env = env;
            _logger = logger;
            _userManager = userManager;
        }

        //for unit tests
        private static RedirectToActionResult GoToDetails(int id) =>
            new RedirectToActionResult(nameof(Details), "Claims", new { id });

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
        [Authorize(Roles = IdentitySeeder.LecturerRole)]
        public async Task<IActionResult> Create(Claim model)
        {
            if (!ModelState.IsValid) return View(model);
            var uid = _userManager.GetUserId(User)!;
            model.LecturerUserId = uid;
            model.Status = ClaimStatus.Submitted;
            _db.Claims.Add(model);
            await _db.SaveChangesAsync();
            return GoToDetails(model.Id);
        }

        // 3) DETAILS (shows line items + documents + status actions)
        public async Task<IActionResult> Details(int id)
        {
            var claim = await _db.Claims
                .Include(c => c.LineItems)
                .Include(c => c.Documents)
                .Include(c => c.StatusHistory)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (claim == null) return NotFound();
            return View(claim);
        }

        // Add line item
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = IdentitySeeder.LecturerRole)]
        public async Task<IActionResult> AddLineItem(int claimId, ClaimLineItem item)
        {
            var uid = _userManager.GetUserId(User)!;
            var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == claimId && c.LecturerUserId == uid);
            if (claim == null || claim.Status != ClaimStatus.Draft && claim.Status != ClaimStatus.Submitted)
            { TempData["Err"] = "You can only edit your own draft/submitted claims."; return GoToDetails(claimId); }

            if (!ModelState.IsValid || !TryValidateModel(item))
            { TempData["Err"] = "Invalid line item."; return GoToDetails(claimId); }

            item.ClaimId = claimId;
            _db.ClaimLineItems.Add(item);
            await _db.SaveChangesAsync();
            TempData["Ok"] = "Line item added.";
            return GoToDetails(claimId);
        }

        // Upload document
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = IdentitySeeder.LecturerRole)]
        public async Task<IActionResult> Upload(int claimId, IFormFile? file)
        {
            try
            {
                var claim = await _db.Claims.AsNoTracking().FirstOrDefaultAsync(c => c.Id == claimId);
                if (claim == null) return NotFound();

                if (file == null || file.Length == 0)
                {
                    TempData["Err"] = "Please choose a file.";
                    return GoToDetails(claimId);
                }

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
                var savePath = Path.Combine(uploadsDir, safeName);

                await using (var fs = System.IO.File.Create(savePath))
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed for claim {ClaimId}", claimId);
                TempData["Err"] = "Upload failed. Please try again or contact support.";
                return GoToDetails(claimId);
            }
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
            try
            {
                var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == id);
                if (claim == null) return NotFound();

                if (IsValidTransition(claim.Status, status))
                {
                    var from = claim.Status;
                    claim.Status = status;
                    if (status is ClaimStatus.Approved or ClaimStatus.Rejected)
                        claim.ReviewedAtUtc = DateTime.UtcNow;

                    _db.ClaimStatusHistories.Add(new ClaimStatusHistory
                    {
                        ClaimId = claim.Id,
                        From = from,
                        To = status,
                        ChangedAtUtc = DateTime.UtcNow,
                        ChangedBy = "Coordinator" // placeholder
                    });

                    await _db.SaveChangesAsync();
                    TempData["Ok"] = $"Status set to {claim.Status}.";
                }
                else
                {
                    TempData["Err"] = $"Invalid transition from {claim.Status} to {status}.";
                }
                return GoToDetails(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SetStatus failed for claim {ClaimId} to {Status}", id, status);
                TempData["Err"] = "Could not update status.";
                return GoToDetails(id);
            }
        }

        // 4) Review queue
        [Authorize(Roles = IdentitySeeder.CoordinatorRole)]
        public async Task<IActionResult> Review(string? q, int? year, int? month)
        {
            var query = _db.Claims
                .AsNoTracking()
                .Include(c => c.LineItems)
                .Where(c => c.Status == ClaimStatus.Submitted || c.Status == ClaimStatus.UnderReview);

            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(c => c.LecturerName.Contains(q) || c.ModuleCode.Contains(q));

            if (year is not null) query = query.Where(c => c.Year == year);
            if (month is not null) query = query.Where(c => c.Month == month);

            var list = await query
                .OrderByDescending(c => c.Year).ThenByDescending(c => c.Month)
                .ThenBy(c => c.LecturerName)
                .ToListAsync();

            return View(list);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = IdentitySeeder.CoordinatorRole)]
        public async Task<IActionResult> CoordinatorDecision(int id, bool approve)
        {
            var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == id);
            if (claim == null) return NotFound();

            var from = claim.Status;

            // Allowed: Submitted/UnderReview → CoordinatorApproved/CoordinatorRejected
            if (from == ClaimStatus.Submitted || from == ClaimStatus.UnderReview)
            {
                claim.Status = approve ? ClaimStatus.CoordinatorApproved : ClaimStatus.CoordinatorRejected;
                claim.CoordinatorUserId = _userManager.GetUserId(User)!;
                _db.ClaimStatusHistories.Add(new ClaimStatusHistory { ClaimId = id, From = from, To = claim.Status });
                await _db.SaveChangesAsync();
                TempData["Ok"] = $"Coordinator {(approve ? "approved" : "rejected")} claim.";
            }
            else TempData["Err"] = $"Invalid stage for coordinator decision: {from}";

            return GoToDetails(id);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = IdentitySeeder.ManagerRole)]
        public async Task<IActionResult> ManagerDecision(int id, bool approve)
        {
            var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == id);
            if (claim == null) return NotFound();

            var from = claim.Status;

            // Manager has the final say regardless of coordinator’s decision:
            claim.Status = approve ? ClaimStatus.ManagerApproved : ClaimStatus.ManagerRejected;
            claim.ManagerUserId = _userManager.GetUserId(User)!;
            claim.ReviewedAtUtc = DateTime.UtcNow;

            _db.ClaimStatusHistories.Add(new ClaimStatusHistory { ClaimId = id, From = from, To = claim.Status });
            await _db.SaveChangesAsync();

            TempData["Ok"] = $"Manager {(approve ? "approved" : "rejected")} claim (final).";
            return GoToDetails(id);
        }
    }
}
