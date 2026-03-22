using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace StudentPortal.Services
{
    public class JitsiJwtService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<JitsiJwtService> _logger;

        public JitsiJwtService(IConfiguration config, ILogger<JitsiJwtService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public string Domain => _config["Jitsi:Domain"] ?? "meet.jit.si";
        public string AppId => _config["Jitsi:AppId"] ?? "studentportal";
        public string Issuer => _config["Jitsi:Issuer"] ?? AppId;
        public string Audience => _config["Jitsi:Audience"] ?? AppId;

        /// <summary>
        /// True when using 8x8 JaaS (Domain is 8x8.vc). JaaS uses RS256 and different claim values.
        /// </summary>
        public bool UseJaaS => _config.GetValue<bool>("Jitsi:UseJaaS")
            || (Domain.IndexOf("8x8.vc", StringComparison.OrdinalIgnoreCase) >= 0);

        public string? KeyId => _config["Jitsi:KeyId"];

        private static string Base64UrlEncode(byte[] data)
        {
            var s = Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            return s;
        }

        /// <summary>
        /// Get RSA private key PEM for JaaS. Reads from Jitsi:PrivateKeyPath file or JITSI_PRIVATE_KEY env var.
        /// </summary>
        private string? GetJaaSPrivateKeyPem()
        {
            var path = _config["Jitsi:PrivateKeyPath"];
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                try
                {
                    return File.ReadAllText(path);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not read Jitsi private key from path {Path}", path);
                }
            }
            var pem = Environment.GetEnvironmentVariable("JITSI_PRIVATE_KEY");
            if (!string.IsNullOrWhiteSpace(pem))
                return pem.Replace("\\n", "\n");
            return null;
        }

        public string GenerateModeratorToken(string room, string displayName, string email, TimeSpan? lifetime = null)
        {
            if (UseJaaS)
                return GenerateJaaSModeratorToken(room, displayName, email, lifetime);
            return GenerateHs256ModeratorToken(room, displayName, email, lifetime);
        }

        /// <summary>
        /// JaaS (8x8) JWT: RS256, aud=jitsi, iss=chat, sub=tenant (vpaas-magic-cookie-xxx), kid in header.
        /// </summary>
        private string GenerateJaaSModeratorToken(string room, string displayName, string email, TimeSpan? lifetime)
        {
            var keyId = KeyId;
            if (string.IsNullOrWhiteSpace(keyId))
                throw new InvalidOperationException("JaaS requires Jitsi:KeyId (API Key ID from 8x8 console, e.g. vpaas-magic-cookie-xxx/yyy).");

            var pem = GetJaaSPrivateKeyPem();
            if (string.IsNullOrWhiteSpace(pem))
                throw new InvalidOperationException("JaaS requires private key: set JITSI_PRIVATE_KEY (PEM) or Jitsi:PrivateKeyPath in config.");

            var now = DateTimeOffset.UtcNow;
            var exp = now.Add(lifetime ?? TimeSpan.FromHours(2));

            var header = new Dictionary<string, object>
            {
                ["alg"] = "RS256",
                ["typ"] = "JWT",
                ["kid"] = keyId
            };

            var sub = AppId.Trim();
            if (!sub.StartsWith("vpaas-magic-cookie-", StringComparison.OrdinalIgnoreCase))
                sub = "vpaas-magic-cookie-" + sub;

            var payload = new Dictionary<string, object>
            {
                ["aud"] = "jitsi",
                ["iss"] = "chat",
                ["sub"] = sub,
                ["room"] = room,
                ["nbf"] = now.ToUnixTimeSeconds(),
                ["exp"] = exp.ToUnixTimeSeconds(),
                ["context"] = new Dictionary<string, object>
                {
                    ["user"] = new Dictionary<string, object>
                    {
                        ["name"] = displayName ?? "User",
                        ["email"] = email ?? "",
                        ["moderator"] = true
                    }
                }
            };

            var headerJson = JsonSerializer.Serialize(header);
            var payloadJson = JsonSerializer.Serialize(payload);
            var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
            var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
            var toSign = $"{headerB64}.{payloadB64}";

            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            var toSignBytes = Encoding.UTF8.GetBytes(toSign);
            var signature = rsa.SignData(toSignBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var sigB64 = Base64UrlEncode(signature);
            var jwt = $"{toSign}.{sigB64}";

            _logger.LogInformation("Generated JaaS moderator token for {Email} room {Room}", email, room);
            return jwt;
        }

        private string GenerateHs256ModeratorToken(string room, string displayName, string email, TimeSpan? lifetime)
        {
            var secret = Environment.GetEnvironmentVariable("JITSI_APP_SECRET");
            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException("Missing JITSI_APP_SECRET environment variable (required for non-JaaS).");

            var now = DateTimeOffset.UtcNow;
            var exp = now.Add(lifetime ?? TimeSpan.FromHours(2));

            var header = new Dictionary<string, object>
            {
                ["alg"] = "HS256",
                ["typ"] = "JWT"
            };

            var payload = new Dictionary<string, object>
            {
                ["aud"] = Audience,
                ["iss"] = Issuer,
                ["sub"] = Domain,
                ["room"] = room,
                ["nbf"] = now.ToUnixTimeSeconds(),
                ["exp"] = exp.ToUnixTimeSeconds(),
                ["context"] = new Dictionary<string, object>
                {
                    ["user"] = new Dictionary<string, object>
                    {
                        ["name"] = displayName,
                        ["email"] = email,
                        ["moderator"] = true
                    }
                }
            };

            var headerJson = JsonSerializer.Serialize(header);
            var payloadJson = JsonSerializer.Serialize(payload);
            var headerB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
            var payloadB64 = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
            var toSign = $"{headerB64}.{payloadB64}";

            var key = Encoding.UTF8.GetBytes(secret);
            using var hmac = new HMACSHA256(key);
            var sig = hmac.ComputeHash(Encoding.UTF8.GetBytes(toSign));
            var sigB64 = Base64UrlEncode(sig);
            var jwt = $"{toSign}.{sigB64}";

            _logger.LogInformation("Generated Jitsi moderator token for {Email} room {Room}", email, room);
            return jwt;
        }
    }
}
