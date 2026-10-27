namespace Spine.Api.Models;

public class AddBookToLibraryRequest
{
    public string Isbn { get; set; } = string.Empty;
    public string Status { get; set; } = "Unread"; // Unread, Reading, Finished, DNF
}