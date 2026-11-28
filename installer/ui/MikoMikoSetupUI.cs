using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

[assembly: AssemblyTitle("MikoMiko 安装程序")]
[assembly: AssemblyProduct("MikoMiko")]
[assembly: AssemblyCompany("MikoMiko Contributors")]
[assembly: AssemblyDescription("MikoMiko 安装与卸载程序")]
[assembly: AssemblyVersion("0.1.60.0")]
[assembly: AssemblyFileVersion("0.1.60.0")]

namespace MikoMikoSetup
{
    internal static class Program
    {
        private const string AppName = "MikoMiko";
        private const string AppVersion = "0.1.60";
        private const string AppExe = "MikoMiko.exe";
        private const string Publisher = "MikoMiko Contributors";
        private const string AppUrl = "https://github.com/Huiusto/MikoMiko";
        private const string PayloadMarker = "MIKOPAY1";

        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                var silent = args.Any(a => a.Equals("/S", StringComparison.OrdinalIgnoreCase));
                var uninstallRun = args.FirstOrDefault(a => a.StartsWith("--uninstall-run=", StringComparison.OrdinalIgnoreCase));
                var isUninstall = IsUninstallInvocation(args) || uninstallRun != null;
                var targetArg = ReadTargetArg(args);

                if (isUninstall && uninstallRun == null && !silent)
                {
                    var target = !string.IsNullOrEmpty(targetArg) ? targetArg : (ReadInstallDir() ?? Path.GetDirectoryName(ApplicationResourcePath()));
                    var temp = Path.Combine(Path.GetTempPath(), "MikoMikoUninstallUI_" + Guid.NewGuid().ToString("N") + ".exe");
                    File.Copy(ApplicationResourcePath(), temp, true);
                    var psi = new ProcessStartInfo(temp)
                    {
                        Arguments = "--uninstall-run=" + Quote(target) + " --uninstall",
                        UseShellExecute = true,
                        WorkingDirectory = Path.GetTempPath()
                    };
                    Process.Start(psi);
                    return;
                }

                if (uninstallRun != null)
                {
                    targetArg = uninstallRun.Substring("--uninstall-run=".Length).Trim('"');
                }

                if (isUninstall && silent)
                {
                    var target = !string.IsNullOrEmpty(targetArg) ? targetArg : (ReadInstallDir() ?? Path.GetDirectoryName(ApplicationResourcePath()));
                    UninstallWorker(target, null, null);
                    return;
                }

                if (silent)
                {
                    var target = !string.IsNullOrEmpty(targetArg) ? targetArg : (ReadInstallDir() ?? DefaultInstallDir());
                    InstallWorker(target, null, null);
                    LaunchApp(Path.Combine(target, AppExe));
                    return;
                }

                var app = new Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                var preview = args.FirstOrDefault(a => a.StartsWith("--preview=", StringComparison.OrdinalIgnoreCase));
                var previewPage = preview == null ? null : preview.Substring("--preview=".Length);
                var window = new SetupWindow(isUninstall, targetArg, previewPage);
                app.Run(window);
            }
            catch (Exception ex)
            {
                try
                {
                    MessageBox.Show("MikoMiko 安装程序遇到问题：\r\n" + ex.Message, AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch { }
                Environment.ExitCode = 1;
            }
        }

        private static bool IsUninstallInvocation(string[] args)
        {
            if (args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase))) return true;
            var name = Path.GetFileNameWithoutExtension(ApplicationResourcePath());
            return string.Equals(name, "Uninstall", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "MikoMikoUninstallUI", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadTargetArg(string[] args)
        {
            var direct = args.FirstOrDefault(a => a.StartsWith("/D=", StringComparison.OrdinalIgnoreCase));
            if (direct != null) return direct.Substring(3).Trim('"');
            return null;
        }

        private static string ApplicationResourcePath()
        {
            return Assembly.GetExecutingAssembly().Location;
        }

        internal static string DefaultInstallDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "MikoMiko");
        }

        internal static string NormalizeInstallTarget(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("请选择安装路径。");
            var full = Path.GetFullPath(value);
            var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.Equals(Path.GetFileName(trimmed), AppName, StringComparison.OrdinalIgnoreCase))
                full = Path.Combine(trimmed, AppName);
            var target = Path.GetFullPath(full).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var root = (Path.GetPathRoot(target) ?? string.Empty).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrEmpty(root) || string.Equals(target, root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("不能直接安装到磁盘根目录。");
            return target;
        }

        internal static long RequiredSpaceBytes()
        {
            long total = 0;
            try
            {
                long payloadLength;
                using (var payload = OpenPayload(out payloadLength))
                using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
                {
                    total = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).Sum(e => e.Length);
                }
            }
            catch
            {
                total = 300L * 1024L * 1024L;
            }
            try
            {
                var uninstaller = Assembly.GetExecutingAssembly()
                    .GetManifestResourceStream("UninstallUI.exe");
                if (uninstaller != null)
                {
                    total += uninstaller.Length;
                    uninstaller.Dispose();
                }
            }
            catch { }
            return total;
        }

        internal static string AvailableSpaceText(string path)
        {
            try
            {
                var root = Path.GetPathRoot(Path.GetFullPath(path));
                if (string.IsNullOrEmpty(root)) return "--";
                return FormatSize(new DriveInfo(root).AvailableFreeSpace);
            }
            catch
            {
                return "--";
            }
        }

        internal static string ReadInstallDir()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\MikoMiko"))
                {
                    var value = key == null ? null : key.GetValue("InstallDir") as string;
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            }
            catch { }
            return null;
        }

        internal static bool IsPayloadAvailable()
        {
            try
            {
                using (var fs = new FileStream(ApplicationResourcePath(), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (fs.Length < 32) return false;
                    fs.Position = fs.Length - 16;
                    var marker = new byte[8];
                    fs.Read(marker, 0, 8);
                    return System.Text.Encoding.ASCII.GetString(marker) == PayloadMarker;
                }
            }
            catch { return false; }
        }

        internal static Stream OpenPayload(out long length)
        {
            var fs = new FileStream(ApplicationResourcePath(), FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fs.Length < 32) throw new InvalidDataException("安装包数据不完整。");
            fs.Position = fs.Length - 16;
            var marker = new byte[8];
            fs.Read(marker, 0, 8);
            if (System.Text.Encoding.ASCII.GetString(marker) != PayloadMarker)
                throw new InvalidDataException("安装包数据不完整。");
            var sizeBytes = new byte[8];
            fs.Read(sizeBytes, 0, 8);
            var size = BitConverter.ToInt64(sizeBytes, 0);
            if (size <= 0 || size > fs.Length - 16) throw new InvalidDataException("安装包数据不完整。");
            length = size;
            return new SubStream(fs, fs.Length - 16 - size, size);
        }

        internal static void LaunchApp(string exe)
        {
            try
            {
                if (File.Exists(exe))
                {
                    Process.Start(new ProcessStartInfo(exe)
                    {
                        WorkingDirectory = Path.GetDirectoryName(exe),
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        internal static void InstallWorker(string target, Action<double> progress, Action<string> status)
        {
            target = NormalizeInstallTarget(target);
            if (string.IsNullOrEmpty(target)) throw new InvalidOperationException("安装路径无效。");

            if (status != null) status("正在结束旧版本进程...");
            foreach (var process in Process.GetProcessesByName("MikoMiko"))
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch { }
            }
            Thread.Sleep(500);
            Directory.CreateDirectory(target);

            if (status != null) status("正在安装...");
            long payloadLength;
            using (var payload = Program.OpenPayload(out payloadLength))
            using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
            {
                long total = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).Sum(e => e.Length);
                long done = 0;
                var targetRoot = target + Path.DirectorySeparatorChar;
                foreach (var entry in archive.Entries)
                {
                    var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                    var destination = Path.GetFullPath(Path.Combine(target, relative));
                    if (!destination.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(destination, target, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("安装包包含无效路径。");

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }

                    var directory = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    using (var input = entry.Open())
                    using (var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[131072];
                        int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            output.Write(buffer, 0, read);
                            done += read;
                            if (progress != null && total > 0)
                                progress(90.0 * done / total);
                        }
                    }
                }
            }

            if (progress != null) progress(92.0);
            if (status != null) status("正在注册应用...");
            var uninstaller = Path.Combine(target, "Uninstall.exe");
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("UninstallUI.exe"))
            {
                if (stream == null) throw new InvalidOperationException("卸载程序资源缺失。");
                using (var output = new FileStream(uninstaller, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(output);
                }
            }

            CreateShortcuts(target);
            WriteInstallRegistry(target, payloadLength);
            if (progress != null) progress(96.0);

            if (status != null) status("正在准备启动资源...");
            try
            {
                var prepare = new ProcessStartInfo(Path.Combine(target, AppExe), "--prepare-launch")
                {
                    WorkingDirectory = target,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var process = Process.Start(prepare))
                {
                    if (process != null) process.WaitForExit(60000);
                }
            }
            catch { }
            if (progress != null) progress(100.0);
        }

        private static void CreateShortcuts(string target)
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            var exe = Path.Combine(target, AppExe);
            var uninstaller = Path.Combine(target, "Uninstall.exe");

            var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "MikoMiko");
            Directory.CreateDirectory(startMenu);
            CreateShortcut(shell, Path.Combine(startMenu, "MikoMiko.lnk"), exe, target, exe);
            CreateShortcut(shell, Path.Combine(startMenu, "卸载 MikoMiko.lnk"), uninstaller, target, uninstaller);

            var desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MikoMiko.lnk");
            CreateShortcut(shell, desktop, exe, target, exe);
        }

        private static void CreateShortcut(dynamic shell, string link, string target, string working, string icon)
        {
            dynamic shortcut = shell.CreateShortcut(link);
            shortcut.TargetPath = target;
            shortcut.WorkingDirectory = working;
            shortcut.IconLocation = icon + ",0";
            shortcut.Save();
        }

        private static void WriteInstallRegistry(string target, long payloadLength)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\MikoMiko"))
            {
                key.SetValue("InstallDir", target);
                key.SetValue("Version", AppVersion);
            }

            using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\MikoMiko"))
            {
                key.SetValue("DisplayName", AppName);
                key.SetValue("DisplayVersion", AppVersion);
                key.SetValue("Publisher", Publisher);
                key.SetValue("DisplayIcon", Path.Combine(target, AppExe));
                key.SetValue("InstallLocation", target);
                key.SetValue("URLInfoAbout", AppUrl);
                key.SetValue("UninstallString", "\"" + Path.Combine(target, "Uninstall.exe") + "\"");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", (int)Math.Max(1, payloadLength / 1024), RegistryValueKind.DWord);
            }
        }

        internal static void UninstallWorker(string target, Action<double> progress, Action<string> status)
        {
            if (string.IsNullOrEmpty(target)) target = ReadInstallDir();
            if (string.IsNullOrEmpty(target)) throw new InvalidOperationException("找不到 MikoMiko 的安装目录。");
            target = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (status != null) status("正在结束 MikoMiko...");
            foreach (var process in Process.GetProcessesByName("MikoMiko"))
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch { }
            }
            Thread.Sleep(500);

            if (status != null) status("正在卸载...");
            var shortcuts = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MikoMiko.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "MikoMiko", "MikoMiko.lnk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "MikoMiko", "卸载 MikoMiko.lnk")
            };
            foreach (var shortcut in shortcuts)
            {
                try { if (File.Exists(shortcut)) File.Delete(shortcut); } catch { }
            }
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "MikoMiko");
                if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            }
            catch { }

            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\MikoMiko", false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\MikoMiko", false); } catch { }

            var files = Directory.Exists(target)
                ? Directory.GetFiles(target, "*", SearchOption.AllDirectories)
                : new string[0];
            var count = Math.Max(1, files.Length);
            for (var i = 0; i < files.Length; i++)
            {
                try { File.Delete(files[i]); } catch { }
                if (progress != null) progress(100.0 * (i + 1) / count);
            }
            try
            {
                if (Directory.Exists(target)) Directory.Delete(target, true);
            }
            catch { }
            if (progress != null) progress(100.0);
        }

        internal static string Quote(string text)
        {
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        internal static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L) return (bytes / 1024.0 / 1024.0 / 1024.0).ToString("0.0") + " GB";
            return Math.Max(1, bytes / 1024 / 1024) + " MB";
        }
    }

    internal sealed class SubStream : Stream
    {
        private readonly Stream _base;
        private readonly long _start;
        private readonly long _length;
        private long _position;

        public SubStream(Stream baseStream, long start, long length)
        {
            _base = baseStream;
            _start = start;
            _length = length;
            _position = 0;
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return _length; } }
        public override long Position
        {
            get { return _position; }
            set { _position = Math.Max(0, Math.Min(_length, value)); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _length) return 0;
            var remaining = (int)Math.Min(count, _length - _position);
            _base.Position = _start + _position;
            var read = _base.Read(buffer, offset, remaining);
            _position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long value;
            if (origin == SeekOrigin.Begin) value = offset;
            else if (origin == SeekOrigin.Current) value = _position + offset;
            else value = _length + offset;
            Position = value;
            return _position;
        }

        public override void Flush() { }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _base.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class SetupWindow : Window
    {
        private const double DesignWidth = 960.0;
        private const double Scale = 0.625;
        private static readonly FontFamily UiFont = new FontFamily("Microsoft YaHei UI");
        private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(0x5F, 0x6B, 0x7A));
        private static readonly Brush BlueBrush = new SolidColorBrush(Color.FromRgb(0x3F, 0xA9, 0xF5));

        private readonly bool _uninstallMode;
        private readonly string _initialTarget;
        private readonly string _previewPage;
        private readonly System.Windows.Controls.Image _background;
        private readonly Canvas _pageCanvas;
        private readonly Canvas _chromeCanvas;

        private TextBox _pathBox;
        private TextBlock _spaceText;
        private long _requiredBytes;
        private Border _progressFill;
        private TextBlock _progressPercent;
        private string _target;
        private bool _busy;

        public SetupWindow(bool uninstallMode, string target, string previewPage)
        {
            _uninstallMode = uninstallMode;
            _previewPage = previewPage;
            _initialTarget = !string.IsNullOrWhiteSpace(target)
                ? target
                : (Program.ReadInstallDir() ?? Program.DefaultInstallDir());

            Title = "MikoMiko " + (uninstallMode ? "卸载" : "安装");
            Width = DesignWidth;
            Height = 640;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SnapsToDevicePixels = true;

            var root = new Grid();
            root.ClipToBounds = true;
            _background = new System.Windows.Controls.Image { Stretch = Stretch.Fill };
            _pageCanvas = new Canvas { ClipToBounds = true };
            _chromeCanvas = new Canvas { ClipToBounds = true };
            root.Children.Add(_background);
            root.Children.Add(_pageCanvas);
            root.Children.Add(_chromeCanvas);
            Content = root;

            Closing += OnClosing;
            Loaded += delegate
            {
                AddWindowChrome();
                if (ShowPreview()) return;
                if (uninstallMode) ShowUninstallConfirm();
                else ShowWelcome();
            };
        }

        private bool ShowPreview()
        {
            if (string.IsNullOrEmpty(_previewPage)) return false;
            if (_previewPage == "directory") ShowDirectory();
            else if (_previewPage == "progress") ShowProgress("正在安装...");
            else if (_previewPage == "finish") ShowFinish();
            else if (_previewPage == "uninstall-confirm") ShowUninstallConfirm();
            else if (_previewPage == "uninstall-finish") ShowUninstallFinish();
            else if (_previewPage == "uninstall-progress") ShowProgress("正在卸载...");
            else ShowWelcome();
            return true;
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_busy)
            {
                e.Cancel = true;
                MessageBox.Show(this, "正在处理，请稍候再关闭。", "MikoMiko", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private static ImageSource LoadImage(string resource)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (stream == null) throw new InvalidOperationException("安装界面资源缺失：" + resource);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }

        private void ShowPage(string resource)
        {
            _background.Source = LoadImage(resource);
            _pageCanvas.Children.Clear();
        }

        private void AddWindowChrome()
        {
            var drag = new Border
            {
                Width = 900,
                Height = 68,
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            drag.MouseLeftButtonDown += delegate { try { DragMove(); } catch { } };
            _chromeCanvas.Children.Add(drag);

            AddHotspot(_chromeCanvas, 900, 25, 50, 40, Close);
            AddHotspot(_chromeCanvas, 848, 25, 45, 40, delegate { WindowState = WindowState.Minimized; });
        }

        private void AddHotspot(Canvas canvas, double x, double y, double w, double h, Action action)
        {
            var border = new Border
            {
                Width = w,
                Height = h,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand
            };
            Canvas.SetLeft(border, x);
            Canvas.SetTop(border, y);
            border.MouseLeftButtonUp += delegate { action(); };
            canvas.Children.Add(border);
        }

        private void AddText(Canvas canvas, string text, double x, double y, double w, double h,
            double size, Brush brush, FontWeight weight, TextAlignment align)
        {
            var block = new TextBlock
            {
                Text = text,
                FontFamily = UiFont,
                FontSize = size,
                Foreground = brush,
                FontWeight = weight,
                TextAlignment = align,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap
            };
            block.Width = w;
            block.Height = h;
            Canvas.SetLeft(block, x);
            Canvas.SetTop(block, y);
            canvas.Children.Add(block);
        }

        private void ShowWelcome()
        {
            ShowPage("Page.Welcome.png");
            AddHotspot(_pageCanvas, 204, 509, 214, 55, Close);
            AddHotspot(_pageCanvas, 545, 509, 220, 55, ShowDirectory);
        }

        private void ShowDirectory()
        {
            ShowPage("Page.Directory.png");
            _target = string.IsNullOrWhiteSpace(_target) ? _initialTarget : _target;
            try { _target = Program.NormalizeInstallTarget(_target); }
            catch { _target = Program.DefaultInstallDir(); }
            _requiredBytes = Program.RequiredSpaceBytes();

            // 用整条同色路径框盖住底图里烘焙的旧输入框和「浏览...」，
            // 再统一绘制路径文字与按钮文字，避免两层边框叠加和基线不一致。
            var boxBack = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xFB, 0xFD, 0xFF)),
                CornerRadius = new CornerRadius(29),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xBF, 0xD9, 0xFA)),
                BorderThickness = new Thickness(1.4),
                Width = 808,
                Height = 58
            };
            Canvas.SetLeft(boxBack, 80);
            Canvas.SetTop(boxBack, 404);
            _pageCanvas.Children.Add(boxBack);

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(126) });
            boxBack.Child = row;

            _pathBox = new TextBox
            {
                Text = _target,
                FontFamily = UiFont,
                FontSize = 19,
                Foreground = new SolidColorBrush(Color.FromRgb(0x50, 0x5A, 0x6B)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(28, 0, 6, 0),
                Height = 58,
                VerticalContentAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap
            };
            Grid.SetColumn(_pathBox, 0);
            row.Children.Add(_pathBox);
            _pathBox.TextChanged += delegate { UpdateSpaceInfo(); };

            var divider = new Border
            {
                Width = 1,
                Height = 29,
                Background = new SolidColorBrush(Color.FromRgb(0xD7, 0xE7, 0xFB)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(divider, 1);
            row.Children.Add(divider);

            var browseZone = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xFB, 0xFD, 0xFF)),
                CornerRadius = new CornerRadius(0, 24, 24, 0)
            };
            Grid.SetColumn(browseZone, 2);
            row.Children.Add(browseZone);

            var browseText = new TextBlock
            {
                Text = "浏览...",
                FontFamily = UiFont,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x49, 0x98, 0xE8)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            browseZone.Child = browseText;

            var spaceBack = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFE)),
                Width = 560,
                Height = 32
            };
            Canvas.SetLeft(spaceBack, 108);
            Canvas.SetTop(spaceBack, 467);
            _pageCanvas.Children.Add(spaceBack);

            _spaceText = new TextBlock
            {
                FontFamily = UiFont,
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x90)),
                Width = 540,
                Height = 22,
                TextAlignment = TextAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            Canvas.SetLeft(_spaceText, 128);
            Canvas.SetTop(_spaceText, 473);
            _pageCanvas.Children.Add(_spaceText);
            UpdateSpaceInfo();

            AddHotspot(_pageCanvas, 161, 507, 214, 55, ShowWelcome);
            AddHotspot(_pageCanvas, 558, 507, 218, 55, delegate { StartInstall(); });
            AddHotspot(_pageCanvas, 762, 404, 126, 58, BrowseDirectory);
        }

        private void UpdateSpaceInfo()
        {
            if (_spaceText == null) return;
            var path = _pathBox == null ? _target : _pathBox.Text.Trim();
            var required = Program.FormatSize(_requiredBytes);
            var available = string.IsNullOrWhiteSpace(path) ? "--" : Program.AvailableSpaceText(path);
            _spaceText.Text = "所需空间：" + required + "    可用空间：" + available;
        }

        private void BrowseDirectory()
        {
            using (var dialog = new WinForms.FolderBrowserDialog())
            {
                dialog.Description = "选择 MikoMiko 的安装位置";
                dialog.ShowNewFolderButton = true;
                try { dialog.SelectedPath = _pathBox.Text; } catch { }
                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    try
                    {
                        _pathBox.Text = Program.NormalizeInstallTarget(dialog.SelectedPath);
                        _pathBox.CaretIndex = _pathBox.Text.Length;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(this, ex.Message, "MikoMiko", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }

        private bool ValidateTarget()
        {
            try
            {
                var value = (_pathBox == null ? _target : _pathBox.Text).Trim();
                if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("请选择安装路径。");
                if (value.IndexOfAny(Path.GetInvalidPathChars()) >= 0) throw new InvalidOperationException("安装路径包含无效字符。");
                _target = Program.NormalizeInstallTarget(value);
                if (_pathBox != null && !string.Equals(_pathBox.Text, _target, StringComparison.OrdinalIgnoreCase))
                {
                    _pathBox.Text = _target;
                    _pathBox.CaretIndex = _pathBox.Text.Length;
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "MikoMiko", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private void StartInstall()
        {
            if (_busy) return;
            if (!ValidateTarget()) return;
            if (!Program.IsPayloadAvailable())
            {
                MessageBox.Show(this, "安装包数据不完整，请重新下载安装包。", "MikoMiko", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            ShowProgress("正在安装...");
            _busy = true;
            var progress = new Progress<double>(value => UpdateProgress(value));
            var status = new Progress<string>(text => SetStatus(text));
            Task.Run(delegate
            {
                Program.InstallWorker(_target, value => ((IProgress<double>)progress).Report(value),
                    text => ((IProgress<string>)status).Report(text));
            }).ContinueWith(task =>
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _busy = false;
                    if (task.IsFaulted)
                    {
                        var ex = task.Exception != null ? task.Exception.GetBaseException() : null;
                        MessageBox.Show(this, "安装失败：\r\n" + (ex == null ? "未知错误" : ex.Message),
                            "MikoMiko", MessageBoxButton.OK, MessageBoxImage.Error);
                        ShowDirectory();
                    }
                    else
                    {
                        ShowFinish();
                    }
                }));
            });
        }

        private void ShowProgress(string status)
        {
            ShowPage("Page.Progress.png");
            AddText(_pageCanvas, status, 0, 462, DesignWidth, 38, 23.5, TextBrush, FontWeights.SemiBold, TextAlignment.Center);

            var track = new Border
            {
                Width = 570,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(Color.FromRgb(0xDD, 0xEB, 0xFB))
            };
            Canvas.SetLeft(track, 164);
            Canvas.SetTop(track, 494);
            _pageCanvas.Children.Add(track);

            _progressFill = new Border
            {
                Width = 0,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = BlueBrush,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Canvas.SetLeft(_progressFill, 164);
            Canvas.SetTop(_progressFill, 494);
            _pageCanvas.Children.Add(_progressFill);

            _progressPercent = new TextBlock
            {
                Text = "0%",
                FontFamily = UiFont,
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x7A, 0x90)),
                Width = 76,
                Height = 34,
                TextAlignment = TextAlignment.Left
            };
            Canvas.SetLeft(_progressPercent, 752);
            Canvas.SetTop(_progressPercent, 489);
            _pageCanvas.Children.Add(_progressPercent);
        }

        private void SetStatus(string text)
        {
            // 进度页只有一条状态文字；由 ShowProgress 创建，换页时销毁。
            foreach (var child in _pageCanvas.Children.OfType<TextBlock>())
            {
                if (child.FontSize >= 20)
                {
                    child.Text = text;
                    break;
                }
            }
        }

        private void UpdateProgress(double value)
        {
            value = Math.Max(0, Math.Min(100, value));
            if (_progressFill != null) _progressFill.Width = 570 * value / 100.0;
            if (_progressPercent != null) _progressPercent.Text = ((int)Math.Round(value)) + "%";
        }

        private void ShowFinish()
        {
            ShowPage("Page.Finish.png");
            AddHotspot(_pageCanvas, 343, 520, 272, 65, delegate
            {
                _busy = false;
                Program.LaunchApp(Path.Combine(_target, "MikoMiko.exe"));
                Close();
            });
        }

        private void ShowUninstallConfirm()
        {
            ShowPage("Page.UninstallConfirm.png");
            AddHotspot(_pageCanvas, 160, 490, 298, 68, Close);
            AddHotspot(_pageCanvas, 498, 490, 300, 68, StartUninstall);
        }

        private void StartUninstall()
        {
            if (_busy) return;
            _busy = true;
            ShowProgress("正在卸载...");
            var progress = new Progress<double>(value => UpdateProgress(value));
            var status = new Progress<string>(text => SetStatus(text));
            Task.Run(delegate
            {
                Program.UninstallWorker(_target, value => ((IProgress<double>)progress).Report(value),
                    text => ((IProgress<string>)status).Report(text));
            }).ContinueWith(task =>
            {
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    _busy = false;
                    if (task.IsFaulted)
                    {
                        var ex = task.Exception != null ? task.Exception.GetBaseException() : null;
                        MessageBox.Show(this, "卸载失败：\r\n" + (ex == null ? "未知错误" : ex.Message),
                            "MikoMiko", MessageBoxButton.OK, MessageBoxImage.Error);
                        ShowUninstallConfirm();
                    }
                    else
                    {
                        ShowUninstallFinish();
                    }
                }));
            });
        }

        private void ShowUninstallFinish()
        {
            ShowPage("Page.UninstallFinish.png");
            AddHotspot(_pageCanvas, 343, 530, 272, 65, delegate
            {
                _busy = false;
                ScheduleUninstallCleanup();
                Close();
            });
        }

        private void ScheduleUninstallCleanup()
        {
            try
            {
                var target = _target;
                var self = Assembly.GetExecutingAssembly().Location;
                var command = "ping 127.0.0.1 -n 3 >nul & ";
                if (!string.IsNullOrEmpty(target)) command += "rd /s /q " + Program.Quote(target) + " & ";
                command += "del /f /q " + Program.Quote(self);
                Process.Start(new ProcessStartInfo("cmd.exe", "/c " + command)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch { }
        }
    }
}
