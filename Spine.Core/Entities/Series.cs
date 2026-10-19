namespace Spine.Core.Entities;

public class Series
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int TotalBooks { get; set; }

    public ICollection<Book> Books { get; set; } = new List<Book>();
}