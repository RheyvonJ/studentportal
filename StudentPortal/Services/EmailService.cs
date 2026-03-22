using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace StudentPortal.Services
{
    public class EmailService
    {
        private readonly string _smtpServer = "smtp.gmail.com";
        private readonly int _smtpPort = 587;
        private readonly string _smtpUser = "mysuqcotp@gmail.com";   // your Gmail
        private readonly string _smtpPass = "eqlp oyav adtw ulzf";   // your App Password

        public async Task<(bool ok, string? error)> SendEmailAsync(string toEmail, string subject, string message, bool isHtml = false)
        {
            var email = new MimeMessage();

            email.From.Add(new MailboxAddress("Sta. Lucia Senior High School", _smtpUser));
            email.To.Add(MailboxAddress.Parse(toEmail));

            email.Subject = subject;
            email.Body = isHtml
                ? new TextPart("html") { Text = message }
                : new TextPart("plain") { Text = message };

            try
            {
                using var client = new SmtpClient();
                await client.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.StartTls);
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
