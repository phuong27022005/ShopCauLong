using System.Net;
using System.Net.Mail;
using BadmintonShop.Core.Entities;

namespace BadmintonShop.Api.Auth;

public sealed class EmailDelivery(IConfiguration configuration, IWebHostEnvironment environment)
{
    public async Task SendAsync(OutgoingEmail message, CancellationToken ct)
    {
        using var mail = new MailMessage(configuration["Email:From"] ?? "no-reply@badminton.local", message.Recipient, message.Subject, message.Body);
        using var smtp = new SmtpClient();
        var host = configuration["Email:SmtpHost"];
        if (string.IsNullOrWhiteSpace(host))
        {
            if (!environment.IsDevelopment()) throw new InvalidOperationException("Email SMTP is not configured.");
            var pickup = Path.Combine(environment.ContentRootPath, ".local-mail");
            Directory.CreateDirectory(pickup);
            smtp.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
            smtp.PickupDirectoryLocation = pickup;
        }
        else
        {
            smtp.Host = host;
            smtp.Port = configuration.GetValue("Email:SmtpPort", 587);
            smtp.EnableSsl = true;
            smtp.Credentials = new NetworkCredential(configuration["Email:SmtpUser"], configuration["Email:SmtpPassword"]);
        }
        await smtp.SendMailAsync(mail, ct);
    }
}
