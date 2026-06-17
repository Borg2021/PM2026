using MediatR;
using ProjectManagement.Application.Projects.DTOs;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Projects.Queries;

public record GetProjectDetailQuery(long Id) : IRequest<ProjectDetailDto?>;

public class GetProjectDetailHandler : IRequestHandler<GetProjectDetailQuery, ProjectDetailDto?>
{
    private readonly IProjectRepository _repo;

    public GetProjectDetailHandler(IProjectRepository repo) => _repo = repo;

    public async Task<ProjectDetailDto?> Handle(GetProjectDetailQuery request, CancellationToken cancellationToken)
    {
        var project = await _repo.GetByIdAsync(request.Id);
        if (project == null) return null;

        return new ProjectDetailDto
        {
            Id = project.Id,
            ProjectCode = project.ProjectCode,
            ProjectName = project.ProjectName,
            ProjectType = project.ProjectType,
            ContractCode = project.ContractCode,
            Status = project.Status,
            EngineeringCenter = project.EngineeringCenter,
            CategoryCode = project.CategoryCode,
            CustomerName = project.CustomerName,
            RegionalManagerId = project.RegionalManagerId,
            RegionalManagerName = project.RegionalManagerName,
            CustomerContactPhone = project.CustomerContactPhone,
            CustomerContactEmail = project.CustomerContactEmail,
            SalesManagerId = project.SalesManagerId,
            SalesManagerName = project.SalesManagerName,
            PreSalesManagerId = project.PreSalesManagerId,
            PreSalesManagerName = project.PreSalesManagerName,
            SalesRegion = project.SalesRegion,
            ProjectManagerId = project.ProjectManagerId,
            ProjectManagerName = project.ProjectManagerName,
            PmCenter = project.PmCenter,
            OwnerContactPhone = project.OwnerContactPhone,
            BusinessContactEmail = project.BusinessContactEmail,
            PlanStartDate = project.PlanStartDate,
            RequiredDelivery = project.RequiredDelivery,
            AcceptedDelivery = project.AcceptedDelivery,
            ActualFinishDate = project.ActualFinishDate,
            DeliveryLocation = project.DeliveryLocation,
            FinalCustomer = project.FinalCustomer,
            ProjectScope = project.ProjectScope,
            SpecialTerms = project.SpecialTerms,
            Remark = project.Remark,
            QualityStrategy = project.QualityStrategy,
            ProjectDelivery = project.ProjectDelivery,
            ReportContent = project.ReportContent,
            RiskStatus = project.RiskStatus,
            CurrentPhaseDate = project.CurrentPhaseDate,
            NextStatus = project.NextStatus,
            ProgressDesc = project.ProgressDesc,
            CreatedByName = project.CreatedByName,
            CreatedAt = project.CreatedAt,
            Products = project.Products.OrderBy(x => x.SortOrder).Select(x => new ProductDto
            {
                Id = x.Id,
                SortOrder = x.SortOrder,
                ProductType = x.ProductType,
                Quantity = x.Quantity,
                PlannedDelivery = x.PlannedDelivery,
                Remark = x.Remark
            }).ToList(),
            Members = project.Members.OrderBy(x => x.SortOrder).Select(x => new ProjectMemberDto
            {
                Id = x.Id,
                SortOrder = x.SortOrder,
                RoleId = x.RoleId,
                RoleName = x.RoleName,
                MemberId = x.MemberId,
                MemberName = x.MemberName,
                DeptId = x.DeptId,
                DeptName = x.DeptName,
                FunctionId = x.FunctionId,
                FunctionName = x.FunctionName,
                Remark = x.Remark
            }).ToList(),
            Milestones = project.Milestones.OrderBy(x => x.SortOrder).Select(x => new ProjectMilestoneDto
            {
                Id = x.Id,
                SortOrder = x.SortOrder,
                MilestoneCode = x.MilestoneCode,
                MilestoneName = x.MilestoneName,
                Status = x.Status,
                PlanFinishDate = x.PlanFinishDate,
                ActualFinishDate = x.ActualFinishDate,
                NodeReference = x.NodeReference,
                Remark = x.Remark
            }).ToList()
        };
    }
}
