using System;
using System.ComponentModel;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Search;
using AvaloniaEdit.TextMate;

namespace SourceGit.Views
{
    public class CodeEditorTextView : TextEditor
    {
        public static readonly StyledProperty<int> TabWidthProperty =
            AvaloniaProperty.Register<CodeEditorTextView, int>(nameof(TabWidth), 4);

        public int TabWidth
        {
            get => GetValue(TabWidthProperty);
            set => SetValue(TabWidthProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(TextEditor);

        public CodeEditorTextView() : base(new TextArea(), new TextDocument())
        {
            IsReadOnly = false;
            ShowLineNumbers = true;
            WordWrap = false;

            Options.IndentationSize = TabWidth;
            Options.EnableHyperlinks = false;
            Options.EnableEmailHyperlinks = false;

            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;

            TextArea.LeftMargins[0].Margin = new Thickness(8, 0);
            TextArea.TextView.Margin = new Thickness(4, 0);
        }

        public void SetContent(string text, string fileName)
        {
            _fileName = fileName;
            Text = text;
            Models.TextMateHelper.SetGrammarByFileName(_textMate, fileName);
            ScrollToHome();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            _searchPanel ??= SearchPanel.Install(this);
            _textMate ??= Models.TextMateHelper.CreateForEditor(this);
            Models.TextMateHelper.SetGrammarByFileName(_textMate, _fileName);
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);

            if (_textMate != null)
            {
                _textMate.Dispose();
                _textMate = null;
            }

            GC.Collect();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TabWidthProperty)
                Options.IndentationSize = TabWidth;
            else if (change.Property.Name == nameof(ActualThemeVariant) && change.NewValue != null)
                Models.TextMateHelper.SetThemeByApp(_textMate);
        }

        private TextMate.Installation _textMate = null;
        private SearchPanel _searchPanel = null;
        private string _fileName = string.Empty;
    }

    public partial class CodeEditorPage : UserControl
    {
        public CodeEditorPage()
        {
            InitializeComponent();
            Editor.TextChanged += OnEditorTextChanged;
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (_vm != null)
                _vm.PropertyChanged -= OnViewModelPropertyChanged;

            _vm = DataContext as ViewModels.CodeEditorPage;
            if (_vm != null)
                _vm.PropertyChanged += OnViewModelPropertyChanged;
        }

        // 页面通过父容器的 IsVisible 切换显示，父容器变为可见时刷新文件树并检查外部改动
        protected override async void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            _host = Parent as Control;
            if (_host == null)
                return;

            _host.PropertyChanged += OnHostPropertyChanged;
            if (_host.IsVisible && _vm != null)
                await _vm.OnShownAsync();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            if (_host != null)
            {
                _host.PropertyChanged -= OnHostPropertyChanged;
                _host = null;
            }
        }

        private async void OnHostPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == IsVisibleProperty && _host is { IsVisible: true } && _vm != null)
                await _vm.OnShownAsync();
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Key.S && e.KeyModifiers == (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
            {
                e.Handled = true;
                await SaveAsync();
            }
        }

        private async void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            await SaveAsync();
            e.Handled = true;
        }

        private async void OnRowTapped(object sender, TappedEventArgs e)
        {
            if (_vm != null && sender is Control { DataContext: ViewModels.CodeEditorNode node })
                await _vm.ActivateAsync(node);

            e.Handled = true;
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            if (_vm is { HasFile: true })
                await _vm.SaveAsync(Editor.Text);
        }

        private async void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_vm == null)
                return;

            if (e.PropertyName == nameof(ViewModels.CodeEditorPage.LoadVersion))
            {
                _loading = true;
                Editor.SetContent(_vm.LoadedText, _vm.CurrentName);
                _loading = false;

                if (_vm.IsPreviewing)
                    await RefreshPreviewAsync();
            }
            else if (e.PropertyName == nameof(ViewModels.CodeEditorPage.IsPreviewing) && _vm.IsPreviewing)
            {
                await RefreshPreviewAsync();
            }
        }

        private async System.Threading.Tasks.Task RefreshPreviewAsync()
        {
            if (_preview == null)
            {
                _preview = new MarkdownPreviewView() { ErrorReporter = _vm.ReportError };
                PreviewHost.Children.Add(_preview);
            }

            await _preview.ShowAsync(Editor.Text, _vm.CurrentDirectory);
        }

        private void OnEditorTextChanged(object sender, EventArgs e)
        {
            if (!_loading && _vm != null)
                _vm.IsDirty = true;
        }

        private ViewModels.CodeEditorPage _vm = null;
        private Control _host = null;
        private MarkdownPreviewView _preview = null;
        private bool _loading = false;
    }
}
