using Unity.Collections;
using UnityEngine;
using Zenject;

namespace SweetBananas.Game
{
    public sealed class RibbonMesh : MonoBehaviour
    {
        public const float OpaqueLengthUnits = 3.39f;
        public const float OpaqueThicknessUnits = 1.17f;

        [SerializeField] float _visualThickness = OpaqueThicknessUnits;
        [SerializeField] Vector2 _uvMin = new(0.022535f, 0.016807f);
        [SerializeField] Vector2 _uvMax = new(0.977465f, 1f);
        [SerializeField] Material _material;

        Ribbon _ribbon;
        Mesh _mesh;
        MeshFilter _filter;
        MeshRenderer _renderer;
        NativeArray<Vector3> _vertices;
        NativeArray<Vector2> _uvs;
        Vector2[] _sections;
        bool _ready;

        public float VisualThickness => _visualThickness;
        public Vector2 UvMin => _uvMin;
        public Vector2 UvMax => _uvMax;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public long LastAllocBytes { get; private set; }
#endif

        [Inject]
        public void Construct(Ribbon ribbon)
        {
            _ribbon = ribbon;
        }

        void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            if (_filter == null)
                _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            if (_renderer == null)
                _renderer = gameObject.AddComponent<MeshRenderer>();

            _mesh = new Mesh { name = "Ribbon" };
            _mesh.MarkDynamic();
            _filter.sharedMesh = _mesh;
            if (_material != null)
                _renderer.sharedMaterial = _material;
            _renderer.sortingLayerName = "World";
            _renderer.sortingOrder = 2;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        void LateUpdate()
        {
            if (_ribbon == null || _ribbon.Bodies.Count < 2)
                return;

            int sections = _ribbon.Bodies.Count + 1;
            EnsureBuffers(sections);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long before = System.GC.GetAllocatedBytesForCurrentThread();
#endif
            Fill(sections);
            int vertCount = sections * 2;
            _mesh.SetVertices(_vertices, 0, vertCount);
            _mesh.SetUVs(0, _uvs, 0, vertCount);
            _mesh.RecalculateBounds();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LastAllocBytes = System.GC.GetAllocatedBytesForCurrentThread() - before;
#endif
        }

        void EnsureBuffers(int sections)
        {
            int verts = sections * 2;
            if (_ready && _vertices.Length == verts)
                return;

            if (_vertices.IsCreated)
                _vertices.Dispose();
            if (_uvs.IsCreated)
                _uvs.Dispose();
            _vertices = new NativeArray<Vector3>(verts, Allocator.Persistent);
            _uvs = new NativeArray<Vector2>(verts, Allocator.Persistent);
            _sections = new Vector2[sections];
            var tris = new int[(sections - 1) * 6];
            int t = 0;
            for (int i = 0; i < sections - 1; i++)
            {
                int v = i * 2;
                tris[t++] = v;
                tris[t++] = v + 2;
                tris[t++] = v + 1;
                tris[t++] = v + 1;
                tris[t++] = v + 2;
                tris[t++] = v + 3;
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetTriangles(tris, 0, false);
            _ready = true;
        }

        void OnDestroy()
        {
            if (_vertices.IsCreated)
                _vertices.Dispose();
            if (_uvs.IsCreated)
                _uvs.Dispose();
            if (_mesh != null)
                Destroy(_mesh);
        }

        void Fill(int sections)
        {
            var bodies = _ribbon.Bodies;
            int count = bodies.Count;
            float halfLen = _ribbon.SegmentLength * 0.5f;
            Vector2 firstDir = Direction(bodies[0].rotation);
            Vector2 lastDir = Direction(bodies[count - 1].rotation);
            _sections[0] = bodies[0].position - firstDir * halfLen;
            for (int i = 0; i < _ribbon.Joints.Count; i++)
            {
                HingeJoint2D joint = _ribbon.Joints[i];
                Vector2 a = joint.attachedRigidbody.transform.TransformPoint(joint.anchor);
                Vector2 b = joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);
                _sections[i + 1] = (a + b) * 0.5f;
            }

            _sections[sections - 1] = bodies[count - 1].position + lastDir * halfLen;

            float half = _visualThickness * 0.5f;
            for (int i = 0; i < sections; i++)
            {
                Vector2 prev = _sections[Mathf.Max(0, i - 1)];
                Vector2 next = _sections[Mathf.Min(sections - 1, i + 1)];
                Vector2 tangent = next - prev;
                if (tangent.sqrMagnitude < 1e-8f)
                    tangent = i == 0 ? firstDir : lastDir;
                tangent.Normalize();
                Vector2 normal = new Vector2(-tangent.y, tangent.x);
                Vector2 p = _sections[i];
                int v = i * 2;
                _vertices[v] = p + normal * half;
                _vertices[v + 1] = p - normal * half;
                float u = sections == 1 ? 0f : i / (float)(sections - 1);
                float uu = Mathf.Lerp(_uvMin.x, _uvMax.x, u);
                _uvs[v] = new Vector2(uu, _uvMax.y);
                _uvs[v + 1] = new Vector2(uu, _uvMin.y);
            }
        }

        static Vector2 Direction(float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        }
    }
}
