using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class HasCommitDescendants : Command
    {
        public HasCommitDescendants(string repo, string commit)
        {
            WorkingDirectory = repo;
            RaiseError = false;
            Args = $"rev-list --branches --remotes --tags --ancestry-path --max-count=1 ^{commit}";
        }

        public async Task<bool?> GetResultAsync()
        {
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return null;

            return !string.IsNullOrWhiteSpace(rs.StdOut);
        }
    }
}
