namespace ProjectManagement.Domain.Entities;

public class ProjectChange
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public int SortOrder { get; set; }
    public string? ChangeType { get; set; }
    public string? ChangeParty { get; set; }
    public string? ChangeContent { get; set; }
    public string? AttachmentUrl { get; set; }
    public long? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public DateTime? EffectEndDate { get; set; }
    public long? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project? Project { get; set; }
}
