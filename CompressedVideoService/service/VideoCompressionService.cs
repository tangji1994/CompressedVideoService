using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CompressedVideoService.entity;
using CompressedVideoService.repository;

namespace CompressedVideoService.service
{
    internal class VideoCompressionService
    {
        private readonly VideoRepository _videoRepository;
        private readonly EventLog _serviceLogger;
        private readonly string _ffmpegPath, _outputDirectory, _vcodec;
       

        public VideoCompressionService(VideoRepository videoRepository, string ffmpegPath, string outputDirectory, string vcodec, EventLog serviceLogger)
        {
            _videoRepository = videoRepository;
            _ffmpegPath = ffmpegPath;
            _outputDirectory = outputDirectory;
            _vcodec = vcodec;
           _serviceLogger = serviceLogger;
        }

        public async Task StartCompress(CancellationToken token)
        {
            _serviceLogger.WriteEntry("开始压缩视频: ", EventLogEntryType.Information);
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Run(() => CompressVideoAsync(token), token);
                    // 等待指定间隔时间
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
                catch (Exception e)
                {
                    _serviceLogger.WriteEntry($"视频压缩已经停止 {e.Message}", EventLogEntryType.Information);
                }
            }
        }

        private async Task CompressVideoAsync(CancellationToken token)
        {
            IEnumerable<VideoFileEntity> videoFiles = null;
            try
            {
                videoFiles = _videoRepository.GetPendingCompression();
            }
            catch (Exception ex)
            {
                _serviceLogger.WriteEntry($"CompressVideoAsync {ex.Message}", EventLogEntryType.Information);
            }

            if (videoFiles == null)
            {
                
                return;
            }
            var tasks = new List<Task>();
            foreach (var videoFile in videoFiles)
            {
                _serviceLogger.WriteEntry($"CompressVideoAsync: {videoFile.FilePath}", EventLogEntryType.Information);
                tasks.Add(ProcessAsync(videoFile, token));
            }
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _serviceLogger.WriteEntry($"CompressVideoAsync {ex.Message}", EventLogEntryType.Information);
            }
        }

        private async Task ProcessAsync(VideoFileEntity videoFile, CancellationToken token)
        {
            if (!File.Exists(_ffmpegPath))
            {
                _serviceLogger.WriteEntry($"FFmpeg 未找到: {_ffmpegPath}", EventLogEntryType.Error);
                return;
            }

            string compressedFilePath = Path.Combine(_outputDirectory, "compressed_" + Path.GetFileName(videoFile.FilePath) + ".mp4");
            string filePath = videoFile.FilePath;
            var arg = $"-i \"{filePath}\" -vcodec libx264 -crf 28 \"{compressedFilePath}\"";
            if (_vcodec.Equals("hevc_qsv"))
            {
                //ffmpeg -hwaccel qsv -i input.mp4 -c:v hevc_qsv -global_quality 23 -preset medium output.mp4
                arg = $"-hwaccel qsv -hwaccel_output_format qsv -i \"{filePath}\" -vcodec {_vcodec} -global_quality 35 -preset 7  -f mp4 \"{compressedFilePath}\"";
            }

            using (Process process = new Process())
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = arg,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                process.StartInfo = startInfo;

                try
                {
                    process.Start();
                    _videoRepository.UpdateStatus(videoFile, 1);

                    var outputReadTask = process.StandardOutput.ReadToEndAsync();
                    var errorReadTask = process.StandardError.ReadToEndAsync();

                    await Task.Run(() => process.WaitForExit(), token);
                    await Task.WhenAll(outputReadTask, errorReadTask);

                    string errorOutput = await errorReadTask;

                    if (process.ExitCode != 0)
                    {
                        if (File.Exists(compressedFilePath)) File.Delete(compressedFilePath);
                        _serviceLogger.WriteEntry($"压缩失败: {errorOutput}", EventLogEntryType.Error);
                        return;
                    }

                    _videoRepository.UpdateStatus(videoFile, 2);
                }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited) process.Kill();
                    _serviceLogger.WriteEntry($"压缩取消: {videoFile.FilePath}", EventLogEntryType.Warning);
                }
                catch (Exception ex)
                {
                    if (!process.HasExited) process.Kill();
                    if (File.Exists(compressedFilePath)) File.Delete(compressedFilePath);
                    _serviceLogger.WriteEntry($"压缩异常: {ex.Message}", EventLogEntryType.Error);
                }
            }
        }

        //private async Task ProcessAsync(VideoFileEntity videoFile, CancellationToken token)
        //{
        //    string compressedFilePath = Path.Combine(_outputDirectory, "compressed_" + Path.GetFileName(videoFile.FilePath) + ".mp4");
        //    string filePath = videoFile.FilePath;
        //    var arg = $"-i \"{filePath}\" -vcodec libx264 -crf 28 \"{compressedFilePath}\"";
        //    if (_vcodec.Equals("hevc_qsv"))
        //    {
        //        //ffmpeg -hwaccel qsv -i input.mp4 -c:v hevc_qsv -global_quality 23 -preset medium output.mp4
        //        arg = $"-hwaccel qsv -hwaccel_output_format qsv -i \"{filePath}\" -vcodec {_vcodec} -global_quality 35 -preset 7  -f mp4 \"{compressedFilePath}\"";
        //    }
        //    try
        //    {
        //        if (!File.Exists(_ffmpegPath))
        //        {
        //            throw new FileNotFoundException($"FFmpeg executable not found at: {_ffmpegPath}");
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        _serviceLogger.WriteEntry($"发生错误,FFmpeg executable not found {ex.Message}", EventLogEntryType.Error);
        //    }
        //    ProcessStartInfo startInfo = new ProcessStartInfo
        //    {
        //        FileName = _ffmpegPath,
        //        Arguments = arg,
        //        RedirectStandardOutput = true,
        //        RedirectStandardError = true,
        //        UseShellExecute = false,
        //        CreateNoWindow = true
        //    };
        //    using (Process process = new Process())
        //    {
        //        process.StartInfo = startInfo;
        //        _serviceLogger.WriteEntry($"arg {process.StartInfo.Arguments}", EventLogEntryType.Information);
        //        process.Start();
        //        // 开始异步读取输出和错误流
        //        var outputReadTask = process.StandardOutput.ReadToEndAsync();
        //        var errorReadTask = process.StandardError.ReadToEndAsync();
        //        try
        //        {
        //            _videoRepository.UpdateStatus(videoFile, 1);
        //            await Task.Run(() => process.WaitForExit(), token);
        //            // 确保流读取完成
        //            try
        //            {
        //                await Task.WhenAll(outputReadTask, errorReadTask);
        //            }
        //            catch(Exception ex)
        //            {
        //                _serviceLogger.WriteEntry($"视频压缩异常停止: {ex.Message}", EventLogEntryType.Error);
        //            }

        //            // 检查 FFmpeg 退出码
        //            if (process.ExitCode != 0)
        //            {
        //                // 清理已创建的压缩文件（如果存在）
        //                if (File.Exists(compressedFilePath))
        //                    File.Delete(compressedFilePath);
        //                var error = await errorReadTask;
        //                _serviceLogger.WriteEntry($"视频压缩异常停止: {error}", EventLogEntryType.Error);
        //                process.Kill();
        //                return;
        //            }
        //            else
        //            {
        //                if (File.Exists(filePath))
        //                {
        //                    //File.Delete(filePath);
        //                }
        //                process.Kill();
        //                return;
        //            }
        //        }
        //        catch (Exception e)
        //        {
        //            process.Kill();
        //            // 清理已创建的压缩文件（如果存在）
        //            if (File.Exists(compressedFilePath))
        //                File.Delete(compressedFilePath);
        //            _serviceLogger.WriteEntry($"视频压缩异常停止: {e.Message}", EventLogEntryType.Error);
        //            return;
        //        }
        //    }
        //}
    }
}
