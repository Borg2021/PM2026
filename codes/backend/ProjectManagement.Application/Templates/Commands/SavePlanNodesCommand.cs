using MediatR;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.Templates.Commands;

public record SavePlanNodesCommand(long TemplateId, List<PlanNodeInput> Nodes) : IRequest;

public class PlanNodeInput
{
    public string NodeCode { get; set; } = "";
    public string NodeName { get; set; } = "";
    public int NodeType { get; set; }
    public int SortOrder { get; set; }
    public int? StdDuration { get; set; }
    public int DeliverableCnt { get; set; }
    public long? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public long? DeptId { get; set; }
    public string? DeptName { get; set; }
    public string? Remark { get; set; }
    public string? TaskCategory { get; set; }
    public List<PredecessorInput> Predecessors { get; set; } = new();
    public List<PlanNodeInput> Children { get; set; } = new();
}

public class PredecessorInput
{
    public string PredecessorCode { get; set; } = "";
    public string DependencyType { get; set; } = "FS";
    public int LagDays { get; set; }
}

public class SavePlanNodesHandler : IRequestHandler<SavePlanNodesCommand>
{
    private static readonly HashSet<string> ValidTypes = new() { "FS", "SS", "FF", "SF" };
    private readonly ITemplateRepository _repo;

    public SavePlanNodesHandler(ITemplateRepository repo) => _repo = repo;

    public async Task Handle(SavePlanNodesCommand request, CancellationToken cancellationToken)
    {
        var rules = ParseCodeRule(await _repo.GetSysParamValueAsync("plan_code_rule"));

        await _repo.DeletePlanNodesAsync(request.TemplateId);

        var codeToId = new Dictionary<string, long>();
        var codeMap = new Dictionary<string, string>();   // oldCode → newCode
        var pendingDeps = new List<(string NodeCode, List<PredecessorInput> Deps)>();

        await InsertNodesAsync(request.TemplateId, request.Nodes, null, codeToId, codeMap, pendingDeps, rules, null, 0);

        // 用 codeMap 更新依赖中的 PredecessorCode，解决移动/重新编号后引用失效的问题
        foreach (var (nodeCode, inputs) in pendingDeps)
        {
            foreach (var pred in inputs)
            {
                if (codeMap.TryGetValue(pred.PredecessorCode, out var newPredCode))
                    pred.PredecessorCode = newPredCode;
            }
        }

        var dependencies = new List<PlanNodeDependency>();
        foreach (var (nodeCode, inputs) in pendingDeps)
        {
            if (!codeToId.TryGetValue(nodeCode, out var planNodeId)) continue;
            foreach (var pred in inputs)
            {
                if (!ValidTypes.Contains(pred.DependencyType)) continue;
                if (!codeToId.TryGetValue(pred.PredecessorCode, out var predId)) continue;
                if (predId == planNodeId) continue;
                dependencies.Add(new PlanNodeDependency
                {
                    PlanNodeId = planNodeId,
                    PredecessorId = predId,
                    DependencyType = pred.DependencyType,
                    LagDays = pred.LagDays
                });
            }
        }

        if (dependencies.Count > 0)
            await _repo.AddDependenciesAsync(dependencies);
    }

    private static int[] ParseCodeRule(string? ruleStr)
    {
        if (string.IsNullOrWhiteSpace(ruleStr)) return new[] { 3, 2, 2 };
        try
        {
            return ruleStr.Split(',')
                .Select(s => int.Parse(s.Trim()))
                .ToArray();
        }
        catch
        {
            return new[] { 3, 2, 2 };
        }
    }

    private async Task InsertNodesAsync(
        long templateId,
        List<PlanNodeInput> inputs,
        long? parentId,
        Dictionary<string, long> codeToId,
        Dictionary<string, string> codeMap,
        List<(string NodeCode, List<PredecessorInput> Deps)> pendingDeps,
        int[] rules,
        string? parentNo,
        int level)
    {
        for (int i = 0; i < inputs.Count; i++)
        {
            var input = inputs[i];

            if (input.NodeType == 2)
            {
                input.StdDuration = 0;
                input.Children = new List<PlanNodeInput>();
            }

            // 按层级规则重新编号，同时记录 oldCode → newCode 映射
            var digits = level < rules.Length ? rules[level] : 2;
            var padded = (i + 1).ToString().PadLeft(digits, '0');
            var newNodeCode = parentNo == null ? padded : $"{parentNo}.{padded}";

            if (!string.IsNullOrWhiteSpace(input.NodeCode) && input.NodeCode != newNodeCode)
                codeMap[input.NodeCode] = newNodeCode;

            input.NodeCode = newNodeCode;

            var node = new PlanNode
            {
                TemplateId = templateId,
                ParentId = parentId,
                NodeCode = input.NodeCode,
                NodeName = input.NodeName,
                NodeType = input.NodeType,
                SortOrder = i + 1,
                StdDuration = input.StdDuration,
                DeliverableCnt = input.DeliverableCnt,
                AssigneeId = input.AssigneeId,
                AssigneeName = input.AssigneeName,
                DeptId = input.DeptId,
                DeptName = input.DeptName,
                Remark = input.Remark,
                TaskCategory = input.TaskCategory
            };

            var savedNode = await _repo.AddPlanNodeAsync(node);
            codeToId[input.NodeCode] = savedNode.Id;

            if (input.Predecessors.Count > 0)
                pendingDeps.Add((input.NodeCode, input.Predecessors));

            if (input.Children.Count > 0)
                await InsertNodesAsync(templateId, input.Children, savedNode.Id, codeToId, codeMap, pendingDeps, rules, input.NodeCode, level + 1);
        }
    }
}
