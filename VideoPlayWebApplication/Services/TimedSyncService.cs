using Microsoft.EntityFrameworkCore;
using VideoPlayWebApplication.Data;
using VideoPlayWebApplication.Models;

namespace VideoPlayWebApplication.Services
{
    public class TimedSyncService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TimedSyncService> _logger;
        private int _syncIntervalHours = 1;
        private int _batchSize = 100; // 每批处理文件数

        public TimedSyncService(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            ILogger<TimedSyncService> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _syncIntervalHours = _configuration.GetValue("SyncIntervalInHours", 1);
            _batchSize = _configuration.GetValue("BatchSize", 100);
            using var timer = new PeriodicTimer(TimeSpan.FromHours(_syncIntervalHours));

            // 首次立即执行
            await ProcessFilesAsync();

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ProcessFilesAsync();
            }
        }

        private async Task ProcessFilesAsync()
        {
            var folderPath = _configuration["WatchedFolder"];
            if (!Directory.Exists(folderPath))
            {
                _logger.LogWarning("Directory {FolderPath} not exists", folderPath);
                return;
            }

            try
            {
                // 使用显式递归扫描获取当前所有文件
                var allFiles = new List<string>();
                ScanDirectoryRecursively(folderPath, allFiles);
                var currentFileSet = new HashSet<string>(allFiles, StringComparer.OrdinalIgnoreCase);

                // 第一阶段：清理已删除的文件记录
                using (var deleteScope = _serviceProvider.CreateScope())
                {
                    var dbContext = deleteScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // 分页查询避免内存溢出
                    //int pageSize = _batchSize;
                    int pageIndex = 0;
                    bool hasMoreRecords;

                    do
                    {
                        var dbFilesPage = await dbContext.Videos
                            .OrderBy(v => v.Id)
                            .Skip(pageIndex * _batchSize)
                            .Take(_batchSize)
                            .Select(v => new { v.Id, v.FilePath })
                            .ToListAsync();

                        hasMoreRecords = dbFilesPage.Any();

                        // 筛选需要删除的记录
                        var toDelete = dbFilesPage
                            .Where(x => !currentFileSet.Contains(x.FilePath))
                            .Select(x => x.Id)
                            .ToList();

                        if (toDelete.Any())
                        {
                            // 使用批量删除提高性能
                            await dbContext.Videos
                                .Where(v => toDelete.Contains(v.Id))
                                .ExecuteDeleteAsync();

                            _logger.LogInformation("Deleted {Count} stale records", toDelete.Count);
                        }

                        pageIndex++;
                    } while (hasMoreRecords);
                }

                // 第二阶段：分批次添加新文件
                foreach (var fileBatch in allFiles.Batch(_batchSize))
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // 获取当前批次的已有路径
                    var existingPaths = await dbContext.Videos
                        .AsNoTracking()
                        .Select(v => v.FilePath)
                        .ToListAsync();

                    var pathHash = new HashSet<string>(existingPaths, StringComparer.OrdinalIgnoreCase);

                    var newVideos = new List<Video>();
                    foreach (var file in fileBatch)
                    {
                        try
                        {
                            if (pathHash.Contains(file)) continue;

                            var video = FileParser.CreateVideoFromPath(file);
                            if (video != null && File.Exists(file)) // 二次验证文件存在
                            {
                                newVideos.Add(video);
                                pathHash.Add(file);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing {FilePath}", file);
                        }
                    }

                    if (newVideos.Any())
                    {
                        // 使用事务确保批量插入原子性
                        await using var transaction = await dbContext.Database.BeginTransactionAsync();
                        try
                        {
                            await dbContext.Videos.AddRangeAsync(newVideos);
                            await dbContext.SaveChangesAsync();
                            await transaction.CommitAsync();
                            _logger.LogInformation("Added {Count} new videos", newVideos.Count);
                        }
                        catch
                        {
                            await transaction.RollbackAsync();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File synchronization failed");
            }
        }


        // 递归扫描核心方法
        private void ScanDirectoryRecursively(string path, List<string> fileList)
        {
            try
            {
                // 先处理当前目录文件
                foreach (var file in Directory.EnumerateFiles(path))
                {
                    fileList.Add(file);
                    _logger.LogDebug("Found file: {FilePath}", file);
                }

                // 递归处理子目录
                foreach (var subDir in Directory.EnumerateDirectories(path))
                {
                    _logger.LogDebug("Entering directory: {SubDirectory}", subDir);
                    ScanDirectoryRecursively(subDir, fileList);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied to directory: {Path}", path);
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogWarning(ex, "Directory not found: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning directory: {Path}", path);
            }
        }


        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Sync service is stopping...");
            await base.StopAsync(stoppingToken);
        }
    }

    // 分页扩展方法
    public static class LinqExtensions
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int size)
        {
            T[] bucket = null!;
            var count = 0;

            foreach (var item in source)
            {
                bucket ??= new T[size];
                bucket[count++] = item;

                if (count != size) continue;

                yield return bucket;
                bucket = null!;
                count = 0;
            }

            if (bucket != null && count > 0)
                yield return bucket.Take(count);
        }
    }
}
