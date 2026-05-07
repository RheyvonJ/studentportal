using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MimeKit.Utils;

namespace StudentPortal.Services
{
    public class EmailService
    {
        private readonly string _smtpServer;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public EmailService(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _env = env;

            _smtpServer = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
            _smtpPort = int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 587;
            _smtpUser = (_configuration["Smtp:Username"] ?? string.Empty).Trim();
            _smtpPass = (_configuration["Smtp:Password"] ?? string.Empty).Replace(" ", string.Empty);
        }

        public async Task<(bool ok, string? error)> SendEmailAsync(string toEmail, string subject, string message, bool isHtml = false)
        {
            var email = new MimeMessage();

            email.From.Add(new MailboxAddress("Sta. Lucia Senior High School", _smtpUser));
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
                using var client = new SmtpClient();
                // Dev/locked-down networks sometimes block certificate revocation checks, causing:
                // "The revocation function was unable to check revocation for the certificate."
                // We still validate the certificate chain, but skip online revocation checks.
                client.CheckCertificateRevocation = false;
                Console.WriteLine($"[EmailService] SMTP connect host={_smtpServer} port={_smtpPort} user={_smtpUser}");
                // Gmail: 587 StartTLS, 465 SSL. Use StartTls by default, but allow implicit SSL when port=465.
                var socketOpt = _smtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
                await client.ConnectAsync(_smtpServer, _smtpPort, socketOpt);
                await client.AuthenticateAsync(_smtpUser, _smtpPass);
                Console.WriteLine($"📨 Sending email from {_smtpUser} to {toEmail} ...");
                await client.SendAsync(email);
                await client.DisconnectAsync(true);
                Console.WriteLine("✅ Email sent successfully!");
                return (true, null);
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
                Console.WriteLine("❌ Failed to send email: " + msg);
                return (false, msg);
            }
        }
    }
}
