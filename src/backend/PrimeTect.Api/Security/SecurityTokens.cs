using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace PrimeTect.Api.Security;

public static class SecurityTokens
{
    public static (string PlainText, string Hash) CreateInvitationToken()
    {
        var plainText = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return (plainText, Hash(plainText));
    }

    public static string Hash(string value) =>
        WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
