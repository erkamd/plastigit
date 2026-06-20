using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace SourceGit.Views
{
    public class CommitGraph : Control
    {
        public static readonly StyledProperty<Models.CommitGraph> GraphProperty =
            AvaloniaProperty.Register<CommitGraph, Models.CommitGraph>(nameof(Graph));

        public Models.CommitGraph Graph
        {
            get => GetValue(GraphProperty);
            set => SetValue(GraphProperty, value);
        }

        public static readonly StyledProperty<IBrush> DotBrushProperty =
            AvaloniaProperty.Register<CommitGraph, IBrush>(nameof(DotBrush), Brushes.Transparent);

        public IBrush DotBrush
        {
            get => GetValue(DotBrushProperty);
            set => SetValue(DotBrushProperty, value);
        }

        public static readonly StyledProperty<Models.CommitGraphLayout> LayoutProperty =
            AvaloniaProperty.Register<CommitGraph, Models.CommitGraphLayout>(nameof(Layout));

        public Models.CommitGraphLayout Layout
        {
            get => GetValue(LayoutProperty);
            set => SetValue(LayoutProperty, value);
        }

        static CommitGraph()
        {
            AffectsRender<CommitGraph>(
                GraphProperty,
                DotBrushProperty,
                LayoutProperty);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (Graph is not { } graph || Layout is not { } layout)
                return;

            var startY = layout.StartY;
            var clipWidth = layout.ClipWidth;
            var clipHeight = Bounds.Height;
            var rowHeight = layout.RowHeight;
            var endY = startY + clipHeight + 28;

            using (context.PushClip(new Rect(0, 0, clipWidth, clipHeight)))
            using (context.PushTransform(Matrix.CreateTranslation(0, -startY)))
            {
                DrawCurves(context, graph, startY, endY, rowHeight);
                DrawAnchors(context, graph, startY, endY, rowHeight);
            }
        }

        private void DrawCurves(DrawingContext context, Models.CommitGraph graph, double top, double bottom, double rowHeight)
        {
            var grayedPen = new Pen(new SolidColorBrush(Colors.Gray, 0.4), Models.CommitGraph.Pens[0].Thickness);

            foreach (var link in graph.Links)
            {
                var startY = link.Start.Y * rowHeight;
                var endY = link.End.Y * rowHeight;

                if (endY < top)
                    continue;
                if (startY > bottom)
                    break;

                var geo = new StreamGeometry();
                using (var ctx = geo.Open())
                {
                    ctx.BeginFigure(new Point(link.Start.X, startY), false);
                    ctx.QuadraticBezierTo(new Point(link.Control.X, link.Control.Y * rowHeight), new Point(link.End.X, endY));
                }

                var pen = link.IsHighlighted ? Models.CommitGraph.Pens[link.Color] : grayedPen;
                context.DrawGeometry(null, pen, geo);
            }

            foreach (var line in graph.Paths)
            {
                var last = new Point(line.Points[0].X, line.Points[0].Y * rowHeight);
                var size = line.Points.Count;
                var endY = line.Points[size - 1].Y * rowHeight;

                if (endY < top)
                    continue;
                if (last.Y > bottom)
                    break;

                var geo = new StreamGeometry();
                var pen = line.IsHighlighted ? Models.CommitGraph.Pens[line.Color] : grayedPen;

                using (var ctx = geo.Open())
                {
                    var started = false;
                    var ended = false;
                    for (int i = 1; i < size; i++)
                    {
                        var cur = new Point(line.Points[i].X, line.Points[i].Y * rowHeight);
                        if (cur.Y < top)
                        {
                            last = cur;
                            continue;
                        }

                        if (!started)
                        {
                            ctx.BeginFigure(last, false);
                            started = true;
                        }

                        if (cur.Y > bottom)
                        {
                            cur = new Point(cur.X, bottom);
                            ended = true;
                        }

                        if (cur.X > last.X)
                        {
                            ctx.QuadraticBezierTo(new Point(cur.X, last.Y), cur);
                        }
                        else if (cur.X < last.X)
                        {
                            if (i < size - 1)
                            {
                                var midY = (last.Y + cur.Y) / 2;
                                ctx.CubicBezierTo(new Point(last.X, midY + 4), new Point(cur.X, midY - 4), cur);
                            }
                            else
                            {
                                ctx.QuadraticBezierTo(new Point(last.X, cur.Y), cur);
                            }
                        }
                        else
                        {
                            ctx.LineTo(cur);
                        }

                        if (ended)
                            break;
                        last = cur;
                    }
                }

                context.DrawGeometry(null, pen, geo);
            }
        }

        private void DrawAnchors(DrawingContext context, Models.CommitGraph graph, double top, double bottom, double rowHeight)
        {
            var dotFill = DotBrush;
            var grayedPen = new Pen(Brushes.Gray, Models.CommitGraph.Pens[0].Thickness);
            var headPen = new Pen(Brushes.Black, 2);

            foreach (var dot in graph.Dots)
            {
                var center = new Point(dot.Center.X, dot.Center.Y * rowHeight);

                if (center.Y < top)
                    continue;
                if (center.Y > bottom)
                    break;

                var pen = dot.IsHighlighted ? Models.CommitGraph.Pens[dot.Color] : grayedPen;

                // A branch's init commit is always rendered as a triangle, no matter if it's
                // also the current Head (a marker can never be a Merge - it has one parent).
                if (dot.IsInit)
                    DrawTriangle(context, dotFill, pen, center, 6);
                else if (dot.Type == Models.CommitGraph.DotType.Merge)
                    DrawSquare(context, dotFill, pen, center, 6);
                else
                    context.DrawEllipse(dotFill, pen, center, 3, 3);

                if (dot.IsCurrentHead)
                    DrawCaret(context, headPen, new Point(center.X, center.Y + 12), 6);
            }
        }

        private static void DrawTriangle(DrawingContext context, IBrush fill, Pen pen, Point center, double size)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(center.X - size * 0.5, center.Y - size * 0.87), true);
                ctx.LineTo(new Point(center.X - size * 0.5, center.Y + size * 0.87));
                ctx.LineTo(new Point(center.X + size, center.Y));
                ctx.EndFigure(true);
            }

            context.DrawGeometry(fill, pen, geo);
        }

        private static void DrawSquare(DrawingContext context, IBrush fill, Pen pen, Point center, double size)
        {
            var half = size * 0.8;
            var rect = new Rect(center.X - half, center.Y - half, half * 2, half * 2);
            context.DrawRectangle(fill, pen, rect);
        }

        private static void DrawCaret(DrawingContext context, Pen pen, Point center, double size)
        {
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(center.X - size, center.Y + size * 0.5), false);
                ctx.LineTo(new Point(center.X, center.Y - size * 0.5));
                ctx.LineTo(new Point(center.X + size, center.Y + size * 0.5));
            }

            context.DrawGeometry(null, pen, geo);
        }
    }
}
