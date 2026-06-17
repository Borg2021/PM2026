namespace ProjectManagement.Domain.Entities;

public class DictType
{
    public long Id { get; set; }
    public string DictTypeCode { get; set; } = "";
    public string DictTypeName { get; set; } = "";
    public string? Remark { get; set; }
}
