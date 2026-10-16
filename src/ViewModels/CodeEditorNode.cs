using System.Collections.Generic;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceGit.ViewModels
{
    public class CodeEditorNode : ObservableObject
    {
        public string FullPath { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public bool IsFolder { get; init; } = false;
        public bool IsIgnored { get; init; } = false;
        public int Depth { get; set; } = 0;
        public List<CodeEditorNode> Children { get; } = [];

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private bool _isExpanded = false;
    }
}
