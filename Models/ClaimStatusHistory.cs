namespace ClaimSystem.Models
{
    public class ClaimStatusHistory
    {
        public int Id { get; set; }
        public int ClaimId { get; set; }
        public Claim? Claim { get; set; }
        public ClaimStatus From { get; set; }
        public ClaimStatus To { get; set; }
        public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
        public string? ChangedBy { get; set; }  // for auth later
    }
}
