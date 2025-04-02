using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CompressedVideoApp
{
    public partial class Form1 : Form
    {
        private string _inputDirectory, _outputDirectory, _ffmpegPath;
        private CancellationTokenSource _cts;
        private bool _isChecked, _isRun;
        private bool _isH264 = false;
        private bool _isH265 = true;
        private bool _is_HW_None = false;
        private bool _is_HW_QSV = true;
        private bool _is_HW_NVENC = false;
        private int _videoQuality = 2;
        private static readonly HashSet<string> _videoExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".mpeg", ".mpg", ".3gp", ".vob", ".amv", ".asf", ".m4a", ".rm", ".rmvb"
        };
        public Form1()
        {
            InitializeComponent();
            progressBar1.Visible = false;
            _isRun = false;
            _isChecked = false;
            if (_isRun)
            {
                button3.Text = "取消压缩";
            }
            else {
                button3.Text = "开始压缩";
            }
            _ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib\\ffmpeg.exe");
            _outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "压缩好的视频");
            _inputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "需要压缩的视频");
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
                AddColoredLine("默认压缩后的视频存放在目录： \r\n" + _outputDirectory, Color.White);
            }
            if (!Directory.Exists(_inputDirectory))
            {
                Directory.CreateDirectory(_inputDirectory);
                AddColoredLine("默认压缩存放在以下目录的视频： \r\n" + _inputDirectory, Color.White);
            }

            AddColoredLine("默认压缩后的视频存放在目录： \r\n" + _outputDirectory, Color.White);
            AddColoredLine("已经默认启用qsv硬件加速，将会使用Intel核显进行加速", Color.White);
            AddColoredLine("如果电脑有Intel核显，建议启用qsv硬件加速", Color.White);
            AddColoredLine("如果电脑有NVIDIA显卡，建议启用nvenc硬件加速", Color.White);
            AddColoredLine("如果压缩出现异常，请把硬件加速选择None", Color.White);
            AddColoredLine("已经默认启用H.265压缩算法", Color.White);
            AddColoredLine("如果需要压缩后的视频在老旧设备上播放或者性能很差的硬件设备上播放，请选择H.264压缩算法", Color.White);
            AddColoredLine("已经默认启用低清晰度的压缩", Color.White);
            AddColoredLine("清晰度越低，压缩后视频文件占用空间越少", Color.White);

        }


        private void AddColoredLine(string text, Color color)
        {
            // 定位到文本末尾
            richTextBox1.SelectionStart = richTextBox1.TextLength;
            // 设置颜色并追加内容
            richTextBox1.SelectionColor = color;
            richTextBox1.AppendText(text + Environment.NewLine);
            // 重置为默认颜色
            richTextBox1.SelectionColor = richTextBox1.ForeColor;
            richTextBox1.ScrollToCaret(); // 滚动到光标位置
        }

        private void button1_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                // 配置对话框属性
                folderDialog.Description = "请选择文件夹"; // 对话框标题
                folderDialog.RootFolder = Environment.SpecialFolder.MyComputer; // 初始根目录
                folderDialog.ShowNewFolderButton = true; // 允许用户新建文件夹

                // 显示对话框并检查用户是否点击了“确定”
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    _inputDirectory = folderDialog.SelectedPath;
                    AddColoredLine("已经选择需要压缩的视频目录：\r\n" + _inputDirectory, Color.Green);
                }
                else
                {
                    AddColoredLine("用户取消需要压缩的视频目录的选择", Color.Yellow);
                }
            }
        }

        private async void button3_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(_inputDirectory))
            {
                MessageBox.Show("请先选择有效的需要压缩的视频目录！");
                return;
            }
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
                AddColoredLine("默认压缩后的视频存放在目录： \r\n" + _outputDirectory, Color.White);
            }
            _isRun = !_isRun;
            if (_isRun)
            {
                button3.Text = "取消压缩";
                // 定义常用视频扩展名列表（可自由增删）


                // 获取指定目录下的所有视频文件
                List<string> videoFiles = Directory
                    .GetFiles(_inputDirectory, "*.*")
                    .Where(file => _videoExtensions.Contains(Path.GetExtension(file)))
                    .ToList();// 转为列表
                //List<string> videoFiles = Directory.GetFiles(_inputDirectory).ToList();

                if (videoFiles.Count == 0)
                {
                    AddColoredLine("需要压缩的视频目录中没有找到视频文件！",Color.Red);
                    AddColoredLine("仅支持以下格式视频文件\r\n.mp4, .avi, .mkv, .mov, .wmv, .flv, .webm, .mpeg, .mpg, .3gp", Color.Red);
                    return;
                }

                // 初始化进度条
                progressBar1.Maximum = videoFiles.Count;
                progressBar1.Value = 0;
                progressBar1.Visible = true;
                _cts = new CancellationTokenSource();
                SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
                checkBox1.Enabled = false;
                button1.Enabled = false;
                button2.Enabled = false;
                foreach (var videoFile in videoFiles)
                {
                    if (_cts.Token.IsCancellationRequested)
                    {
                        break; // 支持取消操作
                    }

                    await semaphore.WaitAsync(_cts.Token);
                    try
                    {
                        await Task.Run(() => ProcessAsync(videoFile, _cts.Token), _cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // 处理取消逻辑
                        //if (File.Exists(videoFile)) File.Delete(videoFile);
                        break;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
                _isRun = false;
                checkBox1.Enabled = true;
                button2.Enabled = true;
                button1.Enabled = true;
                if (button3.Enabled)
                {
                    AddColoredLine("所有的视频都已经完成压缩", Color.Green);
                    button3.Text = "开始压缩";
                }
                else
                {
                    AddColoredLine("已经成功取消所有未完成的压缩任务", Color.Yellow);
                    progressBar1.Visible = false;
                }
                button3.Enabled = true;
            }
            else
            {
                _cts?.Cancel();
                AddColoredLine("正在取消压缩任务...\r\n需要等待当前正在压缩的视频结束", Color.Yellow);
                button3.Text = "开始压缩";
                button3.Enabled = false;
            }
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            _isH264 = radioButton1.Checked;
            if (_isH264)
            {
                AddColoredLine("已经选择H.264压缩算法", Color.Green);
            }
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            _isH265 = radioButton2.Checked;
            if (_isH265)
            {
                AddColoredLine("已经选择H.265压缩算法", Color.Green);
            }
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            _is_HW_None = radioButton3.Checked;
            if (_is_HW_None)
            {
                AddColoredLine("已经选择不使用硬件加速，所有的计算都会使用CPU完成", Color.Green);
                AddColoredLine("如果电脑有Intel核显，建议启用qsv硬件加速", Color.White);
                AddColoredLine("如果电脑有NVIDIA显卡，建议启用nvenc硬件加速", Color.White);
                AddColoredLine("如果压缩出现异常，请把硬件加速选择None",Color.White);
            }
        }

        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {
            _is_HW_QSV = radioButton4.Checked;
            if (_is_HW_QSV)
            {
                AddColoredLine("已经选择使用qsv硬件加速，将会使用Intel核显进行加速", Color.Green);
                AddColoredLine("如果电脑有Intel核显，建议启用qsv硬件加速", Color.White);
                AddColoredLine("如果电脑有NVIDIA显卡，建议启用nvenc硬件加速", Color.White);
                AddColoredLine("如果压缩出现异常，请把硬件加速选择None", Color.White);
            }
        }

        private void radioButton5_CheckedChanged(object sender, EventArgs e)
        {
            _is_HW_NVENC = radioButton5.Checked;
            if (_is_HW_NVENC)
            {
                AddColoredLine("已经选择使用nvenc硬件加速，将会使用NVIDIA显卡进行加速", Color.Green);
                AddColoredLine("如果电脑有Intel核显，建议启用qsv硬件加速", Color.White);
                AddColoredLine("如果电脑有NVIDIA显卡，建议启用nvenc硬件加速", Color.White);
                AddColoredLine("如果压缩出现异常，请把硬件加速选择None", Color.White);
            }
        }

        private void radioButton7_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton7.Checked)
            {
                _videoQuality = 0;
                AddColoredLine("已经选择高清晰度的压缩", Color.Green);
            }
        }

        private void radioButton8_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton8.Checked)
            {
                _videoQuality = 1;
                AddColoredLine("已经选择中清晰度的压缩", Color.Green);
            }
        }

        private void radioButton6_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton6.Checked)
            {
                _videoQuality = 2;
                AddColoredLine("已经选择低清晰度的压缩", Color.Green);
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            _isChecked = checkBox1.Checked;
            if (_isChecked)
            {
                AddColoredLine("已经选择压缩后删除源视频", Color.Red);
            }
            else {
               //textBoxAppendOneLine("已经取消压缩后删除源视频", textBox1);
                AddColoredLine("已经取消压缩后删除源视频", Color.Green);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                // 配置对话框属性
                folderDialog.Description = "请选择文件夹"; // 对话框标题
                folderDialog.RootFolder = Environment.SpecialFolder.MyComputer; // 初始根目录
                folderDialog.ShowNewFolderButton = true; // 允许用户新建文件夹

                // 显示对话框并检查用户是否点击了“确定”
                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    _outputDirectory = folderDialog.SelectedPath;
                    // 这里处理选中的路径，例如显示在文本框或记录到变量中
                    //MessageBox.Show($"已选择文件夹: {_outputDirectory}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    AddColoredLine("已经选择了压缩后的视频的保存目录：\r\n" + _outputDirectory, Color.Green);
                }
                else
                {
                    AddColoredLine("用户取消需压缩后的视频的保存目录的选择", Color.Yellow);
                }
            }
        }

        public string GenerateFFmpegCommand(string inputPath, string outputPath)
        {
            // 基础参数（必须保留转义字符）
            var sb = new StringBuilder("-hide_banner -y -loglevel error -stats ");

            /*---------------------------------------
             * 1. 硬件加速参数
             *--------------------------------------*/
            if (_is_HW_QSV)
            {
                // Intel QSV硬件加速
                sb.Append("-hwaccel qsv -hwaccel_output_format qsv ");
            }
            else if (_is_HW_NVENC)
            {
                // NVIDIA硬件加速
                sb.Append("-hwaccel cuda -hwaccel_output_format cuda ");
            }

            sb.Append($"-i \"{inputPath}\"");

            /*---------------------------------------
             * 2. 视频编码参数
             *--------------------------------------*/
            sb.Append(" -c:v ");

            // 根据编码器类型和硬件加速组合编码器名称
            if (_isH264)
            {
                sb.Append(_is_HW_QSV ? "h264_qsv" :
                         _is_HW_NVENC ? "h264_nvenc" :
                         "libx264");
            }
            else if (_isH265)
            {
                sb.Append(_is_HW_QSV ? "hevc_qsv" :
                         _is_HW_NVENC ? "hevc_nvenc" :
                         "libx265");
            }


            switch (_videoQuality)
            {
                case 0:
                    // 硬件加速专用参数
                    if (_is_HW_QSV)
                    {
                        sb.Append(" -global_quality 20 -preset veryfast");
                    }
                    else if (_is_HW_NVENC)
                    {
                        sb.Append(" -cq 20 -preset p1");
                    }
                    else
                    {
                        sb.Append(" -crf 20 -preset veryfast");  // CPU编码的通用预设
                    }
                    break;
                case 1:
                    // 硬件加速专用参数
                    if (_is_HW_QSV)
                    {
                        sb.Append(" -global_quality 28 -preset 7");
                    }
                    else if (_is_HW_NVENC)
                    {
                        sb.Append(" -cq 28 -preset p1");
                    }
                    else
                    {
                        sb.Append(" -crf 28 -preset veryfast");  // CPU编码的通用预设
                    }
                    break;
                case 2:
                    // 硬件加速专用参数
                    if (_is_HW_QSV)
                    {
                        sb.Append(" -global_quality 35 -preset 7");
                    }
                    else if (_is_HW_NVENC)
                    {
                        sb.Append(" -cq 35 -preset p1");
                    }
                    else
                    {
                        sb.Append(" -crf 35 -preset veryfast");  // CPU编码的通用预设
                    }
                    break;
            }


            /*---------------------------------------
             * 3. 音频参数（保持原音频）
             *--------------------------------------*/
            //sb.Append(" -c:a copy ");
            sb.Append(" -c:a aac -b:a 32k -ar 22050 -ac 1 -vbr 0 ");
            /*---------------------------------------
             * 4. 输入输出路径（注意路径空格处理）
             *--------------------------------------*/
            sb.Append($" \"{outputPath}\"");
            
            return sb.ToString();
        }



        private async Task ProcessAsync(string videoFile, CancellationToken token)
        {
            if (!File.Exists(_ffmpegPath))
            {
                this.Invoke((MethodInvoker)delegate {
                    AddColoredLine($"没有找到{_ffmpegPath}\r\n请确保lib目录内的ffmpeg.exe存在且版本正确", Color.Red);
                });
                return;
            }

            string compressedFilePath = Path.Combine(_outputDirectory, "compressed_" + Path.GetFileNameWithoutExtension(videoFile) + ".mp4");
            if (File.Exists(compressedFilePath))
            {
                string old = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss-fff") + "_" + Path.GetFileName(compressedFilePath);
                string oldPath = Path.Combine(Path.GetDirectoryName(compressedFilePath), old);
                File.Move(compressedFilePath, oldPath);
                this.Invoke((MethodInvoker)delegate {
                    AddColoredLine($"在保存目录在发现旧文件：\r\n{compressedFilePath}\r\n已经将旧文件重命名为：\r\n{oldPath}", Color.Red);
                });
            }
            string filePath = videoFile;
            //var arg = $"-hwaccel qsv -hwaccel_output_format qsv -i \"{filePath}\" -vcodec hevc_qsv -global_quality 35 -preset 7  -f mp4 \"{compressedFilePath}\"";
            var arg = GenerateFFmpegCommand(filePath, compressedFilePath);
            //this.Invoke((MethodInvoker)delegate {
            //    AddColoredLine($"压缩参数为：\r\n{arg}", Color.White);
            //});
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
                    var outputReadTask = process.StandardOutput.ReadToEndAsync();
                    var errorReadTask = process.StandardError.ReadToEndAsync();
                    //textBox1.AppendText($"开始压缩视频{filePath}");
                    // 更新进度条（需跨线程安全）
                    this.Invoke((MethodInvoker)delegate { AddColoredLine($"开始压缩视频：\r\n{filePath}",Color.White); });
                    await Task.Run(() => process.WaitForExit(), token);
                    await Task.WhenAll(outputReadTask, errorReadTask);

                    string errorOutput = await errorReadTask;

                    if (process.ExitCode != 0)
                    {
                        if (File.Exists(compressedFilePath)) File.Delete(compressedFilePath);
                        this.Invoke((MethodInvoker)delegate { AddColoredLine($"压缩异常失败，视频：\r\n{filePath}\r\n没有完成压缩\r\n{errorOutput}", Color.Red); });
                        MessageBox.Show($"压缩异常失败，视频{filePath}没有完成压缩{errorOutput}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    this.Invoke((MethodInvoker)delegate { AddColoredLine($"视频压缩完成：\r\n{filePath}",Color.Green); });
                    if (File.Exists(videoFile) && _isChecked)
                    {
                        File.Delete(videoFile);
                        this.Invoke((MethodInvoker)delegate { AddColoredLine($"已经删除源视频：\r\n{filePath}", Color.Red); });
                    }
                    this.Invoke((MethodInvoker)delegate { progressBar1.Value++; });
                }
                catch (OperationCanceledException)
                {
                    if (File.Exists(compressedFilePath)) File.Delete(compressedFilePath);
                    if (!process.HasExited) process.Kill();
                    this.Invoke((MethodInvoker)delegate { AddColoredLine($"压缩被取消，视频没有完成压缩：\r\n{filePath}", Color.Red); });
                    MessageBox.Show($"压缩被取消，视频{filePath}没有完成压缩", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    if (!process.HasExited) process.Kill();
                    if (File.Exists(compressedFilePath)) File.Delete(compressedFilePath);
                    this.Invoke((MethodInvoker)delegate { AddColoredLine($"压缩异常失败，视频：\r\n{filePath}\r\n没有完成压缩\r\n{ex.Message}", Color.Red); });
                    MessageBox.Show($"压缩异常失败，视频{filePath}没有完成压缩{ex.Message}", "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
