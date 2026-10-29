namespace ImpactNexus.Web.Models;

public enum StakeholderType
{
    CommunityMember = 1,
    Volunteer = 2,
    Donor = 3,
    Beneficiary = 4,
    Partner = 5
}

public enum ActivityStatus
{
    Planned = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4
}

public enum DonationStatus
{
    Pledged = 1,
    Received = 2,
    Allocated = 3,
    Cancelled = 4
}

public enum BursaryApplicationStatus
{
    Submitted = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4,
    Withdrawn = 5
}

public enum ProgrammeStatus
{
    Planned = 1,
    Active = 2,
    Completed = 3,
    Cancelled = 4
}
