namespace ProjectManagement.Domain.Entities;

public class UserFunction
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public long FunctionId { get; set; }
    public User User { get; set; } = null!;
    public Function Function { get; set; } = null!;
}
