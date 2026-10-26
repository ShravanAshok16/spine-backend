using Resend;

namespace Spine.Worker.Services;

public class EmailService
{
    private readonly IResend _resend;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IResend resend, ILogger<EmailService> logger)
    {
        _resend = resend;
        _logger = logger;
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string token)
    {
        var message = new EmailMessage();
        message.From = "onboarding@resend.dev";
        message.To.Add(toEmail);
        message.Subject = "Reset your Spine password";

        // In production, use your actual frontend URL here
        var resetLink = $"http://localhost:5173/reset-password?token={token}";

        message.HtmlBody = $@"
            <h2>Password Reset Request</h2>
            <p>Click the link below to reset your password. This link expires in 15 minutes.</p>
            <a href='{resetLink}'>Reset Password</a>
            <p>If you didn't request this, ignore this email.</p>";

        await _resend.EmailSendAsync(message);
        _logger.LogInformation("Email sent to {Email}", toEmail);
    }

    public async Task SendVerificationEmailAsync(string toEmail, string token)
    {
        var message = new EmailMessage();
        message.From = "onboarding@resend.dev";
        message.To.Add(toEmail);
        message.Subject = "Welcome to Spine! Please verify your email"; // <-- FIXED

        // In production, this will be your React frontend URL
        var verifyLink = $"http://localhost:5173/verify-email?token={token}";

        message.HtmlBody = $@"
        <h2>Welcome to Spine!</h2>
        <p>Thanks for signing up. Please verify your email address by clicking the link below:</p>
        <a href='{verifyLink}'>Verify Email</a>
        <p>If you didn't create an account, you can ignore this email.</p>";

        await _resend.EmailSendAsync(message);
        _logger.LogInformation("Verification email sent to {Email}", toEmail);
    }
}