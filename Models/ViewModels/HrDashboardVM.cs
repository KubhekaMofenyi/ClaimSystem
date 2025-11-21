namespace ClaimSystem.Models
{
    public class HrDashboardVM
    {
        public string LecturerName { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }

        public int ClaimCount { get; set; }
        public decimal TotalHours { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
