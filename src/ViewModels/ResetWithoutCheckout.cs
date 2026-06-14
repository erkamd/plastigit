using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class ResetWithoutCheckout : Popup
    {
        public Models.Branch Target
        {
            get;
        }

        public object To
        {
            get;
        }

        public bool RemovesRemoteCommit => _remoteDeletion != null;
        public string RemoteDeletionDescription => _remoteDeletion == null
            ? string.Empty
            : $"The synced commit will also be removed from {_remoteDeletion.DisplayName}.";

        public ResetWithoutCheckout(
            Repository repo,
            Models.Branch target,
            Models.Branch to,
            SyncedCommitDeletion remoteDeletion = null)
        {
            _repo = repo;
            _remoteDeletion = remoteDeletion;
            _revision = to.Head;
            Target = target;
            To = to;
        }

        public ResetWithoutCheckout(
            Repository repo,
            Models.Branch target,
            Models.Commit to,
            SyncedCommitDeletion remoteDeletion = null)
        {
            _repo = repo;
            _remoteDeletion = remoteDeletion;
            _revision = to.SHA;
            Target = target;
            To = to;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = $"Reset {Target.Name} to {_revision} ...";

            var log = _repo.CreateLog($"Reset '{Target.Name}' to '{_revision}'");
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

            var succ = await new Commands.Branch(_repo.FullPath, Target.Name)
                .Use(log)
                .CreateAsync(_revision, true);

            if (!succ && remoteMoved)
            {
                ProgressDescription = $"Restoring {_remoteDeletion.DisplayName} ...";
                var restored = await _remoteDeletion.RestoreRemoteAsync(_repo, log);
                if (!restored)
                    _repo.SendNotification($"Local branch update failed and {_remoteDeletion.DisplayName} could not be restored automatically.", true);
            }

            log.Complete();
            _repo.MarkBranchesDirtyManually();
            return succ;
        }

        private readonly Repository _repo = null;
        private readonly SyncedCommitDeletion _remoteDeletion = null;
        private readonly string _revision;
    }
}
