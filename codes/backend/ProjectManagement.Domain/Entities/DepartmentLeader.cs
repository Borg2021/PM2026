namespace ProjectManagement.Domain.Entities;

public class DepartmentLeader
{
    public long Id { get; set; }
    public long DepartmentId { get; set; }
    public long UserId { get; set; }
    public Department Department { get; set; } = null!;
    public User User { get; set; } = null!;
}
