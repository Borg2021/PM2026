namespace ProjectManagement.Domain.Entities;

public class PlanBundle
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<PlanBundleItem> Items { get; set; } = new();
}
