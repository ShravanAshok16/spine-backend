namespace Spine.Core.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public string? PublicUsername { get; set; }
    public string? Bio { get; set; }

    public string PrivacySetting { get; set; } = "FriendsOnly"; // Public | FriendsOnly | Private
    public int YearlyGoal { get; set; } = 24;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; } = false;
    public string? EmailVerificationToken { get; set; }

    // Navigation Properties
    public ICollection<UserBook> UserBooks { get; set; } = new List<UserBook>();
    
}