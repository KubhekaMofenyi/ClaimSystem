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
        public IActionResult Create() => View(new Claim { Year = DateTime.Now.Year, Month = DateTime.Now.Month, Status = ClaimStatus.Submitted });

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = IdentitySeeder.LecturerRole)]
        public async Task<IActionResult> Create(Claim model)
        {
            if (!ModelState.IsValid) return View(model);
            var uid = _userManager.GetUserId(User)!;
            model.LecturerUserId = uid;
            model.Status = ClaimStatus.Draft;
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
                .FirstOrDefaultAsync(c => c.Id == id);

            if (claim == null) return NotFound();

            var history = await _db.ClaimStatusHistories
                .Where(h => h.ClaimId == id)
                .OrderByDescending(h => h.ChangedAtUtc)
                .AsNoTracking()
                .ToListAsync();

            ViewBag.StatusHistory = history;

            return View(claim);
        }


        // Add line item
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = IdentitySeeder.LecturerRole)]
        public async Task<IActionResult> AddLineItem(int claimId, ClaimLineItem item)
        {
            var uid = _userManager.GetUserId(User)!;
            var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == claimId && c.LecturerUserId == uid);
            if (claim == null || claim.Status != ClaimStatus.Draft)
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
            var claim = await _db.Claims.FindAsync(id);
            if (claim == null) return NotFound();

            var from = claim.Status;
            var user = User;

            bool isLecturer = user.IsInRole("Lecturer");
            bool isCoord = user.IsInRole("ProgrammeCoordinator");
            bool isManager = user.IsInRole("AcademicManager");

            bool allowed = false;

            // Lecturer transitions
            if (isLecturer)
            {
                if (from == ClaimStatus.Draft && status == ClaimStatus.Submitted) allowed = true;
                if (from == ClaimStatus.Submitted && status == ClaimStatus.Draft) allowed = true; // optional reopen
            }

            // Coordinator transitions (recommendations only; not final)
            if (isCoord)
            {
                // start review
                if (from == ClaimStatus.Submitted && status == ClaimStatus.UnderReview) allowed = true;

                // while reviewing, coordinator can set a recommendation (and revise it)
                if ((from == ClaimStatus.UnderReview || from == ClaimStatus.CoordinatorApproved || from == ClaimStatus.CoordinatorRejected) &&
                    (status == ClaimStatus.CoordinatorApproved || status == ClaimStatus.CoordinatorRejected))
                    allowed = true;
            }

            // Manager transitions (final)
            if (isManager)
            {
                var reviewable = new[] { ClaimStatus.UnderReview, ClaimStatus.CoordinatorApproved, ClaimStatus.CoordinatorRejected };
                if (reviewable.Contains(from) && (status == ClaimStatus.Approved || status == ClaimStatus.Rejected))
                    allowed = true;
            }

            if (isManager && (from == ClaimStatus.Approved || from == ClaimStatus.Rejected) && status == ClaimStatus.UnderReview)
            {
                allowed = true; // allow reopening finalized claims
            }

            if (!allowed)
            {
                TempData["err"] = "You don’t have permission to change to that status from the current state.";
                return RedirectToAction(nameof(Details), new { id });
            }

            claim.Status = status;

            _db.ClaimStatusHistories.Add(new ClaimStatusHistory
            {
                ClaimId = claim.Id,
                From = from,
                To = status,
                ChangedAtUtc = DateTime.UtcNow,
                ChangedBy = User.Identity?.Name
            });

            await _db.SaveChangesAsync();

            TempData["ok"] = $"Status set to {status}.";
            return RedirectToAction(nameof(Details), new { id });
        }



        // 4) Review queue
        [Authorize(Roles = IdentitySeeder.CoordinatorRole + "," + IdentitySeeder.ManagerRole)]
        public async Task<IActionResult> Review()
        {
            var reviewable = new[] {
        ClaimStatus.Submitted,
        ClaimStatus.UnderReview,
        ClaimStatus.CoordinatorApproved,
        ClaimStatus.CoordinatorRejected
    };

            var list = await _db.Claims
                .Include(c => c.LineItems)
                .Where(c => reviewable.Contains(c.Status))
                .OrderBy(c => c.Status)
                .ThenByDescending(c => c.Year).ThenByDescending(c => c.Month)
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
        // ...

        [Authorize(Roles = "AcademicManager")]
        public async Task<IActionResult> Delete(int id)
        {
            var claim = await _db.Claims
                .Include(c => c.LineItems)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (claim == null) return NotFound();

            return View(claim); // simple confirm page
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "AcademicManager")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var claim = await _db.Claims
                .Include(c => c.LineItems)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (claim == null)
            {
                TempData["err"] = "That claim no longer exists.";
                return RedirectToAction(nameof(Index));
            }

            // remove any status history
            var history = _db.ClaimStatusHistories.Where(h => h.ClaimId == id);
            _db.ClaimStatusHistories.RemoveRange(history);

            // delete physical files
            foreach (var d in claim.Documents)
            {
                // StoragePath looks like "/uploads/<file>"
                if (!string.IsNullOrWhiteSpace(d.StoragePath))
                {
                    // handle both absolute and web-root relative
                    var path = d.StoragePath.StartsWith("/")
                        ? Path.Combine(_env.WebRootPath, d.StoragePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))
                        : Path.Combine(_env.WebRootPath, d.StoragePath.Replace('/', Path.DirectorySeparatorChar));

                    try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
                    catch { /* swallow file IO errors; DB will still be consistent */ }
                }
            }

            // remove children then parent
            _db.SupportingDocuments.RemoveRange(claim.Documents);
            _db.ClaimLineItems.RemoveRange(claim.LineItems);
            _db.Claims.Remove(claim);

            await _db.SaveChangesAsync();
            TempData["ok"] = "Claim deleted.";
            return RedirectToAction(nameof(Index));
        }

    }
}
