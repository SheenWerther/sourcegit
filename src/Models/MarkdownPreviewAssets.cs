using System;
using System.IO;

using Avalonia.Platform;

namespace SourceGit.Models
{
    /// <summary>
    ///     Markdown 预览用的网页和前端库：打包在程序资源里，首次使用时释放到缓存目录，再用 file:// 打开。
    /// </summary>
    public static class MarkdownPreviewAssets
    {
        public static string EnsureExtracted()
        {
            var root = Path.Combine(Native.OS.BasicDirectories.CacheDir, "markdown-preview");
            var entry = Path.Combine(root, "preview.html");

            // 程序重新编译后资源可能变了，用模块版本号判断是否需要重新释放
            var stamp = Path.Combine(root, ".stamp");
            var version = typeof(MarkdownPreviewAssets).Module.ModuleVersionId.ToString();
            if (File.Exists(entry) && File.Exists(stamp) && File.ReadAllText(stamp) == version)
                return entry;

            var prefix = ResourceRoot.AbsolutePath;
            foreach (var uri in AssetLoader.GetAssets(ResourceRoot, null))
            {
                var relative = Uri.UnescapeDataString(uri.AbsolutePath[prefix.Length..]);
                var target = Path.Combine(root, relative);

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var source = AssetLoader.Open(uri);
                using var output = File.Create(target);
                source.CopyTo(output);
            }

            File.WriteAllText(stamp, version);
            return entry;
        }

        private static readonly Uri ResourceRoot = new("avares://SourceGit/Resources/Markdown/");
    }
}
