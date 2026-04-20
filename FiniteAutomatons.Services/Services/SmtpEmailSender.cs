using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System.Net;
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
            this.logger.LogWarning("SMTP host is not configured (Smtp:Host). Email sending will fail until configured.");
        }

        this.logger.LogInformation(
            "SMTP settings loaded. Host: {Host}, Port: {Port}, UseSsl: {UseSsl}, UsePickupDirectory: {UsePickupDirectory}, From: {From}, Username: {Username}",
            string.IsNullOrWhiteSpace(settings.Host) ? "<empty>" : settings.Host,
            settings.Port,
            settings.UseSsl,
            settings.UsePickupDirectory,
            string.IsNullOrWhiteSpace(settings.From) ? "<empty>" : settings.From,
            string.IsNullOrWhiteSpace(settings.Username) ? "<empty>" : settings.Username);
    }

    /// <summary>
    /// Sends an e-mail with optional attachments/inline resources using MailKit.
    /// </summary>
    public async Task SendEmailWithAttachmentsAsync(
        string email,
        string subject,
        string htmlMessage,
        IEnumerable<(string FileName, byte[] Content, string MediaType, bool IsInline, string? ContentId)>? attachments = null)
    {
        var message = BuildMessage(email, subject, htmlMessage, attachments);
        await SendAsync(message, email);
    }

    /// <summary>
    /// Sends a plain HTML e-mail using MailKit – required by ASP.NET Identity (IEmailSender).
    /// </summary>
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var message = BuildMessage(email, subject, htmlMessage);
        await SendAsync(message, email);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private MimeMessage BuildMessage(
        string toEmail,
        string subject,
        string htmlMessage,
        IEnumerable<(string FileName, byte[] Content, string MediaType, bool IsInline, string? ContentId)>? attachments = null)
    {
        var fromAddress = settings.From ?? settings.Username ?? "no-reply@localhost";
        var fromName = settings.FromName ?? "Automaton Simulator";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject ?? string.Empty;

        // Build a multipart/alternative body (plain-text + HTML)
        var plainText = HtmlToPlainText(htmlMessage);

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlMessage,
            TextBody = plainText
        };

        // Attach inline resources and regular attachments
        if (attachments != null)
        {
            foreach (var (fileName, content, mediaType, isInline, contentId) in attachments)
            {
                if (isInline && !string.IsNullOrEmpty(contentId))
                {
                    var inline = bodyBuilder.LinkedResources.Add(fileName, content, ContentType.Parse(mediaType));
                    inline.ContentId = contentId;
                    inline.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
                }
                else
                {
                    bodyBuilder.Attachments.Add(fileName, content, ContentType.Parse(mediaType));
                }
            }
        }

        message.Body = bodyBuilder.ToMessageBody();
        return message;
    }

    private async Task SendAsync(MimeMessage message, string recipientEmail)
    {
        // Pickup-directory mode (development / no network)
        if (settings.UsePickupDirectory)
        {
            var pickupDir = ResolvePickupDirectory();
            Directory.CreateDirectory(pickupDir);

            var safeFileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.eml";
            var filePath = Path.Combine(pickupDir, safeFileName);
            await message.WriteToAsync(filePath);

            logger.LogInformation("Email written to pickup directory {Path} for {Email}", filePath, recipientEmail);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            logger.LogError("Attempt to send email but Smtp:Host is not configured.");
            throw new InvalidOperationException("SMTP is not configured. Set Smtp:Host in configuration.");
        }

        // Choose SecureSocketOptions based on the port / UseSsl flag.
        // Port 465 = implicit SSL (SslOnConnect) — required by seznam.cz.
        // Port 587 = STARTTLS.
        // Port 25  = no encryption (or optional STARTTLS).
        var socketOptions = DetermineSocketOptions();

        using var client = new SmtpClient();
        client.Timeout = settings.TimeoutMilliseconds;

        try
        {
            await client.ConnectAsync(settings.Host, settings.Port, socketOptions);

            if (!string.IsNullOrWhiteSpace(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password ?? string.Empty);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            logger.LogInformation("Email sent to {Email} via {Host}:{Port}", recipientEmail, settings.Host, settings.Port);
        }
        catch (MailKit.Security.AuthenticationException ex)
        {
            // Credentials rejected by the server — log clearly and continue.
            // The user/account operation that triggered this email must not fail because of SMTP auth issues.
            logger.LogError(ex,
                "SMTP authentication failed for {Host}:{Port} (user: {Username}). " +
                "Check the password in Smtp:Password and ensure the account has SMTP access enabled. " +
                "Email to {Email} was NOT sent.",
                settings.Host, settings.Port, settings.Username, recipientEmail);
        }
        catch (Exception ex)
        {
            // Any other send failure (network, timeout, etc.) — log and continue.
            logger.LogError(ex, "Failed to send email to {Email} via {Host}:{Port}", recipientEmail, settings.Host, settings.Port);
        }
    }

    private SecureSocketOptions DetermineSocketOptions()
    {
        // Explicit override via config
        if (settings.UseSsl)
        {
            // Port 465 uses implicit TLS (SslOnConnect); other ports typically use STARTTLS
            return settings.Port == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTls;
        }

        // Auto-detect by port
        return settings.Port switch
        {
            465 => SecureSocketOptions.SslOnConnect,
            587 => SecureSocketOptions.StartTls,
            _ => SecureSocketOptions.Auto
        };
    }

    private string ResolvePickupDirectory()
    {
        var dir = string.IsNullOrWhiteSpace(settings.PickupDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "emails")
            : settings.PickupDirectory!;

        return Path.IsPathRooted(dir)
            ? dir
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, dir));
    }

    private static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html)) return string.Empty;

        var plain = Regex.Replace(html, "<.*?>", string.Empty);

        // Append any href URLs that aren't already in the plain text so
        // recipients can copy-paste the link easily.
        var m = Regex.Match(html, "href=['\"](?<u>[^'\"]+)['\"]", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var url = WebUtility.HtmlDecode(m.Groups["u"].Value);
            if (!string.IsNullOrWhiteSpace(url) && !plain.Contains(url))
                plain += "\n" + url;
        }

        return WebUtility.HtmlDecode(plain);
    }

    // -------------------------------------------------------------------------
    // Settings model
    // -------------------------------------------------------------------------

    private sealed class SmtpSettings
    {
        public string? Host { get; set; }
        public int Port { get; set; } = 465;
        public int TimeoutMilliseconds { get; set; } = 15_000;
        public bool UseSsl { get; set; } = true;
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? From { get; set; }
        public string? FromName { get; set; }
        public bool UsePickupDirectory { get; set; } = false;
        public string? PickupDirectory { get; set; }
    }
}
