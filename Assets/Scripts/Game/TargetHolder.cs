using System.Collections.Generic;
using SweetBananas.Data;
using UnityEngine;
using Zenject;

namespace SweetBananas.Game
{
    public sealed class TargetHolder : MonoBehaviour
    {
        [SerializeField] SpriteRenderer _renderer;
        [SerializeField] PolygonCollider2D _collider;

        GameConfig _config;
        PhysicsMaterial2D _material;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public int PathsBeforeSimplify { get; private set; }
        public int PointsBeforeSimplify { get; private set; }
        public int PathsAfterSimplify { get; private set; }
        public int PointsAfterSimplify { get; private set; }
#endif

        public Collider2D Collider => _collider;

        [Inject]
        public void Construct(GameConfig config) => _config = config;

        void Awake()
        {
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();
            if (_collider == null)
                _collider = GetComponent<PolygonCollider2D>();

            ApplyMaterial();
            SimplifyColliderPaths();
        }

        void ApplyMaterial()
        {
            if (_collider == null || _config == null)
                return;

            if (_material == null)
                _material = new PhysicsMaterial2D("TargetFriction");
            _material.friction = _config.TargetFriction;
            _material.bounciness = 0f;
            _collider.sharedMaterial = _material;
        }

        void SimplifyColliderPaths()
        {
            if (_collider == null || _renderer == null || _renderer.sprite == null)
                return;

            Sprite sprite = _renderer.sprite;
            var physics = new List<Vector2>(64);
            int shapes = sprite.GetPhysicsShapeCount();
            var paths = new List<Vector2[]>();

            for (int s = 0; s < shapes; s++)
            {
                physics.Clear();
                sprite.GetPhysicsShape(s, physics);
                if (physics.Count < 4)
                    continue;
                if (PolygonArea(physics) < 0.05f)
                    continue;
                paths.Add(physics.ToArray());
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PointsBeforeSimplify = 0;
            PathsBeforeSimplify = paths.Count;
            foreach (Vector2[] p in paths)
                PointsBeforeSimplify += p.Length;
#endif

            const float tolerance = 0.02f;
            for (int i = 0; i < paths.Count; i++)
                paths[i] = DouglasPeucker(paths[i], tolerance);

            var trimmed = new List<Vector2[]>();
            foreach (Vector2[] p in paths)
            {
                if (p.Length < 4)
                    continue;
                if (PolygonArea(p) < 0.05f)
                    continue;
                trimmed.Add(p);
            }

            _collider.pathCount = trimmed.Count;
            for (int i = 0; i < trimmed.Count; i++)
                _collider.SetPath(i, trimmed[i]);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            PathsAfterSimplify = trimmed.Count;
            PointsAfterSimplify = 0;
            foreach (Vector2[] p in trimmed)
                PointsAfterSimplify += p.Length;
#endif
        }

        static float PolygonArea(IReadOnlyList<Vector2> points)
        {
            if (points.Count < 3)
                return 0f;
            float sum = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Count];
                sum += a.x * b.y - b.x * a.y;
            }

            return Mathf.Abs(sum) * 0.5f;
        }

        static Vector2[] DouglasPeucker(Vector2[] points, float tolerance)
        {
            if (points.Length < 3)
                return points;

            int first = 0;
            int last = points.Length - 1;
            var keep = new bool[points.Length];
            keep[first] = true;
            keep[last] = true;
            DouglasPeuckerRange(points, first, last, tolerance * tolerance, keep);

            var result = new List<Vector2>(points.Length);
            for (int i = 0; i < points.Length; i++)
            {
                if (keep[i])
                    result.Add(points[i]);
            }

            return result.ToArray();
        }

        static void DouglasPeuckerRange(Vector2[] points, int first, int last, float toleranceSq, bool[] keep)
        {
            float maxDist = 0f;
            int index = 0;
            Vector2 lineStart = points[first];
            Vector2 lineEnd = points[last];
            for (int i = first + 1; i < last; i++)
            {
                float d = DistancePointToSegmentSquared(points[i], lineStart, lineEnd);
                if (d > maxDist)
                {
                    maxDist = d;
                    index = i;
                }
            }

            if (maxDist > toleranceSq)
            {
                keep[index] = true;
                DouglasPeuckerRange(points, first, index, toleranceSq, keep);
                DouglasPeuckerRange(points, index, last, toleranceSq, keep);
            }
        }

        static float DistancePointToSegmentSquared(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-8f)
                return (p - a).sqrMagnitude;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            Vector2 closest = a + t * ab;
            return (p - closest).sqrMagnitude;
        }
    }
}