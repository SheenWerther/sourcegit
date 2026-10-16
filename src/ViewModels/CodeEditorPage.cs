using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Collections;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class CodeEditorPage : ObservableObject
    {
        public AvaloniaList<CodeEditorNode> Rows { get; } = [];

        public string CurrentPath
        {
            get => _currentPath;
            private set
            {
                if (SetProperty(ref _currentPath, value))
                {
                    OnPropertyChanged(nameof(HasFile));
                    OnPropertyChanged(nameof(CanPreview));
                    OnPropertyChanged(nameof(ShowEditor));
                    OnPropertyChanged(nameof(CurrentDirectory));
                }
            }
        }

        public string CurrentName
        {
            get => _currentName;
            private set => SetProperty(ref _currentName, value);
        }

        public bool HasFile => _currentPath != null;

        public string CurrentDirectory => _currentPath == null ? string.Empty : Path.GetDirectoryName(_currentPath);

        public bool CanPreview =>
            OperatingSystem.IsWindows() &&
            _currentPath != null &&
            (_currentPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
             _currentPath.EndsWith(".markdown", StringComparison.OrdinalIgnoreCase));

        public bool IsPreviewing
        {
            get => _isPreviewing;
            set
            {
                if (SetProperty(ref _isPreviewing, value))
                    OnPropertyChanged(nameof(ShowEditor));
            }
        }

        public bool ShowEditor => HasFile && !_isPreviewing;

        public string Message
        {
            get => _message;
            private set
            {
                if (SetProperty(ref _message, value))
                    OnPropertyChanged(nameof(HasMessage));
            }
        }

        public bool HasMessage => !string.IsNullOrEmpty(_message);

        public bool IsDirty
        {
            get => _isDirty;
            set => SetProperty(ref _isDirty, value);
        }

        public string LoadedText { get; private set; } = string.Empty;

        /// <summary>
        ///     每次载入文件（含重新加载）自增，视图据此刷新编辑器内容。
        /// </summary>
        public int LoadVersion
        {
            get => _loadVersion;
            private set => SetProperty(ref _loadVersion, value);
        }

        public CodeEditorPage(Repository repo)
        {
            _repo = repo;
        }

        public async Task OnShownAsync()
        {
            await RefreshTreeAsync();
            await CheckExternalChangeAsync();
        }

        public void Close()
        {
            _debounce?.Dispose();
            _watcher?.Dispose();
        }

        public void ReportError(string message)
        {
            _repo.SendNotification(message, true);
        }

        public async Task ActivateAsync(CodeEditorNode node)
        {
            if (node.IsFolder)
                await ToggleFolderAsync(node);
            else
                await OpenFileAsync(node);
        }

        public async Task SaveAsync(string text)
        {
            if (_currentPath == null)
                return;

            if (File.GetLastWriteTimeUtc(_currentPath) != _loadedWriteTime &&
                !await App.AskConfirmAsync(App.Text("FileEditor.OverwriteConfirm")))
                return;

            try
            {
                var path = _currentPath;
                var encoding = _encoding;
                await Task.Run(() => File.WriteAllText(path, text, encoding));

                _loadedWriteTime = File.GetLastWriteTimeUtc(path);
                IsDirty = false;
            }
            catch (Exception e)
            {
                _repo.SendNotification($"Failed to save file: {e.Message}", true);
            }
        }

        /// <summary>
        ///     文件被外部修改后：没有未保存改动就直接重新加载，有未保存改动则询问。
        /// </summary>
        private async Task CheckExternalChangeAsync()
        {
            if (_currentPath == null || _isCheckingExternalChange || !File.Exists(_currentPath))
                return;

            if (File.GetLastWriteTimeUtc(_currentPath) == _loadedWriteTime)
                return;

            _isCheckingExternalChange = true;
            try
            {
                if (!_isDirty || await App.AskConfirmAsync(App.Text("FileEditor.ReloadConfirm")))
                    await LoadFileAsync(_currentPath, _currentName);
            }
            finally
            {
                _isCheckingExternalChange = false;
            }
        }

        private void WatchFile(string path)
        {
            _watcher?.Dispose();
            _watcher = null;

            if (path == null)
                return;

            try
            {
                var watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                };
                watcher.Changed += OnWatchedFileChanged;
                watcher.Created += OnWatchedFileChanged;
                watcher.Renamed += OnWatchedFileChanged;
                watcher.EnableRaisingEvents = true;
                _watcher = watcher;
            }
            catch (Exception)
            {
                // 监听失败时，仍会在切回本页时检查外部改动
            }
        }

        private void OnWatchedFileChanged(object sender, FileSystemEventArgs e)
        {
            // 一次保存常触发多个事件，合并后再处理
            _debounce?.Dispose();
            _debounce = new Timer(
                _ => Dispatcher.UIThread.Post(async () => await CheckExternalChangeAsync()),
                null, 300, Timeout.Infinite);
        }

        private async Task OpenFileAsync(CodeEditorNode node)
        {
            if (node.FullPath == _currentPath)
                return;

            if (_isDirty && !await App.AskConfirmAsync(App.Text("FileEditor.DiscardConfirm")))
                return;

            await LoadFileAsync(node.FullPath, node.Name);
        }

        private async Task LoadFileAsync(string path, string name)
        {
            var result = await Task.Run(() => ReadFile(path));

            CurrentName = name;
            CurrentPath = result.ErrorKey == null ? path : null;
            Message = result.ErrorKey == null ? string.Empty : App.Text(result.ErrorKey);
            LoadedText = result.Text;
            _encoding = result.Encoding;
            _loadedWriteTime = result.WriteTime;
            IsDirty = false;
            if (!CanPreview)
                IsPreviewing = false;

            WatchFile(CurrentPath);
            LoadVersion++;
        }

        private async Task ToggleFolderAsync(CodeEditorNode node)
        {
            var idx = Rows.IndexOf(node);
            if (idx < 0)
                return;

            node.IsExpanded = !node.IsExpanded;

            if (node.IsExpanded)
            {
                _expanded.Add(node.FullPath);

                if (node.Children.Count == 0)
                {
                    var ignored = await QueryIgnoredAsync();
                    node.Children.AddRange(await Task.Run(() => LoadChildren(node.FullPath, ignored, node.IsIgnored)));
                }

                var rows = new List<CodeEditorNode>();
                MakeRows(rows, node.Children, node.Depth + 1);
                Rows.InsertRange(idx + 1, rows);
            }
            else
            {
                _expanded.Remove(node.FullPath);

                var removeCount = 0;
                for (var i = idx + 1; i < Rows.Count && Rows[i].Depth > node.Depth; i++)
                    removeCount++;

                Rows.RemoveRange(idx + 1, removeCount);
            }
        }

        private async Task RefreshTreeAsync()
        {
            var ignored = await QueryIgnoredAsync();
            var roots = await Task.Run(() => LoadTree(_repo.FullPath, ignored, false));

            var rows = new List<CodeEditorNode>();
            MakeRows(rows, roots, 0);

            Rows.Clear();
            Rows.AddRange(rows);
        }

        private async Task<HashSet<string>> QueryIgnoredAsync()
        {
            return await new Commands.QueryIgnoredPaths(_repo.FullPath).GetResultAsync();
        }

        private List<CodeEditorNode> LoadTree(string dir, HashSet<string> ignored, bool parentIgnored)
        {
            var nodes = LoadChildren(dir, ignored, parentIgnored);
            foreach (var node in nodes)
            {
                if (node.IsFolder && _expanded.Contains(node.FullPath))
                {
                    node.IsExpanded = true;
                    node.Children.AddRange(LoadTree(node.FullPath, ignored, node.IsIgnored));
                }
            }

            return nodes;
        }

        private List<CodeEditorNode> LoadChildren(string dir, HashSet<string> ignored, bool parentIgnored)
        {
            var nodes = new List<CodeEditorNode>();

            try
            {
                foreach (var entry in new DirectoryInfo(dir).EnumerateFileSystemInfos())
                {
                    if (entry.Name == ".git")
                        continue;

                    var isFolder = entry is DirectoryInfo;
                    var relative = Path.GetRelativePath(_repo.FullPath, entry.FullName).Replace('\\', '/');
                    var isIgnored = parentIgnored || ignored.Contains(isFolder ? relative + "/" : relative);

                    nodes.Add(new CodeEditorNode
                    {
                        FullPath = entry.FullName,
                        Name = entry.Name,
                        IsFolder = isFolder,
                        IsIgnored = isIgnored,
                    });
                }
            }
            catch (Exception)
            {
                // 无权限等原因读不到的目录，当作空目录
            }

            nodes.Sort((l, r) =>
            {
                if (l.IsFolder == r.IsFolder)
                    return Models.NumericSort.Compare(l.Name, r.Name);
                return l.IsFolder ? -1 : 1;
            });

            return nodes;
        }

        private static void MakeRows(List<CodeEditorNode> rows, List<CodeEditorNode> nodes, int depth)
        {
            foreach (var node in nodes)
            {
                node.Depth = depth;
                rows.Add(node);

                if (node.IsFolder && node.IsExpanded)
                    MakeRows(rows, node.Children, depth + 1);
            }
        }

        private static FileLoadResult ReadFile(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Length > MaxFileSize)
                    return FileLoadResult.Fail("FileEditor.TooLarge");

                var bytes = File.ReadAllBytes(path);
                var writeTime = info.LastWriteTimeUtc;

                Encoding encoding;
                var skip = 0;
                if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                {
                    encoding = new UTF8Encoding(true, true);
                    skip = 3;
                }
                else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
                {
                    encoding = new UnicodeEncoding(false, true, true);
                    skip = 2;
                }
                else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                {
                    encoding = new UnicodeEncoding(true, true, true);
                    skip = 2;
                }
                else
                {
                    if (Array.IndexOf(bytes, (byte)0) >= 0)
                        return FileLoadResult.Fail("FileEditor.Binary");

                    encoding = new UTF8Encoding(false, true);
                }

                try
                {
                    var text = encoding.GetString(bytes, skip, bytes.Length - skip);
                    return new FileLoadResult(text, encoding, writeTime, null);
                }
                catch (DecoderFallbackException)
                {
                    return FileLoadResult.Fail("FileEditor.UnsupportedEncoding");
                }
            }
            catch (Exception)
            {
                return FileLoadResult.Fail("FileEditor.ReadFailed");
            }
        }

        private record FileLoadResult(string Text, Encoding Encoding, DateTime WriteTime, string ErrorKey)
        {
            public static FileLoadResult Fail(string errorKey) => new(string.Empty, new UTF8Encoding(false), default, errorKey);
        }

        private const long MaxFileSize = 1024 * 1024;

        private readonly Repository _repo;
        private readonly HashSet<string> _expanded = [];
        private string _currentPath = null;
        private string _currentName = string.Empty;
        private string _message = string.Empty;
        private bool _isDirty = false;
        private int _loadVersion = 0;
        private bool _isPreviewing = false;
        private bool _isCheckingExternalChange = false;
        private FileSystemWatcher _watcher = null;
        private Timer _debounce = null;
        private Encoding _encoding = new UTF8Encoding(false);
        private DateTime _loadedWriteTime = default;
    }
}
