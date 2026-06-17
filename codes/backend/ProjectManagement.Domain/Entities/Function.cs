namespace ProjectManagement.Domain.Entities;

public class Function
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int SortOrder { get; set; } = 0;
    public List<UserFunction> UserFunctions { get; set; } = new();
}
