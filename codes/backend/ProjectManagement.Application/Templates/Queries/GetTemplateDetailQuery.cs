using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using ProjectManagement.Application.Templates.DTOs;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Templates.Queries;

public record GetTemplateDetailQuery(long Id) : IRequest<TemplateDetailDto?>;

public class GetTemplateDetailHandler : IRequestHandler<GetTemplateDetailQuery, TemplateDetailDto?>
{
    private readonly ITemplateRepository _repo;
    private readonly ILogger<GetTemplateDetailHandler> _logger;

    public GetTemplateDetailHandler(ITemplateRepository repo, ILogger<GetTemplateDetailHandler> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<TemplateDetailDto?> Handle(GetTemplateDetailQuery request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        var template = await _repo.GetByIdAsync(request.Id);
        if (template == null || template.Status == 0) return null;

        var loadMs = sw.Elapsed.TotalMilliseconds;

        var typeNames = new Dictionary<int, string> { { 1, "计划" }, { 2, "里程碑" }, { 3, "项目成员" }, { 4, "文件模板" } };

        var nodeIds = template.PlanNodes.Select(n => n.Id).ToList();
        var depSw = Stopwatch.StartNew();
        var dependencies = await _repo.GetDependenciesByNodeIdsAsync(nodeIds);
        var depsMs = depSw.Elapsed.TotalMilliseconds;
        var depsByNodeId = dependencies.ToLookup(d => d.PlanNodeId);
        var idToCode = template.PlanNodes.ToDictionary(n => n.Id, n => n.NodeCode);

        var treeSw = Stopwatch.StartNew();
        var result = new TemplateDetailDto
        {
            Id = template.Id,
            TemplateCode = template.TemplateCode,
            TemplateName = template.TemplateName,
            TemplateType = template.TemplateType,
            TemplateTypeName = typeNames.GetValueOrDefault(template.TemplateType, ""),
            Description = template.Description,
            CreatedByName = template.CreatedByName,
            CreatedAt = template.CreatedAt,
            PlanNodes = BuildPlanNodeTree(template.PlanNodes, null, depsByNodeId, idToCode),
            Milestones = template.Milestones.OrderBy(m => m.SortOrder).Select(m => new MilestoneDto
            {
                Id = m.Id,
                MilestoneCode = m.MilestoneCode,
                MilestoneName = m.MilestoneName,
                SortOrder = m.SortOrder,
                Remark = m.Remark
            }).ToList(),
            FileItems = template.FileItems.OrderBy(f => f.SortOrder).Select(f => new FileTemplateItemDto
            {
                Id = f.Id,
                SortOrder = f.SortOrder,
                FileName = f.FileName,
                Required = f.Required,
                IsPublic = f.IsPublic,
                ViewRoles = f.ViewRoles,
                DeptId = f.DeptId,
                DeptName = f.DeptName,
                Remark = f.Remark
            }).ToList(),
            Members = template.Members.OrderBy(m => m.SortOrder).Select(m => new MemberDto
            {
                Id = m.Id,
                SortOrder = m.SortOrder,
                RoleId = m.RoleId,
                RoleName = m.RoleName,
                MemberId = m.MemberId,
                MemberName = m.MemberName,
                DeptId = m.DeptId,
                DeptName = m.DeptName,
                Remark = m.Remark
            }).ToList()
        };
        var treeMs = treeSw.Elapsed.TotalMilliseconds;

        _logger.LogDebug(
            "[TMPL-DTL] 模板Code={Code} | 总耗时={TotalMs:F1}ms | DB加载={DbMs:F1}ms | 依赖查询={DepsMs:F1}ms | 树组装={TreeMs:F1}ms | PlanNodes={NodeCount}",
            template.TemplateCode, sw.Elapsed.TotalMilliseconds, loadMs, depsMs, treeMs, template.PlanNodes.Count);

        return result;
    }

    private static List<PlanNodeDto> BuildPlanNodeTree(
        List<PlanNode> allNodes,
        long? parentId,
        ILookup<long, PlanNodeDependency> depsByNodeId,
        Dictionary<long, string> idToCode)
    {
        return allNodes
            .Where(n => n.ParentId == parentId)
            .OrderBy(n => n.SortOrder)
            .Select(n =>
            {
                var nodeTypeName = n.NodeType == 1 ? "计划节点" : "任务节点";
                var nodeDeps = depsByNodeId[n.Id];
                return new PlanNodeDto
                {
                    Id = n.Id,
                    ParentId = n.ParentId,
                    NodeCode = n.NodeCode,
                    NodeName = n.NodeName,
                    NodeType = n.NodeType,
                    NodeTypeName = nodeTypeName,
                    SortOrder = n.SortOrder,
                    StdDuration = n.StdDuration,
                    DeliverableCnt = n.DeliverableCnt,
                    AssigneeId = n.AssigneeId,
                    AssigneeName = n.AssigneeName,
                    DeptId = n.DeptId,
                    DeptName = n.DeptName,
                    Remark = n.Remark,
                    TaskCategory = n.TaskCategory,
                    Predecessors = nodeDeps.Select(d => new PredecessorDto
                    {
                        PredecessorId = d.PredecessorId,
                        PredecessorCode = idToCode.GetValueOrDefault(d.PredecessorId, ""),
                        DependencyType = d.DependencyType,
                        LagDays = d.LagDays
                    }).ToList(),
                    Children = BuildPlanNodeTree(allNodes, n.Id, depsByNodeId, idToCode)
                };
            })
            .ToList();
    }
}
