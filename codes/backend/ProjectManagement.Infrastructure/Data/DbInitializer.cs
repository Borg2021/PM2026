using Microsoft.EntityFrameworkCore;
using ProjectManagement.Domain.Entities;

namespace ProjectManagement.Infrastructure.Data;

public static class DbInitializer
{
    public static void Seed(ModelBuilder modelBuilder)
    {
        // 种子部门
        modelBuilder.Entity<Department>().HasData(
            new Department { Id = 1, Name = "总经办", SortOrder = 1 },
            new Department { Id = 2, Name = "技术部", SortOrder = 2 },
            new Department { Id = 3, Name = "市场部", SortOrder = 3 },
            new Department { Id = 4, Name = "采购部", SortOrder = 4 },
            new Department { Id = 5, Name = "财务部", SortOrder = 5 },
            new Department { Id = 6, Name = "人力资源部", SortOrder = 6 }
        );

        // 种子系统角色
        modelBuilder.Entity<RoleDict>().HasData(
            new RoleDict { Id = 1, Name = "admin" },
            new RoleDict { Id = 2, Name = "templateAdmin" },
            new RoleDict { Id = 3, Name = "user" },
            new RoleDict { Id = 4, Name = "项目经理" },
            new RoleDict { Id = 5, Name = "任务经理" },
            new RoleDict { Id = 6, Name = "开发工程师" },
            new RoleDict { Id = 7, Name = "测试工程师" },
            new RoleDict { Id = 8, Name = "产品经理" },
            new RoleDict { Id = 9, Name = "UI设计师" },
            new RoleDict { Id = 10, Name = "采购专员" }
        );

        // 种子字典类型
        modelBuilder.Entity<DictType>().HasData(
            new DictType { Id = 1, DictTypeCode = "project_type", DictTypeName = "项目类型", Remark = "项目类型分类" },
            new DictType { Id = 2, DictTypeCode = "product_type", DictTypeName = "产品类型", Remark = "产品类型分类" },
            new DictType { Id = 3, DictTypeCode = "task_category", DictTypeName = "任务类别", Remark = "任务类别分类" }
        );

        // 种子字典项
        modelBuilder.Entity<DictItem>().HasData(
            new DictItem { Id = 1, DictType = "project_type", DictCode = "integrated", DictLabel = "集成项目", SortOrder = 1 },
            new DictItem { Id = 2, DictType = "project_type", DictCode = "standalone", DictLabel = "单机项目", SortOrder = 2 }
        );

        // ────── RBAC 权限种子数据 ──────
        modelBuilder.Entity<Permission>().HasData(
            new Permission { Id = 1, Code = "workbench", Name = "我的工作台", Type = 1, SortOrder = 1, Icon = "Monitor", Path = "/workbench" },
            new Permission { Id = 2, Code = "system", Name = "系统管理", Type = 1, SortOrder = 2, Icon = "Setting", Path = "/system" },
            new Permission { Id = 3, Code = "project", Name = "项目管理", Type = 1, SortOrder = 3, Icon = "Folder", Path = "/project" },
            new Permission { Id = 4, Code = "system:users", Name = "人员管理", ParentId = 2, Type = 1, SortOrder = 1, Icon = "User", Path = "/system/users" },
            new Permission { Id = 5, Code = "system:departments", Name = "部门管理", ParentId = 2, Type = 1, SortOrder = 2, Icon = "OfficeBuilding", Path = "/system/departments" },
            new Permission { Id = 6, Code = "system:permissions", Name = "权限管理", ParentId = 2, Type = 1, SortOrder = 4, Icon = "Lock", Path = "/system/permissions" },
            new Permission { Id = 7, Code = "system:config", Name = "模板配置", ParentId = 2, Type = 1, SortOrder = 5, Icon = "Tools", Path = "/system/config" },
            new Permission { Id = 8, Code = "system:param", Name = "参数配置", ParentId = 2, Type = 1, SortOrder = 6, Icon = "Operation", Path = "/system/param-config" },
            new Permission { Id = 9, Code = "template:list", Name = "模板管理", ParentId = 7, Type = 1, SortOrder = 1, Icon = "Document", Path = "/template/list" },
            new Permission { Id = 10, Code = "template:bundles", Name = "计划模板集", ParentId = 7, Type = 1, SortOrder = 2, Icon = "Collection", Path = "/template/bundles" },
            new Permission { Id = 11, Code = "system:dict-types", Name = "字典类型管理", ParentId = 8, Type = 1, SortOrder = 1, Icon = "List", Path = "/system/dict-types" },
            new Permission { Id = 12, Code = "system:dicts", Name = "字典管理", ParentId = 8, Type = 1, SortOrder = 2, Icon = "Reading", Path = "/system/dicts" },
            new Permission { Id = 13, Code = "system:sys-params", Name = "系统参数", ParentId = 8, Type = 1, SortOrder = 3, Icon = "Setting", Path = "/system/sys-params" },
            new Permission { Id = 14, Code = "project:list", Name = "项目管理", ParentId = 3, Type = 1, SortOrder = 1, Icon = "Document", Path = "/project/list" },
            new Permission { Id = 15, Code = "system:user:create", Name = "创建用户", ParentId = 4, Type = 2, SortOrder = 1 },
            new Permission { Id = 16, Code = "system:user:edit", Name = "编辑用户", ParentId = 4, Type = 2, SortOrder = 2 },
            new Permission { Id = 17, Code = "system:user:delete", Name = "删除用户", ParentId = 4, Type = 2, SortOrder = 3 },
            new Permission { Id = 18, Code = "system:user:reset-pwd", Name = "重置密码", ParentId = 4, Type = 2, SortOrder = 4 },
            new Permission { Id = 19, Code = "system:dept:create", Name = "创建部门", ParentId = 5, Type = 2, SortOrder = 1 },
            new Permission { Id = 20, Code = "system:dept:edit", Name = "编辑部门", ParentId = 5, Type = 2, SortOrder = 2 },
            new Permission { Id = 21, Code = "system:dept:delete", Name = "删除部门", ParentId = 5, Type = 2, SortOrder = 3 },
            new Permission { Id = 22, Code = "system:role:create", Name = "创建角色", ParentId = 6, Type = 2, SortOrder = 1 },
            new Permission { Id = 23, Code = "system:role:edit", Name = "编辑角色", ParentId = 6, Type = 2, SortOrder = 2 },
            new Permission { Id = 24, Code = "system:role:delete", Name = "删除角色", ParentId = 6, Type = 2, SortOrder = 3 },
            new Permission { Id = 25, Code = "system:role:assign-perm", Name = "分配权限", ParentId = 6, Type = 2, SortOrder = 4 },
            new Permission { Id = 26, Code = "system:user:assign-role", Name = "分配角色", ParentId = 4, Type = 2, SortOrder = 5 },
            new Permission { Id = 34, Code = "project:list:view", Name = "查看列表", ParentId = 14, Type = 2, SortOrder = 1 },
            new Permission { Id = 35, Code = "project:detail:view", Name = "查看项目", ParentId = 14, Type = 2, SortOrder = 2 },
            new Permission { Id = 27, Code = "project:create", Name = "创建项目", ParentId = 14, Type = 2, SortOrder = 3 },
            new Permission { Id = 28, Code = "project:edit", Name = "编辑项目", ParentId = 14, Type = 2, SortOrder = 4 },
            new Permission { Id = 29, Code = "project:delete", Name = "删除项目", ParentId = 14, Type = 2, SortOrder = 5 },
            new Permission { Id = 30, Code = "template:create", Name = "创建模板", ParentId = 9, Type = 2, SortOrder = 1 },
            new Permission { Id = 31, Code = "template:edit", Name = "编辑模板", ParentId = 9, Type = 2, SortOrder = 2 },
            new Permission { Id = 32, Code = "template:delete", Name = "删除模板", ParentId = 9, Type = 2, SortOrder = 3 },
            new Permission { Id = 33, Code = "system:functions", Name = "职能管理", ParentId = 2, Type = 1, SortOrder = 3, Icon = "Stamp", Path = "/system/functions" },
            new Permission { Id = 36, Code = "project:tasks", Name = "任务管理", ParentId = 3, Type = 1, SortOrder = 2, Icon = "List", Path = "/project/tasks" },
            new Permission { Id = 74, Code = "project:files", Name = "文件管理", ParentId = 3, Type = 1, SortOrder = 3, Icon = "FolderOpened", Path = "/project/files" },
            new Permission { Id = 38, Code = "project:field:basic", Name = "查看项目基本信息", ParentId = 14, Type = 1, SortOrder = 6 },
            new Permission { Id = 39, Code = "project:file:view", Name = "查看项目文件", ParentId = 14, Type = 2, SortOrder = 7 },
            new Permission { Id = 40, Code = "project:file:view:public", Name = "公开", ParentId = 39, Type = 2, SortOrder = 1 },
            new Permission { Id = 41, Code = "project:file:view:nonpublic", Name = "非公开", ParentId = 39, Type = 2, SortOrder = 2 },
            // 查看项目基本信息 → 子字段权限
            new Permission { Id = 42, Code = "project:field:contract-code", Name = "客户合同编号", ParentId = 38, Type = 2, SortOrder = 1 },
            new Permission { Id = 43, Code = "project:field:category-code", Name = "项目地点(省、市)", ParentId = 38, Type = 2, SortOrder = 2 },
            new Permission { Id = 44, Code = "project:field:customer-name", Name = "客户名称", ParentId = 38, Type = 2, SortOrder = 3 },
            new Permission { Id = 45, Code = "project:field:regional-manager", Name = "客户联系人", ParentId = 38, Type = 2, SortOrder = 4 },
            new Permission { Id = 70, Code = "project:field:customer-contact-phone", Name = "客户联系电话", ParentId = 38, Type = 2, SortOrder = 5 },
            new Permission { Id = 71, Code = "project:field:customer-contact-email", Name = "客户联系邮箱", ParentId = 38, Type = 2, SortOrder = 6 },
            new Permission { Id = 46, Code = "project:field:final-customer", Name = "最终业主", ParentId = 38, Type = 2, SortOrder = 7 },
            new Permission { Id = 47, Code = "project:field:pm-center", Name = "业主联系人", ParentId = 38, Type = 2, SortOrder = 8 },
            new Permission { Id = 72, Code = "project:field:owner-contact-phone", Name = "业主联系电话", ParentId = 38, Type = 2, SortOrder = 9 },
            new Permission { Id = 73, Code = "project:field:business-contact-email", Name = "业主联系邮箱", ParentId = 38, Type = 2, SortOrder = 10 },
            new Permission { Id = 48, Code = "project:field:delivery-location", Name = "交付详细地址", ParentId = 38, Type = 2, SortOrder = 11 },
            new Permission { Id = 49, Code = "project:field:project-type", Name = "项目类型", ParentId = 38, Type = 2, SortOrder = 12 },
            new Permission { Id = 50, Code = "project:field:engineering-center", Name = "责任部门", ParentId = 38, Type = 2, SortOrder = 13 },
            new Permission { Id = 51, Code = "project:field:pre-sales", Name = "售前联系人", ParentId = 38, Type = 2, SortOrder = 14 },
            new Permission { Id = 52, Code = "project:field:project-manager", Name = "项目经理", ParentId = 38, Type = 2, SortOrder = 15 },
            new Permission { Id = 53, Code = "project:field:sales-manager", Name = "销售负责人", ParentId = 38, Type = 2, SortOrder = 16 },
            new Permission { Id = 54, Code = "project:field:required-delivery", Name = "合同要求交期", ParentId = 38, Type = 2, SortOrder = 17 },
            new Permission { Id = 55, Code = "project:field:accepted-delivery", Name = "承诺交期", ParentId = 38, Type = 2, SortOrder = 18 },
            new Permission { Id = 56, Code = "project:field:special-terms", Name = "特殊条款", ParentId = 38, Type = 2, SortOrder = 19 },
            new Permission { Id = 57, Code = "project:field:remark", Name = "备注", ParentId = 38, Type = 2, SortOrder = 20 },
            new Permission { Id = 58, Code = "settings", Name = "系统设置", Type = 1, SortOrder = 4, Icon = "Setting", Path = "/system/settings" }
        );

        // ────── RBAC 角色种子数据 ──────
        modelBuilder.Entity<Role>().HasData(
            new Role { Id = 1, Code = "admin", Name = "系统管理员", Description = "拥有所有系统权限", Status = 1, DataScope = 1, CreatedAt = new DateTime(2026, 5, 13, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 2, Code = "template_admin", Name = "模板管理员", Description = "管理模板和配置", Status = 1, DataScope = 4, CreatedAt = new DateTime(2026, 5, 13, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 3, Code = "project_manager", Name = "项目经理", Description = "管理项目", Status = 1, DataScope = 4, CreatedAt = new DateTime(2026, 5, 13, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 4, Code = "task_manager", Name = "任务经理", Description = "管理任务", Status = 1, DataScope = 4, CreatedAt = new DateTime(2026, 5, 13, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 5, Code = "user", Name = "普通用户", Description = "基本访问权限", Status = 1, DataScope = 5, CreatedAt = new DateTime(2026, 5, 13, 0, 0, 0, DateTimeKind.Utc) },
            new Role { Id = 6, Code = "project_admin", Name = "项目管理员", Description = "项目模块全部数据与操作权限", Status = 1, DataScope = 1, CreatedAt = new DateTime(2026, 5, 13, 0, 0, 0, DateTimeKind.Utc) }
        );

        // ────── RBAC 角色-权限映射种子数据 ──────
        var rpId = 0;
        var rp = (long roleId, long permId) => new RolePermission { Id = ++rpId, RoleId = roleId, PermissionId = permId };
        modelBuilder.Entity<RolePermission>().HasData(
            // admin: 所有权限
            rp(1, 1), rp(1, 2), rp(1, 3), rp(1, 4), rp(1, 5), rp(1, 6), rp(1, 7), rp(1, 8), rp(1, 9), rp(1, 10),
            rp(1, 11), rp(1, 12), rp(1, 13), rp(1, 14), rp(1, 15), rp(1, 16), rp(1, 17), rp(1, 18), rp(1, 19), rp(1, 20),
            rp(1, 21), rp(1, 22), rp(1, 23), rp(1, 24), rp(1, 25), rp(1, 26), rp(1, 27), rp(1, 28), rp(1, 29), rp(1, 30),
            rp(1, 31), rp(1, 32), rp(1, 33), rp(1, 34), rp(1, 35), rp(1, 36), rp(1, 38), rp(1, 39), rp(1, 40), rp(1, 41),
            rp(1, 42), rp(1, 43), rp(1, 44), rp(1, 45), rp(1, 46), rp(1, 47), rp(1, 48), rp(1, 49), rp(1, 50), rp(1, 51),
            rp(1, 52), rp(1, 53), rp(1, 54), rp(1, 55), rp(1, 56), rp(1, 57), rp(1, 58),
            rp(1, 70), rp(1, 71), rp(1, 72), rp(1, 73), rp(1, 74),
            // template_admin: 工作台 + 模板配置 + 模板管理 + 计划模板集 + 文件模板 + 项目管理查看
            rp(2, 1), rp(2, 7), rp(2, 9), rp(2, 10), rp(2, 3), rp(2, 14), rp(2, 34), rp(2, 35),
            // project_manager: 工作台 + 项目管理（查看与编辑，不含新建）
            rp(3, 1), rp(3, 3), rp(3, 14), rp(3, 34), rp(3, 35), rp(3, 28),
            // project_admin: 项目域全权限
            rp(6, 1), rp(6, 3), rp(6, 14), rp(6, 34), rp(6, 35), rp(6, 27), rp(6, 28), rp(6, 29),
            // task_manager: 工作台
            rp(4, 1),
            // user: 工作台 + 项目查看 + 创建 + 编辑
            rp(5, 1), rp(5, 3), rp(5, 14), rp(5, 34), rp(5, 35), rp(5, 27), rp(5, 28)
        );
    }

    public static async Task EnsureSeedAsync(AppDbContext db)
    {
        // 确保角色存在（兼容已有数据库升级）
        var allRoles = new[] { "admin", "templateAdmin", "user", "项目经理", "任务经理", "开发工程师", "测试工程师", "产品经理", "UI设计师", "采购专员" };
        foreach (var roleName in allRoles)
        {
            if (!db.RoleDicts.Any(r => r.Name == roleName))
            {
                db.RoleDicts.Add(new RoleDict { Name = roleName });
            }
        }

        // 确保字典类型存在
        var defaultDictTypes = new[]
        {
            new { Code = "project_type", Name = "项目类型", Remark = "项目类型分类" },
            new { Code = "product_type", Name = "产品类型", Remark = "产品类型分类" },
            new { Code = "task_category", Name = "任务类别", Remark = "任务类别分类" }
        };
        foreach (var dt in defaultDictTypes)
        {
            if (!db.DictTypes.Any(d => d.DictTypeCode == dt.Code))
            {
                db.DictTypes.Add(new DictType { DictTypeCode = dt.Code, DictTypeName = dt.Name, Remark = dt.Remark });
            }
        }
        await db.SaveChangesAsync();

        // 确保字典项存在（项目类型）
        if (!db.DictItems.Any(d => d.DictType == "project_type"))
        {
            db.DictItems.AddRange(
                new DictItem { DictType = "project_type", DictCode = "integrated", DictLabel = "集成项目", SortOrder = 1 },
                new DictItem { DictType = "project_type", DictCode = "standalone", DictLabel = "单机项目", SortOrder = 2 }
            );
        }
        await db.SaveChangesAsync();

        if (!db.Users.Any())
        {
            db.Users.Add(new User
            {
                Username = "admin",
                Password = BCrypt.Net.BCrypt.HashPassword("admin123"),
                RealName = "系统管理员",
                Role = "admin",
                Status = 1,
                CreatedAt = new DateTime(2026, 5, 13, 0, 0, 0, DateTimeKind.Utc)
            });
            await db.SaveChangesAsync();
        }

        // 确保项目强制完成参数存在
        if (!db.SysParams.Any(p => p.ParamKey == "is_project_force_finish"))
        {
            db.SysParams.Add(new SysParam { ParamKey = "is_project_force_finish", ParamValue = "0", Description = "是否强制完成项目：1=可强制完成 0=需检查所有任务已完成" });
            await db.SaveChangesAsync();
        }

        // 确保 RBAC 权限存在（兼容已有数据库升级）
        if (!db.Permissions.Any())
        {
            db.Permissions.AddRange(
                new Permission { Id = 1, Code = "workbench", Name = "我的工作台", Type = 1, SortOrder = 1, Icon = "Monitor", Path = "/workbench" },
                new Permission { Id = 2, Code = "system", Name = "系统管理", Type = 1, SortOrder = 2, Icon = "Setting", Path = "/system" },
                new Permission { Id = 3, Code = "project", Name = "项目管理", Type = 1, SortOrder = 3, Icon = "Folder", Path = "/project" },
                new Permission { Id = 4, Code = "system:users", Name = "人员管理", ParentId = 2, Type = 1, SortOrder = 1, Icon = "User", Path = "/system/users" },
                new Permission { Id = 5, Code = "system:departments", Name = "部门管理", ParentId = 2, Type = 1, SortOrder = 2, Icon = "OfficeBuilding", Path = "/system/departments" },
                new Permission { Id = 6, Code = "system:permissions", Name = "权限管理", ParentId = 2, Type = 1, SortOrder = 4, Icon = "Lock", Path = "/system/permissions" },
                new Permission { Id = 7, Code = "system:config", Name = "模板配置", ParentId = 2, Type = 1, SortOrder = 5, Icon = "Tools", Path = "/system/config" },
                new Permission { Id = 8, Code = "system:param", Name = "参数配置", ParentId = 2, Type = 1, SortOrder = 6, Icon = "Operation", Path = "/system/param-config" },
                new Permission { Id = 9, Code = "template:list", Name = "模板管理", ParentId = 7, Type = 1, SortOrder = 1, Icon = "Document", Path = "/template/list" },
                new Permission { Id = 10, Code = "template:bundles", Name = "计划模板集", ParentId = 7, Type = 1, SortOrder = 2, Icon = "Collection", Path = "/template/bundles" },
                new Permission { Id = 11, Code = "system:dict-types", Name = "字典类型管理", ParentId = 8, Type = 1, SortOrder = 1, Icon = "List", Path = "/system/dict-types" },
                new Permission { Id = 12, Code = "system:dicts", Name = "字典管理", ParentId = 8, Type = 1, SortOrder = 2, Icon = "Reading", Path = "/system/dicts" },
                new Permission { Id = 13, Code = "system:sys-params", Name = "系统参数", ParentId = 8, Type = 1, SortOrder = 3, Icon = "Setting", Path = "/system/sys-params" },
                new Permission { Id = 14, Code = "project:list", Name = "项目管理", ParentId = 3, Type = 1, SortOrder = 1, Icon = "Document", Path = "/project/list" },
                new Permission { Id = 15, Code = "system:user:create", Name = "创建用户", ParentId = 4, Type = 2, SortOrder = 1 },
                new Permission { Id = 16, Code = "system:user:edit", Name = "编辑用户", ParentId = 4, Type = 2, SortOrder = 2 },
                new Permission { Id = 17, Code = "system:user:delete", Name = "删除用户", ParentId = 4, Type = 2, SortOrder = 3 },
                new Permission { Id = 18, Code = "system:user:reset-pwd", Name = "重置密码", ParentId = 4, Type = 2, SortOrder = 4 },
                new Permission { Id = 19, Code = "system:dept:create", Name = "创建部门", ParentId = 5, Type = 2, SortOrder = 1 },
                new Permission { Id = 20, Code = "system:dept:edit", Name = "编辑部门", ParentId = 5, Type = 2, SortOrder = 2 },
                new Permission { Id = 21, Code = "system:dept:delete", Name = "删除部门", ParentId = 5, Type = 2, SortOrder = 3 },
                new Permission { Id = 22, Code = "system:role:create", Name = "创建角色", ParentId = 6, Type = 2, SortOrder = 1 },
                new Permission { Id = 23, Code = "system:role:edit", Name = "编辑角色", ParentId = 6, Type = 2, SortOrder = 2 },
                new Permission { Id = 24, Code = "system:role:delete", Name = "删除角色", ParentId = 6, Type = 2, SortOrder = 3 },
                new Permission { Id = 25, Code = "system:role:assign-perm", Name = "分配权限", ParentId = 6, Type = 2, SortOrder = 4 },
                new Permission { Id = 26, Code = "system:user:assign-role", Name = "分配角色", ParentId = 4, Type = 2, SortOrder = 5 },
                new Permission { Id = 34, Code = "project:list:view", Name = "查看列表", ParentId = 14, Type = 2, SortOrder = 1 },
                new Permission { Id = 35, Code = "project:detail:view", Name = "查看项目", ParentId = 14, Type = 2, SortOrder = 2 },
                new Permission { Id = 27, Code = "project:create", Name = "创建项目", ParentId = 14, Type = 2, SortOrder = 3 },
                new Permission { Id = 28, Code = "project:edit", Name = "编辑项目", ParentId = 14, Type = 2, SortOrder = 4 },
                new Permission { Id = 29, Code = "project:delete", Name = "删除项目", ParentId = 14, Type = 2, SortOrder = 5 },
                new Permission { Id = 30, Code = "template:create", Name = "创建模板", ParentId = 9, Type = 2, SortOrder = 1 },
                new Permission { Id = 31, Code = "template:edit", Name = "编辑模板", ParentId = 9, Type = 2, SortOrder = 2 },
                new Permission { Id = 32, Code = "template:delete", Name = "删除模板", ParentId = 9, Type = 2, SortOrder = 3 },
                new Permission { Id = 33, Code = "system:functions", Name = "职能管理", ParentId = 2, Type = 1, SortOrder = 3, Icon = "Stamp", Path = "/system/functions" },
                new Permission { Id = 36, Code = "project:tasks", Name = "任务管理", ParentId = 3, Type = 1, SortOrder = 2, Icon = "List", Path = "/project/tasks" },
                new Permission { Id = 74, Code = "project:files", Name = "文件管理", ParentId = 3, Type = 1, SortOrder = 3, Icon = "FolderOpened", Path = "/project/files" }
            );
            await db.SaveChangesAsync();
        }

        // 确保 RBAC 角色存在（兼容已有数据库升级）
        var rbacRoles = new[]
        {
            new { Code = "admin", Name = "系统管理员", Description = "拥有所有系统权限", DataScope = 1 },
            new { Code = "template_admin", Name = "模板管理员", Description = "管理模板和配置", DataScope = 4 },
            new { Code = "project_manager", Name = "项目经理", Description = "管理项目", DataScope = 4 },
            new { Code = "task_manager", Name = "任务经理", Description = "管理任务", DataScope = 4 },
            new { Code = "user", Name = "普通用户", Description = "基本访问权限", DataScope = 5 },
            new { Code = "project_admin", Name = "项目管理员", Description = "项目模块全部数据与操作权限", DataScope = 1 }
        };
        foreach (var r in rbacRoles)
        {
            var existing = db.Roles.FirstOrDefault(x => x.Code == r.Code);
            if (existing == null)
                db.Roles.Add(new Role { Code = r.Code, Name = r.Name, Description = r.Description, Status = 1, DataScope = r.DataScope, CreatedAt = DateTime.UtcNow });
            else if (existing.DataScope != r.DataScope)
                existing.DataScope = r.DataScope;
        }
        await db.SaveChangesAsync();

        // 角色-权限映射仅在全新空库时由 HasData 种子数据初始化
        // 运行时不再自动补全权限，完全由权限管理页面手动分配

        // 确保职能种子数据存在
        if (!db.Functions.Any())
        {
            db.Functions.AddRange(
                new ProjectManagement.Domain.Entities.Function { Code = "dev", Name = "开发工程师", Description = "负责产品开发实现", SortOrder = 1 },
                new ProjectManagement.Domain.Entities.Function { Code = "test", Name = "测试工程师", Description = "负责产品质量测试", SortOrder = 2 },
                new ProjectManagement.Domain.Entities.Function { Code = "pm", Name = "产品经理", Description = "负责产品需求与规划", SortOrder = 3 },
                new ProjectManagement.Domain.Entities.Function { Code = "ui", Name = "UI设计师", Description = "负责界面视觉设计", SortOrder = 4 },
                new ProjectManagement.Domain.Entities.Function { Code = "purchase", Name = "采购专员", Description = "负责物资采购管理", SortOrder = 5 },
                new ProjectManagement.Domain.Entities.Function { Code = "project_mgr", Name = "项目经理", Description = "负责项目全过程管理", SortOrder = 6 },
                new ProjectManagement.Domain.Entities.Function { Code = "presales", Name = "售前", Description = "负责售前技术支持与方案", SortOrder = 7 },
                new ProjectManagement.Domain.Entities.Function { Code = "sales", Name = "销售", Description = "负责销售与客户对接", SortOrder = 8 }
            );
            await db.SaveChangesAsync();
        }

        // 兼容已有库：补齐项目基本信息选人所需职能
        var requiredFunctions = new (string Code, string Name, string Description, int SortOrder)[]
        {
            ("project_mgr", "项目经理", "负责项目全过程管理", 6),
            ("presales", "售前", "负责售前技术支持与方案", 7),
            ("sales", "销售", "负责销售与客户对接", 8),
        };
        foreach (var f in requiredFunctions)
        {
            if (!db.Functions.Any(x => x.Name == f.Name))
                db.Functions.Add(new ProjectManagement.Domain.Entities.Function
                {
                    Code = f.Code,
                    Name = f.Name,
                    Description = f.Description,
                    SortOrder = f.SortOrder
                });
        }
        await db.SaveChangesAsync();

        // 兼容已有库：修正职能编码为统一值
        var codeFixMap = new Dictionary<string, string>
        {
            ["sq"] = "presales",
            ["xw"] = "sales",
            ["pm"] = "project_mgr",
        };
        foreach (var (oldCode, newCode) in codeFixMap)
        {
            var fn = await db.Functions.FirstOrDefaultAsync(f => f.Code == oldCode);
            if (fn != null && !await db.Functions.AnyAsync(f => f.Code == newCode))
            {
                fn.Code = newCode;
            }
        }
        await db.SaveChangesAsync();

        // 确保项目资料「查看列表 / 查看项目」权限存在（兼容已有库升级）
        var projectListMenu = await db.Permissions.FirstOrDefaultAsync(p => p.Code == "project:list");
        if (projectListMenu != null)
        {
            if (!await db.Permissions.AnyAsync(p => p.Code == "project:list:view"))
            {
                db.Permissions.Add(new Permission
                {
                    Code = "project:list:view",
                    Name = "查看列表",
                    ParentId = projectListMenu.Id,
                    Type = 2,
                    SortOrder = 1
                });
            }
            if (!await db.Permissions.AnyAsync(p => p.Code == "project:detail:view"))
            {
                db.Permissions.Add(new Permission
                {
                    Code = "project:detail:view",
                    Name = "查看项目",
                    ParentId = projectListMenu.Id,
                    Type = 2,
                    SortOrder = 2
                });
            }
            await db.SaveChangesAsync();

            foreach (var code in new[] { "project:create", "project:edit", "project:delete" })
            {
                var perm = await db.Permissions.FirstOrDefaultAsync(p => p.Code == code);
                if (perm == null) continue;
                perm.SortOrder = code switch
                {
                    "project:create" => 3,
                    "project:edit" => 4,
                    "project:delete" => 5,
                    _ => perm.SortOrder
                };
            }
            await db.SaveChangesAsync();

            // 查看/任务菜单权限由权限管理页面手动分配
        }

        // 「任务管理」菜单权限：如不存在则创建，但不自动分配给角色

        // 权限分配由权限管理页面手动控制，运行时不再自动补全

        // 项目资料管理 → 项目管理（兼容已有库升级）
        var projectListPerm = await db.Permissions.FirstOrDefaultAsync(p => p.Code == "project:list");
        if (projectListPerm != null && projectListPerm.Name == "项目资料管理")
        {
            projectListPerm.Name = "项目管理";
            await db.SaveChangesAsync();
        }

        // 将「查看项目基本信息」设为字段组权限（Type=1），并确保子字段权限存在
        var basicFieldGroup = await db.Permissions.FirstOrDefaultAsync(p => p.Code == "project:field:basic");
        if (basicFieldGroup == null)
        {
            var listMenu3 = await db.Permissions.FirstOrDefaultAsync(p => p.Code == "project:list");
            if (listMenu3 != null)
            {
                basicFieldGroup = new Permission
                {
                    Code = "project:field:basic",
                    Name = "查看项目基本信息",
                    ParentId = listMenu3.Id,
                    Type = 1,
                    SortOrder = 6
                };
                db.Permissions.Add(basicFieldGroup);
                await db.SaveChangesAsync();
            }
        }
        else if (basicFieldGroup.Type != 1)
        {
            basicFieldGroup.Type = 1;
            await db.SaveChangesAsync();
        }

        // 确保基础字段权限挂到 project:list 下
        if (basicFieldGroup != null && basicFieldGroup.ParentId != (await db.Permissions.FirstOrDefaultAsync(p => p.Code == "project:list"))?.Id)
        {
            var listMenu = await db.Permissions.FirstOrDefaultAsync(p => p.Code == "project:list");
            if (listMenu != null)
            {
                basicFieldGroup.ParentId = listMenu.Id;
                await db.SaveChangesAsync();
            }
        }

        // 确保子字段权限存在
        var fieldPermDefs = new[]
        {
            ("project:field:contract-code", "客户合同编号", 1),
            ("project:field:category-code", "项目地点(省、市)", 2),
            ("project:field:customer-name", "客户名称", 3),
            ("project:field:regional-manager", "客户联系人", 4),
            ("project:field:customer-contact-phone", "客户联系电话", 5),
            ("project:field:customer-contact-email", "客户联系邮箱", 6),
            ("project:field:final-customer", "最终业主", 7),
            ("project:field:pm-center", "业主联系人", 8),
            ("project:field:owner-contact-phone", "业主联系电话", 9),
            ("project:field:business-contact-email", "业主联系邮箱", 10),
            ("project:field:delivery-location", "交付详细地址", 11),
            ("project:field:project-type", "项目类型", 12),
            ("project:field:engineering-center", "责任部门", 13),
            ("project:field:pre-sales", "售前联系人", 14),
            ("project:field:project-manager", "项目经理", 15),
            ("project:field:sales-manager", "销售负责人", 16),
            ("project:field:required-delivery", "合同要求交期", 17),
            ("project:field:accepted-delivery", "承诺交期", 18),
            ("project:field:special-terms", "特殊条款", 19),
            ("project:field:remark", "备注", 20),
        };
        if (basicFieldGroup != null)
        {
            foreach (var (code, name, sort) in fieldPermDefs)
            {
                if (!await db.Permissions.AnyAsync(p => p.Code == code))
                {
                    db.Permissions.Add(new Permission
                    {
                        Code = code,
                        Name = name,
                        ParentId = basicFieldGroup.Id,
                        Type = 2,
                        SortOrder = sort
                    });
                }
            }
            await db.SaveChangesAsync();
        }

        // 重复权限 project:field:basic-info 如存在不做删除，避免误操作

        // 确保 admin 用户拥有 admin 角色
        var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == "admin");
        if (adminUser != null && adminRole != null && !await db.UserRoles.AnyAsync(ur => ur.UserId == adminUser.Id && ur.RoleId == adminRole.Id))
        {
            db.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });
            await db.SaveChangesAsync();
        }

        // ── 项目文件管理权限（兼容已有数据库升级）──
        var fileViewParent = await db.Permissions.FirstOrDefaultAsync(p => p.Code == "project:file:view");
        if (fileViewParent == null)
        {
            var listMenu = await db.Permissions.FirstOrDefaultAsync(p => p.Code == "project:list");
            var parentId = listMenu?.Id;
            fileViewParent = new Permission { Code = "project:file:view", Name = "查看项目文件", ParentId = parentId, Type = 2, SortOrder = 7 };
            db.Permissions.Add(fileViewParent);
            await db.SaveChangesAsync();
        }
        // 迁移旧 view:all → view:nonpublic，并更新名称
        var oldAllPerm = await db.Permissions.FirstOrDefaultAsync(p => p.Code == "project:file:view:all");
        if (oldAllPerm != null)
        {
            oldAllPerm.Code = "project:file:view:nonpublic";
            oldAllPerm.Name = "非公开";
            await db.SaveChangesAsync();
        }
        var publicPerm = await db.Permissions.FirstOrDefaultAsync(p => p.Code == "project:file:view:public");
        if (publicPerm != null && publicPerm.Name == "仅公开")
        {
            publicPerm.Name = "公开";
            await db.SaveChangesAsync();
        }
        foreach (var (code, name, sort) in new[] {
            ("project:file:view:public", "公开", 1),
            ("project:file:view:nonpublic", "非公开", 2),
        })
        {
            if (!await db.Permissions.AnyAsync(p => p.Code == code))
            {
                db.Permissions.Add(new Permission { Code = code, Name = name, ParentId = fileViewParent.Id, Type = 2, SortOrder = sort });
            }
        }
        await db.SaveChangesAsync();

        // 确保「文件管理」菜单权限存在（兼容已有库升级）
        if (!await db.Permissions.AnyAsync(p => p.Code == "project:files"))
        {
            var projectMenu = await db.Permissions.FirstOrDefaultAsync(p => p.Code == "project");
            if (projectMenu != null)
            {
                db.Permissions.Add(new Permission
                {
                    Code = "project:files",
                    Name = "文件管理",
                    ParentId = projectMenu.Id,
                    Type = 1,
                    SortOrder = 3,
                    Icon = "FolderOpened",
                    Path = "/project/files"
                });
                await db.SaveChangesAsync();
            }
        }

        // 废弃权限 project:file:manage / project:file:delete 已不再创建（如存在不做删除，避免误操作）

        // 文件查看/系统设置权限由权限管理页面手动分配，运行时不再自动补全

        // ── 问题管理权限码（兼容已有库升级）──
        if (!await db.Permissions.AnyAsync(p => p.Code == "issue"))
        {
            var projectPerm = await db.Permissions.FirstOrDefaultAsync(p => p.Code == "project");
            if (projectPerm != null)
            {
                var issueMenu = new Permission
                {
                    Code = "issue",
                    Name = "问题管理",
                    ParentId = projectPerm.Id,
                    Type = 1,
                    SortOrder = 250,
                    Icon = "WarningFilled",
                    Path = "/project/issues"
                };
                db.Permissions.Add(issueMenu);
                await db.SaveChangesAsync();

                var children = new (string Code, string Name)[]
                {
                    ("issue:list",   "查看问题列表"),
                    ("issue:create", "创建问题"),
                    ("issue:edit",   "编辑问题"),
                    ("issue:delete", "删除问题"),
                    ("issue:assign", "指派责任人"),
                    ("issue:verify", "验证问题"),
                };
                int sort = 100;
                foreach (var (code, name) in children)
                {
                    if (!await db.Permissions.AnyAsync(p => p.Code == code))
                    {
                        db.Permissions.Add(new Permission
                        {
                            Code = code,
                            Name = name,
                            ParentId = issueMenu.Id,
                            Type = 2,
                            SortOrder = sort
                        });
                    }
                    sort += 100;
                }
                await db.SaveChangesAsync();
            }
        }

        // ── 问题管理权限自动分配给角色 ──
        var issuePermCodes = new[] { "issue:list", "issue:create", "issue:edit", "issue:delete", "issue:assign", "issue:verify" };
        foreach (var roleCode in new[] { "admin", "project_admin" })
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == roleCode);
            if (role != null)
            {
                foreach (var code in issuePermCodes)
                {
                    var perm = await db.Permissions.FirstOrDefaultAsync(p => p.Code == code);
                    if (perm != null && !await db.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == perm.Id))
                        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = perm.Id });
                }
            }
        }
        // PM: list, create, edit, assign, verify (无 delete)
        var pmRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == "project_manager");
        if (pmRole != null)
        {
            foreach (var code in new[] { "issue:list", "issue:create", "issue:edit", "issue:assign", "issue:verify" })
            {
                var perm = await db.Permissions.FirstOrDefaultAsync(p => p.Code == code);
                if (perm != null && !await db.RolePermissions.AnyAsync(rp => rp.RoleId == pmRole.Id && rp.PermissionId == perm.Id))
                    db.RolePermissions.Add(new RolePermission { RoleId = pmRole.Id, PermissionId = perm.Id });
            }
        }
        // task_manager: list, create
        var tmRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == "task_manager");
        if (tmRole != null)
        {
            foreach (var code in new[] { "issue:list", "issue:create" })
            {
                var perm = await db.Permissions.FirstOrDefaultAsync(p => p.Code == code);
                if (perm != null && !await db.RolePermissions.AnyAsync(rp => rp.RoleId == tmRole.Id && rp.PermissionId == perm.Id))
                    db.RolePermissions.Add(new RolePermission { RoleId = tmRole.Id, PermissionId = perm.Id });
            }
        }
        // user: issue:list only
        var userRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == "user");
        if (userRole != null)
        {
            var permList = await db.Permissions.FirstOrDefaultAsync(p => p.Code == "issue:list");
            if (permList != null && !await db.RolePermissions.AnyAsync(rp => rp.RoleId == userRole.Id && rp.PermissionId == permList.Id))
                db.RolePermissions.Add(new RolePermission { RoleId = userRole.Id, PermissionId = permList.Id });
        }
        await db.SaveChangesAsync();

        // ── 问题管理字典（兼容已有库升级）──
        var issueDicts = new Dictionary<string, (string Name, string[] Items)>
        {
            ["issue_source"] = ("问题来源", new[] { "内部审查", "客户反馈", "测试发现", "日常巡检", "其他" }),
            ["issue_type"]  = ("问题类型", new[] { "技术缺陷", "需求变更", "进度风险", "资源问题", "质量问题", "其他" }),
            ["measure_type"] = ("措施类型", new[] { "纠正措施", "预防措施", "临时措施", "其他" }),
        };
        foreach (var kv in issueDicts)
        {
            if (!await db.DictTypes.AnyAsync(d => d.DictTypeCode == kv.Key))
            {
                db.DictTypes.Add(new DictType { DictTypeCode = kv.Key, DictTypeName = kv.Value.Name });
                await db.SaveChangesAsync();
            }
            for (int i = 0; i < kv.Value.Items.Length; i++)
            {
                if (!await db.DictItems.AnyAsync(di => di.DictType == kv.Key && di.DictCode == kv.Value.Items[i]))
                    db.DictItems.Add(new DictItem { DictType = kv.Key, DictCode = kv.Value.Items[i], DictLabel = kv.Value.Items[i], SortOrder = (i + 1) * 10, Status = 1 });
            }
        }
        await db.SaveChangesAsync();

        // ── 问题编号前缀系统参数 ──
        if (!db.SysParams.Any(p => p.ParamKey == "issue_code_prefix"))
        {
            db.SysParams.Add(new SysParam { ParamKey = "issue_code_prefix", ParamValue = "ISS", Description = "问题编号前缀" });
            await db.SaveChangesAsync();
        }

        // 修正已有数据库中权限图标（兼容旧数据）
        var issuePerm = await db.Permissions.FirstOrDefaultAsync(p => p.Code == "issue" && p.Icon == "Warning");
        if (issuePerm != null)
        {
            issuePerm.Icon = "WarningFilled";
            await db.SaveChangesAsync();
        }

        // 确保所有用户至少有一个 RBAC 角色（默认 user）
        var userRbacRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == "user");
        if (userRbacRole != null)
        {
            var usersWithoutRole = await db.Users
                .Where(u => !db.UserRoles.Any(ur => ur.UserId == u.Id))
                .ToListAsync();
            foreach (var u in usersWithoutRole)
            {
                db.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = userRbacRole.Id });
            }
            if (usersWithoutRole.Count > 0)
                await db.SaveChangesAsync();
        }

        // 「系统设置」菜单权限：如不存在则创建，但不再自动分配给所有角色

        await EnsureDataScopeRolesAsync(db);
    }

    private static async Task EnsureDataScopeRolesAsync(AppDbContext db)
    {
        var scopeByCode = new Dictionary<string, int>
        {
            ["admin"] = 1,
            ["project_admin"] = 1,
            ["template_admin"] = 4,
            ["project_manager"] = 4,
            ["task_manager"] = 4,
            ["user"] = 5
        };
        foreach (var (code, scope) in scopeByCode)
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == code);
            if (role != null && role.DataScope != scope)
            {
                role.DataScope = scope;
            }
        }
        await db.SaveChangesAsync();
    }
}
