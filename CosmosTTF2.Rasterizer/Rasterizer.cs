using MyvarEdit.TrueType;
using System.Drawing;

namespace CosmosTTF2.Rasterizer {
    public static class Rasterizer {
        public static Action<string> dbg = (str) => { };
        public static RenderedGlyph? RasterizeGlyph(TrueTypeFontFile font, char glyph, int pointSize) {
            if (!font.Glyfs.TryGetValue(glyph, out var glyf)) return null;

            pointSize *= 2;
            
            var hMetrics = font.longHorMetrics[glyph];
            var unitsPerEm = font.Header.UnitsPerEm;

            // Convert point size to pixel size
            int pixelSize = (int)(pointSize * (96.0 / 72)); // assuming 96 DPI screen

            // Calculate scale factor
            float scale = (float)pixelSize / unitsPerEm;

            // Use glyph metrics to calculate glyph's dimensions
            int glyphWidthNonNormalized = glyf.Xmax - glyf.Xmin;
            int glyphHeightNonNormalized = glyf.Ymax - glyf.Ymin;

            int bufferWidth = (int)Math.Ceiling((glyphWidthNonNormalized) * scale) + 1;
            int bufferHeight = (int)Math.Ceiling((glyphHeightNonNormalized) * scale) + 1;

            // Use horizontal metrics for advance width
            ushort advWidth16Unscaled = hMetrics.advanceWidth;
            int advWidth32Unscaled = (int)advWidth16Unscaled; // This is NOT uselessly complicated, its for Cosmos compatibility
            
            int advanceWidth = (int)Math.Ceiling(advWidth32Unscaled * scale);

            // Using font.GetBaselineOffset(glyf) to calculate baseline offset and the glyfs height
            int baselineOffset = (int)Math.Ceiling(font.GetBaselineOffset(glyf) * scale);

            // Initialize buffer
            byte[] buffer = new byte[bufferWidth * bufferHeight];

            // Draw Outline
            foreach (var shape in glyf.Shapes) {
                for (int i = 0; i < shape.Count; i++) {
                    var point = shape[i];
                    var nextPoint = shape[(i + 1) % shape.Count];

                    var x1 = (int)((point.X - glyf.Xmin) * scale);
                    var y1 = (int)((point.Y - glyf.Ymin) * scale);
                    var x2 = (int)((nextPoint.X - glyf.Xmin) * scale);
                    var y2 = (int)((nextPoint.Y - glyf.Ymin) * scale);

                    // Adjust coordinates to fit within buffer bounds
                    x1 = Math.Max(0, Math.Min(bufferWidth - 1, x1));
                    y1 = Math.Max(0, Math.Min(bufferHeight - 1, y1));
                    x2 = Math.Max(0, Math.Min(bufferWidth - 1, x2));
                    y2 = Math.Max(0, Math.Min(bufferHeight - 1, y2));

                    Bresenham.DrawLine(buffer, bufferWidth, x1, y1, x2, y2);
                }
            }

            // Fill Outline
            List<List<PointF>> shapes = new List<List<PointF>>();
            foreach (var shape in glyf.Shapes) {
                List<PointF> newShape = new List<PointF>();
                foreach (var point in shape) {
                    newShape.Add(new PointF((point.X - glyf.Xmin) * scale, (point.Y - glyf.Ymin) * scale));
                }
                shapes.Add(newShape);
            }

            FillGlyph(shapes, buffer, bufferWidth, bufferHeight);

            int downscaledHeight = bufferHeight / 2;
            int downscaledWidth = bufferWidth / 2;
            byte[] finalDownscaledBuffer = new byte[downscaledHeight * downscaledWidth];

            for (int y = 0; y < bufferHeight; y += 2) {
                for (int x = 0; x < bufferWidth; x += 2) {
                    int idx = y * bufferWidth + x;
                    int idx2 = (y / 2) * downscaledWidth + (x / 2);

                    // Ensure we don't read outside the buffer
                    int sum = buffer[idx];
                    if (x + 1 < bufferWidth) {
                        sum += buffer[idx + 1];
                    }
                    if (y + 1 < bufferHeight) {
                        sum += buffer[idx + bufferWidth];
                    }
                    if (x + 1 < bufferWidth && y + 1 < bufferHeight) {
                        sum += buffer[idx + bufferWidth + 1];
                    }

                    int count = 1; // Start with 1 for the current pixel
                    if (x + 1 < bufferWidth) count++;
                    if (y + 1 < bufferHeight) count++;
                    if (x + 1 < bufferWidth && y + 1 < bufferHeight) count++;

                    if (idx2 >= finalDownscaledBuffer.Length) idx2 = finalDownscaledBuffer.Length - 1;
                    finalDownscaledBuffer[idx2] = (byte)(sum / count);
                }
            }

            return new RenderedGlyph {
                buffer = finalDownscaledBuffer,
                w = bufferWidth / 2,
                h = bufferHeight / 2,
                advanceWidth = advanceWidth / 2,
                baselineOffset = baselineOffset / 2,
                original2xBuffer = buffer,
                w2x = bufferWidth,
                h2x = bufferHeight
            };
        }

        public static List<Edge> CreateEdgesList(List<List<PointF>> shapes) {
            List<Edge> edges = new List<Edge>();

            foreach (var shape in shapes) {
                for (int i = 0; i < shape.Count; i++) {
                    var start = shape[i];
                    var end = shape[(i + 1) % shape.Count]; // Loop back to the first point

                    // Skip horizontal edges
                    if (start.Y != end.Y) {
                        edges.Add(new Edge(start, end));
                    }
                }
            }

            return edges;
        }

        public static bool IsEdgeActive(Edge edge, int y) {
            // Check if the edge is active for this scanline, excluding edges that just touch the scanline at a point
            return (edge.Start.Y <= y && edge.End.Y > y) || (edge.End.Y <= y && edge.Start.Y > y);
        }

       public static int CompareEdgeXAtY(Edge e1, Edge e2, int y)
        {
            float x1 = e1.IntersectionX(y);
            float x2 = e2.IntersectionX(y);

            // First compare by X coordinate
            int comparison = x1.CompareTo(x2);
            if (comparison != 0)
            {
                return comparison;
            }

            // If X is the same, compare by the slope (to handle edges intersecting at a point)
            float slope1 = (e1.End.X - e1.Start.X) / (e1.End.Y - e1.Start.Y);
            float slope2 = (e2.End.X - e2.Start.X) / (e2.End.Y - e2.Start.Y);
            return slope1.CompareTo(slope2);
        }


        public static void FillGlyph(List<List<PointF>> shapes, byte[] buffer, int bufferWidth, int bufferHeight) {
            List<Edge> edges = CreateEdgesList(shapes);

            for (int y = 0; y < bufferHeight; y++) {
                var activeEdges = edges.Where(e => IsEdgeActive(e, y)).ToList();
                InsertionSort(activeEdges, y);


                for (int i = 0; i < activeEdges.Count; i += 2) {
                    int startX = Math.Max(0, (int)activeEdges[i].IntersectionX(y));
                    int endX = Math.Min(bufferWidth - 1, (int)activeEdges[i + 1].IntersectionX(y));

                    for (int x = startX; x <= endX; x++) {
                        buffer[y * bufferWidth + x] = 255;
                    }
                }
            }
        }

        static void InsertionSort(List<Edge> edges, int y) {
            for (int i = 1; i < edges.Count; i++) {
                var currentEdge = edges[i];
                int j = i - 1;

                // Move elements of edges[0..i-1], that are greater than currentEdge, to one position ahead of their current position
                while (j >= 0 && CompareEdgeXAtY(edges[j], currentEdge, y) > 0) {
                    edges[j + 1] = edges[j];
                    j = j - 1;
                }
                edges[j + 1] = currentEdge;
            }
        }
    }

    public static class Bresenham {
        public static void DrawLine(byte[] buffer, int size, int x1, int y1, int x2, int y2) {
            int dx = x2 - x1;
            int dy = y2 - y1;

            int sx = dx > 0 ? 1 : -1;
            int sy = dy > 0 ? 1 : -1;

            dx = dx > 0 ? dx : -dx;
            dy = dy > 0 ? dy : -dy;

            int err = dx - dy;

            while (true) {
                buffer[y1 * size + x1] = 255;

                if (x1 == x2 && y1 == y2) {
                    break;
                }

                int e2 = 2 * err;
                if (e2 > -dy) {
                    err -= dy;
                    x1 += sx;
                }

                if (e2 < dx) {
                    err += dx;
                    y1 += sy;
                }
            }
        }
        public static void DrawLineAA(byte[] buffer, int size, int x0, int y0, int x1, int y1) {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1; 
            int err = dx - dy, e2, x2;
            float ed = dx + dy == 0 ? 1 : (float)Math.Sqrt(dx * dx + dy * dy);

            for (;;) {
                buffer[y0 * size + x0] = (byte)(255 * Math.Abs(err - dx + dy) / ed);
                e2 = err; x2 = x0;
                if (2 * e2 >= -dx) {
                    if (x0 == x1) break;
                    if (e2 + dy < ed) buffer[(y0 + sy) * size + x0] = (byte)(255 * (e2 + dy) / ed);
                    err -= dy; x0 += sx;
                }
                if (2 * e2 <= dy) {
                    if (y0 == y1) break;
                    if (dx - e2 < ed) buffer[y0 * size + x2 + sx] = (byte)(255 * (dx - e2) / ed);
                    err += dx; y0 += sy;
                }
            }
        }
    }
    
    public class Edge {
        public PointF Start { get; set; }
        public PointF End { get; set; }

        public Edge(PointF start, PointF end) {
            Start = start.Y < end.Y ? start : end; // Ensure Start is the lower point
            End = start.Y < end.Y ? end : start;
        }

        public bool Intersects(int y) {
            return y >= Start.Y && y <= End.Y;
        }

        public float IntersectionX(int y) {
            if (Start.Y == End.Y) return Start.X; // Horizontal line edge case
            return Start.X + (End.X - Start.X) * ((float)y - Start.Y) / (End.Y - Start.Y);
        }
    }

    public class RenderedGlyph {
        public byte[] buffer { get; set; }
        public int w { get; set; }
        public int h { get; set; }
        public byte[] original2xBuffer { get; set; }
        public int w2x { get; set; }
        public int h2x { get; set; }
        public int advanceWidth { get; set; }
        public int baselineOffset { get; set; }
    }
}