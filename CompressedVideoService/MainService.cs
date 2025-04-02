using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CompressedVideoService.entity;
using CompressedVideoService.repository;
using CompressedVideoService.service;

namespace CompressedVideoService
{
    public partial class MainService : ServiceBase
    {
        private InputDirectoryMonitoringService _inputService;
        private VideoCompressionService _compressService;
        private VideoFileArchivingService _archiveService;
        private FileSystemWatcher _fileSystemWatcher;
        private CancellationTokenSource _cts;
        private Task _inputDirectoryMonitoringServiceBackgroundTask, _compressServiceBackgroundTask;
        private VideoRepository _videoRepository;
        private EventLog _serviceLogger;
        private string _dbConnectionString;
        private string _inputDirectory, _ffmpegPath, _outputDirectory, _vcodec;
        public MainService()
        {
            InitializeComponent();
            //创建事件日志实例
           _serviceLogger = new EventLog
           {
               Source = "CompressedVideoService",
               Log = "Application"
           };

            // 确保事件源存在
            if (!EventLog.SourceExists(_serviceLogger.Source))
            {
                EventLog.CreateEventSource(_serviceLogger.Source, _serviceLogger.Log);
            }

            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string targetFilePath = Path.Combine(exeDirectory, "database.db");
            _dbConnectionString = $"Data Source={targetFilePath};Version=3;";

            _videoRepository = new VideoRepository(_dbConnectionString);
            _ffmpegPath = ConfigurationManager.AppSettings["ffmpegPath"];
            _outputDirectory = ConfigurationManager.AppSettings["OutputDirectory"];
            _vcodec = ConfigurationManager.AppSettings["vcodec"];
            _inputDirectory = ConfigurationManager.AppSettings["InputDirectory"];
            _fileSystemWatcher = new FileSystemWatcher(_inputDirectory);
        }

        protected override void OnStart(string[] args)
        {
            InitializeBackgroundTask();
        }

        protected override void OnStop()
        {
            try
            {
                _serviceLogger.WriteEntry("正在停止服务...", EventLogEntryType.Information);

                // 停止后台任务
                _cts?.Cancel();
                _inputDirectoryMonitoringServiceBackgroundTask?.Wait(TimeSpan.FromSeconds(5));
                _compressServiceBackgroundTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ae)
            {
                ae.Handle(ex => ex is TaskCanceledException);
                _serviceLogger.WriteEntry($"停止时发生异常: {ae.Flatten().Message}", EventLogEntryType.Warning);
            }
            finally
            {
                _cts?.Dispose();
                _inputDirectoryMonitoringServiceBackgroundTask?.Dispose();
                _compressServiceBackgroundTask?.Dispose();
                _videoRepository = null;
            }
        }

        private void InitializeBackgroundTask()
        {

            double intervalInMilliseconds = 60000; // 1分钟
            _inputService = new InputDirectoryMonitoringService(_inputDirectory, _fileSystemWatcher, _videoRepository, _serviceLogger);
            _compressService = new VideoCompressionService(_videoRepository, _ffmpegPath, _outputDirectory, _vcodec, _serviceLogger);
            // 启动后台任务
            _cts = new CancellationTokenSource();
            _inputDirectoryMonitoringServiceBackgroundTask = Task.Run(() => _inputService.StartMonitoring(_cts.Token), _cts.Token);
            _compressServiceBackgroundTask = Task.Run(() => _compressService.StartCompress(_cts.Token), _cts.Token);
        }
    }
}
