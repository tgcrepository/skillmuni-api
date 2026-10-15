namespace Identity.Application.Abstractions;

public enum NotificationChannel
{
    Sms,
    Email,
}

public static class NotificationTemplates
{
    public const string LoginCode = "login-code";
    public const string SignupCode = "signup-code";
    public const string ContactCode = "contact-code";
    public const string ClaimCode = "claim-code";
    public const string PasswordReset = "password-reset";
}

/// <summary>A code or link to deliver. <see cref="Secret"/> must never be logged.</summary>
public sealed record CodeNotification(
    string TenantId,
    NotificationChannel Channel,
    string To,
    string Template,
    string Secret,
    int ValidForMinutes);

/// <summary>Hands the message to the Platform service, which owns SMS / email providers (BRD §9: provider still open).</summary>
public interface INotificationSender
{
    Task SendAsync(CodeNotification notification, CancellationToken cancellationToken);
}
