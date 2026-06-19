using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.ViewModels
{
    public class DeleteMultipleBranches : Popup
    {
        public List<Models.Branch> Targets
        {
            get;
        }

        public DeleteMultipleBranches(Repository repo, List<Models.Branch> branches, bool isLocal)
        {
            _repo = repo;
            _isLocal = isLocal;
            Targets = branches;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Deleting multiple branches...";

            var log = _repo.CreateLog("Delete Multiple Branches");
            Use(log);

            if (_isLocal)
            {
                foreach (var target in Targets)
                {
                    await new Commands.Branch(_repo.FullPath, target.Name)
                        .Use(log)
                        .DeleteLocalAsync();

                    if (!string.IsNullOrEmpty(target.Upstream))
                    {
                        var upstream = _repo.Branches.Find(x => !x.IsLocal && x.FullName == target.Upstream);
                        if (upstream != null)
                            await _repo.DeleteRemoteBranchAsync(upstream, log);
                    }
                }
            }
            else
            {
                foreach (var target in Targets)
                {
                    await _repo.DeleteRemoteBranchAsync(target, log);

                    var tracking = _repo.Branches.Find(x => x.IsLocal && x.Upstream == target.FullName);
                    if (tracking != null)
                        await new Commands.Branch(_repo.FullPath, tracking.Name)
                            .Use(log)
                            .DeleteLocalAsync();
                }
            }

            log.Complete();
            _repo.MarkBranchesDirtyManually();
            return true;
        }

        private Repository _repo = null;
        private bool _isLocal = false;
    }
}
