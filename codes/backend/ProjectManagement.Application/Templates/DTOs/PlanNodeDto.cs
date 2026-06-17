namespace ProjectManagement.Application.Templates.DTOs;

public class PredecessorDto
{
    public long PredecessorId { get; set; }
    public string PredecessorCode { get; set; } = "";
    public string DependencyType { get; set; } = "FS";
    public int LagDays { get; set; }
}

public class PlanNodeDto
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public string NodeCode { get; set; } = "";
    public string NodeName { get; set; } = "";
    public int NodeType { get; set; }
    public string NodeTypeName { get; set; } = "";
    public int SortOrder { get; set; }
    public int? StdDuration { get; set; }
    public List<PredecessorDto> Predecessors { get; set; } = new();
    public int DeliverableCnt { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public long? DeptId { get; set; }
    public string? DeptName { get; set; }
    public string? Remark { get; set; }
    public string? TaskCategory { get; set; }
    public List<PlanNodeDto> Children { get; set; } = new();
}
