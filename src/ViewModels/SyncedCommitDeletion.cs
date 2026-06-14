using System;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class SyncedCommitDeletion
    {
        public string DisplayName { get; }

        public SyncedCommitDeletion(
            string remote,
            string remoteBranch,
            string displayName,
            string commitSHA,
            string parentSHA)
        {
            _remote = remote;
            _remoteBranch = remoteBranch;
            DisplayName = displayName;
            _commitSHA = commitSHA;
            _parentSHA = parentSHA;
        }

        public async Task<bool> MoveRemoteToParentAsync(Repository repo, CommandLog log)
        {
            return await RunPushAsync(repo, log, _parentSHA, _commitSHA);
        }

        public async Task<bool> RestoreRemoteAsync(Repository repo, CommandLog log)
        {
            return await RunPushAsync(repo, log, _commitSHA, _parentSHA);
        }

        private async Task<bool> RunPushAsync(
            Repository repo,
            CommandLog log,
            string localRevision,
            string expectedRemoteRevision)
        {
            var command = new Commands.Push(
                repo.FullPath,
                _remote,
                localRevision,
                _remoteBranch,
                expectedRemoteRevision)
            {
                RaiseError = true,
            };

            if (log != null)
                command.Use(log);

            try
            {
                return await command.RunAsync();
            }
            catch (Exception exception)
            {
                repo.SendNotification($"Failed to update remote branch '{DisplayName}': {exception.Message}. Local deletion was cancelled.", true);
                return false;
            }
        }

        private readonly string _remote;
        private readonly string _remoteBranch;
        private readonly string _commitSHA;
        private readonly string _parentSHA;
    }
}
