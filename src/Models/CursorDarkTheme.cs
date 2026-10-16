using System;
using System.Collections.Generic;
using System.IO;

using TextMateSharp.Internal.Themes.Reader;
using TextMateSharp.Themes;

namespace SourceGit.Models
{
    /// <summary>
    ///     代码高亮使用的 Cursor Dark 主题。
    ///     主题文件属于 Cursor，不随本项目分发，按以下顺序查找，都找不到时返回 null，调用方退回内置的 Dark+：
    ///     1. 配置目录下的 themes/cursor-dark-color-theme.json（可手动放一份）
    ///     2. 本机已安装的 Cursor
    /// </summary>
    public static class CursorDarkTheme
    {
        public static IRawTheme Load() => s_theme.Value;

        private static IRawTheme Read()
        {
            foreach (var path in GetCandidatePaths())
            {
                if (!File.Exists(path))
                    continue;

                try
                {
                    using var reader = new StreamReader(path);
                    return ThemeReader.ReadThemeSync(reader);
                }
                catch (Exception)
                {
                    // 文件损坏或格式不兼容，试下一个
                }
            }

            return null;
        }

        private static IEnumerable<string> GetCandidatePaths()
        {
            yield return Path.Combine(Native.OS.BasicDirectories.ConfigDir, "themes", FileName);

            var themeSubDir = Path.Combine("app", "extensions", "theme-cursor", "themes", FileName);

            if (OperatingSystem.IsWindows())
            {
                var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                yield return Path.Combine(local, "Programs", "cursor", "resources", themeSubDir);
                yield return Path.Combine(local, "Programs", "Cursor", "resources", themeSubDir);
            }
            else if (OperatingSystem.IsMacOS())
            {
                yield return Path.Combine("/Applications/Cursor.app/Contents/Resources", themeSubDir);
            }
            else
            {
                yield return Path.Combine("/opt/Cursor/resources", themeSubDir);
                yield return Path.Combine("/usr/share/cursor/resources", themeSubDir);
            }
        }

        private const string FileName = "cursor-dark-color-theme.json";
        private static readonly Lazy<IRawTheme> s_theme = new(Read);
    }
}
