namespace ProjectManagement.Domain.Entities;

public class Department
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public long? ParentId { get; set; }
    public int SortOrder { get; set; }
    public long? LeaderId { get; set; }
    public User? Leader { get; set; }
    public ICollection<User> Users { get; set; } = new List<User>();
}
