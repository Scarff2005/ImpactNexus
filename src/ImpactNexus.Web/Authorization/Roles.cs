namespace ImpactNexus.Web.Authorization;

public static class Roles
{
    public const string Administrator = "IT Administrator";
    public const string CsrManager = "CSR Manager";
    public const string Investor = "Investor / Corporate Donor";
    public const string CommunityMember = "Community Member / Volunteer";

    public static readonly string[] All =
    {
        Administrator,
        CsrManager,
        Investor,
        CommunityMember
    };

    public static readonly string[] Staff = { Administrator, CsrManager };
}

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string StaffOnly = "StaffOnly";
    public const string CsrManagerOnly = "CsrManagerOnly";
    public const string InvestorOnly = "InvestorOnly";
    public const string CommunityOnly = "CommunityOnly";
    public const string Authenticated = "Authenticated";
}
