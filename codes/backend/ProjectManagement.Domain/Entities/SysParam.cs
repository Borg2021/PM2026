namespace ProjectManagement.Domain.Entities;

public class SysParam
{
    public long Id { get; set; }
    public string ParamKey { get; set; } = "";
    public string ParamValue { get; set; } = "";
    public string? Description { get; set; }
}
