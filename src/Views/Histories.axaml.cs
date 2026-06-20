using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class HistoriesLayout : Grid
    {
        public static readonly StyledProperty<bool> UseHorizontalProperty =
            AvaloniaProperty.Register<HistoriesLayout, bool>(nameof(UseHorizontal));

        public bool UseHorizontal
        {
            get => GetValue(UseHorizontalProperty);
            set => SetValue(UseHorizontalProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(Grid);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == UseHorizontalProperty && IsLoaded)
                RefreshLayout();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            RefreshLayout();
        }

        private void RefreshLayout()
        {
            if (UseHorizontal)
            {
                var rowSpan = RowDefinitions.Count;
                for (int i = 0; i < Children.Count; i++)
                {
                    var child = Children[i];
                    child.SetValue(RowProperty, 0);
                    child.SetValue(RowSpanProperty, rowSpan);
                    child.SetValue(ColumnProperty, i);
                    child.SetValue(ColumnSpanProperty, 1);

                    if (child is GridSplitter splitter)
                        splitter.BorderThickness = new Thickness(1, 0, 0, 0);
                }
            }
            else
            {
                var colSpan = ColumnDefinitions.Count;
                for (int i = 0; i < Children.Count; i++)
                {
                    var child = Children[i];
                    child.SetValue(RowProperty, i);
                    child.SetValue(RowSpanProperty, 1);
                    child.SetValue(ColumnProperty, 0);
                    child.SetValue(ColumnSpanProperty, colSpan);

                    if (child is GridSplitter splitter)
                        splitter.BorderThickness = new Thickness(0, 1, 0, 0);
                }
            }
        }
    }

    public class HistoriesCommitList : DataGrid
    {
        public static readonly StyledProperty<int> TotalCommitsProperty =
            AvaloniaProperty.Register<HistoriesCommitList, int>(nameof(TotalCommits), 0);

        public int TotalCommits
        {
            get => GetValue(TotalCommitsProperty);
            set => SetValue(TotalCommitsProperty, value);
        }

        public static readonly StyledProperty<List<Models.Commit>> SelectedCommitsProperty =
            AvaloniaProperty.Register<HistoriesCommitList, List<Models.Commit>>(nameof(SelectedCommits), []);

        public List<Models.Commit> SelectedCommits
        {
            get => GetValue(SelectedCommitsProperty);
            set => SetValue(SelectedCommitsProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(DataGrid);

        public HistoriesCommitList()
        {
            SelectionMode = DataGridSelectionMode.Extended;
            CanUserReorderColumns = false;
            CanUserResizeColumns = false;
            CanUserSortColumns = false;
            AutoGenerateColumns = false;
            IsReadOnly = true;
            HeadersVisibility = DataGridHeadersVisibility.Column;
            ClipboardCopyMode = DataGridClipboardCopyMode.None;
            Focusable = false;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            ApplySelection();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedCommitsProperty && IsLoaded && !_ignoreSelectionChanged)
            {
                if (change.OldValue is List<Models.Commit> { Count: 1 } old &&
                    change.NewValue is List<Models.Commit> { Count: 1 } cur &&
                    old[0] == cur[0])
                    ScrollIntoView(old[0], null);
                else
                    ApplySelection();
            }
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);

            if (ItemsSource is not IList<Models.Commit> items)
                return;

            var commits = new List<Models.Commit>();
            foreach (var o in SelectedItems)
            {
                if (o is Models.Commit c)
                    commits.Add(c);
            }

            if (e.AddedItems.Count == 1)
            {
                ScrollIntoView(e.AddedItems[0], null);
            }
            else if (e.AddedItems.Count > 1 && e.AddedItems[0] is Models.Commit first)
            {
                var firstIndex = items.IndexOf(first);
                if (firstIndex > 0)
                {
                    var prev = items[firstIndex - 1];
                    if (commits.Contains(prev))
                        ScrollIntoView(e.AddedItems[^1], null);
                    else
                        ScrollIntoView(first, null);
                }
            }

            if (!_ignoreSelectionChanged)
            {
                _ignoreSelectionChanged = true;

                var old = SelectedCommits;
                if (old.Count != commits.Count)
                {
                    SetCurrentValue(SelectedCommitsProperty, commits);
                }
                else if (commits.Count > 0)
                {
                    var set = new HashSet<string>();
                    foreach (var c in old)
                        set.Add(c.SHA);

                    var equals = true;
                    foreach (var c in commits)
                    {
                        if (!set.Contains(c.SHA))
                        {
                            equals = false;
                            break;
                        }
                    }

                    if (!equals)
                        SetCurrentValue(SelectedCommitsProperty, commits);
                }

                _ignoreSelectionChanged = false;
            }
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.Alt)
            {
                if (e.Key == Key.Up)
                {
                    e.Handled = true;
                    await this.FindAncestorOfType<Histories>()?.GotoChild();
                }
                else if (e.Key == Key.Down)
                {
                    e.Handled = true;
                    await this.FindAncestorOfType<Histories>()?.GotoParent();
                }
            }
            else if (e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control) &&
                SelectedItems is { Count: > 0 } selected &&
                e.Key == Key.C)
            {
                var builder = new StringBuilder();
                foreach (var item in selected)
                {
                    if (item is Models.Commit commit)
                        builder.Append(commit.SHA.AsSpan(0, 10)).Append(" - ").AppendLine(commit.Subject);
                }

                e.Handled = true;
                await this.CopyTextAsync(builder.ToString());
            }

            if (!e.Handled)
                base.OnKeyDown(e);
        }

        private void ApplySelection()
        {
            _ignoreSelectionChanged = true;

            if (SelectedCommits == null || SelectedCommits.Count == 0)
            {
                SelectedItems.Clear();
            }
            else if (SelectedCommits.Count == TotalCommits)
            {
                SelectAll();
            }
            else
            {
                IncrNoSelectionChangeCount();
                SelectedItems.Clear();
                foreach (var c in SelectedCommits)
                    SelectedItems.Add(c);
                DecrNoSelectionChangeCount();
            }

            _ignoreSelectionChanged = false;
        }

        private void IncrNoSelectionChangeCount()
        {
            var property = typeof(DataGrid).GetProperty("NoSelectionChangeCount", BindingFlags.Instance | BindingFlags.NonPublic);
            if (property != null)
            {
                var old = (int)property.GetValue(this);
                property.SetValue(this, old + 1);
            }
        }

        private void DecrNoSelectionChangeCount()
        {
            var property = typeof(DataGrid).GetProperty("NoSelectionChangeCount", BindingFlags.Instance | BindingFlags.NonPublic);
            if (property != null)
            {
                var old = (int)property.GetValue(this);
                property.SetValue(this, old - 1);
            }
        }

        private bool _ignoreSelectionChanged = false;
    }

    public partial class Histories : UserControl
    {
        public static readonly StyledProperty<Models.Branch> CurrentBranchProperty =
            AvaloniaProperty.Register<Histories, Models.Branch>(nameof(CurrentBranch));

        public Models.Branch CurrentBranch
        {
            get => GetValue(CurrentBranchProperty);
            set => SetValue(CurrentBranchProperty, value);
        }

        public static readonly StyledProperty<Models.Bisect> BisectProperty =
            AvaloniaProperty.Register<Histories, Models.Bisect>(nameof(Bisect));

        public Models.Bisect Bisect
        {
            get => GetValue(BisectProperty);
            set => SetValue(BisectProperty, value);
        }

        public static readonly StyledProperty<AvaloniaList<Models.IssueTracker>> IssueTrackersProperty =
            AvaloniaProperty.Register<Histories, AvaloniaList<Models.IssueTracker>>(nameof(IssueTrackers));

        public AvaloniaList<Models.IssueTracker> IssueTrackers
        {
            get => GetValue(IssueTrackersProperty);
            set => SetValue(IssueTrackersProperty, value);
        }

        public static readonly StyledProperty<bool> IsScrollToTopVisibleProperty =
            AvaloniaProperty.Register<Histories, bool>(nameof(IsScrollToTopVisible));

        public bool IsScrollToTopVisible
        {
            get => GetValue(IsScrollToTopVisibleProperty);
            set => SetValue(IsScrollToTopVisibleProperty, value);
        }

        public static readonly StyledProperty<bool> IsDetailsPanelExpandedProperty =
            AvaloniaProperty.Register<Histories, bool>(nameof(IsDetailsPanelExpanded), true);

        public bool IsDetailsPanelExpanded
        {
            get => GetValue(IsDetailsPanelExpandedProperty);
            set => SetValue(IsDetailsPanelExpandedProperty, value);
        }

        public Histories()
        {
            InitializeComponent();

            BranchExplorerScrollViewer.AddHandler(PointerPressedEvent, OnBranchExplorerScrollPointerPressed, RoutingStrategies.Tunnel, true);
            BranchExplorerScrollViewer.AddHandler(PointerMovedEvent, OnBranchExplorerScrollPointerMoved, RoutingStrategies.Tunnel, true);
            BranchExplorerScrollViewer.AddHandler(PointerReleasedEvent, OnBranchExplorerScrollPointerReleased, RoutingStrategies.Tunnel, true);
        }

        public async Task GotoParent()
        {
            if (DataContext is not ViewModels.Histories vm)
                return;

            if (vm.SelectedCommits is not { Count: 1 } selected)
                return;

            if (selected[0] is not Models.Commit { Parents.Count: > 0 } commit)
                return;

            if (commit.Parents.Count == 1)
            {
                vm.NavigateTo(commit.Parents[0]);
                return;
            }

            var parents = new List<Models.Commit>();
            foreach (var sha in commit.Parents)
            {
                var c = await vm.GetCommitAsync(sha);
                if (c != null)
                    parents.Add(c);
            }

            if (parents.Count == 1)
            {
                vm.NavigateTo(parents[0].SHA);
            }
            else if (parents.Count > 1 && TopLevel.GetTopLevel(this) is Window owner)
            {
                var dialog = new GotoRevisionSelector();
                dialog.RevisionList.ItemsSource = parents;

                var c = await dialog.ShowDialog<Models.Commit>(owner);
                if (c != null)
                    vm.NavigateTo(c.SHA);
            }
        }

        public async Task GotoChild()
        {
            if (DataContext is not ViewModels.Histories vm)
                return;

            if (vm.SelectedCommits is not { Count: 1 } selected)
                return;

            if (selected[0] is not Models.Commit { Parents.Count: > 0 } commit)
                return;

            var children = new List<Models.Commit>();
            var sha = commit.SHA;
            foreach (var c in vm.Commits)
            {
                foreach (var p in c.Parents)
                {
                    if (sha.StartsWith(p, StringComparison.Ordinal))
                        children.Add(c);
                }

                if (sha.Equals(c.SHA, StringComparison.Ordinal))
                    break;
            }

            if (children.Count == 1)
            {
                vm.NavigateTo(children[0].SHA);
            }
            else if (children.Count > 1 && TopLevel.GetTopLevel(this) is Window owner)
            {
                var dialog = new GotoRevisionSelector();
                dialog.RevisionList.ItemsSource = children;

                var c = await dialog.ShowDialog<Models.Commit>(owner);
                if (c != null)
                    vm.NavigateTo(c.SHA);
            }
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
        }

        public void ShowContextMenuForCommit(Models.Commit commit, Control target, string branchKey = null)
        {
            var repoView = this.FindAncestorOfType<Repository>();
            if (repoView is not { DataContext: ViewModels.Repository repo })
                return;

            var menu = CreateContextMenuForSingleCommit(repo, commit, branchKey);
            if (menu != null)
                menu.Open(target);
        }

        public async Task DoubleTapCommit(Models.Commit commit)
        {
            if (DataContext is ViewModels.Histories histories)
            {
                await histories.CheckoutBranchByCommitAsync(commit);
            }
        }

        private void OnScrollViewerScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                bool showScrollToRight = scrollViewer.Offset.X < scrollViewer.Extent.Width - scrollViewer.Viewport.Width - 100;
                SetCurrentValue(IsScrollToTopVisibleProperty, showScrollToRight);
            }
        }

        private string _lastCommitsRepo = null;

        private void OnScrollViewerLayoutUpdated(object sender, EventArgs e)
        {
            if (sender is ScrollViewer scrollViewer && DataContext is ViewModels.Histories vm)
            {
                if (vm.Commits != null && vm.Commits.Count > 0)
                {
                    string currentRepo = vm.Commits[0].SHA;
                    if (_lastCommitsRepo != currentRepo)
                    {
                        _lastCommitsRepo = currentRepo;
                        scrollViewer.Offset = new Point(scrollViewer.Extent.Width - scrollViewer.Viewport.Width, scrollViewer.Offset.Y);
                    }
                }
            }
        }

        private void OnScrollToTopPointerPressed(object sender, PointerPressedEventArgs e)
        {
            var scrollViewer = this.FindDescendantOfType<ScrollViewer>();
            if (scrollViewer != null)
            {
                scrollViewer.Offset = new Point(scrollViewer.Extent.Width - scrollViewer.Viewport.Width, scrollViewer.Offset.Y);
            }
        }

        private void OnBranchExplorerScrollPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
                return;

            var point = e.GetCurrentPoint(scrollViewer);
            if (!point.Properties.IsLeftButtonPressed || IsBranchExplorerScrollBarEvent(e.Source))
                return;

            var maxX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
            var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
            if (maxX <= 0 && maxY <= 0)
                return;

            _branchExplorerPanPressed = true;
            _branchExplorerIsPanning = false;
            _branchExplorerPanStartPosition = e.GetPosition(scrollViewer);
            _branchExplorerPanStartOffset = scrollViewer.Offset;
            e.Pointer.Capture(scrollViewer);
        }

        private void OnBranchExplorerScrollPointerMoved(object sender, PointerEventArgs e)
        {
            if (!_branchExplorerPanPressed || sender is not ScrollViewer scrollViewer)
                return;

            var point = e.GetCurrentPoint(scrollViewer);
            if (!point.Properties.IsLeftButtonPressed)
            {
                EndBranchExplorerPan(scrollViewer, e);
                return;
            }

            var current = e.GetPosition(scrollViewer);
            var dx = current.X - _branchExplorerPanStartPosition.X;
            var dy = current.Y - _branchExplorerPanStartPosition.Y;

            if (!_branchExplorerIsPanning)
            {
                if (dx * dx + dy * dy < BranchExplorerDragPanThreshold * BranchExplorerDragPanThreshold)
                    return;

                _branchExplorerIsPanning = true;
                scrollViewer.Cursor = new Cursor(StandardCursorType.SizeAll);
            }

            var maxX = Math.Max(0, scrollViewer.Extent.Width - scrollViewer.Viewport.Width);
            var maxY = Math.Max(0, scrollViewer.Extent.Height - scrollViewer.Viewport.Height);
            var x = Math.Clamp(_branchExplorerPanStartOffset.X - dx, 0, maxX);
            var y = Math.Clamp(_branchExplorerPanStartOffset.Y - dy, 0, maxY);
            scrollViewer.Offset = new Vector(x, y);

            e.Handled = true;
        }

        private void OnBranchExplorerScrollPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (!_branchExplorerPanPressed || sender is not ScrollViewer scrollViewer)
                return;

            var wasPanning = _branchExplorerIsPanning;
            EndBranchExplorerPan(scrollViewer, e);
            e.Handled = wasPanning;
        }

        private static bool IsBranchExplorerScrollBarEvent(object source)
        {
            if (source is not Control control)
                return false;

            return control is ScrollBar || control.FindAncestorOfType<ScrollBar>() != null;
        }

        private void EndBranchExplorerPan(ScrollViewer scrollViewer, PointerEventArgs e)
        {
            e.Pointer.Capture(null);
            _branchExplorerPanPressed = false;
            _branchExplorerIsPanning = false;
            scrollViewer.Cursor = null;
        }

        private void OnTabHeaderPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (ViewModels.Preferences.Instance.UseTwoColumnsLayoutInHistories)
                return;

            if (DataContext is not ViewModels.Histories vm)
                return;

            if (vm.IsCollapseDetails)
                vm.IsCollapseDetails = false;
        }

        private void OnOpenDetailsAsStandalone(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Histories vm)
            {
                if (vm.DetailContext is ViewModels.CommitDetail detail)
                {
                    var standalone = new CommitDetailStandalone();
                    standalone.DataContext = detail.Clone();
                    standalone.Show(TopLevel.GetTopLevel(this) as Window);
                }
                else if (vm.DetailContext is ViewModels.RevisionCompare compare)
                {
                    var standalone = new RevisionCompareStandalone();
                    standalone.DataContext = compare.Clone();
                    standalone.Show(TopLevel.GetTopLevel(this) as Window);
                }
            }

            e.Handled = true;
        }

        private ContextMenu CreateContextMenuForMultipleCommits(ViewModels.Repository repo, List<Models.Commit> selected)
        {
            var canCherryPick = true;
            var canMerge = true;

            foreach (var c in selected)
            {
                if (c.IsMerged)
                {
                    canMerge = false;
                    canCherryPick = false;
                }
                else if (c.Parents.Count > 1)
                {
                    canCherryPick = false;
                }
            }

            var menu = new ContextMenu();

            if (!repo.IsBare)
            {
                if (canCherryPick)
                {
                    var cherryPick = new MenuItem();
                    cherryPick.Header = App.Text("CommitCM.CherryPickMultiple");
                    cherryPick.Icon = this.CreateMenuIcon("Icons.CherryPick");
                    cherryPick.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.CherryPick(repo, selected));
                        e.Handled = true;
                    };
                    menu.Items.Add(cherryPick);
                }

                if (canMerge)
                {
                    var merge = new MenuItem();
                    merge.Header = App.Text("CommitCM.MergeMultiple");
                    merge.Icon = this.CreateMenuIcon("Icons.Merge");
                    merge.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.MergeMultiple(repo, selected));
                        e.Handled = true;
                    };
                    menu.Items.Add(merge);
                }

                if (canCherryPick || canMerge)
                    menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var saveToPatch = new MenuItem();
            saveToPatch.Icon = this.CreateMenuIcon("Icons.Save");
            saveToPatch.Header = App.Text("CommitCM.SaveAsPatch");
            saveToPatch.Click += async (_, e) =>
            {
                var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
                if (storageProvider == null)
                    return;

                var options = new FolderPickerOpenOptions() { AllowMultiple = false };
                try
                {
                    var picker = await storageProvider.OpenFolderPickerAsync(options);
                    if (picker.Count == 1)
                    {
                        var folder = picker[0];
                        var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder.Path.ToString();
                        var succ = false;
                        for (var i = 0; i < selected.Count; i++)
                        {
                            succ = await repo.SaveCommitAsPatchAsync(selected[i], folderPath, i);
                            if (!succ)
                                break;
                        }

                        if (succ)
                            repo.SendNotification(App.Text("SaveAsPatchSuccess"));
                    }
                }
                catch (Exception exception)
                {
                    repo.SendNotification($"Failed to save as patch: {exception.Message}", true);
                }

                e.Handled = true;
            };
            menu.Items.Add(saveToPatch);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var copyInfos = new MenuItem();
            copyInfos.Header = App.Text("CommitCM.CopySHA") + " - " + App.Text("CommitCM.CopySubject");
            copyInfos.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyInfos.Click += async (_, e) =>
            {
                var builder = new StringBuilder();
                foreach (var c in selected)
                    builder.Append(c.SHA.AsSpan(0, 10)).Append(" - ").AppendLine(c.Subject);

                await this.CopyTextAsync(builder.ToString());
                e.Handled = true;
            };

            var copyShas = new MenuItem();
            copyShas.Header = App.Text("CommitCM.CopySHA");
            copyShas.Icon = this.CreateMenuIcon("Icons.Hash");
            copyShas.Click += async (_, e) =>
            {
                var builder = new StringBuilder();
                foreach (var c in selected)
                    builder.AppendLine(c.SHA);

                await this.CopyTextAsync(builder.ToString());
                e.Handled = true;
            };

            var copySubjects = new MenuItem();
            copySubjects.Header = App.Text("CommitCM.CopySubject");
            copySubjects.Icon = this.CreateMenuIcon("Icons.Subject");
            copySubjects.Click += async (_, e) =>
            {
                var builder = new StringBuilder();
                foreach (var c in selected)
                    builder.AppendLine(c.Subject);

                await this.CopyTextAsync(builder.ToString());
                e.Handled = true;
            };

            var copyMessage = new MenuItem();
            copyMessage.Header = App.Text("CommitCM.CopyCommitMessage");
            copyMessage.Icon = this.CreateMenuIcon("Icons.Message");
            copyMessage.Click += async (_, e) =>
            {
                var vm = DataContext as ViewModels.Histories;
                var messages = new List<string>();
                foreach (var c in selected)
                {
                    var message = await vm!.GetCommitFullMessageAsync(c);
                    messages.Add(message);
                }

                await this.CopyTextAsync(string.Join("\n-----\n", messages));
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = this.CreateMenuIcon("Icons.Copy");
            copy.Items.Add(copyInfos);
            copy.Items.Add(new MenuItem() { Header = "-" });
            copy.Items.Add(copyShas);
            copy.Items.Add(copySubjects);
            copy.Items.Add(copyMessage);
            menu.Items.Add(copy);
            return menu;
        }

        private ContextMenu CreateContextMenuForSingleCommit(ViewModels.Repository repo, Models.Commit commit, string branchKey = null)
        {
            var current = repo.CurrentBranch;
            var vm = DataContext as ViewModels.Histories;
            if (current == null || vm == null)
                return null;

            var menu = new ContextMenu();
            var tags = new List<Models.Tag>();
            var isHead = commit.IsCurrentHead;

            if (commit.HasDecorators)
            {
                foreach (var d in commit.Decorators)
                {
                    switch (d.Type)
                    {
                        case Models.DecoratorType.CurrentBranchHead:
                            FillCurrentBranchMenu(menu, repo, current);
                            break;
                        case Models.DecoratorType.LocalBranchHead:
                            var lb = repo.Branches.Find(x => x.IsLocal && d.Name.Equals(x.Name, StringComparison.Ordinal));
                            FillOtherLocalBranchMenu(menu, repo, lb, current);
                            break;
                        case Models.DecoratorType.RemoteBranchHead:
                            var rb = repo.Branches.Find(x => !x.IsLocal && d.Name.Equals(x.FriendlyName, StringComparison.Ordinal));
                            FillRemoteBranchMenu(menu, repo, rb, current);
                            break;
                        case Models.DecoratorType.Tag:
                            var t = repo.Tags.Find(x => d.Name.Equals(x.Name, StringComparison.Ordinal));
                            if (t != null)
                                tags.Add(t);
                            break;
                    }
                }

                if (menu.Items.Count > 0)
                    menu.Items.Add(new MenuItem() { Header = "-" });
            }

            if (tags.Count > 0)
            {
                foreach (var tag in tags)
                    FillTagMenu(menu, repo, tag, current);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var createBranch = new MenuItem();
            createBranch.Icon = this.CreateMenuIcon("Icons.Branch.Add");
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+B" : "Ctrl+Shift+B";
            createBranch.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.CreateBranch(repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(createBranch);

            var createTag = new MenuItem();
            createTag.Icon = this.CreateMenuIcon("Icons.Tag.Add");
            createTag.Header = App.Text("CreateTag");
            createTag.Tag = OperatingSystem.IsMacOS() ? "⌘+⇧+T" : "Ctrl+Shift+T";
            createTag.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.CreateTag(repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(createTag);
            menu.Items.Add(new MenuItem() { Header = "-" });

            if (!repo.IsBare)
            {
                var target = commit.GetFriendlyName();
                if (target.Length > 40)
                    target = commit.SHA.Substring(0, 10);

                if (!isHead)
                {
                    var reset = new MenuItem();
                    reset.Header = App.Text("CommitCM.Reset", current.Name, target);
                    reset.Icon = this.CreateMenuIcon("Icons.Reset");
                    reset.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.Reset(repo, current, commit));
                        e.Handled = true;
                    };
                    menu.Items.Add(reset);
                }

                if (!commit.IsMerged)
                {
                    var rebase = new MenuItem();
                    rebase.Header = App.Text("CommitCM.Rebase", current.Name, target);
                    rebase.Icon = this.CreateMenuIcon("Icons.Rebase");
                    rebase.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.Rebase(repo, current, commit));
                        e.Handled = true;
                    };
                    menu.Items.Add(rebase);

                    var merge = new MenuItem();
                    merge.Header = App.Text("BranchCM.Merge", target, current.Name);
                    merge.Icon = this.CreateMenuIcon("Icons.Merge");
                    merge.Click += (_, e) =>
                    {
                        ShowMergeFromHerePopup(repo, current, commit);
                        e.Handled = true;
                    };
                    menu.Items.Add(merge);

                    var cherryPick = new MenuItem();
                    cherryPick.Header = App.Text("CommitCM.CherryPick");
                    cherryPick.Icon = this.CreateMenuIcon("Icons.CherryPick");
                    cherryPick.Click += async (_, e) =>
                    {
                        await vm.CherryPickAsync(commit);
                        e.Handled = true;
                    };
                    menu.Items.Add(cherryPick);
                }

                var revert = new MenuItem();
                revert.Header = App.Text("CommitCM.Revert");
                revert.Icon = this.CreateMenuIcon("Icons.Undo");
                revert.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.Revert(repo, commit));
                    e.Handled = true;
                };
                menu.Items.Add(revert);

                if (!isHead)
                {
                    var checkoutCommit = new MenuItem();
                    checkoutCommit.Header = App.Text("CommitCM.Checkout");
                    checkoutCommit.Icon = this.CreateMenuIcon("Icons.Detached");
                    checkoutCommit.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.CheckoutCommit(repo, commit));
                        e.Handled = true;
                    };
                    menu.Items.Add(checkoutCommit);
                }

                if (commit.IsMerged && commit.Parents.Count > 0)
                {
                    var interactiveRebase = new MenuItem();
                    interactiveRebase.Header = App.Text("CommitCM.InteractiveRebase");
                    interactiveRebase.Icon = this.CreateMenuIcon("Icons.InteractiveRebase");

                    if (!isHead)
                    {
                        var manually = new MenuItem();
                        manually.Header = App.Text("CommitCM.InteractiveRebase.Manually", current.Name, target);
                        manually.Icon = this.CreateMenuIcon("Icons.InteractiveRebase");
                        manually.Click += async (_, e) =>
                        {
                            await this.ShowDialogAsync(new ViewModels.InteractiveRebase(repo, commit));
                            e.Handled = true;
                        };

                        interactiveRebase.Items.Add(manually);
                        interactiveRebase.Items.Add(new MenuItem() { Header = "-" });
                    }

                    var reword = new MenuItem();
                    reword.Header = App.Text("CommitCM.InteractiveRebase.Reword");
                    reword.Icon = this.CreateMenuIcon("Icons.Rename");
                    reword.Click += async (_, e) =>
                    {
                        await InteractiveRebaseWithPrefillActionAsync(repo, commit, Models.InteractiveRebaseAction.Reword);
                        e.Handled = true;
                    };

                    var edit = new MenuItem();
                    edit.Header = App.Text("CommitCM.InteractiveRebase.Edit");
                    edit.Icon = this.CreateMenuIcon("Icons.Edit");
                    edit.Click += async (_, e) =>
                    {
                        await InteractiveRebaseWithPrefillActionAsync(repo, commit, Models.InteractiveRebaseAction.Edit);
                        e.Handled = true;
                    };

                    var squash = new MenuItem();
                    squash.Header = App.Text("CommitCM.InteractiveRebase.Squash");
                    squash.Icon = this.CreateMenuIcon("Icons.SquashIntoParent");
                    squash.Click += async (_, e) =>
                    {
                        await InteractiveRebaseWithPrefillActionAsync(repo, commit, Models.InteractiveRebaseAction.Squash);
                        e.Handled = true;
                    };

                    var fixup = new MenuItem();
                    fixup.Header = App.Text("CommitCM.InteractiveRebase.Fixup");
                    fixup.Icon = this.CreateMenuIcon("Icons.Fix");
                    fixup.Click += async (_, e) =>
                    {
                        await InteractiveRebaseWithPrefillActionAsync(repo, commit, Models.InteractiveRebaseAction.Fixup);
                        e.Handled = true;
                    };

                    var drop = new MenuItem();
                    drop.Header = App.Text("CommitCM.InteractiveRebase.Drop");
                    drop.Icon = this.CreateMenuIcon("Icons.Clear");
                    drop.Click += async (_, e) =>
                    {
                        await InteractiveRebaseWithPrefillActionAsync(repo, commit, Models.InteractiveRebaseAction.Drop);
                        e.Handled = true;
                    };

                    interactiveRebase.Items.Add(reword);
                    interactiveRebase.Items.Add(edit);
                    interactiveRebase.Items.Add(squash);
                    interactiveRebase.Items.Add(fixup);
                    interactiveRebase.Items.Add(drop);

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(interactiveRebase);
                }
                else
                {
                    var interactiveRebase = new MenuItem();
                    interactiveRebase.Header = App.Text("CommitCM.InteractiveRebase.Manually", current.Name, target);
                    interactiveRebase.Icon = this.CreateMenuIcon("Icons.InteractiveRebase");
                    interactiveRebase.Click += async (_, e) =>
                    {
                        await this.ShowDialogAsync(new ViewModels.InteractiveRebase(repo, commit));
                        e.Handled = true;
                    };

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(interactiveRebase);
                }

                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            if (!isHead)
            {
                if (current.Ahead.Contains(commit.SHA))
                {
                    var upstream = repo.Branches.Find(x => x.FullName.Equals(current.Upstream, StringComparison.Ordinal));
                    var pushRevision = new MenuItem();
                    pushRevision.Header = App.Text("CommitCM.PushRevision", commit.SHA.Substring(0, 10), upstream.FriendlyName);
                    pushRevision.Icon = this.CreateMenuIcon("Icons.Push");
                    pushRevision.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.PushRevision(repo, commit, upstream));
                        e.Handled = true;
                    };
                    menu.Items.Add(pushRevision);
                    menu.Items.Add(new MenuItem() { Header = "-" });
                }

                var compareWithHead = new MenuItem();
                compareWithHead.Header = App.Text("CommitCM.CompareWithHead");
                compareWithHead.Icon = this.CreateMenuIcon("Icons.Compare");
                compareWithHead.Click += async (_, e) =>
                {
                    var head = await vm.CompareWithHeadAsync(commit);
                    if (head != null)
                    {
                        var list = new List<Models.Commit>(vm.SelectedCommits);
                        if (!list.Contains(head))
                        {
                            list.Add(head);
                            vm.SelectedCommits = list;
                        }
                    }

                    e.Handled = true;
                };
                menu.Items.Add(compareWithHead);

                if (repo.LocalChangesCount > 0)
                {
                    var compareWithWorktree = new MenuItem();
                    compareWithWorktree.Header = App.Text("CommitCM.CompareWithWorktree");
                    compareWithWorktree.Icon = this.CreateMenuIcon("Icons.Compare");
                    compareWithWorktree.Click += (_, e) =>
                    {
                        vm.CompareWithWorktree(commit);
                        e.Handled = true;
                    };
                    menu.Items.Add(compareWithWorktree);
                }

                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var saveToPatch = new MenuItem();
            saveToPatch.Icon = this.CreateMenuIcon("Icons.Save");
            saveToPatch.Header = App.Text("CommitCM.SaveAsPatch");
            saveToPatch.Click += async (_, e) =>
            {
                var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
                if (storageProvider == null)
                    return;

                var options = new FolderPickerOpenOptions() { AllowMultiple = false };
                try
                {
                    var selected = await storageProvider.OpenFolderPickerAsync(options);
                    if (selected.Count == 1)
                    {
                        var folder = selected[0];
                        var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder.Path.ToString();
                        await repo.SaveCommitAsPatchAsync(commit, folderPath);
                    }
                }
                catch (Exception exception)
                {
                    repo.SendNotification($"Failed to save as patch: {exception.Message}", true);
                }

                e.Handled = true;
            };
            menu.Items.Add(saveToPatch);

            var archive = new MenuItem();
            archive.Icon = this.CreateMenuIcon("Icons.Archive");
            archive.Header = App.Text("Archive");
            archive.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.Archive(repo, commit));
                e.Handled = true;
            };
            menu.Items.Add(archive);
            menu.Items.Add(new MenuItem() { Header = "-" });

            var actions = repo.GetCustomActions(Models.CustomActionScope.Commit);
            if (actions.Count > 0)
            {
                var custom = new MenuItem();
                custom.Header = App.Text("CommitCM.CustomAction");
                custom.Icon = this.CreateMenuIcon("Icons.Action");

                foreach (var action in actions)
                {
                    var (dup, label) = action;
                    var item = new MenuItem();
                    item.Icon = this.CreateMenuIcon("Icons.Action");
                    item.Header = label;
                    item.Click += async (_, e) =>
                    {
                        await repo.ExecCustomActionAsync(dup, commit);
                        e.Handled = true;
                    };

                    custom.Items.Add(item);
                }

                menu.Items.Add(custom);
                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            var copyInfo = new MenuItem();
            copyInfo.Header = App.Text("CommitCM.CopySHA") + " - " + App.Text("CommitCM.CopySubject");
            copyInfo.Tag = OperatingSystem.IsMacOS() ? "⌘+C" : "Ctrl+C";
            copyInfo.Click += async (_, e) =>
            {
                await this.CopyTextAsync($"{commit.SHA.AsSpan(0, 10)} - {commit.Subject}");
                e.Handled = true;
            };

            var copySHA = new MenuItem();
            copySHA.Header = App.Text("CommitCM.CopySHA");
            copySHA.Icon = this.CreateMenuIcon("Icons.Hash");
            copySHA.Click += async (_, e) =>
            {
                await this.CopyTextAsync(commit.SHA);
                e.Handled = true;
            };

            var copySubject = new MenuItem();
            copySubject.Header = App.Text("CommitCM.CopySubject");
            copySubject.Icon = this.CreateMenuIcon("Icons.Subject");
            copySubject.Click += async (_, e) =>
            {
                await this.CopyTextAsync(commit.Subject);
                e.Handled = true;
            };

            var copyMessage = new MenuItem();
            copyMessage.Header = App.Text("CommitCM.CopyCommitMessage");
            copyMessage.Icon = this.CreateMenuIcon("Icons.Message");
            copyMessage.Click += async (_, e) =>
            {
                var message = await vm.GetCommitFullMessageAsync(commit);
                await this.CopyTextAsync(message);
                e.Handled = true;
            };

            var copyAuthor = new MenuItem();
            copyAuthor.Header = App.Text("CommitCM.CopyAuthor");
            copyAuthor.Icon = this.CreateMenuIcon("Icons.User");
            copyAuthor.Click += async (_, e) =>
            {
                await this.CopyTextAsync(commit.Author.ToString());
                e.Handled = true;
            };

            var copyCommitter = new MenuItem();
            copyCommitter.Header = App.Text("CommitCM.CopyCommitter");
            copyCommitter.Icon = this.CreateMenuIcon("Icons.User");
            copyCommitter.Click += async (_, e) =>
            {
                await this.CopyTextAsync(commit.Committer.ToString());
                e.Handled = true;
            };

            var copyAuthorTime = new MenuItem();
            copyAuthorTime.Header = App.Text("CommitCM.CopyAuthorTime");
            copyAuthorTime.Icon = this.CreateMenuIcon("Icons.DateTime");
            copyAuthorTime.Click += async (_, e) =>
            {
                await this.CopyTextAsync(Models.DateTimeFormat.Format(commit.AuthorTime));
                e.Handled = true;
            };

            var copyCommitterTime = new MenuItem();
            copyCommitterTime.Header = App.Text("CommitCM.CopyCommitterTime");
            copyCommitterTime.Icon = this.CreateMenuIcon("Icons.DateTime");
            copyCommitterTime.Click += async (_, e) =>
            {
                await this.CopyTextAsync(Models.DateTimeFormat.Format(commit.CommitterTime));
                e.Handled = true;
            };

            var copy = new MenuItem();
            copy.Header = App.Text("Copy");
            copy.Icon = this.CreateMenuIcon("Icons.Copy");
            copy.Items.Add(copyInfo);
            copy.Items.Add(new MenuItem() { Header = "-" });
            copy.Items.Add(copySHA);
            copy.Items.Add(copySubject);
            copy.Items.Add(copyMessage);
            copy.Items.Add(copyAuthor);
            copy.Items.Add(copyCommitter);
            copy.Items.Add(copyAuthorTime);
            copy.Items.Add(copyCommitterTime);
            menu.Items.Add(copy);

            return CreateFocusedCommitContextMenu(repo, current, commit, vm, branchKey, menu);
        }

        private const double BranchExplorerDragPanThreshold = 4;
        private bool _branchExplorerPanPressed = false;
        private bool _branchExplorerIsPanning = false;
        private Point _branchExplorerPanStartPosition;
        private Vector _branchExplorerPanStartOffset;

        private ContextMenu CreateFocusedCommitContextMenu(
            ViewModels.Repository repo,
            Models.Branch current,
            Models.Commit commit,
            ViewModels.Histories vm,
            string branchKey,
            ContextMenu advancedSource)
        {
            var menu = new ContextMenu();

            var goHere = new MenuItem();
            goHere.Header = "Go here";
            goHere.Icon = this.CreateMenuIcon("Icons.Detached");
            goHere.IsEnabled = !commit.IsCurrentHead && !repo.IsBare;
            goHere.Click += async (_, e) =>
            {
                await vm.CheckoutBranchByCommitAsync(commit);
                e.Handled = true;
            };
            menu.Items.Add(goHere);

            var createBranch = new MenuItem();
            createBranch.Header = App.Text("CreateBranch");
            createBranch.Icon = this.CreateMenuIcon("Icons.Branch.Add");
            createBranch.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.CreateBranch(repo, commit));

                e.Handled = true;
            };
            menu.Items.Add(createBranch);

            var mergeToHere = new MenuItem();
            mergeToHere.Header = "Merge to here";
            mergeToHere.Icon = this.CreateMenuIcon("Icons.Merge");
            var targetBranch = FindLocalBranchAtCommit(repo, commit, current);
            mergeToHere.IsEnabled = !repo.IsBare && targetBranch != null;
            mergeToHere.Click += async (_, e) =>
            {
                if (targetBranch != null)
                    await MergeCurrentBranchToTargetBranchAsync(repo, current, targetBranch);

                e.Handled = true;
            };
            menu.Items.Add(mergeToHere);

            var mergeFromHere = new MenuItem();
            mergeFromHere.Header = "Merge from here";
            mergeFromHere.Icon = this.CreateMenuIcon("Icons.Merge");
            mergeFromHere.IsEnabled = !repo.IsBare && !commit.IsMerged;
            mergeFromHere.Click += (_, e) =>
            {
                ShowMergeFromHerePopup(repo, current, commit);
                e.Handled = true;
            };
            menu.Items.Add(mergeFromHere);

            var deleteCommit = new MenuItem();
            deleteCommit.Header = "Delete commit";
            deleteCommit.Icon = this.CreateMenuIcon("Icons.Clear");
            var deleteTarget = FindDeleteCommitTarget(repo, vm, commit, branchKey);
            deleteCommit.IsEnabled = false;
            if (deleteTarget != null)
                _ = UpdateDeleteCommitAvailabilityAsync(repo, deleteTarget, deleteCommit);

            deleteCommit.Click += async (_, e) =>
            {
                if (deleteTarget is { IsValidated: true })
                {
                    // Removing a branch's auto-generated init commit leaves it with no
                    // commits of its own - delete the whole branch (synced both sides)
                    // instead of resetting it to an empty state.
                    if (commit.Subject == Models.BranchInit.CommitMessage)
                        await DeleteEmptiedBranchAsync(repo, deleteTarget);
                    else
                        await DeleteBranchTipCommitAsync(repo, vm, deleteTarget);
                }

                e.Handled = true;
            };
            menu.Items.Add(deleteCommit);

            var advanced = new MenuItem();
            advanced.Header = "Advanced";
            advanced.Icon = this.CreateMenuIcon("Icons.More");
            MoveContextMenuItems(advancedSource, advanced, App.Text("CreateBranch"));
            menu.Items.Add(advanced);

            return menu;
        }

        private static void MoveContextMenuItems(ContextMenu source, MenuItem target, string skipHeader)
        {
            var items = new List<object>();
            var skipped = false;
            foreach (var item in source.Items)
            {
                if (!skipped &&
                    !string.IsNullOrEmpty(skipHeader) &&
                    item is MenuItem menuItem &&
                    Equals(menuItem.Header, skipHeader))
                {
                    skipped = true;
                    continue;
                }

                items.Add(item);
            }

            source.Items.Clear();
            foreach (var item in items)
                target.Items.Add(item);
        }

        private Models.Branch FindLocalBranchAtCommit(ViewModels.Repository repo, Models.Commit commit, Models.Branch current)
        {
            foreach (var d in commit.Decorators)
            {
                if (d.Type != Models.DecoratorType.LocalBranchHead)
                    continue;

                var branch = repo.Branches.Find(x => x.IsLocal && d.Name.Equals(x.Name, StringComparison.Ordinal));
                if (branch != null && !branch.FullName.Equals(current.FullName, StringComparison.Ordinal))
                    return branch;
            }

            foreach (var d in commit.Decorators)
            {
                if (d.Type != Models.DecoratorType.RemoteBranchHead)
                    continue;

                var remote = repo.Branches.Find(x => !x.IsLocal && d.Name.Equals(x.FriendlyName, StringComparison.Ordinal));
                if (remote == null)
                    continue;

                var branch = repo.Branches.Find(x => x.IsLocal && x.Upstream != null && x.Upstream.Equals(remote.FullName, StringComparison.Ordinal));
                if (branch != null && !branch.FullName.Equals(current.FullName, StringComparison.Ordinal))
                    return branch;
            }

            return null;
        }

        private DeleteCommitTarget FindDeleteCommitTarget(ViewModels.Repository repo, ViewModels.Histories vm, Models.Commit commit, string branchKey)
        {
            if (repo is not { IsBare: false } ||
                vm == null ||
                commit is not { Parents.Count: > 0 } ||
                string.IsNullOrEmpty(branchKey))
                return null;

            var branch = repo.Branches.Find(x =>
                x.IsLocal &&
                !x.IsDetachedHead &&
                !string.IsNullOrEmpty(x.FullName) &&
                x.FullName.Equals(branchKey, StringComparison.Ordinal));

            if (branch == null || !commit.SHA.Equals(branch.Head, StringComparison.Ordinal))
                return null;

            if (HasDependentCommit(vm, commit.SHA))
                return null;

            return new DeleteCommitTarget(branch, commit.SHA, commit.Parents[0]);
        }

        private async Task UpdateDeleteCommitAvailabilityAsync(
            ViewModels.Repository repo,
            DeleteCommitTarget target,
            MenuItem menuItem)
        {
            try
            {
                var validation = await ValidateDeleteCommitAsync(repo, target, false, false);
                if (!validation.IsAllowed)
                    return;

                target.IsValidated = true;
                menuItem.IsEnabled = true;
            }
            catch (Exception exception)
            {
                Native.OS.LogException(exception);
                menuItem.IsEnabled = false;
            }
        }

        private static bool HasDependentCommit(ViewModels.Histories vm, string sha)
        {
            foreach (var commit in vm.Commits)
            {
                foreach (var parent in commit.Parents)
                {
                    if (sha.StartsWith(parent, StringComparison.Ordinal) || parent.StartsWith(sha, StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        private async Task DeleteEmptiedBranchAsync(ViewModels.Repository repo, DeleteCommitTarget target)
        {
            if (target is not { IsValidated: true } || !repo.CanCreatePopup())
                return;

            var validation = await ValidateDeleteCommitAsync(repo, target, true, true);
            if (!validation.IsAllowed)
            {
                if (!string.IsNullOrEmpty(validation.Error))
                    repo.SendNotification(validation.Error, true);
                return;
            }

            var branch = repo.Branches.Find(x =>
                x.IsLocal &&
                x.FullName.Equals(target.Branch.FullName, StringComparison.Ordinal));
            if (branch == null)
                return;

            // Can't delete the branch you're currently on - switch away first, to whatever
            // branch already sits at the fork point, or detach onto it directly otherwise.
            if (branch.IsCurrent && !repo.IsBare)
            {
                var destination = repo.Branches.Find(x =>
                    x.IsLocal &&
                    !x.FullName.Equals(branch.FullName, StringComparison.Ordinal) &&
                    x.Head.Equals(target.ParentSHA, StringComparison.Ordinal));

                var log = repo.CreateLog($"Switch away from '{branch.Name}' before deletion");
                bool switched;
                if (destination != null)
                {
                    switched = await new Commands.Checkout(repo.FullPath).Use(log).BranchAsync(destination.Name, false);
                }
                else
                {
                    switched = await new Commands.Checkout(repo.FullPath).Use(log).CommitAsync(target.ParentSHA, false);
                }
                log.Complete();

                if (!switched)
                {
                    repo.SendNotification($"Failed to switch away from '{branch.Name}'. Branch deletion was cancelled.", true);
                    return;
                }

                repo.MarkBranchesDirtyManually();
                repo.MarkWorkingCopyDirtyManually();
            }

            repo.DeleteBranch(branch);
        }

        private async Task DeleteBranchTipCommitAsync(ViewModels.Repository repo, ViewModels.Histories vm, DeleteCommitTarget target)
        {
            if (target is not { IsValidated: true } || !repo.CanCreatePopup())
                return;

            var parent = vm.Commits.Find(x => x.SHA.Equals(target.ParentSHA, StringComparison.Ordinal));
            parent ??= await vm.GetCommitAsync(target.ParentSHA);
            if (parent == null)
            {
                repo.SendNotification($"Commit '{target.ParentSHA}' is not a valid revision for branch reset!", true);
                return;
            }

            var validation = await ValidateDeleteCommitAsync(repo, target, true, true);
            if (!validation.IsAllowed)
            {
                if (!string.IsNullOrEmpty(validation.Error))
                    repo.SendNotification(validation.Error, true);
                return;
            }

            var remoteDeletion = validation.RemoteDeletion;
            var branch = repo.Branches.Find(x =>
                x.IsLocal &&
                x.FullName.Equals(target.Branch.FullName, StringComparison.Ordinal));
            if (branch == null)
                return;

            if (!repo.CanCreatePopup())
                return;

            if (branch.IsCurrent)
            {
                repo.ShowPopup(new ViewModels.Reset(repo, branch, parent, remoteDeletion));
            }
            else
            {
                repo.ShowPopup(new ViewModels.ResetWithoutCheckout(repo, branch, parent, remoteDeletion));
            }
        }

        private async Task<DeleteCommitValidation> ValidateDeleteCommitAsync(
            ViewModels.Repository repo,
            DeleteCommitTarget target,
            bool checkRemoteProtection,
            bool includeError)
        {
            var branch = repo.Branches.Find(x =>
                x.IsLocal &&
                x.FullName.Equals(target.Branch.FullName, StringComparison.Ordinal));
            if (branch == null)
                return DeleteCommitValidation.Blocked(includeError ? "The target branch no longer exists. Commit deletion was cancelled." : null);

            var branchHead = await new Commands.QueryRevisionByRefName(repo.FullPath, branch.FullName).GetResultAsync();
            if (!target.CommitSHA.Equals(branchHead, StringComparison.Ordinal))
                return DeleteCommitValidation.Blocked(includeError ? "The target branch has changed. Commit deletion was cancelled." : null);

            if (repo.IsProtectedBranch(branch))
            {
                var error = includeError
                    ? $"Branch '{branch.Name}' is protected. Commit deletion was cancelled."
                    : null;
                return DeleteCommitValidation.Blocked(error);
            }

            var hasDescendants = await new Commands.HasCommitDescendants(repo.FullPath, target.CommitSHA).GetResultAsync();
            if (hasDescendants == null)
                return DeleteCommitValidation.Blocked(includeError ? "The commit dependency state could not be verified. Commit deletion was cancelled." : null);

            if (hasDescendants.Value)
                return DeleteCommitValidation.Blocked(includeError ? "Another commit depends on the selected commit. Commit deletion was cancelled." : null);

            if (string.IsNullOrEmpty(branch.Upstream))
                return DeleteCommitValidation.Allowed(null);

            var upstream = repo.Branches.Find(x =>
                !x.IsLocal &&
                x.FullName.Equals(branch.Upstream, StringComparison.Ordinal));
            if (upstream == null || string.IsNullOrEmpty(upstream.Remote))
                return DeleteCommitValidation.Allowed(null);

            if (checkRemoteProtection)
            {
                var remote = repo.Remotes.Find(x => x.Name.Equals(upstream.Remote, StringComparison.Ordinal));
                var protection = await Models.RemoteBranchProtection.CheckAsync(remote, upstream.Name);
                if (protection == Models.RemoteBranchProtectionStatus.Protected)
                {
                    var error = includeError
                        ? $"Remote branch '{upstream.FriendlyName}' is protected. Commit deletion was cancelled."
                        : null;
                    return DeleteCommitValidation.Blocked(error);
                }
            }

            var upstreamHead = await new Commands.QueryRevisionByRefName(repo.FullPath, upstream.FullName).GetResultAsync();
            if (!target.CommitSHA.Equals(upstreamHead, StringComparison.Ordinal))
                return DeleteCommitValidation.Allowed(null);

            var remoteDeletion = new ViewModels.SyncedCommitDeletion(
                upstream.Remote,
                $"refs/heads/{upstream.Name}",
                upstream.FriendlyName,
                target.CommitSHA,
                target.ParentSHA);

            return DeleteCommitValidation.Allowed(remoteDeletion);
        }

        private class DeleteCommitTarget
        {
            public DeleteCommitTarget(Models.Branch branch, string commitSHA, string parentSHA)
            {
                Branch = branch;
                CommitSHA = commitSHA;
                ParentSHA = parentSHA;
            }

            public Models.Branch Branch { get; }
            public string CommitSHA { get; }
            public string ParentSHA { get; }
            public bool IsValidated { get; set; }
        }

        private class DeleteCommitValidation
        {
            public bool IsAllowed { get; private set; }
            public string Error { get; private set; }
            public ViewModels.SyncedCommitDeletion RemoteDeletion { get; private set; }

            public static DeleteCommitValidation Allowed(ViewModels.SyncedCommitDeletion remoteDeletion)
            {
                return new DeleteCommitValidation()
                {
                    IsAllowed = true,
                    RemoteDeletion = remoteDeletion,
                };
            }

            public static DeleteCommitValidation Blocked(string error)
            {
                return new DeleteCommitValidation()
                {
                    IsAllowed = false,
                    Error = error,
                };
            }
        }

        private async Task MergeCurrentBranchToTargetBranchAsync(ViewModels.Repository repo, Models.Branch current, Models.Branch target)
        {
            if (!repo.CanCreatePopup() || target == null || current == null)
                return;

            await repo.CheckoutBranchAsync(target);
            if (repo.CurrentBranch != null && repo.CurrentBranch.FullName.Equals(target.FullName, StringComparison.Ordinal) && repo.CanCreatePopup())
                repo.ShowPopup(new ViewModels.Merge(repo, current, target.Name, false));
        }

        private void ShowMergeFromHerePopup(ViewModels.Repository repo, Models.Branch current, Models.Commit commit)
        {
            if (!repo.CanCreatePopup())
                return;

            var found = false;
            foreach (var d in commit.Decorators)
            {
                if (d.Type == Models.DecoratorType.LocalBranchHead)
                {
                    var b = repo.Branches.Find(x => x.IsLocal && x.Name.Equals(d.Name, StringComparison.Ordinal));
                    if (b != null)
                    {
                        found = true;
                        repo.ShowPopup(new ViewModels.Merge(repo, b, current.Name, false));
                        break;
                    }
                }
                else if (d.Type == Models.DecoratorType.RemoteBranchHead)
                {
                    var rb = repo.Branches.Find(x => !x.IsLocal && x.FriendlyName.Equals(d.Name, StringComparison.Ordinal));
                    if (rb != null)
                    {
                        found = true;
                        repo.ShowPopup(new ViewModels.Merge(repo, rb, current.Name, false));
                        break;
                    }
                }
                else if (d.Type == Models.DecoratorType.Tag)
                {
                    var t = repo.Tags.Find(x => x.Name.Equals(d.Name, StringComparison.Ordinal));
                    if (t != null)
                    {
                        found = true;
                        repo.ShowPopup(new ViewModels.Merge(repo, t, current.Name));
                        break;
                    }
                }
            }

            if (!found)
                repo.ShowPopup(new ViewModels.Merge(repo, commit, current.Name));
        }

        private void FillCurrentBranchMenu(ContextMenu menu, ViewModels.Repository repo, Models.Branch current)
        {
            var submenu = new MenuItem();
            submenu.Icon = this.CreateMenuIcon("Icons.Branch");
            submenu.Header = current.Name;

            var visibility = new MenuItem();
            visibility.Classes.Add("filter_mode_switcher");
            visibility.Header = new ViewModels.FilterModeInGraph(repo, current);
            submenu.Items.Add(visibility);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            if (!string.IsNullOrEmpty(current.Upstream))
            {
                var upstream = current.Upstream.Substring(13);

                var fastForward = new MenuItem();
                fastForward.Header = App.Text("BranchCM.FastForward", upstream);
                fastForward.Icon = this.CreateMenuIcon("Icons.FastForward");
                fastForward.IsEnabled = current.Ahead.Count == 0 && current.Behind.Count > 0;
                fastForward.Click += async (_, e) =>
                {
                    var b = repo.Branches.Find(x => x.FriendlyName == upstream);
                    if (b == null)
                        return;

                    if (repo.CanCreatePopup())
                        await repo.ShowAndStartPopupAsync(new ViewModels.Merge(repo, b, current.Name, true));

                    e.Handled = true;
                };
                submenu.Items.Add(fastForward);

                var pull = new MenuItem();
                pull.Header = App.Text("BranchCM.Pull", upstream);
                pull.Icon = this.CreateMenuIcon("Icons.Pull");
                pull.Click += (_, e) =>
                {
                    if (repo.CanCreatePopup())
                        repo.ShowPopup(new ViewModels.Pull(repo, null));
                    e.Handled = true;
                };
                submenu.Items.Add(pull);
            }

            var push = new MenuItem();
            push.Header = App.Text("BranchCM.Push", current.Name);
            push.Icon = this.CreateMenuIcon("Icons.Push");
            push.IsEnabled = repo.Remotes.Count > 0;
            push.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.Push(repo, current));
                e.Handled = true;
            };
            submenu.Items.Add(push);

            var rename = new MenuItem();
            rename.Header = App.Text("BranchCM.Rename", current.Name);
            rename.Icon = this.CreateMenuIcon("Icons.Rename");
            rename.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.RenameBranch(repo, current));
                e.Handled = true;
            };
            submenu.Items.Add(rename);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            if (!repo.IsBare)
            {
                var type = repo.GetGitFlowType(current);
                if (type != Models.GitFlowBranchType.None)
                {
                    var finish = new MenuItem();
                    finish.Header = App.Text("BranchCM.Finish", current.Name);
                    finish.Icon = this.CreateMenuIcon("Icons.GitFlow");
                    finish.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.GitFlowFinish(repo, current, type));
                        e.Handled = true;
                    };
                    submenu.Items.Add(finish);
                    submenu.Items.Add(new MenuItem() { Header = "-" });
                }
            }

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = this.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, e) =>
            {
                await this.CopyTextAsync(current.Name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private void FillOtherLocalBranchMenu(ContextMenu menu, ViewModels.Repository repo, Models.Branch branch, Models.Branch current)
        {
            var submenu = new MenuItem();
            submenu.Icon = this.CreateMenuIcon("Icons.Branch");
            submenu.Header = branch.Name;

            var visibility = new MenuItem();
            visibility.Classes.Add("filter_mode_switcher");
            visibility.Header = new ViewModels.FilterModeInGraph(repo, branch);
            submenu.Items.Add(visibility);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            if (!repo.IsBare)
            {
                var checkout = new MenuItem();
                checkout.Header = App.Text("BranchCM.Checkout", branch.Name);
                checkout.Icon = this.CreateMenuIcon("Icons.Check");
                checkout.Click += async (_, e) =>
                {
                    await repo.CheckoutBranchAsync(branch);
                    e.Handled = true;
                };
                submenu.Items.Add(checkout);
            }

            var rename = new MenuItem();
            rename.Header = App.Text("BranchCM.Rename", branch.Name);
            rename.Icon = this.CreateMenuIcon("Icons.Rename");
            rename.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.RenameBranch(repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(rename);

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", branch.Name);
            delete.Icon = this.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.DeleteBranch(repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(delete);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            if (!repo.IsBare)
            {
                var type = repo.GetGitFlowType(branch);
                if (type != Models.GitFlowBranchType.None)
                {
                    var finish = new MenuItem();
                    finish.Header = App.Text("BranchCM.Finish", branch.Name);
                    finish.Icon = this.CreateMenuIcon("Icons.GitFlow");
                    finish.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.GitFlowFinish(repo, branch, type));
                        e.Handled = true;
                    };
                    submenu.Items.Add(finish);
                    submenu.Items.Add(new MenuItem() { Header = "-" });
                }
            }

            var compare = new MenuItem();
            compare.Header = App.Text("BranchCM.CompareWithSpecial", current.Name);
            compare.Icon = this.CreateMenuIcon("Icons.Compare");
            compare.Click += (_, e) =>
            {
                this.ShowWindow(new ViewModels.Compare(repo, current, branch));
                e.Handled = true;
            };

            submenu.Items.Add(compare);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = this.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, e) =>
            {
                await this.CopyTextAsync(branch.Name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private void FillRemoteBranchMenu(ContextMenu menu, ViewModels.Repository repo, Models.Branch branch, Models.Branch current)
        {
            if (branch == null)
                return;

            var name = branch.FriendlyName;

            var submenu = new MenuItem();
            submenu.Icon = this.CreateMenuIcon("Icons.Branch");
            submenu.Header = name;

            var visibility = new MenuItem();
            visibility.Classes.Add("filter_mode_switcher");
            visibility.Header = new ViewModels.FilterModeInGraph(repo, branch);
            submenu.Items.Add(visibility);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var checkout = new MenuItem();
            checkout.Header = App.Text("BranchCM.Checkout", name);
            checkout.Icon = this.CreateMenuIcon("Icons.Check");
            checkout.Click += async (_, e) =>
            {
                await repo.CheckoutBranchAsync(branch);
                e.Handled = true;
            };
            submenu.Items.Add(checkout);

            var delete = new MenuItem();
            delete.Header = App.Text("BranchCM.Delete", name);
            delete.Icon = this.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.DeleteBranch(repo, branch));
                e.Handled = true;
            };
            submenu.Items.Add(delete);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var compare = new MenuItem();
            compare.Header = App.Text("BranchCM.CompareWithSpecial", current.Name);
            compare.Icon = this.CreateMenuIcon("Icons.Compare");
            compare.Click += (_, e) =>
            {
                this.ShowWindow(new ViewModels.Compare(repo, current, branch));
                e.Handled = true;
            };

            submenu.Items.Add(compare);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var copy = new MenuItem();
            copy.Header = App.Text("BranchCM.CopyName");
            copy.Icon = this.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, e) =>
            {
                await this.CopyTextAsync(name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private void FillTagMenu(ContextMenu menu, ViewModels.Repository repo, Models.Tag tag, Models.Branch current)
        {
            var submenu = new MenuItem();
            submenu.Header = tag.Name;
            submenu.Icon = this.CreateMenuIcon("Icons.Tag");
            submenu.MinWidth = 200;

            var visibility = new MenuItem();
            visibility.Classes.Add("filter_mode_switcher");
            visibility.Header = new ViewModels.FilterModeInGraph(repo, tag);
            submenu.Items.Add(visibility);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var push = new MenuItem();
            push.Header = App.Text("TagCM.Push", tag.Name);
            push.Icon = this.CreateMenuIcon("Icons.Push");
            push.IsEnabled = repo.Remotes.Count > 0;
            push.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.PushTag(repo, tag));
                e.Handled = true;
            };
            submenu.Items.Add(push);

            var delete = new MenuItem();
            delete.Header = App.Text("TagCM.Delete", tag.Name);
            delete.Icon = this.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                if (repo.CanCreatePopup())
                    repo.ShowPopup(new ViewModels.DeleteTag(repo, tag));
                e.Handled = true;
            };
            submenu.Items.Add(delete);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var compare = new MenuItem();
            compare.Header = App.Text("BranchCM.CompareWithSpecial", current.Name);
            compare.Icon = this.CreateMenuIcon("Icons.Compare");
            compare.Click += (_, e) =>
            {
                this.ShowWindow(new ViewModels.Compare(repo, current, tag));
                e.Handled = true;
            };

            submenu.Items.Add(compare);
            submenu.Items.Add(new MenuItem() { Header = "-" });

            var copy = new MenuItem();
            copy.Header = App.Text("TagCM.CopyName");
            copy.Icon = this.CreateMenuIcon("Icons.Copy");
            copy.Click += async (_, e) =>
            {
                await this.CopyTextAsync(tag.Name);
                e.Handled = true;
            };
            submenu.Items.Add(copy);

            menu.Items.Add(submenu);
        }

        private async Task InteractiveRebaseWithPrefillActionAsync(ViewModels.Repository repo, Models.Commit target, Models.InteractiveRebaseAction action)
        {
            var prefill = new ViewModels.InteractiveRebasePrefill(target.SHA, action);
            var start = action switch
            {
                Models.InteractiveRebaseAction.Squash or Models.InteractiveRebaseAction.Fixup => $"{target.SHA}~~",
                _ => $"{target.SHA}~",
            };

            var on = await new Commands.QuerySingleCommit(repo.FullPath, start).GetResultAsync();
            if (on == null)
                repo.SendNotification($"Commit '{start}' is not a valid revision for `git rebase -i`!", true);
            else
                await this.ShowDialogAsync(new ViewModels.InteractiveRebase(repo, on, prefill));
        }
    }
}
