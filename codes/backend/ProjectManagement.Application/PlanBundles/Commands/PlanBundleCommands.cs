using MediatR;
using ProjectManagement.Domain.Entities;
using ProjectManagement.Domain.Interfaces;

namespace ProjectManagement.Application.PlanBundles.Commands;

public class PlanBundleItemInput
{
    public long TemplateId { get; set; }
    public int SortOrder { get; set; }
}

/* ---- 创建 ---- */
public sealed record CreatePlanBundleCommand(
    string Name,
    string? Description,
    long? CreatedBy,
    string? CreatedByName,
    List<PlanBundleItemInput> Items
) : IRequest<long>;

public class CreatePlanBundleHandler : IRequestHandler<CreatePlanBundleCommand, long>
{
    private readonly IPlanBundleRepository _repo;
    public CreatePlanBundleHandler(IPlanBundleRepository repo) => _repo = repo;

    public async Task<long> Handle(CreatePlanBundleCommand cmd, CancellationToken ct)
    {
        var templateIds = cmd.Items.Select(i => i.TemplateId).Distinct().ToList();
        var templates = await _repo.GetTemplatesByIdsAsync(templateIds);
        var templateDict = templates.ToDictionary(t => t.Id);

        foreach (var id in templateIds)
        {
            if (!templateDict.TryGetValue(id, out var t))
                throw new InvalidOperationException($"模板 {id} 不存在");
            if (t.TemplateType != 1)
                throw new InvalidOperationException($"模板「{t.TemplateName}」不是计划类型，不能加入模板集");
            if (t.Status != 1)
                throw new InvalidOperationException($"模板「{t.TemplateName}」已被删除");
        }

        var bundle = new PlanBundle
        {
            Name = cmd.Name.Trim(),
            Description = cmd.Description?.Trim(),
            CreatedBy = cmd.CreatedBy,
            CreatedByName = cmd.CreatedByName,
            CreatedAt = DateTime.UtcNow,
            Items = cmd.Items.Select(i => new PlanBundleItem
            {
                TemplateId = i.TemplateId,
                SortOrder = i.SortOrder
            }).ToList()
        };
        return (await _repo.AddAsync(bundle)).Id;
    }
}

/* ---- 更新 ---- */
public sealed record UpdatePlanBundleCommand(
    long Id,
    string Name,
    string? Description,
    List<PlanBundleItemInput> Items
) : IRequest;

public class UpdatePlanBundleHandler : IRequestHandler<UpdatePlanBundleCommand>
{
    private readonly IPlanBundleRepository _repo;
    public UpdatePlanBundleHandler(IPlanBundleRepository repo) => _repo = repo;

    public async Task Handle(UpdatePlanBundleCommand cmd, CancellationToken ct)
    {
        var bundle = await _repo.GetByIdAsync(cmd.Id)
            ?? throw new InvalidOperationException("模板集不存在");

        var templateIds = cmd.Items.Select(i => i.TemplateId).Distinct().ToList();
        var templates = await _repo.GetTemplatesByIdsAsync(templateIds);
        var templateDict = templates.ToDictionary(t => t.Id);

        foreach (var id in templateIds)
        {
            if (!templateDict.TryGetValue(id, out var t))
                throw new InvalidOperationException($"模板 {id} 不存在");
            if (t.TemplateType != 1)
                throw new InvalidOperationException($"模板「{t.TemplateName}」不是计划类型");
        }

        bundle.Name = cmd.Name.Trim();
        bundle.Description = cmd.Description?.Trim();
        bundle.Items.Clear();
        bundle.Items = cmd.Items.Select(i => new PlanBundleItem
        {
            BundleId = bundle.Id,
            TemplateId = i.TemplateId,
            SortOrder = i.SortOrder
        }).ToList();
        await _repo.UpdateAsync(bundle);
    }
}

/* ---- 删除 ---- */
public sealed record DeletePlanBundleCommand(long Id) : IRequest;

public class DeletePlanBundleHandler : IRequestHandler<DeletePlanBundleCommand>
{
    private readonly IPlanBundleRepository _repo;
    public DeletePlanBundleHandler(IPlanBundleRepository repo) => _repo = repo;

    public async Task Handle(DeletePlanBundleCommand cmd, CancellationToken ct)
    {
        var bundle = await _repo.GetByIdAsync(cmd.Id)
            ?? throw new InvalidOperationException("模板集不存在");
        await _repo.DeleteAsync(bundle);
    }
}

/* ---- 组装 ---- */
public sealed record AssemblePlanBundleCommand(
    long BundleId,
    string NewTemplateName,
    long? CreatedBy,
    string? CreatedByName
) : IRequest<long>;

public class AssemblePlanBundleHandler : IRequestHandler<AssemblePlanBundleCommand, long>
{
    private readonly IPlanBundleRepository _repo;
    public AssemblePlanBundleHandler(IPlanBundleRepository repo) => _repo = repo;

    public async Task<long> Handle(AssemblePlanBundleCommand cmd, CancellationToken ct)
    {
        var bundle = await _repo.GetByIdAsync(cmd.BundleId)
            ?? throw new InvalidOperationException("模板集不存在");

        if (bundle.Items.Count == 0)
            throw new InvalidOperationException("模板集为空，无法组装");

        var templateIds = bundle.Items.Select(i => i.TemplateId).ToList();
        var templates = await _repo.GetTemplatesByIdsAsync(templateIds);

        foreach (var id in templateIds)
        {
            var t = templates.FirstOrDefault(x => x.Id == id);
            if (t == null) throw new InvalidOperationException($"模板 {id} 不存在");
            if (t.TemplateType != 1) throw new InvalidOperationException($"模板「{t.TemplateName}」不是计划类型");
        }

        var allNodes = await _repo.GetPlanNodesByTemplateIdsAsync(templateIds);
        var allDeps = await _repo.GetDependenciesByNodeIdsAsync(allNodes.Select(n => n.Id).ToList());

        // 读取编号规则
        var rules = ParseCodeRule(await _repo.GetSysParamValueAsync("plan_code_rule"));

        var newCode = "ASM-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var newTemplate = new Template
        {
            TemplateCode = newCode,
            TemplateName = cmd.NewTemplateName.Trim(),
            TemplateType = 1,
            Description = $"由模板集「{bundle.Name}」组装生成",
            Status = 1,
            CreatedBy = cmd.CreatedBy,
            CreatedByName = cmd.CreatedByName,
            CreatedAt = DateTime.UtcNow
        };
        await _repo.AddTemplateAsync(newTemplate);

        // 第一遍：创建统一根节点（项目），并按层级收集重新编号所有子节点
        var rootDigits = rules.Length > 0 ? rules[0] : 3;
        var rootCode = 1.ToString().PadLeft(rootDigits, '0');
        var rootNode = new PlanNode
        {
            TemplateId = newTemplate.Id,
            ParentId = null,
            NodeCode = rootCode,
            NodeName = "项目",
            NodeType = 1,
            SortOrder = 1,
            StdDuration = null,
            DeliverableCnt = 0,
            AssigneeId = null,
            AssigneeName = null,
            DeptId = null,
            DeptName = null,
            Remark = null
        };

        // 第一遍：按层级收集并重新编号所有节点（全部挂在“项目”根节点下）
        var flat = new List<(PlanNode Old, PlanNode New, long? OldParentId)>();
        var rootIndex = 0;
        foreach (var item in bundle.Items.OrderBy(i => i.SortOrder))
        {
            var itemNodes = allNodes.Where(n => n.TemplateId == item.TemplateId).ToList();
            var roots = itemNodes.Where(n => n.ParentId == null).OrderBy(n => n.SortOrder).ToList();
            foreach (var root in roots)
            {
                CollectFlat(root, 1, rootIndex, newTemplate.Id, itemNodes, rules, rootCode, flat);
                rootIndex++;
            }
        }

        // 批量保存所有新节点（含“项目”根节点）
        var nodesToAdd = new List<PlanNode> { rootNode };
        nodesToAdd.AddRange(flat.Select(x => x.New));
        await _repo.AddPlanNodesAsync(nodesToAdd);

        // 第二遍：回填 ParentId
        var idMap = flat.ToDictionary(x => x.Old.Id, x => x.New.Id);
        foreach (var (old, newNode, oldParentId) in flat)
        {
            if (oldParentId != null && idMap.TryGetValue(oldParentId.Value, out var newPid))
                newNode.ParentId = newPid;
            else
                newNode.ParentId = rootNode.Id;
        }
        await _repo.SaveChangesAsync();

        // 第三遍：拷贝依赖
        var newDeps = allDeps
            .Where(d => idMap.ContainsKey(d.PlanNodeId) && idMap.ContainsKey(d.PredecessorId))
            .Select(d => new PlanNodeDependency
            {
                PlanNodeId = idMap[d.PlanNodeId],
                PredecessorId = idMap[d.PredecessorId],
                DependencyType = d.DependencyType,
                LagDays = d.LagDays
            })
            .ToList();

        if (newDeps.Count > 0)
            await _repo.AddDependenciesAsync(newDeps);

        return newTemplate.Id;
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

    private static void CollectFlat(
        PlanNode source, int level, int index,
        long newTemplateId,
        List<PlanNode> itemNodes,
        int[] rules,
        string? parentNo,
        List<(PlanNode Old, PlanNode New, long? OldParentId)> flat)
    {
        var digits = level < rules.Length ? rules[level] : 2;
        var padded = (index + 1).ToString().PadLeft(digits, '0');
        var nodeCode = parentNo == null ? padded : $"{parentNo}.{padded}";

        var newNode = new PlanNode
        {
            TemplateId = newTemplateId,
            ParentId = null,
            NodeCode = nodeCode,
            NodeName = source.NodeName,
            NodeType = source.NodeType,
            SortOrder = source.SortOrder,
            StdDuration = source.StdDuration,
            DeliverableCnt = source.DeliverableCnt,
            AssigneeId = source.AssigneeId,
            AssigneeName = source.AssigneeName,
            DeptId = source.DeptId,
            DeptName = source.DeptName,
            Remark = source.Remark,
            TaskCategory = source.TaskCategory
        };
        flat.Add((source, newNode, source.ParentId));

        var children = itemNodes.Where(n => n.ParentId == source.Id).OrderBy(n => n.SortOrder).ToList();
        for (int i = 0; i < children.Count; i++)
        {
            CollectFlat(children[i], level + 1, i, newTemplateId, itemNodes, rules, nodeCode, flat);
        }
    }
}
