namespace SourceGit.Models
{
    // Marker used for the auto-generated empty commit created on every new branch, so a
    // freshly created branch is never left without any commit of its own (and can always
    // be pushed/synced immediately). Carries no branch-specific information.
    public static class BranchInit
    {
        public const string CommitMessage = "Initialize branch (auto-generated empty commit)";
    }
}
