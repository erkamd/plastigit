# PlastiGit

A personal fork of [sourcegit-scm/sourcegit](https://github.com/sourcegit-scm/sourcegit), a cross-platform Git GUI client. Licensed under the same [MIT License](LICENSE) as upstream. Not affiliated with or endorsed by the original SourceGit project — for the official app, releases, and documentation, go to the [upstream repository](https://github.com/sourcegit-scm/sourcegit).

## What's different from upstream

* **Branch ownership tags on commits** — commits made through this app are tagged with the branch they were created on (stored as a `(#branch-name)` prefix in the commit subject, parsed back out via `Models.Commit.BranchTag`). Used to resolve which branch a commit logically belongs to even after merges/rebases, fixing branch-resolution glitches in the commit graph.
* **Branch creation safety checks**:
  * Warns and blocks branch creation if `user.name`/`user.email` aren't configured in git, with a one-click fix link, instead of silently failing.
  * New branches checked out from a remote-tracking branch (e.g. via the "Local Branches" quick actions) no longer get an unwanted automatic empty initial commit.
* **Fetch auto-fast-forwards local branches** — after a fetch, any local branch that's purely behind its upstream (no unpushed commits of its own) is fast-forwarded automatically, instead of staying stale until manually checked out and pulled.
* **More reliable "Delete commit" menu item** — the context menu now waits for the delete-availability check to finish before opening, so the enabled state can't flip after the menu is already on screen.

## Building

```sh
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
dotnet restore
dotnet build
dotnet run --project src/SourceGit.csproj
```

Requires the [.NET SDK](https://dotnet.microsoft.com/en-us/download).

## Contributing

This is a personal fork; it doesn't take outside contributions. For general SourceGit improvements or bug fixes, please submit to [the upstream project](https://github.com/sourcegit-scm/sourcegit) instead.
