using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace AudioCraft
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<FileItem> _files = new();
        private string _sourcePath = string.Empty;
        private string _outputPath = string.Empty;
        private CancellationTokenSource? _cts;
        private bool _isConverting;

        public MainWindow()
        {
            InitializeComponent();
            lstFiles.ItemsSource = _files;
            txtSourcePath.Text = "未选择目录";
            txtOutputPath.Text = "未选择目录";
            cmbSourceFormat.SelectedIndex = 0;
            cmbTargetFormat.SelectedIndex = 1;
            UpdateStatus();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Log("程序已启动，请选择源文件目录和输出目录");
            }
            catch { }
        }

        private void BtnChangeSource_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "选择源文件目录"
            };
            if (dialog.ShowDialog() == true)
            {
                _sourcePath = dialog.FolderName;
                txtSourcePath.Text = _sourcePath;
                Log($"源目录已设置: {_sourcePath}");
            }
        }

        private void BtnLoadSource_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_sourcePath) || !Directory.Exists(_sourcePath))
            {
                MessageBox.Show("请先选择有效的源文件目录", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sourceFormat = ((ComboBoxItem)cmbSourceFormat.SelectedItem).Content.ToString()!.ToLower();
            var files = Directory.GetFiles(_sourcePath, $"*.{sourceFormat}");
            
            if (files.Length == 0)
            {
                MessageBox.Show($"源目录中没有找到 {sourceFormat.ToUpper()} 文件", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AddFiles(files);
            Log($"已加载 {files.Length} 个文件");
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                dragOverlay.Visibility = Visibility.Visible;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            dragOverlay.Visibility = Visibility.Collapsed;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            dragOverlay.Visibility = Visibility.Collapsed;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
                AddFiles(files);
            }
        }

        private void BtnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var sourceFormat = ((ComboBoxItem)cmbSourceFormat.SelectedItem).Content.ToString()!.ToLower();
            var dialog = new OpenFileDialog
            {
                Filter = $"音频文件 (*.{sourceFormat})|*.{sourceFormat}|所有文件|*.*",
                Multiselect = true
            };
            if (dialog.ShowDialog() == true)
            {
                AddFiles(dialog.FileNames);
            }
        }

        private void AddFiles(string[] files)
        {
            var sourceFormat = ((ComboBoxItem)cmbSourceFormat.SelectedItem).Content.ToString()!;
            foreach (var file in files)
            {
                if (_files.Any(f => f.FilePath == file)) continue;
                var ext = Path.GetExtension(file).TrimStart('.').ToUpper();
                if (ext == sourceFormat)
                {
                    var fi = new FileInfo(file);
                    _files.Add(new FileItem
                    {
                        FilePath = file,
                        FileName = Path.GetFileName(file),
                        FileSize = FormatFileSize(fi.Length),
                        Status = "待转换",
                        StatusBrush = new SolidColorBrush(Colors.Gray),
                        Progress = 0
                    });
                }
            }
            UpdateStatus();
            UpdateEmptyState();
        }

        private void BtnOpenOutput_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_outputPath))
            {
                MessageBox.Show("请先选择目标目录", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!Directory.Exists(_outputPath))
                Directory.CreateDirectory(_outputPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = _outputPath,
                UseShellExecute = true
            });
        }

        private void BtnChangeOutput_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "选择输出目录"
            };
            if (dialog.ShowDialog() == true)
            {
                _outputPath = dialog.FolderName;
                txtOutputPath.Text = _outputPath;
                Log($"输出目录已更改为: {_outputPath}");
            }
        }

        private async void BtnConvert_Click(object sender, RoutedEventArgs e)
        {
            if (_isConverting) return;
            if (_files.Count == 0)
            {
                MessageBox.Show("请先添加要转换的文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_outputPath))
            {
                MessageBox.Show("请先选择目标目录", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isConverting = true;
            btnConvert.IsEnabled = false;
            _cts = new CancellationTokenSource();

            var sourceFormat = ((ComboBoxItem)cmbSourceFormat.SelectedItem).Content.ToString()!.ToLower();
            var targetFormat = ((ComboBoxItem)cmbTargetFormat.SelectedItem).Content.ToString()!.ToLower();

            if (!Directory.Exists(_outputPath))
                Directory.CreateDirectory(_outputPath);

            var successCount = 0;
            var failCount = 0;

            Log($"开始批量转换: {_files.Count} 个文件 ({sourceFormat.ToUpper()} → {targetFormat.ToUpper()})");

            foreach (var file in _files.Where(f => f.Status != "已完成"))
            {
                if (_cts.Token.IsCancellationRequested) break;

                file.Status = "转换中...";
                file.StatusBrush = new SolidColorBrush(Color.FromRgb(0, 212, 255));
                file.Progress = 0;

                try
                {
                    var outputFileName = Path.ChangeExtension(Path.GetFileName(file.FilePath), targetFormat);
                    var outputPath = Path.Combine(_outputPath, outputFileName);

                    var success = await ConvertAudioAsync(file.FilePath, outputPath, file, _cts.Token);

                    if (success)
                    {
                        file.Status = "已完成";
                        file.StatusBrush = new SolidColorBrush(Color.FromRgb(0, 255, 136));
                        file.Progress = 100;
                        successCount++;
                        Log($"✓ 转换成功: {file.FileName}");
                    }
                    else
                    {
                        file.Status = "失败";
                        file.StatusBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68));
                        failCount++;
                        Log($"✗ 转换失败: {file.FileName}");
                        Log($"  输入文件: {file.FilePath}");
                        Log($"  输出文件: {outputPath}");
                    }
                }
                catch (Exception ex)
                {
                    file.Status = "失败";
                    file.StatusBrush = new SolidColorBrush(Color.FromRgb(255, 68, 68));
                    failCount++;
                    Log($"✗ 转换异常: {file.FileName} - {ex.Message}");
                }
            }

            Log($"转换完成: 成功 {successCount} 个, 失败 {failCount} 个");
            if (failCount > 0)
            {
                MessageBox.Show($"转换完成!\n成功: {successCount} 个\n失败: {failCount} 个\n\n日志已保存到 logs 目录", 
                    "转换结果", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show($"全部转换成功! 共 {successCount} 个文件", 
                    "转换完成", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _isConverting = false;
            btnConvert.IsEnabled = _files.Count > 0;
        }

        private async Task<bool> ConvertAudioAsync(string inputPath, string outputPath, FileItem item, CancellationToken ct)
        {
            var toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
            var ffmpegPath = Path.Combine(toolsPath, "ffmpeg.exe");

            if (!File.Exists(ffmpegPath))
            {
                Log("错误: 未找到 ffmpeg.exe，请确保它在 tools 目录中");
                return false;
            }

            var targetFormat = Path.GetExtension(outputPath).TrimStart('.').ToLower();
            var arguments = $"-y -i \"{inputPath}\"";
            
            if (targetFormat == "mp3")
                arguments += " -c:a libmp3lame -b:a 64k";
            else if (targetFormat == "aac")
                arguments += " -c:a aac -b:a 128k";
            
            arguments += $" \"{outputPath}\"";

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) outputBuilder.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                    ParseProgress(e.Data, item);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                var errorOutput = errorBuilder.ToString();
                if (!string.IsNullOrEmpty(errorOutput))
                {
                    Log($"FFmpeg错误: {errorOutput.Substring(0, Math.Min(200, errorOutput.Length))}");
                }
            }

            return process.ExitCode == 0;
        }

        private void ParseProgress(string output, FileItem item)
        {
            if (output.Contains("time=") && output.Contains("Duration:"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(output, @"time=(\d+):(\d+):(\d+)\.\d+");
                if (match.Success)
                {
                    var hours = int.Parse(match.Groups[1].Value);
                    var minutes = int.Parse(match.Groups[2].Value);
                    var seconds = int.Parse(match.Groups[3].Value);
                    var currentTime = hours * 3600 + minutes * 60 + seconds;

                    var durationMatch = System.Text.RegularExpressions.Regex.Match(output, @"Duration: (\d+):(\d+):(\d+)\.\d+");
                    if (durationMatch.Success)
                    {
                        var dHours = int.Parse(durationMatch.Groups[1].Value);
                        var dMinutes = int.Parse(durationMatch.Groups[2].Value);
                        var dSeconds = int.Parse(durationMatch.Groups[3].Value);
                        var totalDuration = dHours * 3600 + dMinutes * 60 + dSeconds;

                        if (totalDuration > 0)
                        {
                            var progress = (int)((double)currentTime / totalDuration * 100);
                            Dispatcher.Invoke(() =>
                            {
                                item.Progress = Math.Min(progress, 99);
                            });
                        }
                    }
                }
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (_isConverting)
            {
                MessageBox.Show("正在转换中，无法清空列表", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_files.Count == 0)
            {
                MessageBox.Show("列表已经是空的", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"确定要清空全部 {_files.Count} 个文件吗？", "警告", 
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                _files.Clear();
                UpdateStatus();
                UpdateEmptyState();
            }
        }

        private void BtnRemoveFile_Click(object sender, RoutedEventArgs e)
        {
            if (_isConverting)
            {
                MessageBox.Show("正在转换中，无法移除文件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (sender is Button button && button.Tag is FileItem file)
            {
                _files.Remove(file);
                UpdateStatus();
                UpdateEmptyState();
            }
        }

        private void UpdateStatus()
        {
            txtStatus.Text = _isConverting ? "转换中..." : "就绪";
            txtFileCount.Text = $"{_files.Count} 个文件";
        }

        private void UpdateEmptyState()
        {
            if (_files.Count == 0)
            {
                emptyState.Visibility = Visibility.Visible;
                lstFiles.Visibility = Visibility.Collapsed;
                btnConvert.IsEnabled = false;
            }
            else
            {
                emptyState.Visibility = Visibility.Collapsed;
                lstFiles.Visibility = Visibility.Visible;
                btnConvert.IsEnabled = !_isConverting;
            }
        }

        private void Log(string message)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logMessage = $"[{timestamp}] {message}\r\n";

            Dispatcher.Invoke(() =>
            {
                txtLog.AppendText(logMessage);
                logScroller.ScrollToEnd();
            });

            SaveLogToFile(message);
        }

        private void SaveLogToFile(string message)
        {
            try
            {
                var logsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logsPath))
                    Directory.CreateDirectory(logsPath);

                var logFile = Path.Combine(logsPath, $"{DateTime.Now:yyyy-MM-dd}.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                File.AppendAllText(logFile, $"[{timestamp}] {message}\r\n");
            }
            catch { }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = ["B", "KB", "MB", "GB"];
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
    }

    public class FileItem : System.ComponentModel.INotifyPropertyChanged
    {
        private string _status = string.Empty;
        private double _progress;
        private SolidColorBrush _statusBrush = new(Colors.Gray);

        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FileSize { get; set; } = string.Empty;
        
        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }
        
        public SolidColorBrush StatusBrush
        {
            get => _statusBrush;
            set
            {
                _statusBrush = value;
                OnPropertyChanged();
            }
        }
        
        public double Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged();
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }
}
