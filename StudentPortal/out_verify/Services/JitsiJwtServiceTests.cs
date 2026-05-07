using System;
using System.Text;
using System.Text.Json;

namespace StudentPortal.Services
{
    internal static class JitsiJwtServiceTests
    {
        private static byte[] Base64UrlDecode(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
            return Convert.FromBase64String(s);
        }

        public static bool VerifyModeratorClaim(JitsiJwtService svc, string room, string name, string email)
        {
            var token = svc.GenerateModeratorToken(room, name, email, TimeSpan.FromMinutes(5));
            var parts = token.Split('.');
            if (parts.Length != 3) return false;
            var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("room", out var rp)) return false;
            if (rp.GetString() != room) return false;
            if (!root.TryGetProperty("context", out var ctx)) return false;
            if (!ctx.TryGetProperty("user", out var user)) return false;
            if (!user.TryGetProperty("moderator", out var mod)) return false;
            return mod.ValueKind == JsonValueKind.True;
        }
    }
}
