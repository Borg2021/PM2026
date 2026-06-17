namespace ProjectManagement.Domain.Entities;

public class DictItem
{
    public long Id { get; set; }
    public string DictType { get; set; } = "";
    public string DictCode { get; set; } = "";
    public string DictLabel { get; set; } = "";
    public int SortOrder { get; set; }
    public int Status { get; set; } = 1;
}
