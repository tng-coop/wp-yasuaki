using System.Security.Cryptography;
using System.Text;

namespace BlazorWP;

public record PkceCodes(string CodeVerifier, string CodeChallenge);

public static class PkceUtil
{
    public static PkceCodes Create()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var verifier = Base64UrlEncode(bytes);
        using var sha = SHA256.Create();
        var challengeBytes = sha.ComputeHash(Encoding.ASCII.GetBytes(verifier));
        var challenge = Base64UrlEncode(challengeBytes);
        return new PkceCodes(verifier, challenge);
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
