using System.Threading.Tasks;

namespace SourceGit.Commands
{
    // Creates a new commit with the same tree as `baseRevision` (i.e. no actual changes)
    // and moves `refName` to it, without touching the working tree or the index. Used to
    // give a freshly created branch an initial commit of its own.
    public class CreateEmptyCommitOnRef : Command
    {
        public CreateEmptyCommitOnRef(string repo)
        {
            WorkingDirectory = repo;
            Context = repo;
        }

        public async Task<string> RunAsync(string refName, string baseRevision, string message)
        {
            Args = $"commit-tree {baseRevision}^{{tree}} -p {baseRevision} -m {message.Quoted()}";

            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return null;

            var sha = rs.StdOut.Trim();
            if (string.IsNullOrEmpty(sha))
                return null;

            Args = $"update-ref {refName} {sha}";
            return await ExecAsync().ConfigureAwait(false) ? sha : null;
        }
    }
}
