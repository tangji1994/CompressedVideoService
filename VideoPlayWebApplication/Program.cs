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



builder.Host.UseWindowsService();  // ���� Windows ����֧��

// ��� EventLog �ṩ����
builder.Logging.AddEventLog(eventLogSettings =>
{
    eventLogSettings.SourceName = "VideoPlayWebApplication"; // �Զ����¼�Դ���ƣ�Ĭ��ֵΪӦ�����ƣ�
    eventLogSettings.LogName = "Application"; // Ĭ��д�� Application ��־�����Զ���������־�����Զ�����־����
});

// ע���̨����
builder.Services.AddHostedService<FolderWatcherService>();
//builder.Services.AddScoped<FileParser>();
builder.Services.AddHostedService<TimedSyncService>();


// Add services to the container.
builder.Services.AddControllersWithViews();

// ��ȡ���������ַ���
var baseConnection = builder.Configuration.GetConnectionString("DefaultConnection");

// ��̬�������ݿ����·��
var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "database.db");
var finalConnection = new SqliteConnectionStringBuilder(baseConnection)
{
    DataSource = dbPath
}.ToString();

// ���� DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(finalConnection)
);
var app = builder.Build();
// ��ʼ�����ݿ�
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

// �Զ��徲̬�ļ�·������ѡ��
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(builder.Configuration["WatchedFolder"] ?? "D:\\data\\�Բ�����鳧��Ƶ��¼"),
    RequestPath = "/videos"
});

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Videos}/{action=Index}/{id?}");

app.Run();
