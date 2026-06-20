using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class Fetch : Popup
    {
        public List<Models.Remote> Remotes
        {
            get => _repo.Remotes;
        }

        public bool IsFetchAllRemoteVisible
        {
            get;
        }

        public bool FetchAllRemotes
        {
            get => _fetchAllRemotes;
            set
            {
                if (SetProperty(ref _fetchAllRemotes, value) && IsFetchAllRemoteVisible)
                    _repo.UIStates.FetchAllRemotes = value;
            }
        }

        public Models.Remote SelectedRemote
        {
            get;
            set;
        }

        public bool NoTags
        {
            get => _repo.UIStates.FetchWithoutTags;
            set => _repo.UIStates.FetchWithoutTags = value;
        }

        public bool Force
        {
            get => _repo.UIStates.EnableForceOnFetch;
            set => _repo.UIStates.EnableForceOnFetch = value;
        }

        public Fetch(Repository repo, Models.Remote preferredRemote = null)
        {
            _repo = repo;
            IsFetchAllRemoteVisible = repo.Remotes.Count > 1 && preferredRemote == null;
            _fetchAllRemotes = IsFetchAllRemoteVisible && _repo.UIStates.FetchAllRemotes;

            if (preferredRemote != null)
            {
                SelectedRemote = preferredRemote;
            }
            else if (!string.IsNullOrEmpty(_repo.Settings.DefaultRemote))
            {
                var def = _repo.Remotes.Find(r => r.Name == _repo.Settings.DefaultRemote);
                SelectedRemote = def ?? _repo.Remotes[0];
            }
            else
            {
                SelectedRemote = _repo.Remotes[0];
            }
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();

            var navigateToUpstreamHEAD = _repo.IsHistoriesVisible &&
                _repo.Histories.SelectedCommits.Count == 1 &&
                _repo.Histories.SelectedCommits[0].IsCurrentHead;

            var notags = _repo.UIStates.FetchWithoutTags;
            var force = _repo.UIStates.EnableForceOnFetch;
            var log = _repo.CreateLog("Fetch");
            Use(log);

            if (FetchAllRemotes)
            {
                foreach (var remote in _repo.Remotes)
                    await new Commands.Fetch(_repo.FullPath, remote.Name, notags, force)
                        .Use(log)
                        .RunAsync();
            }
            else
            {
                await new Commands.Fetch(_repo.FullPath, SelectedRemote.Name, notags, force)
                    .Use(log)
                    .RunAsync();
            }

            await FastForwardLocalBranchesAsync(log);

            log.Complete();

            if (navigateToUpstreamHEAD)
            {
                var upstream = _repo.CurrentBranch?.Upstream;
                if (!string.IsNullOrEmpty(upstream))
                {
                    var upstreamHead = await new Commands.QueryRevisionByRefName(_repo.FullPath, upstream.Substring(13)).GetResultAsync();
                    _repo.NavigateToCommit(upstreamHead, true);
                }
            }

            _repo.MarkFetched();
            return true;
        }

        private async Task FastForwardLocalBranchesAsync(CommandLog log)
        {
            var branches = await new Commands.QueryBranches(_repo.FullPath).GetResultAsync().ConfigureAwait(false);
            var refreshed = false;

            foreach (var b in branches)
            {
                if (!b.IsLocal || b.IsCurrent || b.IsUpstreamGone || string.IsNullOrEmpty(b.Upstream))
                    continue;

                if (b.Behind.Count == 0 || b.Ahead.Count > 0)
                    continue;

                if (await new Commands.Branch(_repo.FullPath, b.Name).Use(log).CreateAsync(b.Upstream, true).ConfigureAwait(false))
                    refreshed = true;
            }

            if (refreshed)
                _repo.RefreshBranches();
        }

        private readonly Repository _repo = null;
        private bool _fetchAllRemotes = false;
    }
}
