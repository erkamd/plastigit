using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeleteBranch : Popup
    {
        public Models.Branch Target
        {
            get;
        }

        public Models.Branch TrackingRemoteBranch
        {
            get;
        }

        public Models.Branch TrackingLocalBranch
        {
            get;
        }

        public string SyncedDeleteTip
        {
            get;
            private set;
        }

        public DeleteBranch(Repository repo, Models.Branch branch)
        {
            _repo = repo;
            Target = branch;

            if (branch.IsLocal)
            {
                if (!string.IsNullOrEmpty(branch.Upstream))
                {
                    TrackingRemoteBranch = repo.Branches.Find(x => x.FullName == branch.Upstream);
                    if (TrackingRemoteBranch != null)
                        SyncedDeleteTip = App.Text("DeleteBranch.WithTrackingRemote", TrackingRemoteBranch.FriendlyName);
                }
            }
            else
            {
                TrackingLocalBranch = repo.Branches.Find(x => x.IsLocal && x.Upstream == branch.FullName);
                if (TrackingLocalBranch != null)
                    SyncedDeleteTip = App.Text("DeleteBranch.WithTrackingLocal", TrackingLocalBranch.FriendlyName);
            }
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Deleting branch...";

            var log = _repo.CreateLog("Delete Branch");
            Use(log);

            if (Target.IsLocal)
            {
                await new Commands.Branch(_repo.FullPath, Target.Name)
                    .Use(log)
                    .DeleteLocalAsync();
                _repo.UIStates.RemoveHistoryFilter(Target.FullName, Models.FilterType.LocalBranch);

                if (TrackingRemoteBranch != null)
                {
                    await _repo.DeleteRemoteBranchAsync(TrackingRemoteBranch, log);
                    _repo.UIStates.RemoveHistoryFilter(TrackingRemoteBranch.FullName, Models.FilterType.RemoteBranch);
                }
            }
            else
            {
                await _repo.DeleteRemoteBranchAsync(Target, log);
                _repo.UIStates.RemoveHistoryFilter(Target.FullName, Models.FilterType.RemoteBranch);

                if (TrackingLocalBranch != null)
                {
                    await new Commands.Branch(_repo.FullPath, TrackingLocalBranch.Name)
                        .Use(log)
                        .DeleteLocalAsync();
                    _repo.UIStates.RemoveHistoryFilter(TrackingLocalBranch.FullName, Models.FilterType.LocalBranch);
                }
            }

            log.Complete();
            _repo.MarkBranchesDirtyManually();
            return true;
        }

        private readonly Repository _repo = null;
    }
}
