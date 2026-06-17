namespace ProjectManagement.Application.Projects.DTOs;

public class ProjectDto
{
    public long Id { get; set; }
    public string ProjectCode { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string? ProjectType { get; set; }
    public string? ContractCode { get; set; }
    public int Status { get; set; }
    public string StatusName => Status switch { 0 => "未激活", 1 => "进行中", 2 => "已完成", 3 => "暂停", _ => "未知" };
    public string? EngineeringCenter { get; set; }
    public string? CustomerName { get; set; }
    public string? ProjectManagerName { get; set; }
    public string? PmCenter { get; set; }
    public string? SalesManagerName { get; set; }
    public string? SalesRegion { get; set; }
    public DateTime? RequiredDelivery { get; set; }
    public DateTime? AcceptedDelivery { get; set; }
    public DateTime? PlanStartDate { get; set; }
    public string? ProgressDesc { get; set; }
    public decimal? FirstTaskProgress { get; set; }
    public decimal? PlannedProgress { get; set; }
    public DateTime? RootPlanStartDate { get; set; }
    public DateTime? RootPlanFinishDate { get; set; }
    public int? RootPlanDuration { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProjectDetailDto : ProjectDto
{
    public string? CategoryCode { get; set; }
    public long? RegionalManagerId { get; set; }
    public string? RegionalManagerName { get; set; }
    public string? CustomerContactPhone { get; set; }
    public string? CustomerContactEmail { get; set; }
    public long? SalesManagerId { get; set; }
    public long? PreSalesManagerId { get; set; }
    public string? PreSalesManagerName { get; set; }
    public long? ProjectManagerId { get; set; }
    public string? OwnerContactPhone { get; set; }
    public string? BusinessContactEmail { get; set; }
    public DateTime? ActualFinishDate { get; set; }
    public string? DeliveryLocation { get; set; }
    public string? FinalCustomer { get; set; }
    public string? ProjectScope { get; set; }
    public string? SpecialTerms { get; set; }
    public string? Remark { get; set; }
    public string? QualityStrategy { get; set; }
    public string? ProjectDelivery { get; set; }
    public string? ReportContent { get; set; }
    public string? RiskStatus { get; set; }
    public DateTime? CurrentPhaseDate { get; set; }
    public string? NextStatus { get; set; }
    public string? ProgressDesc { get; set; }

    /// <summary>当前用户是否有确认完成/暂停权限</summary>
    public bool CanManageStatus { get; set; }
    /// <summary>当前用户是否有取消激活权限</summary>
    public bool CanDeactivate { get; set; }

    public List<ProductDto> Products { get; set; } = new();
    public List<ProjectMemberDto> Members { get; set; } = new();
    public List<ProjectMilestoneDto> Milestones { get; set; } = new();
}

public class ProductDto
{
    public long Id { get; set; }
    public int SortOrder { get; set; }
    public string? ProductType { get; set; }
    public int Quantity { get; set; }
    public DateTime? PlannedDelivery { get; set; }
    public string? Remark { get; set; }
}

public class ProjectMemberDto
{
    public long Id { get; set; }
    public int SortOrder { get; set; }
    public long? RoleId { get; set; }
    public string? RoleName { get; set; }
    public long? MemberId { get; set; }
    public string? MemberName { get; set; }
    public long? DeptId { get; set; }
    public string? DeptName { get; set; }
    public long? FunctionId { get; set; }
    public string? FunctionName { get; set; }
    public string? Remark { get; set; }
}

public class ProjectMilestoneDto
{
    public long Id { get; set; }
    public int SortOrder { get; set; }
    public string MilestoneCode { get; set; } = "";
    public string MilestoneName { get; set; } = "";
    public int Status { get; set; }
    public string StatusName => Status == 1 ? "已完成" : "未完成";
    public DateTime? PlanFinishDate { get; set; }
    public DateTime? ActualFinishDate { get; set; }
    public string? NodeReference { get; set; }
    public string? Remark { get; set; }
}
