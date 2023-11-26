using MyvarEdit.TrueType;
using System.Drawing;

namespace CosmosTTF2.Rasterizer {
    public static class Rasterizer {
        public static (byte[] buffer, int w, int h) RasterizeGlyph(TrueTypeFontFile font, char glyph, int size) {
            var glyf = font.Glyfs[glyph];

            // render size is 2x the requested size and then scaled down to get "anti-aliasing"
            size = size * 2;

            // Calculate the width and height of the buffer
            int bufferHeight = size;
            int bufferWidth = size;

            float glyfXmin = int.MaxValue;
            float glyfXmax = int.MinValue;
            float glyfYmin = int.MaxValue;
            float glyfYmax = int.MinValue;

            // Find glyph min/max values
            foreach (var shape in glyf.Shapes) {
                foreach (var point in shape) {
                    glyfXmin = Math.Min(glyfXmin, point.X);
                    glyfXmax = Math.Max(glyfXmax, point.X);
                    glyfYmin = Math.Min(glyfYmin, point.Y);
                    glyfYmax = Math.Max(glyfYmax, point.Y);
                }
            }

            if (glyfXmax - glyfXmin > glyfYmax - glyfYmin) {
                bufferHeight = (int)((glyfYmax - glyfYmin) * size / (glyfXmax - glyfXmin));
            } else {
                bufferWidth = (int)((glyfXmax - glyfXmin) * size / (glyfYmax - glyfYmin));
            }

            byte[] buffer = new byte[bufferWidth * bufferHeight + 1];
            byte[] aaBuffer = new byte[bufferWidth * bufferHeight + 1];
            
            var scaleX = (float)bufferWidth / (glyfXmax - glyfXmin);
            var scaleY = (float)bufferHeight / (glyfYmax - glyfYmin);

            // Draw Outline
            foreach (var shape in glyf.Shapes) {
                for (int i = 0; i < shape.Count; i++) {
                    var point = shape[i];
                    var nextPoint = shape[(i + 1) % shape.Count];

                    var x1 = (int)((point.X - glyfXmin) * scaleX);
                    var y1 = (int)((point.Y - glyfYmin) * scaleY);
                    var x2 = (int)((nextPoint.X - glyfXmin) * scaleX);
                    var y2 = (int)((nextPoint.Y - glyfYmin) * scaleY);

                    // Adjust coordinates to fit within buffer bounds
                    x1 = Math.Max(0, Math.Min(bufferWidth - 1, x1));
                    y1 = Math.Max(0, Math.Min(bufferHeight - 1, y1));
                    x2 = Math.Max(0, Math.Min(bufferWidth - 1, x2));
                    y2 = Math.Max(0, Math.Min(bufferHeight - 1, y2));

                    Bresenham.DrawLine(buffer, bufferWidth, x1, y1, x2, y2);
                    //Bresenham.DrawLineAA(aaBuffer, bufferWidth, x1, y1, x2, y2);
                }
            }

            // Merge buffers
            for(var y = 0; y < bufferHeight; y++) {
                for(var x = 0; x < bufferWidth; x++) {
                    int idx = y * bufferWidth + x;

                    buffer[idx] = (byte)Math.Clamp(buffer[idx] + aaBuffer[idx], 0, 255);
                }
            }

            // Fill Outline
            List<List<PointF>> shapes = new List<List<PointF>>();
            foreach (var shape in glyf.Shapes) {
                List<PointF> newShape = new List<PointF>();
                foreach (var point in shape) {
                    newShape.Add(new PointF((point.X - glyfXmin) * scaleX, (point.Y - glyfYmin) * scaleY));
                }
                shapes.Add(newShape);
            }

            FillGlyph(shapes, buffer, bufferWidth, bufferHeight);

            byte[] finalDownscaledBuffer = new byte[size / 2 * size / 2 + 1];

            // Downscale buffer
            for (int y = 0; y < bufferHeight; y += 2) {
                for (int x = 0; x < bufferWidth; x += 2) {
                    int idx = y * bufferWidth + x;
                    int idx2 = y / 2 * bufferWidth / 2 + x / 2;

                    // Calculate average of 4 pixels
                    int sum = buffer[idx] + buffer[idx + 1] + buffer[idx + bufferWidth] + buffer[idx + bufferWidth + 1];
                    finalDownscaledBuffer[idx2] = (byte)(sum / 4);
                }
            }
            
            return (finalDownscaledBuffer, bufferWidth / 2, bufferHeight / 2);
        }

        public static List<Edge> CreateEdgesList(List<List<PointF>> shapes) {
            List<Edge> edges = new List<Edge>();

            foreach (var shape in shapes) {
                for (int i = 0; i < shape.Count; i++) {
                    var start = shape[i];
                    var end = shape[(i + 1) % shape.Count]; // Loop back to the first point
                    edges.Add(new Edge(start, end));
                }
            }

            return edges;
        }

        public static bool IsEdgeActive(Edge edge, int y) {
            return edge.Intersects(y);
        }

        public static int CompareEdgeXAtY(Edge e1, Edge e2, int y) {
            return e1.IntersectionX(y).CompareTo(e2.IntersectionX(y));
        }

        public static void FillGlyph(List<List<PointF>> shapes, byte[] buffer, int bufferWidth, int bufferHeight) {
            List<Edge> edges = CreateEdgesList(shapes);

            for (int y = 0; y < bufferHeight; y++) {
                var activeEdges = edges.Where(e => IsEdgeActive(e, y)).ToList();
                activeEdges.Sort((e1, e2) => CompareEdgeXAtY(e1, e2, y));

                for (int i = 0; i < activeEdges.Count; i += 2) {
                    int startX = Math.Max(0, (int)activeEdges[i].IntersectionX(y));
                    int endX = Math.Min(bufferWidth - 1, (int)activeEdges[i + 1].IntersectionX(y));

                    for (int x = startX; x <= endX; x++) {
                        buffer[y * bufferWidth + x] = 255;
                    }
                }
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
}