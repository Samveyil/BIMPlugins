using Autodesk.Revit.DB;
using Autodesk.Revit.DB.DirectContext3D;
using Autodesk.Revit.DB.ExternalService;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BIMPlugins.Classes
{
    /// <summary>A shape drawn over the model: segments as lines, triangles see-through, in model coordinates.</summary>
    public sealed class TemporaryShape(Color color)
    {
        public Color Color { get; } = color;

        /// <summary>Pairs of points: every two make a segment.</summary>
        public List<XYZ> Lines { get; } = [];

        /// <summary>Triples of points: every three make a triangle.</summary>
        public List<XYZ> Triangles { get; } = [];

        /// <summary>A box between two corners: its 12 edges and its 6 faces.</summary>
        public TemporaryShape AddBox(XYZ min, XYZ max)
        {
            var c = new[]
            {
                new XYZ(min.X, min.Y, min.Z), new XYZ(max.X, min.Y, min.Z), new XYZ(max.X, max.Y, min.Z), new XYZ(min.X, max.Y, min.Z),
                new XYZ(min.X, min.Y, max.Z), new XYZ(max.X, min.Y, max.Z), new XYZ(max.X, max.Y, max.Z), new XYZ(min.X, max.Y, max.Z)
            };

            foreach (var (a, b) in new[] { (0, 1), (1, 2), (2, 3), (3, 0), (4, 5), (5, 6), (6, 7), (7, 4), (0, 4), (1, 5), (2, 6), (3, 7) })
                Lines.AddRange([c[a], c[b]]);

            foreach (var (a, b, d, e) in new[] { (0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7) })
                AddQuad(c[a], c[b], c[d], c[e]);

            return this;
        }

        /// <summary>A horizontal rectangle between two corners at the given height: its edges and its face.</summary>
        public TemporaryShape AddRectangle(XYZ min, XYZ max, double z)
        {
            var c = new[] { new XYZ(min.X, min.Y, z), new XYZ(max.X, min.Y, z), new XYZ(max.X, max.Y, z), new XYZ(min.X, max.Y, z) };

            for (var i = 0; i < 4; i++)
                Lines.AddRange([c[i], c[(i + 1) % 4]]);

            AddQuad(c[0], c[1], c[2], c[3]);
            return this;
        }

        private void AddQuad(XYZ a, XYZ b, XYZ c, XYZ d) => Triangles.AddRange([a, b, c, a, c, d]);
    }

    /// <summary>
    /// Shapes drawn over the views of a document without being elements of the model: nothing to delete, no transaction,
    /// nothing in the undo list. Drawn by <see cref="TemporaryGraphicsServer"/>, which the application registers at start-up.
    /// Each shape has a key of the caller's own, by which it is replaced or removed.
    /// </summary>
    public static class TemporaryGraphics
    {
        private static readonly object Sync = new();
        private static readonly Dictionary<object, (Document Document, TemporaryShape Shape)> Shapes = [];

        /// <summary>Bumped on every change: the server rebuilds its buffers when it differs from the one they were built for.</summary>
        internal static int Version { get; private set; }

        /// <summary>Shows the shape in the 3D views of the document, in place of the one shown under the same key.</summary>
        public static void Set(object key, Document document, TemporaryShape shape)
        {
            lock (Sync)
            {
                Shapes[key] = (document, shape);
                Version++;
            }
        }

        public static void Remove(object key)
        {
            lock (Sync)
            {
                if (Shapes.Remove(key))
                    Version++;
            }
        }

        public static void RemoveAll(IEnumerable<object> keys)
        {
            lock (Sync)
            {
                var removed = false;
                foreach (var key in keys)
                    removed |= Shapes.Remove(key);

                if (removed)
                    Version++;
            }
        }

        internal static List<TemporaryShape> Snapshot(Document document)
        {
            lock (Sync)
            {
                return Shapes.Values
                    .Where(s => IsSame(s.Document, document))
                    .Select(s => s.Shape)
                    .ToList();
            }
        }

        internal static bool HasShapes(Document document)
        {
            lock (Sync)
                return Shapes.Values.Any(s => IsSame(s.Document, document));
        }

        private static bool IsSame(Document a, Document b)
        {
            try { return a.IsValidObject && b.IsValidObject && a.Equals(b); }
            catch { return false; }
        }
    }

    /// <summary>
    /// Draws <see cref="TemporaryGraphics"/> over the model through DirectContext3D, Revit's way of drawing graphics that are
    /// not elements, as RevitDevKit shows geometry. Segments go in the opaque pass, faces see-through in the transparent one.
    /// Buffers are rebuilt only when the shapes change; an index buffer holds 16-bit indices, so large shapes go in pieces.
    /// </summary>
    public sealed class TemporaryGraphicsServer : IDirectContext3DServer
    {
        private static readonly Guid ServerId = new("4f6b2d8e-1c3a-4e7b-9a5d-8b2c6e0f7a31");

        // Of 65 536 a buffer may index; lines and triangles both end on a whole primitive within it.
        private const int MaxVertices = 65000;

        // 0 opaque, 1 invisible: faces let the model show through. Set on the effect alone, with opaque vertex colours:
        // given on both, the two multiply and the faces all but vanish.
        private const double FaceTransparency = 0.6;

        private static TemporaryGraphicsServer _instance;

        private readonly object _sync = new();
        private int _builtVersion = -1;
        private Document _builtDocument;
        private List<Batch> _lines = [];
        private List<Batch> _faces = [];
        private Outline _bounds;

        /// <summary>Called once at start-up.</summary>
        public static void Register()
        {
            if (_instance != null) return;

            var service = (MultiServerService)ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService);
            _instance = new TemporaryGraphicsServer();
            service.AddServer(_instance);

            var active = service.GetActiveServerIds();
            active.Add(ServerId);
            service.SetActiveServers(active);
        }

        /// <summary>Called once at shut-down.</summary>
        public static void Unregister()
        {
            if (_instance == null) return;

            try
            {
                var service = (MultiServerService)ExternalServiceRegistry.GetService(ExternalServices.BuiltInExternalServices.DirectContext3DService);
                service.RemoveServer(ServerId);
            }
            catch { }

            _instance.Release();
            _instance = null;
        }

        public Guid GetServerId() => ServerId;
        public ExternalServiceId GetServiceId() => ExternalServices.BuiltInExternalServices.DirectContext3DService;
        public string GetName() => "BIMPlugins temporary graphics";
        public string GetVendorId() => "BIMPlugins";
        public string GetDescription() => "Временная графика BIMPlugins: диапазоны видимости и другие фигуры поверх модели";
        public string GetApplicationId() => string.Empty;
        public string GetSourceId() => string.Empty;
        public bool UsesHandles() => false;

        // Shapes are drawn in the 3D views of their document only.
        public bool CanExecute(View view) => view is View3D && TemporaryGraphics.HasShapes(view.Document);

        public Outline GetBoundingBox(View view)
        {
            Build(view.Document);
            return _bounds;
        }

        public bool UseInTransparentPass(View view)
        {
            Build(view.Document);
            return _faces.Count > 0;
        }

        public void RenderScene(View view, DisplayStyle displayStyle)
        {
            try
            {
                Build(view.Document);
                var transparent = DrawContext.IsTransparentPass();
                lock (_sync)
                {
                    foreach (var batch in transparent ? _faces : _lines)
                    {
                        if (!batch.Vertices.IsValid() || !batch.Indices.IsValid()) continue;
                        DrawContext.FlushBuffer(batch.Vertices, batch.VertexCount, batch.Indices, batch.IndexCount,
                            batch.Format, batch.Effect, batch.Primitive, 0, batch.PrimitiveCount);
                    }
                }
            }
            catch { } // Drawing must never break the view.
        }

        private void Build(Document document)
        {
            if (_builtVersion == TemporaryGraphics.Version && ReferenceEquals(_builtDocument, document)) return;

            lock (_sync)
            {
                var version = TemporaryGraphics.Version;
                var shapes = TemporaryGraphics.Snapshot(document);
                Release();

                foreach (var shape in shapes)
                {
                    _lines.AddRange(LineBatches(shape));
                    _faces.AddRange(FaceBatches(shape));
                }

                _bounds = Bounds(shapes);
                _builtVersion = version;
                _builtDocument = document;
            }
        }

        private void Release()
        {
            foreach (var batch in _lines.Concat(_faces)) batch.Dispose();
            _lines = [];
            _faces = [];
            _bounds = null;
        }

        private static IEnumerable<Batch> LineBatches(TemporaryShape shape)
        {
            var color = new ColorWithTransparency(shape.Color.Red, shape.Color.Green, shape.Color.Blue, 0);
            var step = MaxVertices - MaxVertices % 2;
            for (var start = 0; start < shape.Lines.Count; start += step)
            {
                var count = Math.Min(step, shape.Lines.Count - start);
                var vertices = new VertexBuffer(count * VertexPositionColored.GetSizeInFloats());
                vertices.Map(count * VertexPositionColored.GetSizeInFloats());
                var stream = vertices.GetVertexStreamPositionColored();
                for (var i = 0; i < count; i++)
                    stream.AddVertex(new VertexPositionColored(shape.Lines[start + i], color));
                vertices.Unmap();

                var lines = count / 2;
                var indices = new IndexBuffer(lines * IndexLine.GetSizeInShortInts());
                indices.Map(lines * IndexLine.GetSizeInShortInts());
                var indexStream = indices.GetIndexStreamLine();
                for (var i = 0; i < lines; i++)
                    indexStream.AddLine(new IndexLine(2 * i, 2 * i + 1));
                indices.Unmap();

                yield return new Batch(vertices, count, indices, lines * IndexLine.GetSizeInShortInts(), VertexFormatBits.PositionColored,
                    PrimitiveType.LineList, lines, null);
            }
        }

        private static IEnumerable<Batch> FaceBatches(TemporaryShape shape)
        {
            var color = new ColorWithTransparency(shape.Color.Red, shape.Color.Green, shape.Color.Blue, 0);
            var step = MaxVertices - MaxVertices % 3;
            for (var start = 0; start < shape.Triangles.Count; start += step)
            {
                var count = Math.Min(step, shape.Triangles.Count - start);
                count -= count % 3;
                if (count == 0) yield break;

                var vertices = new VertexBuffer(count * VertexPositionNormalColored.GetSizeInFloats());
                vertices.Map(count * VertexPositionNormalColored.GetSizeInFloats());
                var stream = vertices.GetVertexStreamPositionNormalColored();
                for (var i = 0; i < count; i += 3)
                {
                    var a = shape.Triangles[start + i];
                    var b = shape.Triangles[start + i + 1];
                    var c = shape.Triangles[start + i + 2];
                    var normal = (b - a).CrossProduct(c - a);
                    normal = normal.GetLength() > 1e-12 ? normal.Normalize() : XYZ.BasisZ;
                    stream.AddVertex(new VertexPositionNormalColored(a, normal, color));
                    stream.AddVertex(new VertexPositionNormalColored(b, normal, color));
                    stream.AddVertex(new VertexPositionNormalColored(c, normal, color));
                }
                vertices.Unmap();

                var triangles = count / 3;
                var indices = new IndexBuffer(triangles * IndexTriangle.GetSizeInShortInts());
                indices.Map(triangles * IndexTriangle.GetSizeInShortInts());
                var indexStream = indices.GetIndexStreamTriangle();
                for (var i = 0; i < triangles; i++)
                    indexStream.AddTriangle(new IndexTriangle(3 * i, 3 * i + 1, 3 * i + 2));
                indices.Unmap();

                yield return new Batch(vertices, count, indices, triangles * IndexTriangle.GetSizeInShortInts(), VertexFormatBits.PositionNormalColored,
                    PrimitiveType.TriangleList, triangles, FaceTransparency);
            }
        }

        private static Outline Bounds(IEnumerable<TemporaryShape> shapes)
        {
            Outline bounds = null;
            foreach (var point in shapes.SelectMany(s => s.Lines.Concat(s.Triangles)))
            {
                if (bounds == null) bounds = new Outline(point, point);
                else bounds.AddPoint(point);
            }

            return bounds;
        }

        private sealed class Batch : IDisposable
        {
            public Batch(VertexBuffer vertices, int vertexCount, IndexBuffer indices, int indexCount, VertexFormatBits format,
                PrimitiveType primitive, int primitiveCount, double? transparency)
            {
                Vertices = vertices;
                VertexCount = vertexCount;
                Indices = indices;
                IndexCount = indexCount;
                Format = new VertexFormat(format);
                Effect = new EffectInstance(format);
                if (transparency.HasValue) Effect.SetTransparency(transparency.Value);
                Primitive = primitive;
                PrimitiveCount = primitiveCount;
            }

            public VertexBuffer Vertices { get; }
            public int VertexCount { get; }
            public IndexBuffer Indices { get; }
            public int IndexCount { get; }
            public VertexFormat Format { get; }
            public EffectInstance Effect { get; }
            public PrimitiveType Primitive { get; }
            public int PrimitiveCount { get; }

            public void Dispose()
            {
                Vertices.Dispose();
                Indices.Dispose();
                Format.Dispose();
                Effect.Dispose();
            }
        }
    }
}
