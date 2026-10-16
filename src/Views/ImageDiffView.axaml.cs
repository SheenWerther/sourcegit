using Avalonia.Controls;
using Avalonia.Threading;

namespace SourceGit.Views
{
    public partial class ImageDiffView : UserControl
    {
        public ImageDiffView()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => Dispatcher.UIThread.Post(ResetHiddenSelection);
        }

        private void ResetHiddenSelection()
        {
            if (Tabs.SelectedItem is TabItem { IsVisible: false })
                Tabs.SelectedIndex = 0;
        }
    }
}
