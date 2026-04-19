using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.Services.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly ILogger<SmtpEmailSender> logger;
    private readonly SmtpSettings settings;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        this.logger = logger;
        settings = new SmtpSettings();
        configuration.GetSection("Smtp").Bind(settings);

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            logger.LogWarning("SMTP host is not configured (Smtp:Host). Email sending will fail until configured.");
        }

        logger.LogInformation(
            "SMTP settings loaded. Host: {Host}, Port: {Port}, EnableSsl: {EnableSsl}, UsePickupDirectory: {UsePickupDirectory}, From: {From}",
            string.IsNullOrWhiteSpace(settings.Host) ? "<empty>" : settings.Host,
            settings.Port,
            settings.EnableSsl,
            settings.UsePickupDirectory,
            string.IsNullOrWhiteSpace(settings.From) ? "<empty>" : settings.From);
    }

    public async Task SendEmailWithAttachmentsAsync(
        string email,
        string subject,
        string htmlMessage,
        IEnumerable<(string FileName, byte[] Content, string MediaType, bool IsInline, string? ContentId)>? attachments = null)
    {
        // Allow operating in pickup-directory-only mode when Host is not configured.
        if (string.IsNullOrWhiteSpace(settings.Host) && !settings.UsePickupDirectory)
        {
            logger.LogError("Attempt to send email but SMTP host (Smtp:Host) is not configured and UsePickupDirectory is false");
            throw new InvalidOperationException("SMTP is not configured. Set Smtp:Host in configuration or enable UsePickupDirectory for development.");
        }

        var mail = new MailMessage();
        mail.To.Add(new MailAddress(email));
        mail.Subject = subject ?? string.Empty;
        mail.IsBodyHtml = true;
        mail.BodyEncoding = Encoding.UTF8;
        mail.SubjectEncoding = Encoding.UTF8;

        var fromAddress = settings.From ?? settings.Username ?? "no-reply@localhost";
        var fromName = settings.FromName ?? "FiniteAutomatons";
        mail.From = new MailAddress(fromAddress, fromName);

        // Build alternate view for HTML with inline resources and a plain-text alternative
        AlternateView? htmlView = null;
        AlternateView? plainView = null;
        if (!string.IsNullOrEmpty(htmlMessage))
        {
            htmlView = AlternateView.CreateAlternateViewFromString(htmlMessage, Encoding.UTF8, "text/html");

            // Create a simple plain-text alternative by stripping tags and extracting any hrefs
            var plain = Regex.Replace(htmlMessage, "<.*?>", string.Empty);
            // If there is an href, append it explicitly so recipients can copy/paste easily
            var m = Regex.Match(htmlMessage, "href=[\'\"](?<u>[^\'\"]+)[\'\"]", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var url = WebUtility.HtmlDecode(m.Groups["u"].Value);
                if (!string.IsNullOrWhiteSpace(url) && !plain.Contains(url))
                {
                    plain += "\n" + url;
                }
            }
            plain = WebUtility.HtmlDecode(plain);
            plainView = AlternateView.CreateAlternateViewFromString(plain, Encoding.UTF8, "text/plain");
        }

        if (attachments != null)
        {
            foreach (var (FileName, Content, MediaType, IsInline, ContentId) in attachments)
            {
                if (IsInline && !string.IsNullOrEmpty(ContentId) && htmlView != null)
                {
                    var lr = new LinkedResource(new System.IO.MemoryStream(Content), MediaType)
                    {
                        ContentId = ContentId,
                        TransferEncoding = System.Net.Mime.TransferEncoding.Base64
                    };
                    htmlView.LinkedResources.Add(lr);
                }
                else
                {
                    var a = new Attachment(new System.IO.MemoryStream(Content), FileName, MediaType);
                    mail.Attachments.Add(a);
                }
            }
        }

        if (htmlView != null)
        {
            if (plainView != null) mail.AlternateViews.Add(plainView);
            mail.AlternateViews.Add(htmlView);
        }

        // If pickup configured, write to pickup directory otherwise send via SMTP
        if (settings.UsePickupDirectory)
        {
            var configured = string.IsNullOrWhiteSpace(settings.PickupDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "emails")
                : settings.PickupDirectory!;
            var pickup = configured;
            if (!Path.IsPathRooted(pickup))
            {
                pickup = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, pickup));
            }
            Directory.CreateDirectory(pickup);

            using var client = new SmtpClient()
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = pickup
            };

            await client.SendMailAsync(mail);
            logger.LogInformation("Email written to pickup directory {Pickup} for {Email}", pickup, email);
            return;
        }

        using var smtpClient = new SmtpClient(settings.Host, settings.Port) { EnableSsl = settings.EnableSsl };
        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            smtpClient.Credentials = new NetworkCredential(settings.Username, settings.Password ?? string.Empty);
        }

        await smtpClient.SendMailAsync(mail);
        logger.LogInformation("Email with attachments sent to {Email} via SMTP host {Host}", email, settings.Host);
    }

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        // Allow operating in pickup-directory-only mode when Host is not configured.
        if (string.IsNullOrWhiteSpace(settings.Host) && !settings.UsePickupDirectory)
        {
            logger.LogError("Attempt to send email but SMTP host (Smtp:Host) is not configured and UsePickupDirectory is false");
            throw new InvalidOperationException("SMTP is not configured. Set Smtp:Host in configuration or enable UsePickupDirectory for development.");
        }

        var mail = new MailMessage();
        mail.To.Add(new MailAddress(email));
        mail.Subject = subject ?? string.Empty;
        mail.Body = htmlMessage ?? string.Empty;
        mail.IsBodyHtml = true;
        mail.BodyEncoding = Encoding.UTF8;
        mail.SubjectEncoding = Encoding.UTF8;

        var fromAddress = settings.From ?? settings.Username ?? "no-reply@localhost";
        var fromName = settings.FromName ?? "FiniteAutomatons";
        mail.From = new MailAddress(fromAddress, fromName);

        // If configured, write emails to a pickup directory (development-friendly, no network required).
        if (settings.UsePickupDirectory)
        {
            var configured = string.IsNullOrWhiteSpace(settings.PickupDirectory)
                ? Path.Combine(AppContext.BaseDirectory, "emails")
                : settings.PickupDirectory!;
            var pickup = configured;
            if (!Path.IsPathRooted(pickup))
            {
                pickup = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, pickup));
            }
            Directory.CreateDirectory(pickup);

            // Ensure the mail has UTF-8 encoding and add a plain-text alternate if not present
            if (!mail.AlternateViews.Any(av => av.ContentType.MediaType == "text/plain"))
            {
                var plain = WebUtility.HtmlDecode(Regex.Replace(mail.Body ?? string.Empty, "<.*?>", string.Empty));
                mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(plain, Encoding.UTF8, "text/plain"));
            }

            using var client = new SmtpClient()
            {
                DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                PickupDirectoryLocation = pickup
            };

            try
            {
                await client.SendMailAsync(mail);
                logger.LogInformation("Email written to pickup directory {Pickup} for {Email}", pickup, email);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write email to pickup directory {Pickup} for {Email}", pickup, email);
                throw;
            }
        }
        else
        {
            using var client = new SmtpClient(settings.Host, settings.Port)
            {
                EnableSsl = settings.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                client.Credentials = new NetworkCredential(settings.Username, settings.Password ?? string.Empty);
            }

            try
            {
                await client.SendMailAsync(mail);
                logger.LogInformation("Email sent to {Email} via SMTP host {Host}", email, settings.Host);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send email via SMTP host {Host}; attempting to write to pickup directory as fallback", settings.Host);

                // Fallback: write to pickup directory if available. Do not throw to avoid breaking user registration in dev.
                try
                {
                    var configured = Path.Combine(AppContext.BaseDirectory, "emails");
                    var pickup = configured;
                    if (!Path.IsPathRooted(pickup))
                    {
                        pickup = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, pickup));
                    }
                    Directory.CreateDirectory(pickup);
                    using var pickupClient = new SmtpClient()
                    {
                        DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory,
                        PickupDirectoryLocation = pickup
                    };
                    await pickupClient.SendMailAsync(mail);
                    logger.LogInformation("Email written to pickup directory {Pickup} for {Email} after SMTP failure", pickup, email);
                }
                catch (Exception ex2)
                {
                    logger.LogError(ex2, "Failed to write email to pickup directory as fallback for {Email}", email);
                }
            }
        }
    }

    private sealed class SmtpSettings
    {
        public string? Host { get; set; }
        public int Port { get; set; } = 25;
        public int TimeoutMilliseconds { get; set; } = 15000;
        public bool EnableSsl { get; set; } = true;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? From { get; set; }
        public string? FromName { get; set; }
        public bool UsePickupDirectory { get; set; } = false;
        public string? PickupDirectory { get; set; }
    }
}
