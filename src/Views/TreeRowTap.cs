using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    /// <summary>
    ///     树节点单击展开/收起的公共判断。
    /// </summary>
    public static class TreeRowTap
    {
        /// <summary>
        ///     是否为单纯的单击：没有按 Ctrl/Shift/Alt（用于多选），也不是点在展开箭头上（箭头自己处理）。
        /// </summary>
        public static bool IsPlainTap(TappedEventArgs e)
        {
            if (e.KeyModifiers != KeyModifiers.None)
                return false;

            return !(e.Source is Visual source && source.FindAncestorOfType<ToggleButton>(true) != null);
        }
    }
}
