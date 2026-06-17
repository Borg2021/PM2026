namespace ProjectManagement.Domain.Entities;

public class ProjectProduct
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public int SortOrder { get; set; }
    public string? ProductType { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTime? PlannedDelivery { get; set; }
    public string? Remark { get; set; }

    public Project? Project { get; set; }
}
