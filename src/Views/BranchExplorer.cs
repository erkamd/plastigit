using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public class BranchExplorer : Control
    {
        public static readonly StyledProperty<Models.CommitGraph> GraphProperty =
            AvaloniaProperty.Register<BranchExplorer, Models.CommitGraph>(nameof(Graph));

        public Models.CommitGraph Graph
        {
            get => GetValue(GraphProperty);
            set => SetValue(GraphProperty, value);
        }

        public static readonly StyledProperty<List<Models.Commit>> CommitsProperty =
            AvaloniaProperty.Register<BranchExplorer, List<Models.Commit>>(nameof(Commits));

        public List<Models.Commit> Commits
        {
            get => GetValue(CommitsProperty);
            set => SetValue(CommitsProperty, value);
        }

        public static readonly StyledProperty<List<Models.Commit>> SelectedCommitsProperty =
            AvaloniaProperty.Register<BranchExplorer, List<Models.Commit>>(nameof(SelectedCommits));

        public List<Models.Commit> SelectedCommits
        {
            get => GetValue(SelectedCommitsProperty);
            set => SetValue(SelectedCommitsProperty, value);
        }

        public static readonly StyledProperty<IBrush> DotBrushProperty =
            AvaloniaProperty.Register<BranchExplorer, IBrush>(nameof(DotBrush), Brushes.Transparent);

        public IBrush DotBrush
        {
            get => GetValue(DotBrushProperty);
            set => SetValue(DotBrushProperty, value);
        }

        internal const double ColumnWidth = 56;
        internal const double LaneHeight = 48;
        internal const double StartX = 176;
        internal const double StartY = 40;
        internal const double RightPadding = 40;
        internal const double BottomPadding = 40;
        private const double FastForwardMarkerFirstOffset = 16;
        private const double FastForwardMarkerSlotHeight = 16;

        static BranchExplorer()
        {
            AffectsMeasure<BranchExplorer>(GraphProperty, CommitsProperty);
            AffectsRender<BranchExplorer>(GraphProperty, CommitsProperty, SelectedCommitsProperty, DotBrushProperty);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Graph == null || Commits == null || Commits.Count == 0)
                return new Size(0, 0);

            return CalculateContentSize(Graph, Commits.Count);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            return base.ArrangeOverride(finalSize);
        }

        private Point Transform(Point p, int N)
        {
            return ToContentPoint(p, N + 1);
        }

        internal static Size CalculateContentSize(Models.CommitGraph graph, int commitCount)
        {
            double width = StartX + Math.Max(1, commitCount) * ColumnWidth + RightPadding;
            double height = StartY + Math.Max(1, graph?.TotalLanes ?? 0) * LaneHeight + BottomPadding;

            if (graph != null)
            {
                foreach (var location in graph.FastForwardBranchLocations)
                {
                    var markerBottom = StartY +
                        location.BaseLane * LaneHeight +
                        FastForwardMarkerFirstOffset +
                        (location.Slot + 1) * FastForwardMarkerSlotHeight;
                    height = Math.Max(height, markerBottom + BottomPadding);
                }
            }

            return new Size(width, height);
        }

        internal static Point ToContentPoint(Point graphPoint, int commitCount)
        {
            double lane = Models.CommitGraph.GetLaneFromX(graphPoint.X);
            double x = StartX + (Math.Max(0, commitCount - 1) - graphPoint.Y) * ColumnWidth;
            double y = StartY + lane * LaneHeight;
            return new Point(x, y);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Graph == null || Commits == null || Commits.Count == 0)
                return;

            int N = Commits.Count - 1;
            var scrollViewer = this.FindAncestorOfType<ScrollViewer>();
            double scrollX = scrollViewer?.Offset.X ?? 0;

            // Viewport clipping for performance optimization
            double viewportMinX = scrollX;
            double viewportMaxX = scrollX + (scrollViewer?.Viewport.Width ?? Bounds.Width);

            // Fetch dynamic theme brushes
            var fgBrush = this.FindResource("Brush.FG1") as IBrush ?? Brushes.White;
            var bgBrush = this.FindResource("Brush.Popup") as IBrush ?? Brushes.Black;
            // 1. Draw curves (horizontal branch lanes and branch/merge links)
            var grayedPen = new Pen(new SolidColorBrush(Colors.Gray, 0.4), 2.5);
            var grayedDashedPen = CreateDashedPen(grayedPen);
            var dashedPens = new List<Pen>(Models.CommitGraph.Pens.Count);
            foreach (var pen in Models.CommitGraph.Pens)
                dashedPens.Add(CreateDashedPen(pen));

            foreach (var link in Graph.Links)
            {
                var hStart = Transform(link.Start, N);
                var hEnd = Transform(link.End, N);
                var hControl = Transform(link.Control, N);

                if (hEnd.X > viewportMaxX + 50 && hStart.X > viewportMaxX + 50)
                    continue;
                if (hEnd.X < viewportMinX - 50 && hStart.X < viewportMinX - 50)
                    continue;

                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(hStart, false);
                    ctx.QuadraticBezierTo(hControl, hEnd);
                }

                var pen = GetBranchPen(link.Color, link.IsHighlighted, link.IsLocal, grayedPen, grayedDashedPen, dashedPens);
                context.DrawGeometry(null, pen, geo);
            }

            // Draw stable horizontal branch paths
            foreach (var line in Graph.Paths)
            {
                if (line.Points.Count == 0)
                    continue;

                var points = new List<Point>();
                foreach (var p in line.Points)
                    points.Add(Transform(p, N));

                // Check visible bounds
                double minX = double.MaxValue;
                double maxX = double.MinValue;
                foreach (var pt in points)
                {
                    if (pt.X < minX) minX = pt.X;
                    if (pt.X > maxX) maxX = pt.X;
                }
                if (maxX < viewportMinX - 50 || minX > viewportMaxX + 50)
                    continue;

                var geo = new StreamGeometry();
                var pen = GetBranchPen(line.Color, line.IsHighlighted, line.IsLocal, grayedPen, grayedDashedPen, dashedPens);

                using (var ctx = geo.Open())
                {
                    var last = points[0];
                    ctx.BeginFigure(last, false);
                    for (int i = 1; i < points.Count; i++)
                    {
                        var cur = points[i];
                        if (cur.Y != last.Y)
                        {
                            // Curve to new lane
                            ctx.QuadraticBezierTo(new Point(last.X, cur.Y), cur);
                        }
                        else
                        {
                            ctx.LineTo(cur);
                        }
                        last = cur;
                    }
                }

                context.DrawGeometry(null, pen, geo);
            }

            // 2. Draw anchors (commit dots)
            var dotFill = DotBrush;
            var dotFillPen = new Pen(dotFill, 2);
            var grayedDotPen = new Pen(Brushes.Gray, 2.5);
            var selectionPen = new Pen(new SolidColorBrush(Color.Parse("#f6a500")), 3);

            foreach (var lane in Graph.Lanes)
            {
                if (!lane.IsEmpty || lane.HeadIndex < 0)
                    continue;

                var center = Transform(new Point(Models.CommitGraph.GetLaneX(lane.Index), lane.HeadIndex), N);
                if (center.X < viewportMinX - 20 || center.X > viewportMaxX + 20)
                    continue;

                var pen = lane.IsHighlighted ? Models.CommitGraph.Pens[lane.Color] : grayedDotPen;
                var rect = GetEmptyBranchMarkerRect(center);
                if (_selectedEmptyLane == lane.Index)
                    context.DrawRectangle(null, selectionPen, rect.Inflate(4), 4, 4);

                context.DrawRectangle(dotFill, pen, rect, 3, 3);
                context.DrawLine(pen, new Point(center.X - 4, center.Y), new Point(center.X + 4, center.Y));
            }

            for (int i = 0; i < Graph.Dots.Count; i++)
            {
                var dot = Graph.Dots[i];
                var center = Transform(dot.Center, N);

                if (center.X < viewportMinX - 20 || center.X > viewportMaxX + 20)
                    continue;

                var commitIndex = dot.CommitIndex >= 0 ? dot.CommitIndex : i;
                if (commitIndex < 0 || commitIndex >= Commits.Count)
                    continue;

                var commit = Commits[commitIndex];
                bool isSelected = SelectedCommits != null && SelectedCommits.Contains(commit);

                // Draw selection highlight ring
                if (isSelected)
                    context.DrawEllipse(null, selectionPen, center, 10, 10);

                var pen = dot.IsHighlighted ? Models.CommitGraph.Pens[dot.Color] : grayedDotPen;
                switch (dot.Type)
                {
                    case Models.CommitGraph.DotType.Head:
                        context.DrawEllipse(dot.IsOnRemote ? pen.Brush : dotFill, pen, center, 8, 8);
                        break;
                    case Models.CommitGraph.DotType.Merge:
                        context.DrawEllipse(dot.IsOnRemote ? pen.Brush : dotFill, pen, center, 8, 8);
                        var mergeMarkPen = dot.IsOnRemote ? dotFillPen : pen;
                        context.DrawLine(mergeMarkPen, new Point(center.X, center.Y - 4), new Point(center.X, center.Y + 4));
                        context.DrawLine(mergeMarkPen, new Point(center.X - 4, center.Y), new Point(center.X + 4, center.Y));
                        break;
                    default:
                        context.DrawEllipse(dot.IsOnRemote ? pen.Brush : dotFill, pen, center, 5, 5);
                        break;
                }
            }

            foreach (var location in Graph.FastForwardBranchLocations)
            {
                if (location.CommitIndex < 0 || location.BaseLane < 0)
                    continue;

                var center = Transform(new Point(Models.CommitGraph.GetLaneX(location.BaseLane), location.CommitIndex), N);
                var y = center.Y + FastForwardMarkerFirstOffset + location.Slot * FastForwardMarkerSlotHeight;
                var name = CompactBranchName(location.Name, 22);
                if (!location.IsLocal)
                    name = $"REMOTE {name}";

                var formattedText = new FormattedText(
                    name,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
                    10,
                    location.IsHighlighted ? fgBrush : Brushes.Gray
                );

                var width = Math.Max(24, formattedText.Width + 12);
                var rect = new Rect(center.X - width / 2.0, y - 8, width, 16);
                if (rect.Right < viewportMinX - 20 || rect.Left > viewportMaxX + 20)
                    continue;

                var pen = GetBranchPen(location.Color, location.IsHighlighted, location.IsLocal, grayedPen, grayedDashedPen, dashedPens);
                context.DrawLine(pen, new Point(center.X, center.Y + 8), new Point(center.X, rect.Top));
                var corner = location.IsLocal ? 3 : 1;
                context.DrawRectangle(bgBrush, pen, rect, corner, corner);
                context.DrawText(
                    formattedText,
                    new Point(rect.X + (rect.Width - formattedText.Width) / 2.0, rect.Y + (rect.Height - formattedText.Height) / 2.0));
            }

            // 3. Draw frozen branch label boxes on the left (X = scrollX + 10)
            double labelBoxX = scrollX + 10;
            double labelBoxWidth = 160;
            double labelBoxHeight = 26;

            foreach (var lane in Graph.Lanes)
            {
                double y = StartY + lane.Index * LaneHeight;
                var rect = new Rect(labelBoxX, y - labelBoxHeight / 2.0, labelBoxWidth, labelBoxHeight);
                var labelPen = GetBranchPen(lane.Color, lane.IsHighlighted, lane.IsLocal, grayedPen, grayedDashedPen, dashedPens);
                var corner = lane.IsLocal ? 4 : 1;

                context.DrawRectangle(bgBrush, labelPen, rect, corner, corner);

                if (lane.IsLocal && !string.IsNullOrEmpty(lane.UpstreamName))
                {
                    var localText = new FormattedText(
                        CompactBranchName(lane.Name, 10),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
                        10,
                        lane.IsHighlighted ? fgBrush : Brushes.Gray
                    );
                    var upstreamText = new FormattedText(
                        CompactBranchName(lane.UpstreamName, 11),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
                        9,
                        lane.IsHighlighted ? fgBrush : Brushes.Gray
                    );

                    var splitX = rect.X + rect.Width * 0.46;
                    context.DrawText(
                        localText,
                        new Point(
                            rect.X + 6 + (splitX - rect.X - 12 - localText.Width) / 2.0,
                            y - localText.Height / 2.0));

                    var solidPen = lane.IsHighlighted ? Models.CommitGraph.Pens[lane.Color] : grayedPen;
                    context.DrawLine(solidPen, new Point(splitX + 4, y), new Point(splitX + 16, y));
                    context.DrawText(
                        upstreamText,
                        new Point(
                            splitX + 20 + (rect.Right - splitX - 24 - upstreamText.Width) / 2.0,
                            y - upstreamText.Height / 2.0));
                    continue;
                }

                var textAreaX = rect.X;
                var textAreaWidth = rect.Width;
                if (!lane.IsLocal)
                {
                    var badgePen = lane.IsHighlighted ? Models.CommitGraph.Pens[lane.Color] : grayedDotPen;
                    var badgeText = new FormattedText(
                        "REMOTE",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
                        8,
                        badgePen.Brush
                    );

                    var badgeWidth = badgeText.Width + 10;
                    var badgeRect = new Rect(rect.X + 5, rect.Y + 5, badgeWidth, rect.Height - 10);
                    context.DrawRectangle(null, badgePen, badgeRect, 2, 2);
                    context.DrawText(
                        badgeText,
                        new Point(
                            badgeRect.X + (badgeRect.Width - badgeText.Width) / 2.0,
                            badgeRect.Y + (badgeRect.Height - badgeText.Height) / 2.0));

                    textAreaX = badgeRect.Right + 4;
                    textAreaWidth = Math.Max(0, rect.Right - textAreaX - 4);
                }

                var name = CompactBranchName(lane.Name, lane.IsLocal ? 20 : 13);

                var formattedText = new FormattedText(
                    name,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold),
                    11,
                    lane.IsHighlighted ? fgBrush : Brushes.Gray
                );

                var textPos = new Point(
                    textAreaX + (textAreaWidth - formattedText.Width) / 2.0,
                    y - formattedText.Height / 2.0
                );
                context.DrawText(formattedText, textPos);
            }
        }

        private static Pen CreateDashedPen(Pen source)
        {
            return new Pen(source.Brush, source.Thickness)
            {
                DashStyle = DashStyle.Dash,
            };
        }

        private static Pen GetBranchPen(
            int color,
            bool isHighlighted,
            bool isLocal,
            Pen grayedPen,
            Pen grayedDashedPen,
            List<Pen> dashedPens)
        {
            if (!isHighlighted)
                return isLocal ? grayedDashedPen : grayedPen;

            return isLocal ? dashedPens[color] : Models.CommitGraph.Pens[color];
        }

        private static string CompactBranchName(string name, int maxLength)
        {
            if (string.IsNullOrEmpty(name) || name.Length <= maxLength)
                return name ?? string.Empty;

            return name[..Math.Max(1, maxLength - 3)] + "...";
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (Graph == null || Commits == null || Commits.Count == 0)
                return;

            int N = Commits.Count - 1;
            var pos = e.GetPosition(this);
            var point = e.GetCurrentPoint(this);

            // Find clicked commit dot
            for (int i = 0; i < Graph.Dots.Count; i++)
            {
                var dot = Graph.Dots[i];
                var center = Transform(dot.Center, N);
                double dx = pos.X - center.X;
                double dy = pos.Y - center.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < 14)
                {
                    var commitIndex = dot.CommitIndex >= 0 ? dot.CommitIndex : i;
                    if (commitIndex < 0 || commitIndex >= Commits.Count)
                        continue;

                    var commit = Commits[commitIndex];
                    var historiesView = this.FindAncestorOfType<Histories>();

                    if (point.Properties.IsLeftButtonPressed)
                    {
                        _selectedEmptyLane = -1;

                        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                        {
                            var selected = SelectedCommits != null ? new List<Models.Commit>(SelectedCommits) : new List<Models.Commit>();
                            if (selected.Contains(commit))
                                selected.Remove(commit);
                            else
                                selected.Add(commit);

                            SetCurrentValue(SelectedCommitsProperty, selected);
                        }
                        else
                        {
                            SetCurrentValue(SelectedCommitsProperty, new List<Models.Commit> { commit });
                        }
                        
                        if (e.ClickCount == 2 && historiesView != null)
                        {
                            _ = historiesView.DoubleTapCommit(commit);
                        }
                    }
                    else if (point.Properties.IsRightButtonPressed)
                    {
                        _selectedEmptyLane = -1;
                        SetCurrentValue(SelectedCommitsProperty, new List<Models.Commit> { commit });
                        
                        if (historiesView != null)
                        {
                            var branchKey = GetBranchKeyForLane(dot.Lane);
                            historiesView.ShowContextMenuForCommit(commit, this, branchKey);
                        }
                    }

                    e.Handled = true;
                    break;
                }
            }

            foreach (var lane in Graph.Lanes)
            {
                if (!lane.IsEmpty || lane.HeadIndex < 0)
                    continue;

                var center = Transform(new Point(Models.CommitGraph.GetLaneX(lane.Index), lane.HeadIndex), N);
                if (!GetEmptyBranchMarkerRect(center).Inflate(6).Contains(pos))
                    continue;

                if (point.Properties.IsLeftButtonPressed)
                {
                    _selectedEmptyLane = lane.Index;
                    SetCurrentValue(SelectedCommitsProperty, new List<Models.Commit>());
                    InvalidateVisual();
                }

                e.Handled = true;
                return;
            }
        }

        private static Rect GetEmptyBranchMarkerRect(Point center)
        {
            return new Rect(center.X - 8, center.Y - 6, 16, 12);
        }

        private string GetBranchKeyForLane(int laneIndex)
        {
            if (Graph == null)
                return null;

            foreach (var lane in Graph.Lanes)
            {
                if (lane.Index == laneIndex)
                    return lane.Key;
            }

            return null;
        }

        private ScrollViewer _parentScroll = null;
        private int _selectedEmptyLane = -1;

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _parentScroll = this.FindAncestorOfType<ScrollViewer>();
            if (_parentScroll != null)
            {
                _parentScroll.ScrollChanged += OnScrollChanged;
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            if (_parentScroll != null)
            {
                _parentScroll.ScrollChanged -= OnScrollChanged;
                _parentScroll = null;
            }
            base.OnDetachedFromVisualTree(e);
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            InvalidateVisual();
        }
    }

    public class BranchExplorerMap : Control
    {
        public static readonly StyledProperty<Models.CommitGraph> GraphProperty =
            AvaloniaProperty.Register<BranchExplorerMap, Models.CommitGraph>(nameof(Graph));

        public Models.CommitGraph Graph
        {
            get => GetValue(GraphProperty);
            set => SetValue(GraphProperty, value);
        }

        public static readonly StyledProperty<List<Models.Commit>> CommitsProperty =
            AvaloniaProperty.Register<BranchExplorerMap, List<Models.Commit>>(nameof(Commits));

        public List<Models.Commit> Commits
        {
            get => GetValue(CommitsProperty);
            set => SetValue(CommitsProperty, value);
        }

        public static readonly StyledProperty<ScrollViewer> TargetProperty =
            AvaloniaProperty.Register<BranchExplorerMap, ScrollViewer>(nameof(Target));

        public ScrollViewer Target
        {
            get => GetValue(TargetProperty);
            set => SetValue(TargetProperty, value);
        }

        private const double Padding = 8;

        static BranchExplorerMap()
        {
            AffectsRender<BranchExplorerMap>(GraphProperty, CommitsProperty, TargetProperty);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var graph = Graph;
            var commits = Commits;
            var target = Target;
            if (graph == null || commits == null || commits.Count == 0 || target == null)
                return;

            var background = this.FindResource("Brush.Popup") as IBrush ?? Brushes.Black;
            var border = this.FindResource("Brush.Border2") as IBrush ?? Brushes.Gray;
            var accent = this.FindResource("Brush.Accent") as IBrush ?? Brushes.Orange;
            var contentSize = BranchExplorer.CalculateContentSize(graph, commits.Count);
            var mapRect = new Rect(
                Padding,
                Padding,
                Math.Max(1, Bounds.Width - Padding * 2),
                Math.Max(1, Bounds.Height - Padding * 2));

            context.DrawRectangle(background, new Pen(border), new Rect(Bounds.Size), 4, 4);

            var scaleX = mapRect.Width / Math.Max(1, contentSize.Width);
            var scaleY = mapRect.Height / Math.Max(1, contentSize.Height);

            Point MapGraphPoint(Point point)
            {
                var content = BranchExplorer.ToContentPoint(point, commits.Count);
                return new Point(mapRect.X + content.X * scaleX, mapRect.Y + content.Y * scaleY);
            }

            foreach (var link in graph.Links)
            {
                var start = MapGraphPoint(link.Start);
                var end = MapGraphPoint(link.End);
                var control = MapGraphPoint(link.Control);
                var pen = CreateMapPen(link.Color, link.IsHighlighted, link.IsLocal);
                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    ctx.BeginFigure(start, false);
                    ctx.QuadraticBezierTo(control, end);
                }

                context.DrawGeometry(null, pen, geometry);
            }

            foreach (var path in graph.Paths)
            {
                if (path.Points.Count == 0)
                    continue;

                var geometry = new StreamGeometry();
                using (var ctx = geometry.Open())
                {
                    var last = MapGraphPoint(path.Points[0]);
                    ctx.BeginFigure(last, false);
                    for (var i = 1; i < path.Points.Count; i++)
                    {
                        var current = MapGraphPoint(path.Points[i]);
                        if (Math.Abs(current.Y - last.Y) > 0.01)
                            ctx.QuadraticBezierTo(new Point(last.X, current.Y), current);
                        else
                            ctx.LineTo(current);

                        last = current;
                    }
                }

                context.DrawGeometry(null, CreateMapPen(path.Color, path.IsHighlighted, path.IsLocal), geometry);
            }

            foreach (var dot in graph.Dots)
            {
                var center = MapGraphPoint(dot.Center);
                var sourcePen = dot.IsHighlighted ? Models.CommitGraph.Pens[dot.Color] : new Pen(Brushes.Gray);
                var pen = new Pen(sourcePen.Brush, 1);
                context.DrawEllipse(dot.IsOnRemote ? pen.Brush : background, pen, center, 2.5, 2.5);
            }

            var extentWidth = Math.Max(1, target.Extent.Width);
            var extentHeight = Math.Max(1, target.Extent.Height);
            var viewport = new Rect(
                mapRect.X + target.Offset.X / extentWidth * mapRect.Width,
                mapRect.Y + target.Offset.Y / extentHeight * mapRect.Height,
                Math.Min(mapRect.Width, target.Viewport.Width / extentWidth * mapRect.Width),
                Math.Min(mapRect.Height, target.Viewport.Height / extentHeight * mapRect.Height));
            context.DrawRectangle(null, new Pen(accent, 2), viewport, 2, 2);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TargetProperty)
                SubscribeToTarget(change.NewValue as ScrollViewer);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            SubscribeToTarget(Target);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            SubscribeToTarget(null);
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && NavigateTarget(e.GetPosition(this)))
            {
                e.Pointer.Capture(this);
                e.Handled = true;
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (Equals(e.Pointer.Captured, this) &&
                e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
                NavigateTarget(e.GetPosition(this)))
            {
                e.Handled = true;
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (Equals(e.Pointer.Captured, this))
            {
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }

        private static Pen CreateMapPen(int color, bool isHighlighted, bool isLocal)
        {
            var source = isHighlighted ? Models.CommitGraph.Pens[color] : new Pen(new SolidColorBrush(Colors.Gray, 0.65));
            return new Pen(source.Brush, 1)
            {
                DashStyle = isLocal ? DashStyle.Dash : null,
            };
        }

        private bool NavigateTarget(Point position)
        {
            var target = Target;
            if (target == null || Bounds.Width <= Padding * 2 || Bounds.Height <= Padding * 2)
                return false;

            var mapWidth = Bounds.Width - Padding * 2;
            var mapHeight = Bounds.Height - Padding * 2;
            var normalizedX = Math.Clamp((position.X - Padding) / mapWidth, 0, 1);
            var normalizedY = Math.Clamp((position.Y - Padding) / mapHeight, 0, 1);
            var maxX = Math.Max(0, target.Extent.Width - target.Viewport.Width);
            var maxY = Math.Max(0, target.Extent.Height - target.Viewport.Height);
            var centerX = normalizedX * target.Extent.Width;
            var centerY = normalizedY * target.Extent.Height;
            target.Offset = new Vector(
                Math.Clamp(centerX - target.Viewport.Width / 2.0, 0, maxX),
                Math.Clamp(centerY - target.Viewport.Height / 2.0, 0, maxY));
            return true;
        }

        private void SubscribeToTarget(ScrollViewer target)
        {
            if (ReferenceEquals(_subscribedTarget, target))
                return;

            if (_subscribedTarget != null)
                _subscribedTarget.ScrollChanged -= OnTargetScrollChanged;

            _subscribedTarget = target;
            if (_subscribedTarget != null)
                _subscribedTarget.ScrollChanged += OnTargetScrollChanged;

            InvalidateVisual();
        }

        private void OnTargetScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            InvalidateVisual();
        }

        private ScrollViewer _subscribedTarget;
    }
}
