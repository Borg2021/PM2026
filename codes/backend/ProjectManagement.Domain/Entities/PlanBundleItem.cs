namespace ProjectManagement.Domain.Entities;

public class PlanBundleItem
{
    public long Id { get; set; }
    public long BundleId { get; set; }
    public long TemplateId { get; set; }
    public int SortOrder { get; set; }
    public PlanBundle Bundle { get; set; } = null!;
    public Template Template { get; set; } = null!;
}
