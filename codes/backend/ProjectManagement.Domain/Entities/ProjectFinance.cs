using System.Text.Json.Serialization;

namespace ProjectManagement.Domain.Entities;

public class ProjectFinance
{
    public long Id { get; set; }
    public long ProjectId { get; set; }
    public decimal? TaxContractAmount { get; set; }
    public decimal? TaxRate { get; set; }
    public string? CurrencyType { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal? CurrencyAmount { get; set; }
    public decimal? ContributionRate { get; set; }
    public decimal? InvoiceRate { get; set; }
    public string? Remark { get; set; }
    public DateTime? UpdatedAt { get; set; }

    [JsonIgnore]
    public Project? Project { get; set; }
    public List<ProjectPlanReceipt> PlanReceipts { get; set; } = new();
    public List<ProjectReceipt> Receipts { get; set; } = new();
    public List<ProjectInvoice> Invoices { get; set; } = new();
}

public class ProjectPlanReceipt
{
    public long Id { get; set; }
    public long ProjectFinanceId { get; set; }
    public int SortOrder { get; set; }
    public decimal PlanAmount { get; set; }
    public string? ReceiptType { get; set; }
    public DateTime? PlanDate { get; set; }
    public string? Remark { get; set; }
    [JsonIgnore]
    public ProjectFinance? Finance { get; set; }
}

public class ProjectReceipt
{
    public long Id { get; set; }
    public long ProjectFinanceId { get; set; }
    public int SortOrder { get; set; }
    public decimal ActualAmount { get; set; }
    public string? ReceiptType { get; set; }
    public DateTime? ReceiptTime { get; set; }
    public string? Remark { get; set; }
    [JsonIgnore]
    public ProjectFinance? Finance { get; set; }
}

public class ProjectInvoice
{
    public long Id { get; set; }
    public long ProjectFinanceId { get; set; }
    public int SortOrder { get; set; }
    public decimal InvoiceAmount { get; set; }
    public decimal? InvoiceRate { get; set; }
    public DateTime? InvoiceTime { get; set; }
    public string? InvoiceNo { get; set; }
    public string? Remark { get; set; }
    [JsonIgnore]
    public ProjectFinance? Finance { get; set; }
}
