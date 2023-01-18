using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MCLauncher {
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.IO.Compression;
    using System.Threading;
    using System.Windows.Data;
    using Windows.ApplicationModel;
    using Windows.Foundation;
    using Windows.Management.Core;
    using Windows.Management.Deployment;
    using Windows.System;
    using WPFDataTypes;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, ICommonVersionCommands {

        private static readonly string PREFS_PATH = @"preferences.json";
        private static readonly string IMPORTED_VERSIONS_PATH = @"imported_versions";
        private static readonly string VERSIONS_API = "https://raw.githubusercontents.com/MCMrARM/mc-w10-versiondb/master/versions.json.min";

        private VersionList _versions;
        public Preferences UserPrefs { get; }

        private HashSet<CollectionViewSource> _versionListViews = new HashSet<CollectionViewSource>();

        private readonly VersionDownloader _anonVersionDownloader = new VersionDownloader();
        private readonly VersionDownloader _userVersionDownloader = new VersionDownloader();
        private readonly Task _userVersionDownloaderLoginTask;
        private volatile int _userVersionDownloaderLoginTaskStarted;
        private volatile bool _hasLaunchTask = false;

        public MainWindow() {
            _versions = new VersionList("versions.json", IMPORTED_VERSIONS_PATH, VERSIONS_API, this, VersionEntryPropertyChanged);
            InitializeComponent();
            ShowInstalledVersionsOnlyCheckbox.DataContext = this;


            if (File.Exists(PREFS_PATH)) {
                UserPrefs = JsonConvert.DeserializeObject<Preferences>(File.ReadAllText(PREFS_PATH));
            } else {
                UserPrefs = new Preferences();
                RewritePrefs();
            }

            var versionListViewRelease = Resources["versionListViewRelease"] as CollectionViewSource;
            versionListViewRelease.Filter += new FilterEventHandler((object sender, FilterEventArgs e) => {
                var v = e.Item as Version;
                e.Accepted = v.VersionType == VersionType.Release && (v.IsInstalled || v.IsStateChanging || !(ShowInstalledVersionsOnlyCheckbox.IsChecked ?? false));
            });
            versionListViewRelease.Source = _versions;
            ReleaseVersionList.DataContext = versionListViewRelease;
            _versionListViews.Add(versionListViewRelease);

            var versionListViewBeta = Resources["versionListViewBeta"] as CollectionViewSource;
            versionListViewBeta.Filter += new FilterEventHandler((object sender, FilterEventArgs e) => {
                var v = e.Item as Version;
                e.Accepted = v.VersionType == VersionType.Beta && (v.IsInstalled || v.IsStateChanging || !(ShowInstalledVersionsOnlyCheckbox.IsChecked ?? false));
            });
            versionListViewBeta.Source = _versions;
            BetaVersionList.DataContext = versionListViewBeta;
            _versionListViews.Add(versionListViewBeta);

            var versionListViewPreview = Resources["versionListViewPreview"] as CollectionViewSource;
            versionListViewPreview.Filter += new FilterEventHandler((object sender, FilterEventArgs e) => {
                var v = e.Item as Version;
                e.Accepted = v.VersionType == VersionType.Preview && (v.IsInstalled || v.IsStateChanging || !(ShowInstalledVersionsOnlyCheckbox.IsChecked ?? false));
            });
            versionListViewPreview.Source = _versions;
            PreviewVersionList.DataContext = versionListViewPreview;
            _versionListViews.Add(versionListViewPreview);

            var versionListViewImported = Resources["versionListViewImported"] as CollectionViewSource;
            versionListViewImported.Filter += new FilterEventHandler((object sender, FilterEventArgs e) => {
                var v = e.Item as Version;
                e.Accepted = v.VersionType == VersionType.Imported;
            });

            versionListViewImported.Source = _versions;
            ImportedVersionList.DataContext = versionListViewImported;
            _versionListViews.Add(versionListViewImported);

            _userVersionDownloaderLoginTask = new Task(() => {
                _userVersionDownloader.EnableUserAuthorization();
            });
            Dispatcher.Invoke(LoadVersionList);
        }

        private async void LoadVersionList() {
            LoadingProgressLabel.Content = "Loading versions from cache";
            LoadingProgressBar.Value = 1;

            LoadingProgressGrid.Visibility = Visibility.Visible;

            try {
                await _versions.LoadFromCache();
            } catch (Exception e) {
                Debug.WriteLine("List cache load failed:\n" + e.ToString());
            }

            LoadingProgressLabel.Content = "更新版本列表从 " + VERSIONS_API;
            LoadingProgressBar.Value = 2;
            try {
                await _versions.DownloadList();
            } catch (Exception e) {
                Debug.WriteLine("List download failed:\n" + e.ToString());
                MessageBox.Show("无法从互联网更新版本列表。某些新版本可能丢失。", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            LoadingProgressLabel.Content = "Loading imported versions";
            LoadingProgressBar.Value = 3;
            await _versions.LoadImported();

            LoadingProgressGrid.Visibility = Visibility.Collapsed;
        }

        private void VersionEntryPropertyChanged(object sender, PropertyChangedEventArgs e) {
            RefreshLists();
        }

        private async void ImportButtonClicked(object sender, RoutedEventArgs e) {
            Microsoft.Win32.OpenFileDialog openFileDlg = new Microsoft.Win32.OpenFileDialog();
            openFileDlg.Filter = "UWP App Package (*.appx)|*.appx|All Files|*.*";
            Nullable<bool> result = openFileDlg.ShowDialog();
            if (result == true) {
                string directory = Path.Combine(IMPORTED_VERSIONS_PATH, openFileDlg.SafeFileName);
                if (Directory.Exists(directory)) {
                    var found = false;
                    foreach (var version in _versions) {
                        if (version.IsImported && version.GameDirectory == directory) {
                            if (version.IsStateChanging) {
                                MessageBox.Show("具有相同名称的版本已导入，并且当前正在修改。请稍等片刻，然后重试。", "Error");
                                return;
                            }
                            MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show("已导入具有相同名称的版本。是否要删除它？", "确认删除", System.Windows.MessageBoxButton.YesNo);
                            if (messageBoxResult == MessageBoxResult.Yes) {
                                await Remove(version);
                                found = true;
                                break;
                            } else {
                                return;
                            }
                        }
                    }
                    if (!found) {
                        MessageBox.Show("导入的目标路径已存在，并且不包含启动器已知的 Minecraft 安装。为避免数据丢失，导入已中止。请手动删除文件。", "Error");
                        return;
                    }
                }

                var versionEntry = _versions.AddEntry(openFileDlg.SafeFileName, directory);
                versionEntry.StateChangeInfo = new VersionStateChangeInfo(VersionState.Extracting);
                await Task.Run(() => {
                    try {
                        ZipFile.ExtractToDirectory(openFileDlg.FileName, directory);
                    } catch (InvalidDataException ex) {
                        Debug.WriteLine("Failed extracting appx " + openFileDlg.FileName + ": " + ex.ToString());
                        MessageBox.Show("未能导入应用 " + openFileDlg.SafeFileName + ". 它可能已损坏或不是 appx 文件。\n\n提取错误： " + ex.Message, "导入失败");
                        return;
                    } finally {
                        versionEntry.StateChangeInfo = null;
                    }
                });
            }
        }

        public ICommand LaunchCommand => new RelayCommand((v) => InvokeLaunch((Version)v));

        public ICommand RemoveCommand => new RelayCommand((v) => InvokeRemove((Version)v));

        public ICommand DownloadCommand => new RelayCommand((v) => InvokeDownload((Version)v));

        private void InvokeLaunch(Version v) {
            if (_hasLaunchTask)
                return;
            _hasLaunchTask = true;
            Task.Run(async () => {
                v.StateChangeInfo = new VersionStateChangeInfo(VersionState.Registering);
                string gameDir = Path.GetFullPath(v.GameDirectory);
                try {
                    await ReRegisterPackage(v.GamePackageFamily, gameDir);
                } catch (Exception e) {
                    Debug.WriteLine("App re-register failed:\n" + e.ToString());
                    MessageBox.Show("应用重新注册失败:\n" + e.ToString());
                    _hasLaunchTask = false;
                    v.StateChangeInfo = null;
                    return;
                }
                v.StateChangeInfo = new VersionStateChangeInfo(VersionState.Launching);
                try {
                    var pkg = await AppDiagnosticInfo.RequestInfoForPackageAsync(v.GamePackageFamily);
                    if (pkg.Count > 0)
                        await pkg[0].LaunchAsync();
                    Debug.WriteLine("App launch finished!");
                    _hasLaunchTask = false;
                    v.StateChangeInfo = null;
                } catch (Exception e) {
                    Debug.WriteLine("App launch failed:\n" + e.ToString());
                    MessageBox.Show("应用启动失败:\n" + e.ToString());
                    _hasLaunchTask = false;
                    v.StateChangeInfo = null;
                    return;
                }
            });
        }

        private async Task DeploymentProgressWrapper(IAsyncOperationWithProgress<DeploymentResult, DeploymentProgress> t) {
            TaskCompletionSource<int> src = new TaskCompletionSource<int>();
            t.Progress += (v, p) => {
                Debug.WriteLine("Deployment progress: " + p.state + " " + p.percentage + "%");
            };
            t.Completed += (v, p) => {
                if (p == AsyncStatus.Error) {
                    Debug.WriteLine("Deployment failed: " + v.GetResults().ErrorText);
                    src.SetException(new Exception("Deployment failed: " + v.GetResults().ErrorText));
                } else {
                    Debug.WriteLine("Deployment done: " + p);
                    src.SetResult(1);
                }
            };
            await src.Task;
        }

        private string GetBackupMinecraftDataDir() {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string tmpDir = Path.Combine(localAppData, "TmpMinecraftLocalState");
            return tmpDir;
        }

        private void BackupMinecraftDataForRemoval(string packageFamily) {
            var data = ApplicationDataManager.CreateForPackageFamily(packageFamily);
            string tmpDir = GetBackupMinecraftDataDir();
            if (Directory.Exists(tmpDir)) {
                Debug.WriteLine("BackupMinecraftDataForRemoval error: " + tmpDir + " already exists");
                Process.Start("explorer.exe", tmpDir);
                MessageBox.Show("用于备份 MC 数据的临时目录已存在。这可能意味着我们上次备份数据失败。请手动备份目录。");
                throw new Exception("存在临时目录");
            }
            Debug.WriteLine("Moving Minecraft data to: " + tmpDir);
            Directory.Move(data.LocalFolder.Path, tmpDir);
        }

        private void RestoreMove(string from, string to) {
            foreach (var f in Directory.EnumerateFiles(from)) {
                string ft = Path.Combine(to, Path.GetFileName(f));
                if (File.Exists(ft)) {
                    if (MessageBox.Show("文件 " + ft + " 目标中已存在\n是否要替换它？否则，旧文件将丢失。", "Restoring data directory from previous installation", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        continue;
                    File.Delete(ft);
                }
                File.Move(f, ft);
            }
            foreach (var f in Directory.EnumerateDirectories(from)) {
                string tp = Path.Combine(to, Path.GetFileName(f));
                if (!Directory.Exists(tp)) {
                    if (File.Exists(tp) && MessageBox.Show("文件 " + tp + " 不是目录。是否要删除它？否则，旧目录中的数据将丢失。", "从以前的安装恢复数据目录", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        continue;
                    Directory.CreateDirectory(tp);
                }
                RestoreMove(f, tp);
            }
        }

        private void RestoreMinecraftDataFromReinstall(string packageFamily) {
            string tmpDir = GetBackupMinecraftDataDir();
            if (!Directory.Exists(tmpDir))
                return;
            var data = ApplicationDataManager.CreateForPackageFamily(packageFamily);
            Debug.WriteLine("Moving backup Minecraft data to: " + data.LocalFolder.Path);
            RestoreMove(tmpDir, data.LocalFolder.Path);
            Directory.Delete(tmpDir, true);
        }

        private async Task RemovePackage(Package pkg, string packageFamily) {
            Debug.WriteLine("Removing package: " + pkg.Id.FullName);
            if (!pkg.IsDevelopmentMode) {
                BackupMinecraftDataForRemoval(packageFamily);
                await DeploymentProgressWrapper(new PackageManager().RemovePackageAsync(pkg.Id.FullName, 0));
            } else {
                Debug.WriteLine("Package is in development mode");
                await DeploymentProgressWrapper(new PackageManager().RemovePackageAsync(pkg.Id.FullName, RemovalOptions.PreserveApplicationData));
            }
            Debug.WriteLine("Removal of package done: " + pkg.Id.FullName);
        }

        private string GetPackagePath(Package pkg) {
            try {
                return pkg.InstalledLocation.Path;
            } catch (FileNotFoundException) {
                return "";
            }
        }

        private async Task UnregisterPackage(string packageFamily, string gameDir) {
            foreach (var pkg in new PackageManager().FindPackages(packageFamily)) {
                string location = GetPackagePath(pkg);
                if (location == "" || location == gameDir) {
                    await RemovePackage(pkg, packageFamily);
                }
            }
        }

        private async Task ReRegisterPackage(string packageFamily, string gameDir) {
            foreach (var pkg in new PackageManager().FindPackages(packageFamily)) {
                string location = GetPackagePath(pkg);
                if (location == gameDir) {
                    Debug.WriteLine("Skipping package removal - same path: " + pkg.Id.FullName + " " + location);
                    return;
                }
                await RemovePackage(pkg, packageFamily);
            }
            Debug.WriteLine("Registering package");
            string manifestPath = Path.Combine(gameDir, "AppxManifest.xml");
            await DeploymentProgressWrapper(new PackageManager().RegisterPackageAsync(new Uri(manifestPath), null, DeploymentOptions.DevelopmentMode));
            Debug.WriteLine("App re-register done!");
            RestoreMinecraftDataFromReinstall(packageFamily);
        }

        private void InvokeDownload(Version v) {
            CancellationTokenSource cancelSource = new CancellationTokenSource();
            v.IsNew = false;
            v.StateChangeInfo = new VersionStateChangeInfo(VersionState.Initializing);
            v.StateChangeInfo.CancelCommand = new RelayCommand((o) => cancelSource.Cancel());

            Debug.WriteLine("Download start");
            Task.Run(async () => {
                string dlPath = (v.VersionType == VersionType.Preview ? "Minecraft-Preview-" : "Minecraft-") + v.Name + ".Appx";
                VersionDownloader downloader = _anonVersionDownloader;
                if (v.VersionType == VersionType.Beta) {
                    downloader = _userVersionDownloader;
                    if (Interlocked.CompareExchange(ref _userVersionDownloaderLoginTaskStarted, 1, 0) == 0) {
                        _userVersionDownloaderLoginTask.Start();
                    }
                    Debug.WriteLine("Waiting for authentication");
                    try {
                        await _userVersionDownloaderLoginTask;
                        Debug.WriteLine("Authentication complete");
                    } catch (WUTokenHelper.WUTokenException e) {
                        Debug.WriteLine("Authentication failed:\n" + e.ToString());
                        MessageBox.Show("无法进行身份验证，因为: " + e.Message, "身份验证失败");
                        v.StateChangeInfo = null;
                        return;
                    } catch (Exception e) {
                        Debug.WriteLine("Authentication failed:\n" + e.ToString());
                        MessageBox.Show(e.ToString(), "身份验证失败");
                        v.StateChangeInfo = null;
                        return;
                    }
                }
                try {
                    await downloader.Download(v.UUID, "1", dlPath, (current, total) => {
                        if (v.StateChangeInfo.VersionState != VersionState.Downloading) {
                            Debug.WriteLine("Actual download started");
                            v.StateChangeInfo.VersionState = VersionState.Downloading;
                            if (total.HasValue)
                                v.StateChangeInfo.TotalSize = total.Value;
                        }
                        v.StateChangeInfo.DownloadedBytes = current;
                    }, cancelSource.Token);
                    Debug.WriteLine("Download complete");
                } catch (BadUpdateIdentityException) {
                    Debug.WriteLine("由于无法获取下载 URL，下载失败");
                    MessageBox.Show(
                        "无法获取版本的下载 URL." +
                        (v.VersionType == VersionType.Beta ? "\n对于测试版，请确保您的帐户已订阅 Xbox 预览体验中心应用程序中的 Minecraft 测试版计划。" : "")
                    );
                    v.StateChangeInfo = null;
                    return;
                } catch (Exception e) {
                    Debug.WriteLine("Download failed:\n" + e.ToString());
                    if (!(e is TaskCanceledException))
                        MessageBox.Show("下载失败:\n" + e.ToString());
                    v.StateChangeInfo = null;
                    return;
                }
                try {
                    v.StateChangeInfo.VersionState = VersionState.Extracting;
                    string dirPath = v.GameDirectory;
                    if (Directory.Exists(dirPath))
                        Directory.Delete(dirPath, true);
                    ZipFile.ExtractToDirectory(dlPath, dirPath);
                    v.StateChangeInfo = null;
                    File.Delete(Path.Combine(dirPath, "AppxSignature.p7x"));
                    if (UserPrefs.DeleteAppxAfterDownload) {
                        Debug.WriteLine("Deleting APPX to reduce disk usage");
                        File.Delete(dlPath);
                    } else {
                        Debug.WriteLine("Not deleting APPX due to user preferences");
                    }
                } catch (Exception e) {
                    Debug.WriteLine("Extraction failed:\n" + e.ToString());
                    MessageBox.Show("提取失败:\n" + e.ToString());
                    v.StateChangeInfo = null;
                    return;
                }
                v.StateChangeInfo = null;
                v.UpdateInstallStatus();
            });
        }

        private async Task Remove(Version v) {
            v.StateChangeInfo = new VersionStateChangeInfo(VersionState.Uninstalling);
            await UnregisterPackage(v.GamePackageFamily, Path.GetFullPath(v.GameDirectory));
            Directory.Delete(v.GameDirectory, true);
            v.StateChangeInfo = null;
            if (v.IsImported) {
                Dispatcher.Invoke(() => _versions.Remove(v));
                Debug.WriteLine("已删除导入的版本 " + v.DisplayName);
            } else {
                v.UpdateInstallStatus();
                Debug.WriteLine("已删除正式版 " + v.DisplayName);
            }
        }

        private void InvokeRemove(Version v) {
            Task.Run(async () => await Remove(v));
        }

        private void ShowInstalledVersionsOnlyCheckbox_Changed(object sender, RoutedEventArgs e) {
            UserPrefs.ShowInstalledOnly = ShowInstalledVersionsOnlyCheckbox.IsChecked ?? false;
            RefreshLists();
            RewritePrefs();
        }

        private void RefreshLists() {
            Dispatcher.Invoke(() => {
                foreach (var list in _versionListViews) {
                    list.View.Refresh();
                }
            });
        }

        private void DeleteAppxAfterDownloadCheck_Changed(object sender, RoutedEventArgs e) {
            UserPrefs.DeleteAppxAfterDownload = DeleteAppxAfterDownloadOption.IsChecked;
        }

        private void RewritePrefs() {
            File.WriteAllText(PREFS_PATH, JsonConvert.SerializeObject(UserPrefs));
        }

        private void MenuItemOpenLogFileClicked(object sender, RoutedEventArgs e) {
            if (!File.Exists(@"Log.txt")) {
                MessageBox.Show("未找到日志文件", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            } else 
                Process.Start(@"Log.txt");
        }

        private void MenuItemOpenDataDirClicked(object sender, RoutedEventArgs e) {
            Process.Start(@"explorer.exe", Directory.GetCurrentDirectory());
        }

        private void MenuItemCleanupForMicrosoftStoreReinstallClicked(object sender, RoutedEventArgs e) {
            var result = MessageBox.Show(
                "启动器安装的我的世界版本将被卸载。\n" +
                    "这将允许您从微软商店重新安装我的世界。您的数据（世界等）不会被删除。\n\n" +
                    "是否确实要继续？",
                "卸载所有版本",
                MessageBoxButton.OKCancel
            );
            if (result == MessageBoxResult.OK) {
                Debug.WriteLine("开始卸载所有版本！");
                foreach (var version in _versions) {
                    if (version.IsInstalled) {
                        InvokeRemove(version);
                    }
                }
                Debug.WriteLine("计划卸载所有版本。");
            }
        }

        private void MenuItemRefreshVersionListClicked(object sender, RoutedEventArgs e) {
            Dispatcher.Invoke(LoadVersionList);
        }
    }

    struct MinecraftPackageFamilies
    {
        public static readonly string MINECRAFT = "Microsoft.MinecraftUWP_8wekyb3d8bbwe";
        public static readonly string MINECRAFT_PREVIEW = "Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe";
    }

    namespace WPFDataTypes {


        public class NotifyPropertyChangedBase : INotifyPropertyChanged {

            public event PropertyChangedEventHandler PropertyChanged;

            protected void OnPropertyChanged(string name) {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(name));
            }

        }

        public interface ICommonVersionCommands {

            ICommand LaunchCommand { get; }

            ICommand DownloadCommand { get; }

            ICommand RemoveCommand { get; }

        }

        public enum VersionType : int
        {
            Release = 0,
            Beta = 1,
            Preview = 2,
            Imported = 100
        }

        public class Version : NotifyPropertyChangedBase {
            public static readonly string UNKNOWN_UUID = "UNKNOWN";

            public Version(string uuid, string name, VersionType versionType, bool isNew, ICommonVersionCommands commands) {
                this.UUID = uuid;
                this.Name = name;
                this.VersionType = versionType;
                this.IsNew = isNew;
                this.DownloadCommand = commands.DownloadCommand;
                this.LaunchCommand = commands.LaunchCommand;
                this.RemoveCommand = commands.RemoveCommand;
                this.GameDirectory = (versionType == VersionType.Preview ? "Minecraft-Preview-" : "Minecraft-") + Name;
            }
            public Version(string name, string directory, ICommonVersionCommands commands) {
                this.UUID = UNKNOWN_UUID;
                this.Name = name;
                this.VersionType = VersionType.Imported;
                this.DownloadCommand = commands.DownloadCommand;
                this.LaunchCommand = commands.LaunchCommand;
                this.RemoveCommand = commands.RemoveCommand;
                this.GameDirectory = directory;
            }

            public string UUID { get; set; }
            public string Name { get; set; }
            public VersionType VersionType { get; set; }
            public bool IsNew {
                get { return _isNew; }
                set {
                    _isNew = value;
                    OnPropertyChanged("是新的");
                }
            }
            public bool IsImported {
                get => VersionType == VersionType.Imported;
            }

            public string GameDirectory { get; set; }

            public string GamePackageFamily
            {
                get => VersionType == VersionType.Preview ? MinecraftPackageFamilies.MINECRAFT_PREVIEW : MinecraftPackageFamilies.MINECRAFT;
            }

            public bool IsInstalled => Directory.Exists(GameDirectory);

            public string DisplayName {
                get {
                    string typeTag = "";
                    if (VersionType == VersionType.Beta)
                        typeTag = "(测试)";
                    else if (VersionType == VersionType.Preview)
                        typeTag = "(预览)";
                    return Name + (typeTag.Length > 0 ? " " + typeTag : "") + (IsNew ? " (最新的!)" : "");
                }
            }
            public string DisplayInstallStatus {
                get {
                    return IsInstalled ? "已安装" : "未安装";
                }
            }

            public ICommand LaunchCommand { get; set; }
            public ICommand DownloadCommand { get; set; }
            public ICommand RemoveCommand { get; set; }

            private VersionStateChangeInfo _stateChangeInfo;
            private bool _isNew = false;
            public VersionStateChangeInfo StateChangeInfo {
                get { return _stateChangeInfo; }
                set { _stateChangeInfo = value; OnPropertyChanged("StateChangeInfo"); OnPropertyChanged("IsStateChanging"); }
            }

            public bool IsStateChanging => StateChangeInfo != null;

            public void UpdateInstallStatus() {
                OnPropertyChanged("已安装");
            }

        }

        public enum VersionState {
            Initializing,
            Downloading,
            Extracting,
            Registering,
            Launching,
            Uninstalling
        };

        public class VersionStateChangeInfo : NotifyPropertyChangedBase {

            private VersionState _versionState;

            private long _downloadedBytes;
            private long _totalSize;

            public VersionStateChangeInfo(VersionState versionState) {
                _versionState = versionState;
            }

            public VersionState VersionState {
                get { return _versionState; }
                set {
                    _versionState = value;
                    OnPropertyChanged("IsProgressIndeterminate");
                    OnPropertyChanged("DisplayStatus");
                }
            }

            public bool IsProgressIndeterminate {
                get {
                    switch (_versionState) {
                        case VersionState.Initializing:
                        case VersionState.Extracting:
                        case VersionState.Uninstalling:
                        case VersionState.Registering:
                        case VersionState.Launching:
                            return true;
                        default: return false;
                    }
                }
            }

            public long DownloadedBytes {
                get { return _downloadedBytes; }
                set { _downloadedBytes = value; OnPropertyChanged("DownloadedBytes"); OnPropertyChanged("DisplayStatus"); }
            }

            public long TotalSize {
                get { return _totalSize; }
                set { _totalSize = value; OnPropertyChanged("TotalSize"); OnPropertyChanged("DisplayStatus"); }
            }

            public string DisplayStatus {
                get {
                    switch (_versionState) {
                        case VersionState.Initializing: return "准备中...";
                        case VersionState.Downloading:
                            return "下载中... " + (DownloadedBytes / 1024 / 1024) + "MiB/" + (TotalSize / 1024 / 1024) + "MiB";
                        case VersionState.Extracting: return "提取中...";
                        case VersionState.Registering: return "注册包...";
                        case VersionState.Launching: return "启动中...";
                        case VersionState.Uninstalling: return "卸载中...";
                        default: return "发生了什么? ...";
                    }
                }
            }

            public ICommand CancelCommand { get; set; }

        }

    }
}
