namespace ProjectManagement.Domain.Entities;

public class PlanNode
{
    public long Id { get; set; }
    public long TemplateId { get; set; }
    public long? ParentId { get; set; }
    public string NodeCode { get; set; } = "";
    public string NodeName { get; set; } = "";
    public int NodeType { get; set; }
    public int SortOrder { get; set; }
    public int? StdDuration { get; set; }

    public int DeliverableCnt { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public long? DeptId { get; set; }
    public string? DeptName { get; set; }
    public string? Remark { get; set; }
    public string? TaskCategory { get; set; }

    public Template Template { get; set; } = null!;
}
