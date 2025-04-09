using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoPlayWebApplication.Data;
using VideoPlayWebApplication.Models;

namespace VideoPlayWebApplication.Services
{
    public class FolderWatcherService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FolderWatcherService> _logger;
        private FileSystemWatcher _watcher;

        public FolderWatcherService(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<FolderWatcherService> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var folderPath = _configuration["WatchedFolder"];
            if (string.IsNullOrEmpty(folderPath))
            {
                throw new InvalidOperationException("未配置监视文件夹路径");
            }

            _watcher = new FileSystemWatcher
            {
                Path = folderPath,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = "*.*",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            // 绑定事件处理器
            _watcher.Created += OnFileCreated;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Error += OnWatcherError;

            _logger.LogInformation("开始监控文件夹: {FolderPath}", folderPath);
            return Task.CompletedTask;
        }

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (IsDirectory(e.FullPath)) return;

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // 防止重复添加
                if (await dbContext.Videos.AnyAsync(v => v.FilePath == e.FullPath))
                {
                    _logger.LogWarning("文件已存在数据库: {FilePath}", e.FullPath);
                    return;
                }

                var video = FileParser.CreateVideoFromPath(e.FullPath);
                if (video != null)
                {
                    await dbContext.Videos.AddAsync(video);
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("新增文件记录: {FileName}", e.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "文件创建处理失败: {FilePath}", e.FullPath);
            }
        }

        private async void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (IsDirectory(e.FullPath)) return;

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // 查找并删除对应记录
                var record = await dbContext.Videos
                    .FirstOrDefaultAsync(v => v.FilePath == e.FullPath);

                if (record != null)
                {
                    dbContext.Videos.Remove(record);
                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("删除文件记录: {FileName}", e.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "文件删除处理失败: {FilePath}", e.FullPath);
            }
        }

        private async void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                if (IsDirectory(e.FullPath)) return;

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // 处理旧记录
                var oldRecord = await dbContext.Videos
                    .FirstOrDefaultAsync(v => v.FilePath == e.OldFullPath);

                if (oldRecord != null)
                {
                    // 删除旧记录
                    dbContext.Videos.Remove(oldRecord);

                    // 添加新记录
                    var newVideo = FileParser.CreateVideoFromPath(e.FullPath);
                    if (newVideo != null)
                    {
                        await dbContext.Videos.AddAsync(newVideo);
                    }

                    await dbContext.SaveChangesAsync();
                    _logger.LogInformation("更新重命名文件: {OldName} -> {NewName}",
                        e.OldName, e.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "文件重命名处理失败: {OldPath} -> {NewPath}",
                    e.OldFullPath, e.FullPath);
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            var ex = e.GetException();
            _logger.LogCritical(ex, "文件监控系统发生错误");
        }


        private static bool IsDirectory(string path)
        {
            try
            {
                return File.GetAttributes(path).HasFlag(FileAttributes.Directory);
            }
            catch
            {
                return false;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _watcher?.Dispose();
            _logger.LogInformation("停止文件夹监控服务");
            return Task.CompletedTask;
        }
    }
}
