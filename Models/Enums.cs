namespace ClaimSystem.Models
{
    public enum Role { Lecturer, ProgrammeCoordinator, AcademicManager }
    public enum ClaimStatus { Draft = 0, Submitted = 1, UnderReview = 2, CoordinatorApproved = 3, CoordinatorRejected = 4, ManagerApproved = 5, ManagerRejected = 6, Approved = ManagerApproved, Rejected = ManagerRejected }
}
