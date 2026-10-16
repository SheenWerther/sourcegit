using System;
using System.Text.Json;
using System.Threading.Tasks;

using Avalonia.Controls;

namespace SourceGit.Views
{
    /// <summary>
    ///     只读的 Markdown 预览，基于 WebView 渲染。需要 Windows 的 WebView2 运行时。
    /// </summary>
    public class MarkdownPreviewView : UserControl
    {
        public MarkdownPreviewView()
        {
            _web = new NativeWebView();
            _web.AdapterCreated += OnAdapterCreated;
            _web.NavigationStarted += OnNavigationStarted;
            _web.NavigationCompleted += OnNavigationCompleted;
            _web.WebMessageReceived += OnWebMessageReceived;
            Content = _web;
        }

        public Action<string> ErrorReporter { get; set; }

        public async Task ShowAsync(string markdown, string baseDirectory)
        {
            _pending = (markdown, baseDirectory);
            if (_ready)
                await FlushAsync();
        }

        private void OnAdapterCreated(object sender, WebViewAdapterEventArgs e)
        {
            try
            {
                _web.Navigate(new Uri(Models.MarkdownPreviewAssets.EnsureExtracted()));
            }
            catch (Exception ex)
            {
                ErrorReporter?.Invoke($"Failed to open Markdown preview: {ex.Message}");
            }
        }

        private void OnNavigationStarted(object sender, WebViewNavigationStartingEventArgs e)
        {
            // 页面只在初始加载时导航，之后一律拦截
            if (_ready)
                e.Cancel = true;
        }

        private async void OnNavigationCompleted(object sender, WebViewNavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || _ready)
                return;

            _ready = true;
            await FlushAsync();
        }

        private void OnWebMessageReceived(object sender, WebMessageReceivedEventArgs e)
        {
            if (Uri.TryCreate(e.Body, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                Native.OS.OpenBrowser(uri.AbsoluteUri);
        }

        private async Task FlushAsync()
        {
            if (_pending is not { } pending)
                return;

            _pending = null;

            var baseHref = string.IsNullOrEmpty(pending.BaseDirectory)
                ? string.Empty
                : new Uri(pending.BaseDirectory.TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar).AbsoluteUri;

            var script = $"renderMarkdown(\"{JsonEncodedText.Encode(pending.Markdown)}\", \"{JsonEncodedText.Encode(baseHref)}\")";
            await _web.InvokeScript(script);
        }

        private readonly NativeWebView _web;
        private (string Markdown, string BaseDirectory)? _pending = null;
        private bool _ready = false;
    }
}
