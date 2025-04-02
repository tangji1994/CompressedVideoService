using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CompressedVideoService.entity;
using CompressedVideoService.repository;
using CompressedVideoService.utility;

namespace CompressedVideoService.service
{
    internal class InputDirectoryMonitoringService
    {
        private FileSystemWatcher _fileSystemWatcher;
        private VideoRepository _videoRepository;
        private CancellationToken _cancellationToken;
        private readonly EventLog _serviceLogger;
        private readonly string _inputDirectory;

        public InputDirectoryMonitoringService(string inputDirectory, FileSystemWatcher watcher, VideoRepository videoRepository, EventLog serviceLogger)
        {
            _inputDirectory = inputDirectory;
            _fileSystemWatcher = watcher;
            _videoRepository = videoRepository;
            _serviceLogger = serviceLogger;
        }

        public async void StartMonitoring(CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _fileSystemWatcher.Created += OnFileCreated;
            _fileSystemWatcher.Renamed += OnFileRenamed;
            _fileSystemWatcher.Deleted += OnFileDeleted;
            _fileSystemWatcher.EnableRaisingEvents = true;
            HashSet<string> currentFiles = new HashSet<string>(Directory.GetFiles(_inputDirectory));
            //IEnumerable<VideoFileEntity> dataBaseExistingFiles = _videoRepository.GetAll();
            foreach (var currentFile in currentFiles)
            {
                await TrySaveToDatabase(currentFile);
            }
            
        }

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            _serviceLogger.WriteEntry($"发现新文件 {e.FullPath}", EventLogEntryType.Information);
            try
            {
                await Task.Delay(1000, _cancellationToken);
                await TrySaveToDatabase(e.FullPath);
            }
            catch (Exception ex)
            {
                _serviceLogger.WriteEntry($"发生错误 {ex.Message}", EventLogEntryType.Error);
            }
        }


        private async void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            _serviceLogger.WriteEntry($"发现文件被删除 {e.FullPath}", EventLogEntryType.Information);
            try
            {
                await Task.Delay(1000, _cancellationToken);
                await TryDeletedFileFromDatabase(e.FullPath);
            }
            catch (Exception ex)
            {
                _serviceLogger.WriteEntry($"发生错误 {ex.Message}", EventLogEntryType.Error);
            }
        }

        private async Task TryDeletedFileFromDatabase(string fullPath)
        {
            var count = 0;
            while (!_cancellationToken.IsCancellationRequested)
            {
                if (File.Exists(fullPath)) { return; }
                try
                {
                    VideoFileEntity videoFileEntity = _videoRepository.FindByFilePath(fullPath);
                    if (videoFileEntity != null)
                    {
                        _videoRepository.DeleteVideo(videoFileEntity);
                    }
                }
                catch (Exception e)
                {
                    count++;
                    _serviceLogger.WriteEntry($"第{count}次尝试从数据库中删除失败:{e.Message}", EventLogEntryType.Information);
                    if (count <= 3)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(10, count)));
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        private async void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            _serviceLogger.WriteEntry($"发现文件被重新命名 {e.FullPath}", EventLogEntryType.Information);
            try
            {
                await Task.Delay(1000, _cancellationToken);
                await TryUpdataFilePathToDatabase(e.FullPath,e.OldFullPath);
            }
            catch (Exception ex)
            {
                _serviceLogger.WriteEntry($"发生错误 {ex.Message}", EventLogEntryType.Error);
            }
        }

        private async Task TryUpdataFilePathToDatabase(string fullPath, string oldFullPath)
        {
            var count = 0;
            while (!_cancellationToken.IsCancellationRequested)
            {
                if (!File.Exists(fullPath)) { return; }
                try
                {

                    using (var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        VideoFileEntity videoFileEntity = _videoRepository.FindByFilePath(oldFullPath);
                        var hash = HashUtility.ComputeFileHash(fullPath);
                        if (videoFileEntity == null)
                        {
                            _videoRepository.AddVideo(
                                new VideoFileEntity
                                {
                                    FilePath = fullPath,
                                    Hash = hash,
                                    Status = 0,
                                });
                        }
                        else
                        {
                            _serviceLogger.WriteEntry($"else,{videoFileEntity.FilePath}", EventLogEntryType.Information);
                            videoFileEntity.FilePath = fullPath;
                            videoFileEntity.Hash = hash;
                            videoFileEntity.Status = 0;
                            _videoRepository.UpdateVideo(videoFileEntity);
                        }
                        return;
                    }
                }
                catch (Exception e)
                {
                    count++;
                    _serviceLogger.WriteEntry($"第{count}次尝试保存文件到数据库失败:{e.Message}", EventLogEntryType.Information);
                    if (count <= 3)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(10, count)));
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        private async Task TrySaveToDatabase(string fullPath)
        {
            var count = 0;
            while (!_cancellationToken.IsCancellationRequested) {
                if (!File.Exists(fullPath)) { return; }
                try
                {
                    
                    using (var stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        VideoFileEntity videoFileEntity = _videoRepository.FindByFilePath(fullPath);
                        var hash = HashUtility.ComputeFileHash(fullPath);
                        if (videoFileEntity == null)
                        {
                            _videoRepository.AddVideo(
                                new VideoFileEntity
                                {
                                    FilePath = fullPath,
                                    Hash = hash,
                                    Status = 0,
                                });
                        }
                        else if(!videoFileEntity.Hash.Equals(hash))
                        {
                            videoFileEntity.Hash = hash;
                            videoFileEntity.Status = 0;
                            _videoRepository.UpdateVideo(videoFileEntity);
                        }
                        return;
                    }
                }
                catch (IOException e)
                {
                    count++;
                    _serviceLogger.WriteEntry($"第{count}次尝试保存文件到数据库失败:{e.Message}", EventLogEntryType.Information);
                    if (count <= 3)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(10, count)));
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }
    }
}
