namespace ProjectManagement.Domain.Entities;

public class ProjectTask
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public long? ParentId { get; set; }
    public string TaskNo { get; set; } = "";
    public string WbsCode { get; set; } = "";
    public string TaskName { get; set; } = "";

    /// <summary>节点类型：1=计划节点, 2=任务节点</summary>
    public int NodeType { get; set; } = 1;

    /// <summary>任务类别：设计/采购/生产/营销/调检/发货</summary>
    public string? TaskCategory { get; set; }

    public int SortOrder { get; set; }

    /// <summary>状态：0=未开始, 1=进行中, 2=已完成, 3=已延误</summary>
    public int Status { get; set; } = 0;

    /// <summary>优先级：1=最高, 2=高, 3=中, 4=低</summary>
    public int Priority { get; set; } = 3;

    public DateTime? PlanStartDate { get; set; }
    public DateTime? PlanFinishDate { get; set; }
    public DateTime? ActualStartDate { get; set; }
    public DateTime? ActualFinishDate { get; set; }

    public int? PlanDuration { get; set; }
    public int? ActualDuration { get; set; }
    public int? ReferenceDuration { get; set; }
    public string? PreTaskCodes { get; set; }
    public int DeliverableCnt { get; set; } = 0;
    public decimal ProgressPct { get; set; } = 0;

    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public long? DeptId { get; set; }
    public string? DeptName { get; set; }
    public long? MilestoneId { get; set; }
    public string? Remark { get; set; }

    public Project? Project { get; set; }
}
