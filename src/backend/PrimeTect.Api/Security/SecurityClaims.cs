namespace PrimeTect.Api.Security;

/// <summary>Claim types used by the PrimeTect authentication cookie.</summary>
public static class SecurityClaims
{
    /// <summary>Carries the Identity security stamp so a stamp change invalidates the cookie immediately.</summary>
    public const string SecurityStamp = "primetect:stamp";

    /// <summary>Marks a restricted recovery session that must change a temporary password first.</summary>
    public const string RecoverySession = "primetect:recovery";
}
