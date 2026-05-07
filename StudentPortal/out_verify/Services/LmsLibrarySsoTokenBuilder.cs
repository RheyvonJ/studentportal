using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StudentPortal.Services
{
    /// <summary>
    /// Builds HMAC-signed SSO tokens for handoff to the Library System (payload + HMAC must match server validator).
    /// </summary>
    public static class LmsLibrarySsoTokenBuilder
    {
        public static string? TryCreateToken(string? sharedSecret, string email, TimeSpan lifetime)
        {
            if (string.IsNullOrWhiteSpace(sharedSecret) || string.IsNullOrWhiteSpace(email))
                return null;

            var exp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)lifetime.TotalSeconds;
            var payload = new { email = email.Trim(), exp };
            var payloadJson = JsonSerializer.Serialize(payload);
            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sharedSecret));
            var sig = hmac.ComputeHash(payloadBytes);

            return Base64UrlEncode(payloadBytes) + "." + Base64UrlEncode(sig);
        }

        private static string Base64UrlEncode(byte[] data)
        {
            return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
