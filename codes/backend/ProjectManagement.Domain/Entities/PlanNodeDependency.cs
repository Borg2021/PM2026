namespace ProjectManagement.Domain.Entities;

public class PlanNodeDependency
{
    public long Id { get; set; }
    public long PlanNodeId { get; set; }
    public long PredecessorId { get; set; }
    public string DependencyType { get; set; } = "FS";
    public int LagDays { get; set; }
}
