namespace Spine.Api.Events;

public class PasswordResetRequestedEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}