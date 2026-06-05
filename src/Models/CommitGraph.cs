using System;
using System.Collections.Generic;

using Avalonia;
using Avalonia.Media;

namespace SourceGit.Models
{
    public record CommitGraphLayout(double StartY, double ClipWidth, double RowHeight);

    public enum CommitGraphHighlighting
    {
        All = 0,
        CurrentBranchOnly,
        SelectedCommitsOnly,
        CurrentBranchAndSelectedCommits,
    }

    public class CommitGraph
    {
        public int TotalLanes { get; set; } = 0;
        public const double LaneUnitWidth = 12;
        public const double LaneOriginX = 10;

        public static List<Pen> Pens { get; } = [];

        private static int s_penCount = 0;
        private static readonly List<Color> s_defaultPenColors = [
            Color.Parse("#1c98e1"), // Blue
            Color.Parse("#29b87e"), // Green
            Color.Parse("#e03a3a"), // Red
            Color.Parse("#f6a500"), // Orange
            Color.Parse("#a300d9"), // Purple
            Color.Parse("#00d9cc"), // Teal
            Color.Parse("#dc5b23"), // Coral
            Color.Parse("#8ac007"), // Lime
            Color.Parse("#ffcc00"), // Yellow
            Color.Parse("#e138e8"), // Pink
        ];

        public static void SetDefaultPens(double thickness = 2.5)
        {
            SetPens(s_defaultPenColors, thickness);
        }

        public static void SetPens(List<Color> colors, double thickness)
        {
            Pens.Clear();

            foreach (var c in colors)
                Pens.Add(new Pen(c.ToUInt32(), thickness));

            s_penCount = colors.Count;
        }

        public class Path(int color, bool isHighlighted)
        {
            public List<Point> Points { get; } = [];
            public int Color { get; } = color;
            public bool IsHighlighted { get; } = isHighlighted;
            public int Lane { get; set; } = 0;
            public string BranchName { get; set; } = null;
            public bool IsLocal { get; set; } = true;
        }

        public class Lane
        {
            public int Index { get; set; } = 0;
            public int Color { get; set; } = 0;
            public string Key { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Head { get; set; } = string.Empty;
            public int HeadIndex { get; set; } = -1;
            public bool IsCurrent { get; set; } = false;
            public bool IsPrimary { get; set; } = false;
            public bool IsHighlighted { get; set; } = false;
            public bool IsEmpty { get; set; } = false;
            public bool IsLocal { get; set; } = true;
            public string UpstreamName { get; set; } = string.Empty;
            public string UpstreamHead { get; set; } = string.Empty;
        }

        public class FastForwardBranchLocation
        {
            public int BaseLane { get; set; } = 0;
            public int CommitIndex { get; set; } = -1;
            public int Slot { get; set; } = 0;
            public int Color { get; set; } = 0;
            public string Name { get; set; } = string.Empty;
            public string Head { get; set; } = string.Empty;
            public bool IsCurrent { get; set; } = false;
            public bool IsHighlighted { get; set; } = false;
            public bool IsLocal { get; set; } = true;
        }

        public class PathHelper
        {
            public Path Path { get; private set; }
            public string Next { get; set; }
            public double LastX { get; private set; }
            public int Lane { get; }
            public bool IsHighlighted { get => Path.IsHighlighted; }

            public PathHelper(string next, bool IsHighlighted, int lane, Point start)
            {
                Next = next;
                Lane = lane;
                LastX = start.X;
                _lastY = start.Y;

                Path = new Path(lane % s_penCount, IsHighlighted) { Lane = lane };
                Path.Points.Add(start);
            }

            public PathHelper(string next, bool IsHighlighted, int lane, Point start, Point to)
            {
                Next = next;
                Lane = lane;
                LastX = to.X;
                _lastY = to.Y;

                Path = new Path(lane % s_penCount, IsHighlighted) { Lane = lane };
                Path.Points.Add(start);
                Path.Points.Add(to);
            }

            public void Pass(double x, double y, double halfHeight)
            {
                if (x > LastX)
                {
                    Add(LastX, _lastY);
                    Add(x, y - halfHeight);
                }
                else if (x < LastX)
                {
                    Add(LastX, y - halfHeight);
                    y += halfHeight;
                    Add(x, y);
                }

                LastX = x;
                _lastY = y;
            }

            public void Goto(double x, double y, double halfHeight)
            {
                if (x > LastX)
                {
                    Add(LastX, _lastY);
                    Add(x, y - halfHeight);
                }
                else if (x < LastX)
                {
                    var minY = y - halfHeight;
                    if (minY > _lastY)
                        minY -= halfHeight;

                    Add(LastX, minY);
                    Add(x, y);
                }

                LastX = x;
                _lastY = y;
            }

            public void End(double x, double y, double halfHeight)
            {
                if (x > LastX)
                {
                    Add(LastX, _lastY);
                    Add(x, y - halfHeight);
                }
                else if (x < LastX)
                {
                    Add(LastX, y - halfHeight);
                }

                Add(x, y);

                LastX = x;
                _lastY = y;
            }

            public void Highlight()
            {
                Add(LastX, _lastY);

                Path = new Path(Lane % s_penCount, true) { Lane = Lane };
                Path.Points.Add(new Point(LastX, _lastY));
                _endY = 0;
            }

            private void Add(double x, double y)
            {
                if (_endY < y)
                {
                    Path.Points.Add(new Point(x, y));
                    _endY = y;
                }
            }

            private double _lastY = 0;
            private double _endY = 0;
        }

        public class Link
        {
            public Point Start;
            public Point Control;
            public Point End;
            public int Color;
            public bool IsHighlighted;
            public bool IsLocal = true;
        }

        public enum DotType
        {
            Default,
            Head,
            Merge,
        }

        public class Dot
        {
            public DotType Type;
            public Point Center;
            public int Color;
            public bool IsHighlighted;
            public int Lane;
            public int CommitIndex;
            public string CommitSHA;
            public bool IsLocal = true;
            public bool IsOnRemote;
        }

        public List<Lane> Lanes { get; } = [];
        public List<FastForwardBranchLocation> FastForwardBranchLocations { get; } = [];
        public List<Path> Paths { get; } = [];
        public List<Link> Links { get; } = [];
        public List<Dot> Dots { get; } = [];

        public static CommitGraph Generate(List<Commit> commits, bool recalculateMergeState, bool firstParentOnlyEnabled, CommitGraphHighlighting highlighting, HashSet<string> highlightExtraCommits)
        {
            return Generate(commits, null, recalculateMergeState, firstParentOnlyEnabled, highlighting, highlightExtraCommits);
        }

        public static CommitGraph Generate(List<Commit> commits, List<Branch> branches, bool recalculateMergeState, bool firstParentOnlyEnabled, CommitGraphHighlighting highlighting, HashSet<string> highlightExtraCommits, bool showFastForwardedBranchLocations = false, IReadOnlyDictionary<string, int> branchColors = null)
        {
            if (s_penCount == 0)
                SetDefaultPens();

            var graph = new CommitGraph();
            if (commits == null || commits.Count == 0)
                return graph;

            RecalculateCurrentBranchMembership(commits, recalculateMergeState);

            var commitBySha = new Dictionary<string, Commit>(StringComparer.Ordinal);
            var indexBySha = new Dictionary<string, int>(StringComparer.Ordinal);
            var childCountByParent = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < commits.Count; i++)
            {
                var commit = commits[i];
                commitBySha[commit.SHA] = commit;
                indexBySha[commit.SHA] = i;
                commit.IsHighlightedInGraph = false;
                commit.Color = 0;
                commit.LeftMargin = 0;

                foreach (var parent in commit.Parents)
                    childCountByParent[parent] = childCountByParent.GetValueOrDefault(parent) + 1;
            }

            var visibleBranches = BuildVisibleBranches(commits, branches, indexBySha);
            if (visibleBranches.Count == 0)
                return graph;

            var remoteCommits = CollectRemoteCommits(visibleBranches, commitBySha);
            var pairedUpstreams = BuildPairedUpstreams(visibleBranches, commitBySha);
            var pairedRemoteKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var upstream in pairedUpstreams.Values)
                pairedRemoteKeys.Add(GetBranchKey(upstream));

            var laneBranches = new List<Branch>();
            foreach (var branch in visibleBranches)
            {
                if (!pairedRemoteKeys.Contains(GetBranchKey(branch)))
                    laneBranches.Add(branch);
            }

            var primaryKey = GetBranchKey(SelectPrimaryBranch(laneBranches));
            laneBranches.Sort((l, r) => CompareBranches(l, r, primaryKey, indexBySha));

            var branchHeadOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            var branchHeads = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var branch in laneBranches)
            {
                var key = GetBranchKey(branch);
                if (!branchHeadOwners.ContainsKey(branch.Head))
                    branchHeadOwners.Add(branch.Head, key);

                if (!branchHeads.TryGetValue(branch.Head, out var owners))
                {
                    owners = [];
                    branchHeads.Add(branch.Head, owners);
                }

                owners.Add(key);
            }

            var lanes = new List<BranchLane>();
            var fastForwardBranches = new List<FastForwardBranch>();
            var ownedLaneByCommit = new Dictionary<string, BranchLane>(StringComparer.Ordinal);
            for (int i = 0; i < laneBranches.Count; i++)
            {
                var branch = laneBranches[i];
                var key = GetBranchKey(branch);
                var isPrimary = key.Equals(primaryKey, StringComparison.Ordinal);
                var ownsHeadChain = branchHeadOwners.TryGetValue(branch.Head, out var owner) && owner.Equals(key, StringComparison.Ordinal);
                var isFastForwarded = !ownsHeadChain || (!isPrimary && ownedLaneByCommit.ContainsKey(branch.Head));

                if (isFastForwarded && !branch.IsCurrent)
                {
                    fastForwardBranches.Add(new FastForwardBranch()
                    {
                        Branch = branch,
                        Key = key,
                        Name = GetBranchName(branch),
                        Head = branch.Head,
                    });
                    continue;
                }

                var lane = new BranchLane()
                {
                    Branch = branch,
                    Key = key,
                    LaneIndex = lanes.Count,
                    Name = GetBranchName(branch),
                    IsPrimary = isPrimary,
                    OwnsHeadChain = ownsHeadChain && !isFastForwarded,
                    UpstreamBranch = pairedUpstreams.GetValueOrDefault(key),
                };

                if (lane.UpstreamBranch != null)
                    lane.UpstreamCommits = CollectCommitsFromHead(lane.UpstreamBranch.Head, commitBySha);

                if (lane.OwnsHeadChain)
                    BuildOwnedChain(lane, commitBySha, indexBySha, childCountByParent, branchHeads);

                if (lane.Commits.Count == 0 && !lane.Branch.IsCurrent)
                {
                    fastForwardBranches.Add(new FastForwardBranch()
                    {
                        Branch = branch,
                        Key = key,
                        Name = lane.Name,
                        Head = branch.Head,
                    });
                    continue;
                }

                lanes.Add(lane);
                foreach (var sha in lane.Commits)
                {
                    if (!ownedLaneByCommit.ContainsKey(sha) || lane.IsPrimary || lane.Branch.IsCurrent)
                        ownedLaneByCommit[sha] = lane;
                }
            }

            var pointsBySha = new Dictionary<string, List<CommitPoint>>(StringComparer.Ordinal);
            var assignedCommitLane = new bool[commits.Count];

            foreach (var lane in lanes)
            {
                var color = GetBranchColor(lane.Branch, lane.LaneIndex, branchColors);
                var laneHighlighted = IsLaneHighlighted(lane, highlighting, highlightExtraCommits, commitBySha);
                var headIndex = indexBySha.GetValueOrDefault(lane.Branch.Head, -1);

                graph.Lanes.Add(new Lane()
                {
                    Index = lane.LaneIndex,
                    Color = color,
                    Key = lane.Key,
                    Name = lane.Name,
                    Head = lane.Branch.Head,
                    HeadIndex = headIndex,
                    IsCurrent = lane.Branch.IsCurrent,
                    IsPrimary = lane.IsPrimary,
                    IsHighlighted = laneHighlighted,
                    IsEmpty = lane.Commits.Count == 0,
                    IsLocal = lane.Branch.IsLocal,
                    UpstreamName = lane.UpstreamBranch == null ? string.Empty : GetBranchName(lane.UpstreamBranch),
                    UpstreamHead = lane.UpstreamBranch?.Head ?? string.Empty,
                });

                AddLanePaths(graph, lane, color, laneHighlighted, indexBySha, headIndex, commits.Count);

                foreach (var sha in lane.Commits)
                {
                    if (!indexBySha.TryGetValue(sha, out var commitIndex) || !commitBySha.TryGetValue(sha, out var commit))
                        continue;

                    var isHighlighted = IsCommitHighlighted(commit, lane, highlighting, highlightExtraCommits);
                    commit.IsHighlightedInGraph |= isHighlighted;
                    if (!assignedCommitLane[commitIndex] || lane.IsPrimary || lane.Branch.IsCurrent)
                    {
                        commit.Color = color;
                        assignedCommitLane[commitIndex] = true;
                    }

                    var point = new Point(GetLaneX(lane.LaneIndex), commitIndex);
                    var dot = new Dot()
                    {
                        Center = point,
                        Color = color,
                        IsHighlighted = isHighlighted,
                        Lane = lane.LaneIndex,
                        CommitIndex = commitIndex,
                        CommitSHA = sha,
                        IsLocal = IsLaneCommitLocal(lane, sha),
                        IsOnRemote = remoteCommits.Contains(sha),
                        Type = commit.IsCurrentHead ? DotType.Head : commit.Parents.Count > 1 ? DotType.Merge : DotType.Default,
                    };

                    graph.Dots.Add(dot);

                    if (!pointsBySha.TryGetValue(sha, out var points))
                    {
                        points = [];
                        pointsBySha.Add(sha, points);
                    }

                    points.Add(new CommitPoint()
                    {
                        Point = point,
                        Lane = lane.LaneIndex,
                        Color = color,
                        IsPrimary = lane.IsPrimary,
                        IsCurrent = lane.Branch.IsCurrent,
                        IsLocal = IsLaneCommitLocal(lane, sha),
                    });
                }
            }

            if (showFastForwardedBranchLocations)
            {
                var slotByHead = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var branch in fastForwardBranches)
                {
                    if (!indexBySha.TryGetValue(branch.Head, out var headIndex) ||
                        !TryFindPoint(pointsBySha, branch.Head, out var basePoint))
                        continue;

                    var slot = slotByHead.GetValueOrDefault(branch.Head);
                    slotByHead[branch.Head] = slot + 1;
                    graph.FastForwardBranchLocations.Add(new FastForwardBranchLocation()
                    {
                        BaseLane = basePoint.Lane,
                        CommitIndex = headIndex,
                        Slot = slot,
                        Color = basePoint.Color,
                        Name = branch.Name,
                        Head = branch.Head,
                        IsCurrent = branch.Branch.IsCurrent,
                        IsHighlighted = IsFastForwardBranchHighlighted(branch, highlighting, highlightExtraCommits, commitBySha),
                        IsLocal = branch.Branch.IsLocal,
                    });
                }
            }

            foreach (var lane in lanes)
            {
                if (lane.Commits.Count == 0)
                {
                    if (lane.Branch.IsCurrent &&
                        TryFindPoint(pointsBySha, lane.Branch.Head, out var emptyParentPoint) &&
                        indexBySha.TryGetValue(lane.Branch.Head, out var emptyHeadIndex))
                    {
                        var emptyPoint = new Point(GetLaneX(lane.LaneIndex), emptyHeadIndex);
                        graph.Links.Add(new Link()
                        {
                            Start = emptyParentPoint.Point,
                            End = emptyPoint,
                            Control = new Point(emptyParentPoint.Point.X, emptyPoint.Y),
                            Color = GetBranchColor(lane.Branch, lane.LaneIndex, branchColors),
                            IsHighlighted = IsLaneHighlighted(lane, highlighting, highlightExtraCommits, commitBySha),
                            IsLocal = lane.Branch.IsLocal,
                        });
                    }

                    continue;
                }

                if (lane.ForkParent != null && lane.Commits.Count > 0 && TryFindPoint(pointsBySha, lane.ForkParent, out var parentPoint))
                {
                    var childSha = lane.Commits[^1];
                    if (indexBySha.TryGetValue(childSha, out var childIndex))
                    {
                        var childPoint = new Point(GetLaneX(lane.LaneIndex), childIndex);
                        graph.Links.Add(new Link()
                        {
                            Start = parentPoint.Point,
                            End = childPoint,
                            Control = new Point(parentPoint.Point.X, childPoint.Y),
                            Color = GetBranchColor(lane.Branch, lane.LaneIndex, branchColors),
                            IsHighlighted = IsLaneHighlighted(lane, highlighting, highlightExtraCommits, commitBySha),
                            IsLocal = IsLaneCommitLocal(lane, childSha),
                        });
                    }
                }

                if (firstParentOnlyEnabled)
                    continue;

                foreach (var sha in lane.Commits)
                {
                    if (!commitBySha.TryGetValue(sha, out var commit) || commit.Parents.Count < 2)
                        continue;

                    if (!indexBySha.TryGetValue(sha, out var commitIndex))
                        continue;

                    var mergePoint = new Point(GetLaneX(lane.LaneIndex), commitIndex);
                    for (int i = 1; i < commit.Parents.Count; i++)
                    {
                        if (!TryFindPoint(pointsBySha, commit.Parents[i], out var mergeParentPoint))
                            continue;

                        graph.Links.Add(new Link()
                        {
                            Start = mergeParentPoint.Point,
                            End = mergePoint,
                            Control = new Point(mergeParentPoint.Point.X, mergePoint.Y),
                            Color = mergeParentPoint.Color,
                            IsHighlighted = IsCommitHighlighted(commit, lane, highlighting, highlightExtraCommits),
                            IsLocal = mergeParentPoint.IsLocal,
                        });
                    }
                }
            }

            graph.TotalLanes = lanes.Count;
            var leftMargin = GetLaneX(Math.Max(0, graph.TotalLanes - 1)) + 12;
            foreach (var commit in commits)
                commit.LeftMargin = leftMargin;

            return graph;
        }

        public static List<Branch> FindBranchesWithoutOwnedCommits(List<Commit> commits, List<Branch> branches)
        {
            var result = new List<Branch>();
            if (commits == null || commits.Count == 0 || branches == null || branches.Count == 0)
                return result;

            var commitBySha = new Dictionary<string, Commit>(StringComparer.Ordinal);
            var indexBySha = new Dictionary<string, int>(StringComparer.Ordinal);
            var childCountByParent = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < commits.Count; i++)
            {
                var commit = commits[i];
                commitBySha[commit.SHA] = commit;
                indexBySha[commit.SHA] = i;

                foreach (var parent in commit.Parents)
                    childCountByParent[parent] = childCountByParent.GetValueOrDefault(parent) + 1;
            }

            var visibleBranches = BuildVisibleBranches(commits, branches, indexBySha);
            if (visibleBranches.Count == 0)
                return result;

            var primaryKey = GetBranchKey(SelectPrimaryBranch(visibleBranches));
            visibleBranches.Sort((l, r) => CompareBranches(l, r, primaryKey, indexBySha));

            var branchHeadOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            var branchHeads = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var branch in visibleBranches)
            {
                var key = GetBranchKey(branch);
                if (!branchHeadOwners.ContainsKey(branch.Head))
                    branchHeadOwners.Add(branch.Head, key);

                if (!branchHeads.TryGetValue(branch.Head, out var owners))
                {
                    owners = [];
                    branchHeads.Add(branch.Head, owners);
                }

                owners.Add(key);
            }

            var redundantKeys = new HashSet<string>(StringComparer.Ordinal);
            var ownedLaneByCommit = new Dictionary<string, BranchLane>(StringComparer.Ordinal);
            foreach (var branch in visibleBranches)
            {
                var key = GetBranchKey(branch);
                var isPrimary = key.Equals(primaryKey, StringComparison.Ordinal);
                var ownsHeadChain = branchHeadOwners.TryGetValue(branch.Head, out var owner) && owner.Equals(key, StringComparison.Ordinal);
                var isFastForwarded = !ownsHeadChain || (!isPrimary && ownedLaneByCommit.ContainsKey(branch.Head));

                if (isFastForwarded && !branch.IsCurrent)
                {
                    redundantKeys.Add(key);
                    continue;
                }

                var lane = new BranchLane()
                {
                    Branch = branch,
                    Key = key,
                    IsPrimary = isPrimary,
                    OwnsHeadChain = ownsHeadChain && !isFastForwarded,
                };

                if (lane.OwnsHeadChain)
                    BuildOwnedChain(lane, commitBySha, indexBySha, childCountByParent, branchHeads);

                if (lane.Commits.Count == 0 && !lane.Branch.IsCurrent)
                {
                    redundantKeys.Add(key);
                    continue;
                }

                foreach (var sha in lane.Commits)
                {
                    if (!ownedLaneByCommit.ContainsKey(sha) || lane.IsPrimary || lane.Branch.IsCurrent)
                        ownedLaneByCommit[sha] = lane;
                }
            }

            foreach (var branch in branches)
            {
                if (branch != null && redundantKeys.Contains(GetBranchKey(branch)))
                    result.Add(branch);
            }

            return result;
        }

        public static double GetLaneX(int lane)
        {
            return LaneOriginX + lane * LaneUnitWidth;
        }

        public static double GetLaneFromX(double x)
        {
            return (x - LaneOriginX) / LaneUnitWidth;
        }

        private static void RecalculateCurrentBranchMembership(List<Commit> commits, bool recalculateMergeState)
        {
            if (!recalculateMergeState)
                return;

            var merged = new HashSet<string>(StringComparer.Ordinal);
            foreach (var commit in commits)
            {
                if (commit.IsMerged)
                {
                    merged.Remove(commit.SHA);
                    foreach (var p in commit.Parents)
                        merged.Add(p);
                }
                else if (merged.Remove(commit.SHA))
                {
                    commit.IsMerged = true;
                    foreach (var p in commit.Parents)
                        merged.Add(p);
                }
            }
        }

        private static List<Branch> BuildVisibleBranches(List<Commit> commits, List<Branch> branches, Dictionary<string, int> indexBySha)
        {
            var visible = new List<Branch>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (branches != null)
            {
                foreach (var branch in branches)
                {
                    if (branch == null ||
                        branch.IsDetachedHead ||
                        string.IsNullOrEmpty(branch.Head) ||
                        !indexBySha.ContainsKey(branch.Head))
                        continue;

                    if (seen.Add(GetBranchKey(branch)))
                        visible.Add(branch);
                }
            }

            foreach (var commit in commits)
            {
                foreach (var decorator in commit.Decorators)
                {
                    if (decorator.Type is not (DecoratorType.CurrentBranchHead or DecoratorType.LocalBranchHead or DecoratorType.RemoteBranchHead))
                        continue;

                    var branch = CreateBranchFromDecorator(decorator, commit.SHA);
                    if (seen.Add(GetBranchKey(branch)))
                        visible.Add(branch);
                }
            }

            if (visible.Count == 0 && commits.Count > 0)
            {
                visible.Add(new Branch()
                {
                    Name = "HEAD",
                    FullName = "HEAD",
                    Head = commits[0].SHA,
                    IsCurrent = true,
                    IsLocal = true,
                });
            }

            return visible;
        }

        private static Branch CreateBranchFromDecorator(Decorator decorator, string head)
        {
            if (decorator.Type == DecoratorType.RemoteBranchHead)
            {
                var parts = decorator.Name.Split('/', 2);
                return new Branch()
                {
                    Name = parts.Length == 2 ? parts[1] : decorator.Name,
                    FullName = $"refs/remotes/{decorator.Name}",
                    Head = head,
                    IsCurrent = false,
                    IsLocal = false,
                    Remote = parts.Length == 2 ? parts[0] : string.Empty,
                };
            }

            return new Branch()
            {
                Name = decorator.Name,
                FullName = $"refs/heads/{decorator.Name}",
                Head = head,
                IsCurrent = decorator.Type == DecoratorType.CurrentBranchHead,
                IsLocal = true,
            };
        }

        private static Branch SelectPrimaryBranch(List<Branch> branches)
        {
            Branch current = null;
            Branch firstLocal = null;
            foreach (var branch in branches)
            {
                if (IsMainBranch(branch))
                    return branch;

                if (branch.IsCurrent)
                    current ??= branch;

                if (branch.IsLocal)
                    firstLocal ??= branch;
            }

            return current ?? firstLocal ?? branches[0];
        }

        private static HashSet<string> CollectRemoteCommits(List<Branch> branches, Dictionary<string, Commit> commitBySha)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();

            foreach (var branch in branches)
            {
                if (!branch.IsLocal && !string.IsNullOrEmpty(branch.Head))
                    pending.Push(branch.Head);
            }

            while (pending.Count > 0)
            {
                var sha = pending.Pop();
                if (!result.Add(sha) || !commitBySha.TryGetValue(sha, out var commit))
                    continue;

                foreach (var parent in commit.Parents)
                    pending.Push(parent);
            }

            return result;
        }

        private static Dictionary<string, Branch> BuildPairedUpstreams(List<Branch> branches, Dictionary<string, Commit> commitBySha)
        {
            var result = new Dictionary<string, Branch>(StringComparer.Ordinal);
            var byFullName = new Dictionary<string, Branch>(StringComparer.Ordinal);
            foreach (var branch in branches)
            {
                if (!string.IsNullOrEmpty(branch.FullName))
                    byFullName[branch.FullName] = branch;
            }

            foreach (var branch in branches)
            {
                if (!branch.IsLocal ||
                    string.IsNullOrEmpty(branch.Upstream) ||
                    !byFullName.TryGetValue(branch.Upstream, out var upstream) ||
                    upstream.IsLocal ||
                    !IsFirstParentAncestor(upstream.Head, branch.Head, commitBySha))
                    continue;

                result[GetBranchKey(branch)] = upstream;
            }

            return result;
        }

        private static bool IsFirstParentAncestor(string ancestor, string descendant, Dictionary<string, Commit> commitBySha)
        {
            if (string.IsNullOrEmpty(ancestor) || string.IsNullOrEmpty(descendant))
                return false;

            var current = descendant;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrEmpty(current) && visited.Add(current))
            {
                if (current.Equals(ancestor, StringComparison.Ordinal))
                    return true;

                if (!commitBySha.TryGetValue(current, out var commit) || commit.Parents.Count == 0)
                    break;

                current = commit.Parents[0];
            }

            return false;
        }

        private static HashSet<string> CollectCommitsFromHead(string head, Dictionary<string, Commit> commitBySha)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var pending = new Stack<string>();
            if (!string.IsNullOrEmpty(head))
                pending.Push(head);

            while (pending.Count > 0)
            {
                var sha = pending.Pop();
                if (!result.Add(sha) || !commitBySha.TryGetValue(sha, out var commit))
                    continue;

                foreach (var parent in commit.Parents)
                    pending.Push(parent);
            }

            return result;
        }

        private static bool IsMainBranch(Branch branch)
        {
            var name = GetBranchName(branch);
            return name.Equals("main", StringComparison.Ordinal) ||
                name.Equals("master", StringComparison.Ordinal) ||
                name.Equals("origin/main", StringComparison.Ordinal) ||
                name.Equals("origin/master", StringComparison.Ordinal);
        }

        private static int CompareBranches(Branch left, Branch right, string primaryKey, Dictionary<string, int> indexBySha)
        {
            var leftKey = GetBranchKey(left);
            var rightKey = GetBranchKey(right);

            if (leftKey.Equals(primaryKey, StringComparison.Ordinal))
                return -1;
            if (rightKey.Equals(primaryKey, StringComparison.Ordinal))
                return 1;

            var leftIndex = indexBySha.GetValueOrDefault(left.Head, int.MaxValue);
            var rightIndex = indexBySha.GetValueOrDefault(right.Head, int.MaxValue);
            if (leftIndex != rightIndex)
                return leftIndex.CompareTo(rightIndex);

            if (left.IsLocal != right.IsLocal)
                return left.IsLocal ? -1 : 1;

            return NumericSort.Compare(GetBranchName(left), GetBranchName(right));
        }

        private static void BuildOwnedChain(
            BranchLane lane,
            Dictionary<string, Commit> commitBySha,
            Dictionary<string, int> indexBySha,
            Dictionary<string, int> childCountByParent,
            Dictionary<string, List<string>> branchHeads)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = lane.Branch.Head;
            while (!string.IsNullOrEmpty(current) &&
                indexBySha.ContainsKey(current) &&
                commitBySha.TryGetValue(current, out var commit) &&
                visited.Add(current))
            {
                lane.Commits.Add(current);
                if (commit.Parents.Count == 0)
                    break;

                var parent = commit.Parents[0];
                if (!lane.IsPrimary && ShouldStopAtParent(parent, lane.Key, childCountByParent, branchHeads))
                {
                    lane.ForkParent = parent;
                    break;
                }

                current = parent;
            }
        }

        private static bool ShouldStopAtParent(
            string parent,
            string branchKey,
            Dictionary<string, int> childCountByParent,
            Dictionary<string, List<string>> branchHeads)
        {
            if (branchHeads.TryGetValue(parent, out var branches))
            {
                foreach (var key in branches)
                {
                    if (!key.Equals(branchKey, StringComparison.Ordinal))
                        return true;
                }
            }

            return childCountByParent.GetValueOrDefault(parent) > 1;
        }

        private static int GetBranchColor(Branch branch, int fallback, IReadOnlyDictionary<string, int> branchColors)
        {
            if (branchColors != null &&
                branch != null &&
                branchColors.TryGetValue(GetBranchKey(branch), out var assigned))
            {
                var color = assigned % s_penCount;
                return color < 0 ? color + s_penCount : color;
            }

            return fallback % s_penCount;
        }

        private static void AddLanePaths(
            CommitGraph graph,
            BranchLane lane,
            int color,
            bool isHighlighted,
            Dictionary<string, int> indexBySha,
            int headIndex,
            int commitCount)
        {
            var points = new List<(Point Point, bool IsLocal)>();
            for (var i = lane.Commits.Count - 1; i >= 0; i--)
            {
                var sha = lane.Commits[i];
                if (indexBySha.TryGetValue(sha, out var commitIndex))
                    points.Add((new Point(GetLaneX(lane.LaneIndex), commitIndex), IsLaneCommitLocal(lane, sha)));
            }

            if (points.Count == 0)
            {
                var emptyPath = CreateLanePath(lane, color, isHighlighted, lane.Branch.IsLocal);
                if (lane.Branch.IsCurrent && headIndex >= 0)
                    AddEmptyBranchPath(emptyPath, lane.LaneIndex, headIndex, commitCount);

                graph.Paths.Add(emptyPath);
                return;
            }

            var path = CreateLanePath(lane, color, isHighlighted, points[0].IsLocal);
            path.Points.Add(points[0].Point);
            var previous = points[0];

            for (var i = 1; i < points.Count; i++)
            {
                var current = points[i];
                if (current.IsLocal != path.IsLocal)
                {
                    graph.Paths.Add(path);
                    path = CreateLanePath(lane, color, isHighlighted, current.IsLocal);
                    path.Points.Add(previous.Point);
                }

                path.Points.Add(current.Point);
                previous = current;
            }

            graph.Paths.Add(path);
        }

        private static Path CreateLanePath(BranchLane lane, int color, bool isHighlighted, bool isLocal)
        {
            return new Path(color, isHighlighted)
            {
                Lane = lane.LaneIndex,
                BranchName = lane.Name,
                IsLocal = isLocal,
            };
        }

        private static bool IsLaneCommitLocal(BranchLane lane, string sha)
        {
            if (!lane.Branch.IsLocal)
                return false;

            return lane.UpstreamCommits == null || !lane.UpstreamCommits.Contains(sha);
        }

        private static void AddEmptyBranchPath(Path path, int lane, int headIndex, int commitCount)
        {
            var from = Math.Max(0, headIndex - 0.45);
            var to = Math.Min(Math.Max(0, commitCount - 1), headIndex + 0.45);

            if (Math.Abs(from - to) < 0.0001)
            {
                from = headIndex;
                to = headIndex + 0.45;
            }

            path.Points.Add(new Point(GetLaneX(lane), to));
            path.Points.Add(new Point(GetLaneX(lane), from));
        }

        private static bool IsLaneHighlighted(BranchLane lane, CommitGraphHighlighting highlighting, HashSet<string> highlightExtraCommits, Dictionary<string, Commit> commitBySha)
        {
            if (highlighting == CommitGraphHighlighting.All)
                return true;
            if (highlighting == CommitGraphHighlighting.CurrentBranchOnly)
                return lane.Branch.IsCurrent;

            foreach (var sha in lane.Commits)
            {
                if (highlightExtraCommits.Contains(sha))
                    return true;

                if (highlighting == CommitGraphHighlighting.CurrentBranchAndSelectedCommits &&
                    commitBySha.TryGetValue(sha, out var commit) &&
                    commit.IsMerged)
                    return true;
            }

            return highlighting == CommitGraphHighlighting.CurrentBranchAndSelectedCommits && lane.Branch.IsCurrent;
        }

        private static bool IsCommitHighlighted(Commit commit, BranchLane lane, CommitGraphHighlighting highlighting, HashSet<string> highlightExtraCommits)
        {
            if (highlighting == CommitGraphHighlighting.All)
                return true;
            if (highlighting == CommitGraphHighlighting.CurrentBranchOnly)
                return lane.Branch.IsCurrent || commit.IsMerged;
            if (highlighting == CommitGraphHighlighting.SelectedCommitsOnly)
                return highlightExtraCommits.Contains(commit.SHA);

            return lane.Branch.IsCurrent || commit.IsMerged || highlightExtraCommits.Contains(commit.SHA);
        }

        private static bool IsFastForwardBranchHighlighted(FastForwardBranch branch, CommitGraphHighlighting highlighting, HashSet<string> highlightExtraCommits, Dictionary<string, Commit> commitBySha)
        {
            if (highlighting == CommitGraphHighlighting.All)
                return true;
            if (highlighting == CommitGraphHighlighting.CurrentBranchOnly)
                return branch.Branch.IsCurrent;
            if (highlighting == CommitGraphHighlighting.SelectedCommitsOnly)
                return highlightExtraCommits.Contains(branch.Head);

            return branch.Branch.IsCurrent ||
                highlightExtraCommits.Contains(branch.Head) ||
                (commitBySha.TryGetValue(branch.Head, out var commit) && commit.IsMerged);
        }

        private static bool TryFindPoint(Dictionary<string, List<CommitPoint>> pointsBySha, string sha, out CommitPoint point)
        {
            point = null;
            if (!pointsBySha.TryGetValue(sha, out var points) || points.Count == 0)
                return false;

            foreach (var p in points)
            {
                if (p.IsCurrent)
                {
                    point = p;
                    return true;
                }
            }

            foreach (var p in points)
            {
                if (p.IsPrimary)
                {
                    point = p;
                    return true;
                }
            }

            point = points[0];
            return true;
        }

        private static string GetBranchName(Branch branch)
        {
            if (branch == null)
                return "HEAD";

            if (!branch.IsLocal && !string.IsNullOrEmpty(branch.Remote))
                return branch.FriendlyName;

            if (!string.IsNullOrEmpty(branch.Name))
                return branch.Name;

            if (!string.IsNullOrEmpty(branch.FullName))
                return branch.FullName;

            return !string.IsNullOrEmpty(branch.Head) && branch.Head.Length > 10 ? branch.Head[..10] : "HEAD";
        }

        private static string GetBranchKey(Branch branch)
        {
            if (branch == null)
                return string.Empty;

            if (!string.IsNullOrEmpty(branch.FullName))
                return branch.FullName;

            return $"{(branch.IsLocal ? "local" : "remote")}:{GetBranchName(branch)}:{branch.Head}";
        }

        private class BranchLane
        {
            public Branch Branch { get; set; } = null;
            public Branch UpstreamBranch { get; set; } = null;
            public HashSet<string> UpstreamCommits { get; set; } = null;
            public string Key { get; set; } = string.Empty;
            public int LaneIndex { get; set; } = 0;
            public string Name { get; set; } = string.Empty;
            public bool IsPrimary { get; set; } = false;
            public bool OwnsHeadChain { get; set; } = false;
            public string ForkParent { get; set; } = null;
            public List<string> Commits { get; } = [];
        }

        private class FastForwardBranch
        {
            public Branch Branch { get; set; } = null;
            public string Key { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string Head { get; set; } = string.Empty;
        }

        private class CommitPoint
        {
            public Point Point;
            public int Lane;
            public int Color;
            public bool IsPrimary;
            public bool IsCurrent;
            public bool IsLocal;
        }

        public static CommitGraph GenerateActivePathGraph(List<Commit> commits, bool recalculateMergeState, bool firstParentOnlyEnabled, CommitGraphHighlighting highlighting, HashSet<string> highlightExtraCommits)
        {
            const double unitWidth = 12;
            const double halfWidth = 6;
            const double unitHeight = 1;
            const double halfHeight = 0.5;

            var temp = new CommitGraph();
            var unsolved = new List<PathHelper>();
            var ended = new List<PathHelper>();
            var offsetY = -halfHeight;
            var merged = new HashSet<string>();
            int nextLane = 0;

            Func<int, double> getX = (lane) => 4 - halfWidth + (lane + 1) * unitWidth;

            Commit primaryCommit = null;
            foreach (var commit in commits)
            {
                foreach (var d in commit.Decorators)
                {
                    if (d.Type is DecoratorType.LocalBranchHead or DecoratorType.CurrentBranchHead or DecoratorType.RemoteBranchHead)
                    {
                        if (d.Name == "main" || d.Name == "master" || d.Name == "origin/main" || d.Name == "origin/master" || d.Name == "refs/heads/main" || d.Name == "refs/heads/master")
                        {
                            primaryCommit = commit;
                            break;
                        }
                    }
                }
                if (primaryCommit != null)
                    break;
            }

            if (primaryCommit == null)
                primaryCommit = commits.Find(c => c.IsCurrentHead);

            if (primaryCommit != null)
            {
                var headIndex = commits.IndexOf(primaryCommit);
                
                bool isHeadHighlighted = false;
                if (highlighting == CommitGraphHighlighting.All)
                {
                    isHeadHighlighted = true;
                }
                else if (highlighting == CommitGraphHighlighting.CurrentBranchOnly)
                {
                    isHeadHighlighted = primaryCommit.IsMerged;
                }
                else if (highlighting == CommitGraphHighlighting.SelectedCommitsOnly)
                {
                    isHeadHighlighted = highlightExtraCommits.Contains(primaryCommit.SHA);
                }
                else // CurrentBranchAndSelectedCommits
                {
                    isHeadHighlighted = primaryCommit.IsMerged || highlightExtraCommits.Contains(primaryCommit.SHA);
                }

                var headPath = new PathHelper(primaryCommit.SHA, isHeadHighlighted, 0, new Point(getX(0), -halfHeight));
                
                string branchName = null;
                foreach (var d in primaryCommit.Decorators)
                {
                    if (d.Type is DecoratorType.CurrentBranchHead or DecoratorType.LocalBranchHead or DecoratorType.RemoteBranchHead)
                    {
                        branchName = d.Name;
                        break;
                    }
                }
                headPath.Path.BranchName = branchName ?? "/main";
                
                unsolved.Add(headPath);
                temp.Paths.Add(headPath.Path);
                nextLane = 1;
            }

            foreach (var commit in commits)
            {
                PathHelper major = null;

                // Update merge state of this commit.
                if (recalculateMergeState)
                {
                    if (commit.IsMerged)
                    {
                        merged.Remove(commit.SHA);
                        foreach (var p in commit.Parents)
                            merged.Add(p);
                    }
                    else if (merged.Remove(commit.SHA))
                    {
                        commit.IsMerged = true;
                        foreach (var p in commit.Parents)
                            merged.Add(p);
                    }
                }

                // Update current y offset
                offsetY += unitHeight;

                // Find first curves that links to this commit and marks others that links to this commit ended.
                var isHighlighted = false;
                foreach (var l in unsolved)
                {
                    if (l.Next.Equals(commit.SHA, StringComparison.Ordinal))
                    {
                        if (major == null)
                        {
                            major = l;
                            isHighlighted = major.IsHighlighted;

                            if (commit.Parents.Count > 0)
                            {
                                major.Next = commit.Parents[0];
                                major.Goto(getX(major.Lane), offsetY, halfHeight);
                            }
                            else
                            {
                                major.End(getX(major.Lane), offsetY, halfHeight);
                                ended.Add(l);
                            }
                        }
                        else
                        {
                            l.End(getX(major.Lane), offsetY, halfHeight);
                            ended.Add(l);

                            if (!isHighlighted && l.IsHighlighted)
                                isHighlighted = true;
                        }
                    }
                    else
                    {
                        l.Pass(getX(l.Lane), offsetY, halfHeight);
                    }
                }

                // Remove ended curves from unsolved
                foreach (var l in ended)
                {
                    unsolved.Remove(l);
                }
                ended.Clear();

                // Calculate highlighted state
                if (!isHighlighted)
                {
                    if (highlighting == CommitGraphHighlighting.All)
                    {
                        isHighlighted = true;
                    }
                    else if (highlighting == CommitGraphHighlighting.CurrentBranchOnly)
                    {
                        isHighlighted = commit.IsMerged;
                    }
                    else if (highlighting == CommitGraphHighlighting.SelectedCommitsOnly)
                    {
                        isHighlighted = highlightExtraCommits.Remove(commit.SHA);
                        if (isHighlighted)
                        {
                            foreach (var p in commit.Parents)
                                highlightExtraCommits.Add(p);
                        }
                    }
                    else
                    {
                        if (commit.IsMerged)
                        {
                            isHighlighted = true;
                        }
                        else if (highlightExtraCommits.Remove(commit.SHA))
                        {
                            isHighlighted = true;
                            foreach (var p in commit.Parents)
                                highlightExtraCommits.Add(p);
                        }
                    }
                }
                commit.IsHighlightedInGraph = isHighlighted;

                // If no path found, create new curve for branch head
                // Otherwise, create new curve for new merged commit
                if (major == null)
                {
                    int lane = nextLane++;
                    double x = getX(lane);
                    if (commit.Parents.Count > 0)
                    {
                        major = new PathHelper(commit.Parents[0], isHighlighted, lane, new Point(x, offsetY));
                        unsolved.Add(major);
                        temp.Paths.Add(major.Path);
                    }
                    else
                    {
                        major = new PathHelper("", isHighlighted, lane, new Point(x, offsetY));
                    }
                }
                else if (isHighlighted && !major.IsHighlighted && commit.Parents.Count > 0)
                {
                    major.Highlight();
                    temp.Paths.Add(major.Path);
                }

                // Set branch name if possible
                if (major.Path.BranchName == null)
                {
                    string branchName = null;
                    foreach (var d in commit.Decorators)
                    {
                        if (d.Type is DecoratorType.CurrentBranchHead or DecoratorType.LocalBranchHead or DecoratorType.RemoteBranchHead)
                        {
                            branchName = d.Name;
                            break;
                        }
                    }
                    major.Path.BranchName = branchName ?? (major.Lane == 0 ? "/main" : $"/main/branch-{major.Lane}");
                }

                // Calculate link position of this commit.
                var position = new Point(getX(major.Lane), offsetY);
                var dotColor = major.Lane % s_penCount;
                var anchor = new Dot() { Center = position, Color = dotColor, IsHighlighted = isHighlighted };
                if (commit.IsCurrentHead)
                    anchor.Type = DotType.Head;
                else if (commit.Parents.Count > 1)
                    anchor.Type = DotType.Merge;
                else
                    anchor.Type = DotType.Default;
                temp.Dots.Add(anchor);

                // Deal with other parents (the first parent has been processed)
                if (!firstParentOnlyEnabled)
                {
                    for (int j = 1; j < commit.Parents.Count; j++)
                    {
                        var parentHash = commit.Parents[j];
                        var parent = unsolved.Find(x => x.Next.Equals(parentHash, StringComparison.Ordinal));
                        if (parent != null)
                        {
                            if (isHighlighted && !parent.IsHighlighted)
                            {
                                parent.Goto(getX(parent.Lane), offsetY + halfHeight, halfHeight);
                                parent.Highlight();
                                temp.Paths.Add(parent.Path);
                            }

                            temp.Links.Add(new Link
                            {
                                Start = position,
                                End = new Point(getX(parent.Lane), offsetY + halfHeight),
                                Control = new Point(getX(parent.Lane), position.Y),
                                Color = parent.Lane % s_penCount,
                                IsHighlighted = isHighlighted,
                            });
                        }
                        else
                        {
                            int lane = nextLane++;
                            double x = getX(lane);

                            // Create new curve for parent commit that not includes before
                            var l = new PathHelper(parentHash, isHighlighted, lane, position, new Point(x, position.Y + halfHeight));
                            unsolved.Add(l);
                            temp.Paths.Add(l.Path);
                        }
                    }
                }

                // Calculate max active X in this row to determine LeftMargin
                double maxActiveX = 4 - halfWidth;
                foreach (var l in unsolved)
                {
                    if (l.LastX > maxActiveX)
                        maxActiveX = l.LastX;
                }
                if (major.LastX > maxActiveX)
                    maxActiveX = major.LastX;

                // Margins & colors (used by Views.Histories).
                commit.Color = dotColor;
                commit.LeftMargin = maxActiveX + halfWidth + 6;
            }

            // Deal with curves haven't ended yet.
            for (var i = 0; i < unsolved.Count; i++)
            {
                var path = unsolved[i];
                var endY = (commits.Count - 0.5) * unitHeight;

                if (path.Path.Points.Count == 1 && Math.Abs(path.Path.Points[0].Y - endY) < 0.0001)
                    continue;

                path.End(getX(path.Lane), endY + halfHeight, halfHeight);
            }
            unsolved.Clear();

            temp.TotalLanes = nextLane;
            return temp;
        }
    }
}
