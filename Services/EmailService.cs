using MailKit.Security;
using MimeKit;
using MailKit.Net.Smtp;

namespace UserManagementApp.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        public EmailService(IConfiguration config) => _config = config;

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var smtpSection = _config.GetSection("SmtpSettings");
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("User Management", smtpSection["Username"]));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpSection["Host"], int.Parse(smtpSection["Port"]!), SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpSection["Username"], smtpSection["Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}