namespace ClaimSystem.Models
{
    public class LecturerDashboardVM
    {
        public int ClaimCount { get; set; }

        public decimal TotalHours { get; set; }

        public decimal TotalAmount { get; set; }

        // optional breakdowns (nice for badges)
        public decimal ApprovedAmount { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal RejectedAmount { get; set; }
    }
}
