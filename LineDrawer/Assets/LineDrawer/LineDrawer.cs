using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor.Compilation;
using UnityEditor;
#endif

namespace AALineDrawer
{
    /// <summary>
    /// This class is a simple way to draw an anti-aliased screen-space-constant line with colour and width varying across. It's not a Gizmo or Handle so it will work just fine in builds.
    /// </summary>
    [ExecuteInEditMode]
    public class LineDrawer : MonoBehaviour
    {
        #region Constants/Fields

        private const string MATERIAL_PATH = "Material_LineDrawer";

        private readonly static int POINT_DATA_BUFFER = Shader.PropertyToID("_PointDataBuffer");
        private readonly static int POINT_COUNT = Shader.PropertyToID("_PointCount");

        [SerializeField]
        //[HideInInspector]
        private Material m_material;

        [SerializeField]
        [Min(0f)]
        private float m_meshPadding = 1f;
        public float MeshPadding => m_meshPadding;

        [SerializeField]
        private MeshMode m_meshMode = MeshMode.AxisAligned;
        public MeshMode CurrentMeshMode => m_meshMode;

        [SerializeField]
        [HideInInspector]
        private readonly Vector3[] m_meshVertices = new Vector3[4];
        private readonly List<Vector3> m_convexHull = new List<Vector3>();
        private readonly int[] m_meshTriangles = { 0, 2, 1, 2, 0, 3 };

        [SerializeField]
        [HideInInspector]
        private List<PointData> m_points = new List<PointData>();

        private ComputeBuffer m_pointsDataBuffer;
        private MaterialPropertyBlock m_materialPropertyBlock;
        private Mesh m_mesh;

        public bool IsVisible { get; private set; } = true;

        public enum MeshMode { AxisAligned, Oriented };
        private enum PointLineRelationship { OnLeft, OnRight, Colinear };

        [SerializeField]
        private float m_defaultWidth = 1f;
        public float DefaultWidth => m_defaultWidth;

        [SerializeField]
        private Color m_defaultColour = Color.white;
        public Color DefaultColour => m_defaultColour;

        private bool m_initialized = false;

        [SerializeField]
        [HideInInspector]
        // this bool is toggled off/on whenever the Unity callbacks OnEnable/OnDisable are called.
        // note that this doesn't always give the same result as "enabled" because OnEnable/OnDisable are
        // called during recompiles etc. you can basically read this bool as "is recompiling"
        private bool m_isEnabled = false;

        #endregion

        #region Unity Callbacks

        private void Reset() => Init();
        private void OnValidate() => Init();
        private void OnEnable()
        {
#if UNITY_EDITOR
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif

            m_isEnabled = true;

            Init();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
            m_isEnabled = false;

            m_pointsDataBuffer?.Dispose();

            DestroyImmediate(m_mesh);
            m_mesh = null;
        }

        private void Update()
        {
            if (!m_initialized)
                Init();

            if (IsVisible)
                Graphics.DrawMesh(m_mesh, Matrix4x4.identity, m_material, gameObject.layer, null, 0, m_materialPropertyBlock, false, false);
        }

        #endregion

        #region Init + Events

        /// <summary>
        /// Called when the line data is first created, e.g. from OnEnable.
        /// </summary>
        private void Init()
        {
            m_initialized = true;
            m_material = Resources.Load<Material>(MATERIAL_PATH);

            m_materialPropertyBlock = new MaterialPropertyBlock();

            OnPointsChanged();
        }

        /// <summary>
        /// Called every time any number of points are added or removed.
        /// </summary>
        private void OnPointsChanged()
        {
            if (!m_isEnabled)
                return;

            if (m_pointsDataBuffer == null || !m_pointsDataBuffer.IsValid() || m_points.Count > m_pointsDataBuffer.count)
            {
                m_pointsDataBuffer?.Dispose();
                m_pointsDataBuffer = new ComputeBuffer(Mathf.Max(1, m_points.Count), PointData.Stride);
            }

            m_pointsDataBuffer.SetData(m_points);

            if (m_materialPropertyBlock == null)
                m_materialPropertyBlock = new MaterialPropertyBlock();

            m_materialPropertyBlock.SetBuffer(POINT_DATA_BUFFER, m_pointsDataBuffer);
            m_materialPropertyBlock.SetInt(POINT_COUNT, m_points.Count);

            GenerateMesh();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Set the default colour, which will be set on any new points not explicitly given a colour.
        /// </summary>
        public void SetDefaultColour(Color colour)
        {
            m_defaultColour = colour;
        }

        /// <summary>
        /// Set the default width, which will be set on any new points not explicitly given a width.
        /// </summary>
        public void SetDefaultWidth(float width)
        {
            m_defaultWidth = width;
        }

        /// <summary>
        /// Set the width of the point at the given index. Returns true on success.
        /// </summary>
        public bool SetPointWidth(int index, float width)
        {
            if (m_points == null || m_points.Count == 0)
                return false;

            if (index < 0 || index >= m_points.Count)
                return false;

            PointData pointData = m_points[index];
            pointData.Width = width;
            m_points[index] = pointData;

            OnPointsChanged();

            return true;
        }

        /// <summary>
        /// Set the colour of the point at the given index. Returns true on success.
        /// </summary>
        public bool SetPointColour(int index, Color colour)
        {
            if (m_points == null || m_points.Count == 0)
                return false;

            if (index < 0 || index >= m_points.Count)
                return false;

            PointData pointData = m_points[index];
            pointData.Colour = colour;
            m_points[index] = pointData;

            OnPointsChanged();

            return true;
        }

        /// <summary>
        /// Set the position of the point at the given index. Returns true on success.
        /// </summary>
        public bool SetPointPosition(int index, Vector2 position)
        {
            if (m_points == null || m_points.Count == 0)
                return false;

            if (index < 0 || index >= m_points.Count)
                return false;

            PointData pointData = m_points[index];
            pointData.Position = position;
            m_points[index] = pointData;

            OnPointsChanged();

            return true;
        }

        /// <summary>
        /// Set the description of the point at the given index. Returns true on success.
        /// </summary>
        public bool SetPointPosition(int index, PointData pointData)
        {
            if (m_points == null || m_points.Count == 0)
                return false;

            if (index < 0 || index >= m_points.Count)
                return false;

            m_points[index] = pointData;

            OnPointsChanged();

            return true;
        }

        /// <summary>
        /// Replace the line with a new one. Each new line segment will have the given width and colour.
        /// </summary>
        public void SetPoints(float width, Color colour, params Vector2[] points)
        {
            m_points.Clear();
            AddPoints(width, colour, points);
        }

        /// <summary>
        /// Replace the line with a new one. Each new line segment will have the given point data.
        /// </summary>
        public void SetPoints(params PointData[] points)
        {
            m_points.Clear();
            AddPoints(points);
        }

        /// <summary>
        /// Replace the line with a new one. Each new line segment will have the given point data, up to the given length.
        /// </summary>
        public void SetPoints(int length, params PointData[] points)
        {
            m_points.Clear();

            if (length <= 0)
                return;

            AddPoints(points, length);
        }

        /// <summary>
        /// Replace the line with a new one. Each new line segment will have the given point data.
        /// </summary>
        public void SetPoints(IList<PointData> points)
        {
            m_points.Clear();
            AddPoints(points);
        }

        /// <summary>
        /// Replace the line with a new one. Each new line segment will have the given colour.
        /// </summary>
        public void SetPoints(Color colour, params Vector2[] positions) => SetPoints(m_defaultWidth, colour, positions);

        /// <summary>
        /// Replace the line with a new one. Each new line segment will have the given width.
        /// </summary>
        public void SetPoints(float width, params Vector2[] positions) => SetPoints(width, m_defaultColour, positions);

        /// <summary>
        /// Replace the line with a new one.
        /// </summary>
        public void SetPoints(params Vector2[] positions) => SetPoints(m_defaultWidth, m_defaultColour, positions);

        /// <summary>
        /// Add a number of new points to the line. Each new line segment will have the given width and colour.
        /// </summary>
        public void AddPoints(float width, Color colour, params Vector2[] points)
        {
            width = Mathf.Max(0f, width);

            for (int i = 0; i < points.Length; i++)
            {
                PointData pointData = new PointData()
                {
                    Position = points[i],
                    Width = width,
                    Colour = colour
                };

                m_points.Add(pointData);
            }

            OnPointsChanged();
        }

        /// <summary>
        /// Add a number of new points to the line. Each new line segment will have the given width and colour.
        /// </summary>
        public void AddPoints(float width, Color colour, IList<Vector2> points)
        {
            width = Mathf.Max(0f, width);

            for (int i = 0; i < points.Count; i++)
            {
                PointData pointData = new PointData()
                {
                    Position = points[i],
                    Width = width,
                    Colour = colour
                };

                m_points.Add(pointData);
            }

            OnPointsChanged();
        }

        /// <summary>
        /// Add a number of new points to the line.
        /// </summary>
        public void AddPoints(params PointData[] pointData)
        {
            for (int i = 0; i < pointData.Length; i++)
                m_points.Add(pointData[i]);

            OnPointsChanged();
        }

        /// <summary>
        /// Add a number of new points to the line.
        /// </summary>
        public void AddPoints(IList<PointData> pointData)
        {
            for (int i = 0; i < pointData.Count; i++)
                m_points.Add(pointData[i]);

            OnPointsChanged();
        }

        /// <summary>
        /// Add a number of new points to the line, up to the given length.
        /// </summary>
        public void AddPoints(IList<PointData> pointData, int length)
        {
            for (int i = 0; i < Mathf.Min(length, pointData.Count); i++)
                m_points.Add(pointData[i]);

            OnPointsChanged();
        }

        /// <summary>
        /// Add a number of new points to the line. Each new line segment will have the given colour.
        /// </summary>
        public void AddPoints(Color colour, params Vector2[] positions) => AddPoints(m_defaultWidth, colour, positions);

        /// <summary>
        /// Add a number of new points to the line. Each new line segment will have the given width.
        /// </summary>
        public void AddPoints(float width, params Vector2[] positions) => AddPoints(width, m_defaultColour, positions);

        /// <summary>
        /// Add a number of new points to the line.
        /// </summary>
        public void AddPoints(params Vector2[] positions) => AddPoints(m_defaultWidth, m_defaultColour, positions);

        /// <summary>
        /// Add a new point to the line. The point will have the given width and colour.
        /// </summary>
        public int AddPoint(Vector2 point, float width, Color colour)
        {
            width = Mathf.Max(0f, width);

            PointData pointData = new PointData()
            {
                Position = point,
                Width = width,
                Colour = colour
            };

            return AddPoint(pointData);
        }

        /// <summary>
        /// Add a new point to the line. The point will be described by the given point data struct.
        /// </summary>
        public int AddPoint(PointData pointData)
        {
            Debug.Assert(pointData.Width >= 0f, "Point Data structs must have non-negative width!");

            m_points.Add(pointData);

            OnPointsChanged();

            return m_points.Count - 1;
        }

        /// <summary>
        /// Add a new point to the line. The point will have the given width.
        /// </summary>
        public int AddPoint(Vector2 position, float width) => AddPoint(position, width, m_defaultColour);

        /// <summary>
        /// Add a new point to the line. The point will have the given colour.
        /// </summary>
        public int AddPoint(Vector2 position, Color colour) => AddPoint(position, m_defaultWidth, colour);

        /// <summary>
        /// Add a new point to the line.
        /// </summary>
        public int AddPoint(Vector2 position) => AddPoint(position, m_defaultWidth, m_defaultColour);

        /// <summary>
        /// Remove a point of the line at the given index. The points on either side, if any, will connect. Returns true on success.
        /// </summary>
        public bool RemovePoint(int index)
        {
            if (m_points == null || m_points.Count == 0)
                return false;

            if (index < 0 || index >= m_points.Count)
                return false;

            m_points.RemoveAt(index);
            OnPointsChanged();

            return true;
        }

        /// <summary>
        /// Remove <paramref name="count"/> points, from <paramref name="index"/>. The points on either side, if any, will connect.
        /// </summary>
        public void RemovePoints(int index, int count)
        {
            if (m_points == null || m_points.Count == 0)
                return;

            index = Mathf.Max(0, index);

            m_points.RemoveRange(index, count);
            OnPointsChanged();
        }

        /// <summary>
        /// Remove the last point on the line. Returns true on success.
        /// </summary>
        public bool RemoveLast() => RemovePoint(m_points.Count - 1);

        /// <summary>
        /// Remove the first point on the line. Returns true on success.
        /// </summary>
        public bool RemoveFirst() => RemovePoint(0);

        /// <summary>
        /// Clear all points from the line.
        /// </summary>
        public void Clear()
        {
            m_points.Clear();
            OnPointsChanged();
        }

        #endregion

        private void OnCompilationStarted(object param)
        {
            m_isEnabled = false;

            m_pointsDataBuffer?.Dispose();
        }

#if UNITY_EDITOR
        private void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            // this ensures "m_isEnabled" is set to false while transitioning between play modes
            m_isEnabled = stateChange == PlayModeStateChange.EnteredPlayMode || stateChange == PlayModeStateChange.EnteredEditMode;
        }
#endif

        #region Mesh Generations Methods

        /// <summary>
        /// Generate the parallogram points which wrap the line.
        /// </summary>
        private void GenerateMesh()
        {
            if (m_points.Count == 0)
            {
                IsVisible = false;
                return;
            }

            GenerateBoundingBox();

            float dist = Vector2.Distance(m_meshVertices[0], m_meshVertices[2]);
            IsVisible = dist > 0.1f && !float.IsInfinity(dist);

            if (!IsVisible)
                return;

            if (m_mesh == null)
                m_mesh = new Mesh();

            m_mesh.SetVertices(m_meshVertices);
            m_mesh.SetTriangles(m_meshTriangles, 0);
            m_mesh.RecalculateBounds();
        }

        /// <summary>
        /// Generate a simple parallelogram that wraps the whole generated line.
        /// </summary>
        private void GenerateBoundingBox()
        {
            float maxWidth = 0f;
            for (int i = 0; i < m_points.Count; i++)
                maxWidth = Mathf.Max(m_points[i].Width, maxWidth);

            float padding = m_meshPadding + Mathf.Max(0f, 0.1f * (maxWidth - 1f));

            if (m_meshMode == MeshMode.Oriented && FindConvexHull())
            {
                FindMinimalOrientedBoundingBox(padding);
            }
            else
            {
                Vector3 bottomLeft = Vector2.positiveInfinity;
                Vector3 topRight = Vector2.negativeInfinity;

                for (int i = 0; i < m_points.Count; i++)
                {
                    Vector2 pos = m_points[i].Position;

                    bottomLeft = new Vector3(Mathf.Min(bottomLeft.x, pos.x), Mathf.Min(bottomLeft.y, pos.y));
                    topRight = new Vector3(Mathf.Max(topRight.x, pos.x), Mathf.Max(topRight.y, pos.y));
                }

                bottomLeft -= new Vector3(padding, padding);
                topRight += new Vector3(padding, padding);

                m_meshVertices[0] = bottomLeft;
                m_meshVertices[1] = new Vector3(topRight.x, bottomLeft.y);
                m_meshVertices[2] = topRight;
                m_meshVertices[3] = new Vector3(bottomLeft.x, topRight.y);
            }
        }

        /// <summary>
        /// Run the gift wrapping algorithm on the current set of points in order to find a convex hull around them. Store the result in <see cref="m_convexHull"/>.
        /// 
        /// Returns false on failure.
        /// </summary>
        private bool FindConvexHull()
        {
            if (m_points == null || m_points.Count == 0)
                return false;

            int leftMost = 0;

            for (int j = 1; j < m_points.Count; j++)
            {
                if (m_points[j].Position.x < m_points[leftMost].Position.x)
                    leftMost = j;
            }

            // shouldn't really happen but i'd feel weird leaving in a possible infinite loop
            const int maxIterations = 2000;

            m_convexHull.Clear();

            int currentPoint = leftMost;
            int failCount = 0;
            int pointCount = m_points.Count;

            // starting from the leftmost point, draw a line from that point to every other point, looking for whichever point forms a line is on the left of every other point.
            // this represents the next edge of the convex hull. repeat from that point, and keep going until we circle back around to the first point. 
            do
            {
                Vector3 current = m_points[currentPoint].Position;

                m_convexHull.Add(current);

                int candidatePoint = (currentPoint + 1) % pointCount;
                Vector3 candidate = m_points[candidatePoint].Position;

                for (int i = 0; i < pointCount; i++)
                {
                    // we found a better candidate
                    if (i != candidatePoint && i != currentPoint)
                    {
                        if (GetPointLineRelationship(current, m_points[i].Position, candidate) != PointLineRelationship.OnRight)
                            candidatePoint = i;
                    }
                }

                currentPoint = candidatePoint;

                ++failCount;

            } while (currentPoint != leftMost && failCount < maxIterations);

            if (failCount >= maxIterations)
                return false;

            m_convexHull.Add(m_points[leftMost].Position);

            return true;
        }

        /// <summary>
        /// An extremely brute-force implementation of the rotating calipers algorithm. Finds the smallest possible rectangle around a convex hull and stores the results
        /// in <see cref="m_meshVertices"/>.
        /// </summary>
        private void FindMinimalOrientedBoundingBox(float padding)
        {
            float leastSqrArea = Mathf.Infinity;
            Vector3 bestMin = Vector3.zero;
            Vector3 bestMax = Vector3.zero;
            Quaternion bestRot = Quaternion.identity;
            int hullCount = m_convexHull.Count;

            // iterate over every edge in the convex hull.
            // for each edge, rotate the entire shape so that that edge is aligned with the y axis.
            // then generate a simple axis aligned bounding box for that rotated polygon, compute the area, and see
            // if that rectangle is smaller than the current smallest.
            for (int i = 0; i < hullCount; i++)
            {
                Vector3 a = m_convexHull[i];
                Vector3 b = m_convexHull[(i + 1) % hullCount];

                Quaternion rotation = Quaternion.FromToRotation((b - a).normalized, Vector3.up);

                Vector3 min = Vector3.positiveInfinity;
                Vector3 max = Vector3.negativeInfinity;

                for (int j = 0; j < hullCount; j++)
                {
                    Vector3 rotated = rotation * m_convexHull[j];

                    min = new Vector3(Mathf.Min(rotated.x, min.x), Mathf.Min(rotated.y, min.y));
                    max = new Vector3(Mathf.Max(rotated.x, max.x), Mathf.Max(rotated.y, max.y));
                }

                Vector3 corner = new Vector3(max.x, min.y);

                float sqrArea = Vector3.Cross((corner - min), (max - corner)).sqrMagnitude;

                if (sqrArea < leastSqrArea)
                {
                    leastSqrArea = sqrArea;
                    bestMin = min;
                    bestMax = max;
                    bestRot = rotation;
                }
            }

            // now we have our smallest bounding box we need to rotate it back into its original orientation
            bestRot = Quaternion.Inverse(bestRot);

            Vector3 _a = bestRot * bestMin;
            Vector3 _b = bestRot * new Vector3(bestMax.x, bestMin.y);
            Vector3 _c = bestRot * bestMax;
            Vector3 _d = bestRot * new Vector3(bestMin.x, bestMax.y);

            // and then we do some witchcraft to apply some nice padding to the final result
            Vector3 centroid = (_a + _b + _c + _d) * 0.25f;

            Vector3 _abCross = Vector3.Cross(Vector3.back, (_b - _a));
            Vector3 _bcCross = Vector3.Cross(Vector3.back, (_c - _b));
            Vector3 _cdCross = Vector3.Cross(Vector3.back, (_d - _c));
            Vector3 _daCross = Vector3.Cross(Vector3.back, (_a - _d));

            m_meshVertices[0] = _a + ((_a - centroid) + (_abCross + _daCross)).normalized * padding;
            m_meshVertices[1] = _b + ((_b - centroid) + (_abCross + _bcCross)).normalized * padding;
            m_meshVertices[2] = _c + ((_c - centroid) + (_bcCross + _cdCross)).normalized * padding;
            m_meshVertices[3] = _d + ((_d - centroid) + (_cdCross + _daCross)).normalized * padding;
        }

        #endregion

        #region Static Methods

        /// <summary>
        /// Determine whether the given point <paramref name="p"/> is to the left of the line from <paramref name="a"/> to <paramref name="b"/>, to the right, or colinear.
        /// 
        /// The 'left'/'right' relationship is as if you were at point <paramref name="a"/>, looking at point <paramref name="b"/>.
        /// </summary>
        private static PointLineRelationship GetPointLineRelationship(Vector3 a, Vector3 b, Vector3 p)
        {
            float val = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);

            if (val > 0f)
                return PointLineRelationship.OnLeft;
            else if (val < 0f)
                return PointLineRelationship.OnRight;

            return PointLineRelationship.Colinear;
        }

        #endregion

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        [System.Serializable]
        public struct PointData
        {
            public static int Stride = sizeof(float) * 7;

            public Vector2 Position;
            public float Width;
            public Color Colour;
        }
    }

}