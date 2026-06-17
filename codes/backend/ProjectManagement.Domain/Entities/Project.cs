namespace ProjectManagement.Domain.Entities;

public class Project
{
    public long Id { get; set; }
    public string ProjectCode { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string? ProjectType { get; set; }
    public string? ContractCode { get; set; }

    /// <summary>状态：0=未激活, 1=进行中, 2=已完成, 3=暂停</summary>
    public int Status { get; set; } = 0;

    public string? EngineeringCenter { get; set; }
    public string? CategoryCode { get; set; }
    public string? CustomerName { get; set; }

    public long? RegionalManagerId { get; set; }
    public string? RegionalManagerName { get; set; }
    public string? CustomerContactPhone { get; set; }
    public string? CustomerContactEmail { get; set; }

    public long? SalesManagerId { get; set; }
    public string? SalesManagerName { get; set; }
    public long? PreSalesManagerId { get; set; }
    public string? PreSalesManagerName { get; set; }
    public string? SalesRegion { get; set; }

    public long? ProjectManagerId { get; set; }
    public string? ProjectManagerName { get; set; }
    public string? PmCenter { get; set; }
    public string? OwnerContactPhone { get; set; }
    public string? BusinessContactEmail { get; set; }

    public DateTime? PlanStartDate { get; set; }
    public DateTime? RequiredDelivery { get; set; }
    public DateTime? AcceptedDelivery { get; set; }
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

    public long? CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedBy { get; set; }

    public List<ProjectProduct> Products { get; set; } = new();
    public List<ProjectMember> Members { get; set; } = new();
    public List<ProjectMilestone> Milestones { get; set; } = new();
    public List<ProjectTask> Tasks { get; set; } = new();
    public List<ProjectFileItem> FileItems { get; set; } = new();
}
