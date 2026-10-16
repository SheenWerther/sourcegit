using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    /// <summary>
    ///     查询被 .gitignore 忽略的路径。目录以 '/' 结尾，且整个被忽略的目录只返回目录本身。
    /// </summary>
    public class QueryIgnoredPaths : Command
    {
        public QueryIgnoredPaths(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = "ls-files --others --ignored --exclude-standard --directory -z";
            RaiseError = false;
        }

        public async Task<HashSet<string>> GetResultAsync()
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return paths;

            foreach (var path in rs.StdOut.Split('\0', StringSplitOptions.RemoveEmptyEntries))
                paths.Add(path);

            return paths;
        }
    }
}
