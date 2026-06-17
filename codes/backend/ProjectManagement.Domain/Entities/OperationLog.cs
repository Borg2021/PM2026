namespace ProjectManagement.Domain.Entities;

public class OperationLog
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public long UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
