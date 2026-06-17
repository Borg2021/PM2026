using Microsoft.EntityFrameworkCore;
using ProjectManagement.Domain.Entities;

namespace ProjectManagement.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<RoleDict> RoleDicts => Set<RoleDict>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<PlanNode> PlanNodes => Set<PlanNode>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<TemplateMember> TemplateMembers => Set<TemplateMember>();
    public DbSet<PlanNodeDependency> PlanNodeDependencies => Set<PlanNodeDependency>();
    public DbSet<FileTemplateItem> FileTemplateItems => Set<FileTemplateItem>();
    public DbSet<PlanBundle> PlanBundles => Set<PlanBundle>();
    public DbSet<PlanBundleItem> PlanBundleItems => Set<PlanBundleItem>();

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectProduct> ProjectProducts => Set<ProjectProduct>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectMilestone> ProjectMilestones => Set<ProjectMilestone>();
    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();
    public DbSet<ProjectChange> ProjectChanges => Set<ProjectChange>();
    public DbSet<ProjectFinance> ProjectFinances => Set<ProjectFinance>();
    public DbSet<ProjectPlanReceipt> ProjectPlanReceipts => Set<ProjectPlanReceipt>();
    public DbSet<ProjectReceipt> ProjectReceipts => Set<ProjectReceipt>();
    public DbSet<ProjectInvoice> ProjectInvoices => Set<ProjectInvoice>();
    public DbSet<DictItem> DictItems => Set<DictItem>();
    public DbSet<DictType> DictTypes => Set<DictType>();
    public DbSet<ProjectFile> ProjectFiles => Set<ProjectFile>();
    public DbSet<SysParam> SysParams => Set<SysParam>();
    public DbSet<OperationLog> OperationLogs => Set<OperationLog>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<ProjectManagement.Domain.Entities.Function> Functions => Set<ProjectManagement.Domain.Entities.Function>();
    public DbSet<ProjectFileItem> ProjectFileItems => Set<ProjectFileItem>();
    public DbSet<ProjectFileVersion> ProjectFileVersions => Set<ProjectFileVersion>();
    public DbSet<ProjectFileVersionFile> ProjectFileVersionFiles => Set<ProjectFileVersionFile>();
    public DbSet<UserFunction> UserFunctions => Set<UserFunction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 种子数据
        DbInitializer.Seed(modelBuilder);

        modelBuilder.Entity<Template>(e =>
        {
            e.HasQueryFilter(t => t.Status != 0);
            e.HasIndex(t => t.TemplateCode).IsUnique();
            e.HasIndex(t => t.TemplateType);
            e.HasIndex(t => t.CreatedAt).IsDescending();
            e.HasIndex(t => t.CreatedByName);
        });

        modelBuilder.Entity<PlanNode>(e =>
        {
            e.HasIndex(p => p.TemplateId);
            e.HasIndex(p => p.ParentId);
        });

        modelBuilder.Entity<PlanNodeDependency>(e =>
        {
            e.HasIndex(d => d.PlanNodeId);
            e.HasIndex(d => d.PredecessorId);
        });

        modelBuilder.Entity<Milestone>(e =>
        {
            e.HasIndex(m => m.TemplateId);
        });

        modelBuilder.Entity<TemplateMember>(e =>
        {
            e.HasIndex(m => m.TemplateId);
        });

        modelBuilder.Entity<FileTemplateItem>(e =>
        {
            e.ToTable("FileTemplateItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).IsRequired().HasMaxLength(200);
            e.HasIndex(x => x.TemplateId);
            e.HasOne(x => x.Template).WithMany(t => t.FileItems).HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Username).IsUnique();
            e.HasOne(u => u.Department)
                .WithMany(d => d.Users)
                .HasForeignKey(u => u.DepartmentId)
                .IsRequired(false);
            e.HasIndex(u => u.DepartmentId);
            e.HasMany(u => u.UserFunctions)
                .WithOne(uf => uf.User)
                .HasForeignKey(uf => uf.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlanBundle>(e =>
        {
            e.HasMany(b => b.Items)
                .WithOne(i => i.Bundle)
                .HasForeignKey(i => i.BundleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlanBundleItem>(e =>
        {
            e.HasIndex(i => i.BundleId);
            e.HasIndex(i => i.TemplateId);
            e.HasOne(i => i.Template)
                .WithMany()
                .HasForeignKey(i => i.TemplateId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.HasIndex(p => p.ProjectCode).IsUnique();
            e.HasIndex(p => p.Status);
            e.HasMany(p => p.Products).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Members).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.Milestones).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(p => p.FileItems).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectProduct>(e => e.HasIndex(x => x.ProjectId));
        modelBuilder.Entity<ProjectMember>(e => e.HasIndex(x => x.ProjectId));
        modelBuilder.Entity<ProjectMilestone>(e => e.HasIndex(x => x.ProjectId));

        modelBuilder.Entity<ProjectTask>(e =>
        {
            e.HasIndex(x => x.ProjectId);
            e.HasIndex(x => x.ParentId);
            e.Property(x => x.ProgressPct).HasColumnType("decimal(5,2)");
        });

        modelBuilder.Entity<ProjectChange>(e => e.HasIndex(x => x.ProjectId));

        modelBuilder.Entity<OperationLog>(e =>
        {
            e.HasIndex(x => x.ProjectId);
            e.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<ProjectFinance>(e =>
        {
            e.HasIndex(x => x.ProjectId).IsUnique();
            e.Property(x => x.ContributionRate).HasColumnType("decimal(18,4)");
            e.Property(x => x.CurrencyAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.InvoiceRate).HasColumnType("decimal(18,4)");
            e.Property(x => x.TaxContractAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.TaxRate).HasColumnType("decimal(18,4)");
            e.HasMany(f => f.PlanReceipts).WithOne(r => r.Finance).HasForeignKey(r => r.ProjectFinanceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(f => f.Receipts).WithOne(r => r.Finance).HasForeignKey(r => r.ProjectFinanceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(f => f.Invoices).WithOne(i => i.Finance).HasForeignKey(i => i.ProjectFinanceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectPlanReceipt>(e =>
        {
            e.Property(x => x.PlanAmount).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<ProjectReceipt>(e =>
        {
            e.Property(x => x.ActualAmount).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<ProjectInvoice>(e =>
        {
            e.Property(x => x.InvoiceAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.InvoiceRate).HasColumnType("decimal(18,4)");
        });

        modelBuilder.Entity<Permission>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
            e.HasOne(p => p.Parent)
                .WithMany(p => p.Children)
                .HasForeignKey(p => p.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>(e =>
        {
            e.HasIndex(r => r.Name).IsUnique();
            e.HasIndex(r => r.Code).IsUnique();
        });

        modelBuilder.Entity<RolePermission>(e =>
        {
            e.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
            e.HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(rp => rp.Permission)
                .WithMany()
                .HasForeignKey(rp => rp.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserRole>(e =>
        {
            e.HasIndex(ur => new { ur.UserId, ur.RoleId }).IsUnique();
            e.HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ur => ur.Role)
                .WithMany()
                .HasForeignKey(ur => ur.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectManagement.Domain.Entities.Function>(e =>
        {
            e.HasIndex(f => f.Code).IsUnique();
            e.HasMany(f => f.UserFunctions)
                .WithOne(uf => uf.Function)
                .HasForeignKey(uf => uf.FunctionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserFunction>(e =>
        {
            e.HasIndex(uf => new { uf.UserId, uf.FunctionId }).IsUnique();
        });

        modelBuilder.Entity<ProjectFileItem>(e =>
        {
            e.ToTable("ProjectFileItems");
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).IsRequired().HasMaxLength(200);
            e.HasIndex(x => x.ProjectId);
            e.HasMany(x => x.Versions)
                .WithOne(v => v.ProjectFileItem)
                .HasForeignKey(v => v.ProjectFileItemId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.LatestVersion)
                .WithMany()
                .HasForeignKey(x => x.LatestVersionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ProjectFileVersion>(e =>
        {
            e.ToTable("ProjectFileVersions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ProjectFileItemId);
            e.HasIndex(x => new { x.ProjectFileItemId, x.VersionNumber }).IsUnique();
        });

        modelBuilder.Entity<ProjectFileVersionFile>(e =>
        {
            e.ToTable("ProjectFileVersionFiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.FilePath).IsRequired().HasMaxLength(500);
            e.HasOne(x => x.Version)
                .WithMany(v => v.Files)
                .HasForeignKey(x => x.ProjectFileVersionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ProjectFileVersionId);
        });
    }
}
