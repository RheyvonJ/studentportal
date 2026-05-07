using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MimeKit.Utils;
using System.Net.Http;
using System.Text;

namespace StudentPortal.Services
{
    public class EmailService
    {
        private static readonly HttpClient Http = new HttpClient();
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;
        private readonly string _smtpFrom;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;
        private readonly string _brevoApiKey;

        public EmailService(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _env = env;

            _smtpServer = GetConfig("Smtp:Host", "SMTP_HOST") ?? "smtp.gmail.com";
            _smtpPort = int.TryParse(GetConfig("Smtp:Port", "SMTP_PORT"), out var p) ? p : 587;
            _smtpUser = (GetConfig("Smtp:Username", "SMTP_USERNAME", "SMTP_USER", "MAIL_USERNAME") ?? string.Empty).Trim();
            _smtpPass = (GetConfig("Smtp:Password", "SMTP_PASSWORD", "SMTP_PASS", "MAIL_PASSWORD") ?? string.Empty)
                .Replace(" ", string.Empty);
            _smtpFrom = (GetConfig("Smtp:From", "SMTP_FROM", "MAIL_FROM") ?? _smtpUser).Trim();
            _brevoApiKey = (GetConfig("Brevo:ApiKey", "BREVO_API_KEY", "SENDINBLUE_API_KEY") ?? string.Empty).Trim();
        }

        private string? GetConfig(params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = _configuration[key];
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }

        public async Task<(bool ok, string? error)> SendEmailAsync(string toEmail, string subject, string message, bool isHtml = false)
        {
            // If Brevo API is configured, prefer HTTPS immediately.
            // Hosted platforms often block outbound SMTP, and waiting on SMTP timeouts adds 1–2 minutes of delay.
            if (!string.IsNullOrWhiteSpace(_brevoApiKey))
                return await SendViaBrevoApiAsync(toEmail, subject, message, isHtml);

            var email = new MimeMessage();

            email.From.Add(new MailboxAddress("Sta. Lucia Senior High School", _smtpFrom));
            email.To.Add(MailboxAddress.Parse(toEmail));

            email.Subject = subject;
            if (isHtml)
            {
                var builder = new BodyBuilder { HtmlBody = message };

                // Inline-embed the school logo for email clients that block remote images.
                // If the template uses <img src="cid:school-logo" ...>, we'll attach it as a LinkedResource.
                try
                {
                    var embedEnabled = string.Equals(_configuration["Portal:EmailEmbedLogo"], "true", StringComparison.OrdinalIgnoreCase);
                    var usesCid = message != null && message.Contains("cid:school-logo", StringComparison.OrdinalIgnoreCase);

                    if (embedEnabled || usesCid)
                    {
                        var logoFile = _configuration["Portal:SchoolLogoFile"] ?? "images/SLSHS.png";
                        var physical = Path.IsPathRooted(logoFile)
                            ? logoFile
                            : Path.Combine(_env.WebRootPath ?? Directory.GetCurrentDirectory(), logoFile.Replace("/", Path.DirectorySeparatorChar.ToString()));

                        if (File.Exists(physical))
                        {
                            var resource = builder.LinkedResources.Add(physical);
                            resource.ContentId = "school-logo";
                            resource.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
                            resource.ContentLocation = new Uri("cid:school-logo");
                        }
                    }
                }
                catch
                {
                    // Best-effort only: still send email without inline logo.
                }

                email.Body = builder.ToMessageBody();
            }
            else
            {
                email.Body = new TextPart("plain") { Text = message };
            }

            try
            {
                if (string.IsNullOrWhiteSpace(_smtpUser) || string.IsNullOrWhiteSpace(_smtpPass))
                {
                    Console.WriteLine("[EmailService] SMTP is not configured (missing Smtp:Username or Smtp:Password).");
                    return (false, "SMTP is not configured.");
                }
                var endpoints = BuildEndpoints();

                string? lastError = null;
                foreach (var endpoint in endpoints.Distinct())
                {
                    var result = await TrySendAsync(email, toEmail, endpoint.host, endpoint.port, endpoint.socketOpt);
                    if (result.ok)
                        return (true, null);

                    lastError = result.error;
                }

                return (false, lastError ?? "Unknown SMTP failure.");
            }
            catch (Exception ex)
            {
                var msg = ex.ToString();
                Console.WriteLine("❌ Failed to send email: " + msg);
                return (false, msg);
            }
        }

        private async Task<(bool ok, string? error)> SendViaBrevoApiAsync(
            string toEmail,
            string subject,
            string message,
            bool isHtml)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_brevoApiKey))
                    return (false, "Brevo API key is not configured.");
                if (string.IsNullOrWhiteSpace(_smtpFrom))
                    return (false, "Sender email is not configured.");

                var senderName = _configuration["Portal:SchoolName"] ?? "Sta. Lucia Senior High School";
                var contentField = isHtml ? "htmlContent" : "textContent";
                var safeSubject = JsonEscape(subject ?? string.Empty);
                var safeMsg = JsonEscape(message ?? string.Empty);
                var safeFrom = JsonEscape(_smtpFrom);
                var safeTo = JsonEscape(toEmail);
                var safeName = JsonEscape(senderName);

                var payload = $@"{{
  ""sender"": {{ ""name"": ""{safeName}"", ""email"": ""{safeFrom}"" }},
  ""to"": [ {{ ""email"": ""{safeTo}"" }} ],
  ""subject"": ""{safeSubject}"",
  ""{contentField}"": ""{safeMsg}""
}}";

                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
                req.Headers.TryAddWithoutValidation("api-key", _brevoApiKey);
                req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                Console.WriteLine($"[EmailService] Brevo API send from={_smtpFrom} to={toEmail}");
                using var resp = await Http.SendAsync(req);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[EmailService] Brevo API failed status={(int)resp.StatusCode} body={body}");
                    return (false, $"Brevo API error: {(int)resp.StatusCode}");
                }

                Console.WriteLine("[EmailService] Brevo API email sent.");
                return (true, null);
            }
            catch (Exception ex)
            {
                var msg = ex.ToString();
                Console.WriteLine("[EmailService] Brevo API exception: " + msg);
                return (false, msg);
            }
        }

        private static string JsonEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private List<(string host, int port, SecureSocketOptions socketOpt)> BuildEndpoints()
        {
            if (_smtpServer.Contains("gmail", StringComparison.OrdinalIgnoreCase))
            {
                // For Gmail, enforce STARTTLS on 587 first (more reliable than Auto on some hosts),
                // then fallback to implicit SSL on 465.
                var gmailEndpoints = new List<(string host, int port, SecureSocketOptions socketOpt)>
                {
                    (_smtpServer, 587, SecureSocketOptions.StartTls),
                    (_smtpServer, 465, SecureSocketOptions.SslOnConnect)
                };

                // If custom port is used, also keep it as last attempt.
                if (_smtpPort != 587 && _smtpPort != 465)
                {
                    var customOpt = _smtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto;
                    gmailEndpoints.Add((_smtpServer, _smtpPort, customOpt));
                }

                return gmailEndpoints.Distinct().ToList();
            }

            return new List<(string host, int port, SecureSocketOptions socketOpt)>
            {
                (_smtpServer, _smtpPort, _smtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto)
            };
        }
        private async Task<(bool ok, string? error)> TrySendAsync(
            MimeMessage email,
            string toEmail,
            string host,
            int port,
            SecureSocketOptions socketOpt)
        {
            try
            {
                using var client = new SmtpClient();
                client.Timeout = 7000;
                client.CheckCertificateRevocation = false;
                Console.WriteLine($"[EmailService] SMTP connect host={host} port={port} tls={socketOpt} user={_smtpUser}");
                await client.ConnectAsync(host, port, socketOpt);
                await client.AuthenticateAsync(_smtpUser, _smtpPass);
                Console.WriteLine($"📨 Sending email from {_smtpFrom} to {toEmail} ...");
                await client.SendAsync(email);
                await client.DisconnectAsync(true);
                Console.WriteLine("✅ Email sent successfully!");
                return (true, null);
            }
            catch (Exception ex)
            {
                var msg = ex.ToString();
                Console.WriteLine($"[EmailService] SMTP attempt failed host={host} port={port} tls={socketOpt}: {msg}");
                return (false, msg);
            }
        }
    }
}
