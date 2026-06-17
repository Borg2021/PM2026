using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ProjectManagement.Application.Templates.Commands;
using ProjectManagement.Domain.Interfaces;
using ProjectManagement.Infrastructure.Data;
using ProjectManagement.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 配置 Kestrel：HTTP(3001) + HTTPS(3002)，启动时自动生成自签名证书
var certPassword = "ProjectMgmt2026!";
var certPath = Path.Combine(AppContext.BaseDirectory, "cert.pfx");
if (!File.Exists(certPath))
{
    using var rsa = RSA.Create(2048);
    var req = new CertificateRequest("cn=ProjectManagement", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var cert = req.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(10));
    File.WriteAllBytes(certPath, cert.Export(X509ContentType.Pfx, certPassword));
}

// 上传文件大小限制由中间件动态读取系统参数 upload_filesize（可热更新，无需重启）
// Kestrel/FormOptions 设安全高值仅做兜底
const long MAX_SAFETY_LIMIT = 500L * 1024 * 1024;

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MAX_SAFETY_LIMIT;
    options.Listen(IPAddress.Any, 9090); // HTTP
    options.Listen(IPAddress.Any, 9091, listenOptions =>
    {
        listenOptions.UseHttps(certPath, certPassword);
    });
});

// 同步配置表单上传兜底限制
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = (int)Math.Min(MAX_SAFETY_LIMIT, int.MaxValue);
    options.ValueLengthLimit = (int)Math.Min(MAX_SAFETY_LIMIT, int.MaxValue);
});

// EF Core + SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Default"),
           sqlOptions => sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
           .ConfigureWarnings(w => w.Ignore(new EventId(10622))));

// Repository
builder.Services.AddScoped<ITemplateRepository, TemplateRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPlanBundleRepository, PlanBundleRepository>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();

// MediatR — 注册所有 Handler
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(CreateTemplateCommand).Assembly));

// JWT 认证
var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtExpireHours = double.Parse(builder.Configuration["Jwt:ExpireHours"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        // 允许从 query string ?token=xxx 提取 JWT（用于 window.open 文件下载）
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["token"].FirstOrDefault();
                if (!string.IsNullOrEmpty(token))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, ProjectManagement.API.Auth.PermissionAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, ProjectManagement.API.Auth.PermissionAuthorizationHandler>();

// CORS — 允许前端开发服务器
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
// 自定义模型验证错误响应，返回业务可读的错误信息
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .SelectMany(e => e.Value!.Errors.Select(x => x.ErrorMessage))
            .ToList();
        var message = errors.Count > 0 ? errors[0] : "请求参数验证失败";

        // 翻译文件超限等通用英语错误
        if (message.Contains("Request body too large") || message.Contains("Failed to read the request form"))
        {
            var mb = UploadConfig.CurrentLimit / 1024 / 1024;
            message = $"上传文件大小超过系统限制（最大 {mb} MB），请压缩后重试";
        }

        return new BadRequestObjectResult(new { code = 400, message });
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 9091;
});

var app = builder.Build();

// 初始化数据库
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // 从 SQLite 迁移数据到 SQL Server（库文件存在时才执行）
    var sqlitePath = builder.Configuration["Migration:SqlitePath"];
    var migrated = false;
    if (!string.IsNullOrEmpty(sqlitePath))
    {
        var migrator = new SqlServerDataMigrator(db, sqlitePath);
        migrated = await migrator.MigrateIfNeededAsync();
        if (migrated)
            Console.WriteLine("[Migration] Successfully migrated data from SQLite to SQL Server.");
    }

    // 以下 T-SQL 安全网仅对非迁移的新库执行（迁移库已含完整结构）
    if (!migrated)
    {
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[OperationLogs]') AND type = 'U')
CREATE TABLE [OperationLogs] ([Id] BIGINT IDENTITY(1,1) NOT NULL, [ProjectId] BIGINT NOT NULL, [UserId] BIGINT NOT NULL, [UserName] NVARCHAR(MAX) NOT NULL DEFAULT '', [Operation] NVARCHAR(MAX) NOT NULL DEFAULT '', [Content] NVARCHAR(MAX) NOT NULL DEFAULT '', [CreatedAt] DATETIME2 NOT NULL DEFAULT '0001-01-01 00:00:00', CONSTRAINT [PK_OperationLogs] PRIMARY KEY CLUSTERED ([Id]))");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OperationLogs_ProjectId' AND object_id = OBJECT_ID(N'[dbo].[OperationLogs]'))
CREATE INDEX [IX_OperationLogs_ProjectId] ON [OperationLogs] ([ProjectId])");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OperationLogs_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[OperationLogs]'))
CREATE INDEX [IX_OperationLogs_CreatedAt] ON [OperationLogs] ([CreatedAt])");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Permissions]') AND type = 'U')
CREATE TABLE [Permissions] ([Id] BIGINT IDENTITY(1,1) NOT NULL, [Code] NVARCHAR(MAX) NOT NULL DEFAULT '', [Name] NVARCHAR(MAX) NOT NULL DEFAULT '', [ParentId] BIGINT NULL, [Type] INT NOT NULL DEFAULT 1, [SortOrder] INT NOT NULL DEFAULT 0, [Icon] NVARCHAR(MAX) NULL, [Path] NVARCHAR(MAX) NULL, [CreatedAt] DATETIME2 NOT NULL DEFAULT '0001-01-01 00:00:00', CONSTRAINT [PK_Permissions] PRIMARY KEY CLUSTERED ([Id]))");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Permissions_Code' AND object_id = OBJECT_ID(N'[dbo].[Permissions]'))
CREATE UNIQUE INDEX [IX_Permissions_Code] ON [Permissions] ([Code])");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Roles]') AND type = 'U')
CREATE TABLE [Roles] ([Id] BIGINT IDENTITY(1,1) NOT NULL, [Name] NVARCHAR(MAX) NOT NULL DEFAULT '', [Code] NVARCHAR(MAX) NOT NULL DEFAULT '', [Description] NVARCHAR(MAX) NULL, [Status] INT NOT NULL DEFAULT 1, [DataScope] INT NOT NULL DEFAULT 5, [CreatedAt] DATETIME2 NOT NULL DEFAULT '0001-01-01 00:00:00', CONSTRAINT [PK_Roles] PRIMARY KEY CLUSTERED ([Id]))");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Roles_Name' AND object_id = OBJECT_ID(N'[dbo].[Roles]'))
CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name])");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Roles_Code' AND object_id = OBJECT_ID(N'[dbo].[Roles]'))
CREATE UNIQUE INDEX [IX_Roles_Code] ON [Roles] ([Code])");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[RolePermissions]') AND type = 'U')
CREATE TABLE [RolePermissions] ([Id] BIGINT IDENTITY(1,1) NOT NULL, [RoleId] BIGINT NOT NULL, [PermissionId] BIGINT NOT NULL, CONSTRAINT [PK_RolePermissions] PRIMARY KEY CLUSTERED ([Id]), FOREIGN KEY ([RoleId]) REFERENCES [Roles]([Id]) ON DELETE CASCADE, FOREIGN KEY ([PermissionId]) REFERENCES [Permissions]([Id]) ON DELETE CASCADE)");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RolePermissions_RoleId_PermissionId' AND object_id = OBJECT_ID(N'[dbo].[RolePermissions]'))
CREATE UNIQUE INDEX [IX_RolePermissions_RoleId_PermissionId] ON [RolePermissions] ([RoleId], [PermissionId])");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserRoles]') AND type = 'U')
CREATE TABLE [UserRoles] ([Id] BIGINT IDENTITY(1,1) NOT NULL, [UserId] BIGINT NOT NULL, [RoleId] BIGINT NOT NULL, CONSTRAINT [PK_UserRoles] PRIMARY KEY CLUSTERED ([Id]), FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE, FOREIGN KEY ([RoleId]) REFERENCES [Roles]([Id]) ON DELETE CASCADE)");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserRoles_UserId_RoleId' AND object_id = OBJECT_ID(N'[dbo].[UserRoles]'))
CREATE UNIQUE INDEX [IX_UserRoles_UserId_RoleId] ON [UserRoles] ([UserId], [RoleId])");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Functions]') AND type = 'U')
CREATE TABLE [Functions] ([Id] BIGINT IDENTITY(1,1) NOT NULL, [Code] NVARCHAR(MAX) NOT NULL DEFAULT '', [Name] NVARCHAR(MAX) NOT NULL DEFAULT '', [Description] NVARCHAR(MAX) NULL, [SortOrder] INT NOT NULL DEFAULT 0, CONSTRAINT [PK_Functions] PRIMARY KEY CLUSTERED ([Id]))");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Functions_Code' AND object_id = OBJECT_ID(N'[dbo].[Functions]'))
CREATE UNIQUE INDEX [IX_Functions_Code] ON [Functions] ([Code])");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserFunctions]') AND type = 'U')
CREATE TABLE [UserFunctions] ([Id] BIGINT IDENTITY(1,1) NOT NULL, [UserId] BIGINT NOT NULL, [FunctionId] BIGINT NOT NULL, CONSTRAINT [PK_UserFunctions] PRIMARY KEY CLUSTERED ([Id]), FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE, FOREIGN KEY ([FunctionId]) REFERENCES [Functions]([Id]) ON DELETE CASCADE)");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserFunctions_UserId_FunctionId' AND object_id = OBJECT_ID(N'[dbo].[UserFunctions]'))
CREATE UNIQUE INDEX [IX_UserFunctions_UserId_FunctionId] ON [UserFunctions] ([UserId], [FunctionId])");
        // 迁移旧数据：将 Users.FunctionId 迁移到 UserFunctions
        var conn = db.Database.GetDbConnection();
        var wasOpen = conn.State == System.Data.ConnectionState.Open;
        if (!wasOpen) conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Users' AND COLUMN_NAME = 'FunctionId'";
        var oldFunctionIdExists = (int)cmd.ExecuteScalar()! > 0;
        if (!wasOpen) conn.Close();
        if (oldFunctionIdExists)
        {
            db.Database.ExecuteSqlRaw(@"INSERT INTO [UserFunctions] ([UserId], [FunctionId]) SELECT [Id], [FunctionId] FROM [Users] WHERE [FunctionId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [UserFunctions] WHERE [UserFunctions].[UserId] = [Users].[Id] AND [UserFunctions].[FunctionId] = [Users].[FunctionId])");
        }
        AddColumnIfNotExists(db, "ProjectMembers", "FunctionId", "BIGINT NULL");
        AddColumnIfNotExists(db, "ProjectMembers", "FunctionName", "NVARCHAR(MAX) NULL");
        AddColumnIfNotExists(db, "Roles", "DataScope", "INT NOT NULL DEFAULT 5");
        AddColumnIfNotExists(db, "Users", "DataScope", "BIGINT NULL");
        AddColumnIfNotExists(db, "PlanNodes", "TaskCategory", "NVARCHAR(MAX) NULL");
        AddColumnIfNotExists(db, "Projects", "SpecialTerms", "NVARCHAR(MAX) NULL");
        AddColumnIfNotExists(db, "Projects", "CustomerContactPhone", "NVARCHAR(MAX) NULL");
        AddColumnIfNotExists(db, "Projects", "CustomerContactEmail", "NVARCHAR(MAX) NULL");
        AddColumnIfNotExists(db, "Projects", "OwnerContactPhone", "NVARCHAR(MAX) NULL");
        AddColumnIfNotExists(db, "Projects", "BusinessContactEmail", "NVARCHAR(MAX) NULL");
        // 创建 DepartmentLeaders 表（多负责人）
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[DepartmentLeaders]') AND type = 'U')
CREATE TABLE [DepartmentLeaders] ([Id] BIGINT IDENTITY(1,1) NOT NULL, [DepartmentId] BIGINT NOT NULL, [UserId] BIGINT NOT NULL, CONSTRAINT [PK_DepartmentLeaders] PRIMARY KEY CLUSTERED ([Id]), FOREIGN KEY ([DepartmentId]) REFERENCES [Departments]([Id]) ON DELETE CASCADE, FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]))");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DepartmentLeaders_DepartmentId' AND object_id = OBJECT_ID(N'[dbo].[DepartmentLeaders]'))
CREATE INDEX [IX_DepartmentLeaders_DepartmentId] ON [DepartmentLeaders] ([DepartmentId])");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DepartmentLeaders_UserId' AND object_id = OBJECT_ID(N'[dbo].[DepartmentLeaders]'))
CREATE INDEX [IX_DepartmentLeaders_UserId] ON [DepartmentLeaders] ([UserId])");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DepartmentLeaders_DeptId_UserId' AND object_id = OBJECT_ID(N'[dbo].[DepartmentLeaders]'))
CREATE UNIQUE INDEX [IX_DepartmentLeaders_DeptId_UserId] ON [DepartmentLeaders] ([DepartmentId], [UserId])");

        // 迁移旧数据：将 Departments.LeaderId 迁移到 DepartmentLeaders
        {
            var conn2 = db.Database.GetDbConnection();
            var wasOpen2 = conn2.State == System.Data.ConnectionState.Open;
            if (!wasOpen2) conn2.Open();
            using var cmd2 = conn2.CreateCommand();
            cmd2.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Departments' AND COLUMN_NAME = 'LeaderId'";
            var oldLeaderIdExists = (int)cmd2.ExecuteScalar()! > 0;
            if (!wasOpen2) conn2.Close();
            if (oldLeaderIdExists)
            {
                db.Database.ExecuteSqlRaw(@"INSERT INTO [DepartmentLeaders] ([DepartmentId], [UserId])
SELECT [Id], [LeaderId] FROM [Departments] WHERE [LeaderId] IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM [DepartmentLeaders] WHERE [DepartmentLeaders].[DepartmentId] = [Departments].[Id] AND [DepartmentLeaders].[UserId] = [Departments].[LeaderId])");
            }
        }

        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[FileTemplateItems]') AND type = 'U')
CREATE TABLE [FileTemplateItems] ([Id] BIGINT IDENTITY(1,1) NOT NULL, [TemplateId] BIGINT NOT NULL, [SortOrder] INT NOT NULL DEFAULT 0, [FileName] NVARCHAR(200) NOT NULL DEFAULT '', [Required] INT NOT NULL DEFAULT 0, [DeptId] BIGINT NULL, [DeptName] NVARCHAR(MAX) NULL, [Remark] NVARCHAR(MAX) NULL, [IsPublic] INT NOT NULL DEFAULT 1, [ViewRoles] NVARCHAR(MAX) NULL, CONSTRAINT [PK_FileTemplateItems] PRIMARY KEY CLUSTERED ([Id]), FOREIGN KEY ([TemplateId]) REFERENCES [Templates]([Id]) ON DELETE CASCADE)");
        AddColumnIfNotExists(db, "FileTemplateItems", "Remark", "NVARCHAR(MAX) NULL");
        AddColumnIfNotExists(db, "FileTemplateItems", "IsPublic", "INT NOT NULL DEFAULT 1");
        AddColumnIfNotExists(db, "FileTemplateItems", "ViewRoles", "NVARCHAR(MAX) NULL");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FileTemplateItems_TemplateId' AND object_id = OBJECT_ID(N'[dbo].[FileTemplateItems]'))
CREATE INDEX [IX_FileTemplateItems_TemplateId] ON [FileTemplateItems] ([TemplateId])");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProjectFileItems]') AND type = 'U')
CREATE TABLE [ProjectFileItems] ([Id] BIGINT IDENTITY(1,1) NOT NULL, [ProjectId] BIGINT NOT NULL, [TemplateItemId] BIGINT NULL, [IsPublic] INT NOT NULL DEFAULT 1, [ViewRoles] NVARCHAR(MAX) NULL, [SortOrder] INT NOT NULL DEFAULT 0, [FileName] NVARCHAR(200) NOT NULL DEFAULT '', [Required] INT NOT NULL DEFAULT 0, [AssigneeId] BIGINT NULL, [AssigneeName] NVARCHAR(MAX) NULL, [DeptId] BIGINT NULL, [DeptName] NVARCHAR(MAX) NULL, [PlanFinishDate] NVARCHAR(MAX) NULL, [LatestVersionId] BIGINT NULL, [Remark] NVARCHAR(MAX) NULL, CONSTRAINT [PK_ProjectFileItems] PRIMARY KEY CLUSTERED ([Id]), FOREIGN KEY ([ProjectId]) REFERENCES [Projects]([Id]) ON DELETE CASCADE)");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProjectFileVersions]') AND type = 'U')
CREATE TABLE [ProjectFileVersions] ([Id] BIGINT IDENTITY(1,1) NOT NULL, [ProjectFileItemId] BIGINT NOT NULL, [VersionNumber] INT NOT NULL DEFAULT 1, [UploadedBy] BIGINT NOT NULL DEFAULT 0, [UploadedByName] NVARCHAR(MAX) NOT NULL DEFAULT '', [UploadedAt] DATETIME2 NOT NULL, [Remark] NVARCHAR(MAX) NULL, CONSTRAINT [PK_ProjectFileVersions] PRIMARY KEY CLUSTERED ([Id]), FOREIGN KEY ([ProjectFileItemId]) REFERENCES [ProjectFileItems]([Id]) ON DELETE CASCADE)");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProjectFileVersions_ItemId_Version' AND object_id = OBJECT_ID(N'[dbo].[ProjectFileVersions]'))
CREATE UNIQUE INDEX [IX_ProjectFileVersions_ItemId_Version] ON [ProjectFileVersions] ([ProjectFileItemId], [VersionNumber])");

        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProjectFileItems_ProjectId' AND object_id = OBJECT_ID(N'[dbo].[ProjectFileItems]'))
CREATE INDEX [IX_ProjectFileItems_ProjectId] ON [ProjectFileItems] ([ProjectId])");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProjectFileVersions_ProjectFileItemId' AND object_id = OBJECT_ID(N'[dbo].[ProjectFileVersions]'))
CREATE INDEX [IX_ProjectFileVersions_ProjectFileItemId] ON [ProjectFileVersions] ([ProjectFileItemId])");

        // 新增 ProjectFileVersionFiles 表（多文件支持）
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProjectFileVersionFiles]') AND type = 'U')
CREATE TABLE [ProjectFileVersionFiles] ([Id] BIGINT IDENTITY(1,1) NOT NULL, [ProjectFileVersionId] BIGINT NOT NULL, [FilePath] NVARCHAR(500) NOT NULL DEFAULT '', [OriginalFileName] NVARCHAR(MAX) NOT NULL DEFAULT '', [FileSize] BIGINT NOT NULL DEFAULT 0, [FileExt] NVARCHAR(MAX) NULL, CONSTRAINT [PK_ProjectFileVersionFiles] PRIMARY KEY CLUSTERED ([Id]), FOREIGN KEY ([ProjectFileVersionId]) REFERENCES [ProjectFileVersions]([Id]) ON DELETE CASCADE)");
        db.Database.ExecuteSqlRaw(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProjectFileVersionFiles_ProjectFileVersionId' AND object_id = OBJECT_ID(N'[dbo].[ProjectFileVersionFiles]'))
CREATE INDEX [IX_ProjectFileVersionFiles_ProjectFileVersionId] ON [ProjectFileVersionFiles] ([ProjectFileVersionId])");

        // 将 ProjectFileVersions 中的单文件数据迁移到 ProjectFileVersionFiles（仅对有 FilePath 列的旧表执行）
        var migrationConn = db.Database.GetDbConnection();
        var migrationWasOpen = migrationConn.State == System.Data.ConnectionState.Open;
        if (!migrationWasOpen) migrationConn.Open();
        using var migrationCmd = migrationConn.CreateCommand();
        migrationCmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProjectFileVersions' AND COLUMN_NAME = 'FilePath'";
        var oldFilePathExists = (int)migrationCmd.ExecuteScalar()! > 0;
        if (!migrationWasOpen) migrationConn.Close();
        if (oldFilePathExists)
        {
            db.Database.ExecuteSqlRaw(@"INSERT INTO [ProjectFileVersionFiles] ([ProjectFileVersionId], [FilePath], [OriginalFileName], [FileSize], [FileExt])
SELECT [Id], [FilePath], [OriginalFileName], [FileSize], [FileExt] FROM [ProjectFileVersions]
WHERE EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProjectFileVersions' AND COLUMN_NAME = 'FilePath')
AND NOT EXISTS (SELECT 1 FROM [ProjectFileVersionFiles] WHERE [ProjectFileVersionFiles].[ProjectFileVersionId] = [ProjectFileVersions].[Id])");

            // 数据迁移完成后删除旧列，否则 EF 插入新实体时报 NOT NULL 约束错误
            db.Database.ExecuteSqlRaw(@"IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProjectFileVersions' AND COLUMN_NAME = 'FilePath')
ALTER TABLE [ProjectFileVersions] DROP COLUMN [FilePath]");
            db.Database.ExecuteSqlRaw(@"IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProjectFileVersions' AND COLUMN_NAME = 'OriginalFileName')
ALTER TABLE [ProjectFileVersions] DROP COLUMN [OriginalFileName]");
            db.Database.ExecuteSqlRaw(@"IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProjectFileVersions' AND COLUMN_NAME = 'FileSize')
ALTER TABLE [ProjectFileVersions] DROP COLUMN [FileSize]");
            db.Database.ExecuteSqlRaw(@"IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ProjectFileVersions' AND COLUMN_NAME = 'FileExt')
ALTER TABLE [ProjectFileVersions] DROP COLUMN [FileExt]");
        }
    }

    await DbInitializer.EnsureSeedAsync(db);
    MigratePreTaskCodesToIds(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();

// 动态读取 upload_filesize / upload_filemaxtime 系统参数（内存缓存 30 秒）
long? _cachedLimit = null; int _cachedTimeout = 60; DateTime _lastFetch = DateTime.MinValue;
app.Use(async (context, next) =>
{
    if (_lastFetch.AddSeconds(30) < DateTime.Now)
    {
        try
        {
            var db = context.RequestServices.GetRequiredService<AppDbContext>();
            // 读取大小限制
            var sizeParam = await db.SysParams.FirstOrDefaultAsync(p => p.ParamKey == "upload_filesize");
            if (sizeParam != null)
            {
                var numeric = new string(sizeParam.ParamValue.Where(c => c >= '0' && c <= '9').ToArray());
                if (long.TryParse(numeric, out var mb))
                {
                    _cachedLimit = mb * 1024 * 1024;
                    UploadConfig.CurrentLimit = _cachedLimit.Value;
                }
            }
            // 读取超时限制（秒）
            var timeoutParam = await db.SysParams.FirstOrDefaultAsync(p => p.ParamKey == "upload_filemaxtime");
            if (timeoutParam != null && int.TryParse(timeoutParam.ParamValue.Trim(), out var sec) && sec > 0)
                _cachedTimeout = sec;
            else
                _cachedTimeout = 60; // 默认 60 秒
            UploadConfig.CurrentTimeoutSeconds = _cachedTimeout;
        }
        catch (Exception ex)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning(ex, "读取上传系统参数失败，延用缓存值");
        }
        _lastFetch = DateTime.Now;
    }

    // 请求体大小限制
    var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
    if (sizeFeature != null && !sizeFeature.IsReadOnly && _cachedLimit.HasValue)
        sizeFeature.MaxRequestBodySize = _cachedLimit.Value;

    // 上传类路径：设置请求超时
    CancellationTokenSource? linkedCts = null;
    var path = context.Request.Path.Value ?? "";
    if (path.Contains("/upload") || (path.Contains("/files") && context.Request.Method == "POST"))
    {
        var ct = new CancellationTokenSource(_cachedTimeout * 1000);
        linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, ct.Token);
        context.RequestAborted = linkedCts.Token;
    }

    await next();

    linkedCts?.Dispose();
});

app.UseMiddleware<ProjectManagement.API.Middleware.ExceptionMiddleware>();
app.UseCors();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapFallbackToFile("index.html");

app.Run();

static void AddColumnIfNotExists(AppDbContext db, string table, string column, string definition)
{
    var conn = db.Database.GetDbConnection();
    var wasOpen = conn.State == System.Data.ConnectionState.Open;
    if (!wasOpen) conn.Open();
    using var cmd = conn.CreateCommand();
    cmd.CommandText = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'";
    var exists = (int)cmd.ExecuteScalar()!;
    if (!wasOpen) conn.Close();
    if (exists == 0)
        db.Database.ExecuteSqlRaw($"ALTER TABLE [{table}] ADD [{column}] {definition}");
}

static void MigratePreTaskCodesToIds(AppDbContext db)
{
    var tasks = db.ProjectTasks.Where(t => t.PreTaskCodes != null && t.PreTaskCodes != "").ToList();
    if (tasks.Count == 0) return;

    var projectIds = tasks.Select(t => t.ProjectId).Distinct().ToList();
    foreach (var pid in projectIds)
    {
        var projectTasks = db.ProjectTasks.Where(t => t.ProjectId == pid).ToList();
        var taskNoToId = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var t in projectTasks)
        {
            if (!string.IsNullOrWhiteSpace(t.TaskNo) && !taskNoToId.ContainsKey(t.TaskNo))
                taskNoToId[t.TaskNo] = t.Id;
        }

        foreach (var t in tasks.Where(t => t.ProjectId == pid))
        {
            if (string.IsNullOrWhiteSpace(t.PreTaskCodes)) continue;
            var parts = t.PreTaskCodes!.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var migrated = false;
            for (var i = 0; i < parts.Length; i++)
            {
                var parenIdx = parts[i].IndexOf('(');
                var noPart = parenIdx > 0 ? parts[i].AsSpan(0, parenIdx).Trim().ToString() : parts[i].Trim();
                if (noPart.Length == 0) continue;
                if (noPart.Contains('.') && taskNoToId.TryGetValue(noPart, out var id))
                {
                    parts[i] = $"{id}{(parenIdx > 0 ? parts[i][parenIdx..] : "")}";
                    migrated = true;
                }
            }
            if (migrated)
            {
                t.PreTaskCodes = string.Join(",", parts);
                db.ProjectTasks.Update(t);
            }
        }
    }
    db.SaveChanges();
}

/// <summary>上传大小限制配置（由中间件动态刷新，供错误消息使用）</summary>
public static class UploadConfig
{
    public static long CurrentLimit { get; set; } = 100L * 1024 * 1024;
    public static int CurrentTimeoutSeconds { get; set; } = 60;
}
