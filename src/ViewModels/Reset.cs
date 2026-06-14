using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Reset : Popup
    {
        public Models.Branch Current
        {
            get;
        }

        public Models.Commit To
        {
            get;
        }

        public Models.ResetMode SelectedMode
        {
            get;
            set;
        }

        public bool RemovesRemoteCommit => _remoteDeletion != null;
        public string RemoteDeletionDescription => _remoteDeletion == null
            ? string.Empty
            : $"The synced commit will also be removed from {_remoteDeletion.DisplayName}.";

        public Reset(
            Repository repo,
            Models.Branch current,
            Models.Commit to,
            SyncedCommitDeletion remoteDeletion = null)
        {
            _repo = repo;
            _remoteDeletion = remoteDeletion;
            Current = current;
            To = to;
            SelectedMode = Models.ResetMode.Supported[1];
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Reset current branch to {To.SHA} ...";

            var log = _repo.CreateLog($"Reset HEAD to '{To.SHA}'");
            Use(log);

            var remoteMoved = false;
            if (_remoteDeletion != null)
            {
                ProgressDescription = $"Removing synced commit from {_remoteDeletion.DisplayName} ...";
                remoteMoved = await _remoteDeletion.MoveRemoteToParentAsync(_repo, log);
                if (!remoteMoved)
                {
                    log.Complete();
                    _repo.MarkBranchesDirtyManually();
                    return false;
                }
            }

            var succ = await new Commands.Reset(_repo.FullPath, To.SHA, SelectedMode.Arg)
                .Use(log)
                .ExecAsync();

            if (succ)
            {
                await _repo.AutoUpdateSubmodulesAsync(log);
            }
            else if (remoteMoved)
            {
                ProgressDescription = $"Restoring {_remoteDeletion.DisplayName} ...";
                var restored = await _remoteDeletion.RestoreRemoteAsync(_repo, log);
                if (!restored)
                    _repo.SendNotification($"Local reset failed and {_remoteDeletion.DisplayName} could not be restored automatically.", true);
            }

            log.Complete();
            _repo.MarkBranchesDirtyManually();
            return succ;
        }

        private readonly Repository _repo = null;
        private readonly SyncedCommitDeletion _remoteDeletion = null;
    }
}
