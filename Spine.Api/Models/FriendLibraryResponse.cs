namespace Spine.Api.Models;

public class FriendLibraryResponse
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public List<UserBookResponse> Books { get; set; } = new();
}