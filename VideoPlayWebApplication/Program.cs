using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting.WindowsServices;
using VideoPlayWebApplication.Data;
using VideoPlayWebApplication.Services;

var options = new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService()
        ? AppContext.BaseDirectory
        : default
};

var builder = WebApplication.CreateBuilder(options);



builder.Host.UseWindowsService();  // 启用 Windows 服务支持

// 添加 EventLog 提供程序
builder.Logging.AddEventLog(eventLogSettings =>
{
    eventLogSettings.SourceName = "VideoPlayWebApplication"; // 自定义事件源名称（默认值为应用名称）
    eventLogSettings.LogName = "Application"; // 默认写入 Application 日志，可自定义其他日志（如自定义日志名）
});

// 注册后台服务
builder.Services.AddHostedService<FolderWatcherService>();
//builder.Services.AddScoped<FileParser>();
builder.Services.AddHostedService<TimedSyncService>();


// Add services to the container.
builder.Services.AddControllersWithViews();

// 获取基础连接字符串
var baseConnection = builder.Configuration.GetConnectionString("DefaultConnection");

// 动态计算数据库绝对路径
var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database.db");
var finalConnection = new SqliteConnectionStringBuilder(baseConnection)
{
    DataSource = dbPath
}.ToString();

// 配置 DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(finalConnection)
);
var app = builder.Build();
// 初始化数据库
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}
app.UseStaticFiles();

// 自定义静态文件路径（可选）
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(builder.Configuration["WatchedFolder"] ?? "D:\\data\\试产验货验厂视频记录"),
    RequestPath = "/videos"
});

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Videos}/{action=Index}/{id?}");

app.Run();
