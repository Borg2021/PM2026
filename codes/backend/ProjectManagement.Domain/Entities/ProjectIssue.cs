namespace ProjectManagement.Domain.Entities;

public class ProjectIssue
{
    public long Id { get; set; }

    // ── 归属 ──
    public long ProjectId { get; set; }
    public string IssueCode { get; set; } = "";
    public Project Project { get; set; } = null!;

    // ── 基本信息 ──
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string IssueSource { get; set; } = "";
    public string IssueType { get; set; } = "";
    public string Severity { get; set; } = "一般";
    public string Priority { get; set; } = "一般";
    public int Status { get; set; } = 0;                 // 0=待处理 1=处理中 2=已完成

    // ── 分析与日期 ──
    public string? CauseAnalysis { get; set; }
    public DateOnly DiscoveredDate { get; set; }
    public DateOnly? PlannedDate { get; set; }

    // ── 人员 ──
    public long? ResponsibleDeptId { get; set; }
    public string? ResponsibleDeptName { get; set; }
    public long AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public long SubmitterId { get; set; }
    public string? SubmitterName { get; set; }
    public long CreatorId { get; set; }
    public string? CreatorName { get; set; }
    public long? VerifierId { get; set; }
    public string? VerifierName { get; set; }
    public DateOnly? VerifiedDate { get; set; }

    // ── 统计 ──
    public int ReopenCount { get; set; } = 0;

    // ── 审计 ──
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── 导航 ──
    public List<ProjectIssueMeasure> Measures { get; set; } = new();
}
