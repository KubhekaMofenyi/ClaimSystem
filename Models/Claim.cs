using System.ComponentModel.DataAnnotations;

namespace ClaimSystem.Models
{
    public class Claim
    {
        public int Id { get; set; }

        [Required] public string LecturerName { get; set; } = "";
        [Required] public string ModuleCode { get; set; } = "";
        [Range(2020, 2100)] public int Year { get; set; }
        [Range(1, 12)] public int Month { get; set; }

        //for logins
        public string LecturerUserId { get; set; } = "";
        public string? CoordinatorUserId { get; set; }
        public string? ManagerUserId { get; set; }


        public ClaimStatus Status { get; set; } = ClaimStatus.Draft;
        public decimal TotalAmount => LineItems?.Sum(li => li.Amount) ?? 0m;

        public List<ClaimLineItem> LineItems { get; set; } = new();
        public List<SupportingDocument> Documents { get; set; } = new();
        public List<ClaimStatusHistory> StatusHistory { get; set; } = new();

        public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAtUtc { get; set; }
    }
    public class ClaimLineItem
    {
        public int Id { get; set; }
        public int ClaimId { get; set; }
        public Claim? Claim { get; set; }

        [DataType(DataType.Date)] public DateTime Date { get; set; } = DateTime.Today;
        [Range(0, 24)] public decimal Hours { get; set; }
        [Range(0, 99999)] public decimal RatePerHour { get; set; }
        public decimal Amount => Math.Round(Hours * RatePerHour);
        public string? Notes { get; set; }
    }

    public class SupportingDocument
    {
        public int Id { get; set; }
        public int ClaimId { get; set; }
        public Claim? Claim { get; set; }

        public string FileName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public long SizeBytes { get; set; }
        public string StoragePath { get; set; } = ""; // /uploads/...
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
