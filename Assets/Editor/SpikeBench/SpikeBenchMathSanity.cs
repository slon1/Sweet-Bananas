using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchMathSanity
    {
        const float ExpectedX = 3.8660254f;
        const float ExpectedY = 2.5f;

        [MenuItem("Sweet Bananas/SpikeBench/Run Math Sanity")]
        public static void Run()
        {
            SpikeBenchMethodology.EnsureLogDirectory();
            var go = new GameObject("SpikeBenchMathSanity");
            var body = go.AddComponent<Rigidbody2D>();
            body.position = new Vector2(3f, 2f);
            body.rotation = 30f;
            body.bodyType = RigidbodyType2D.Kinematic;

            Vector2 local = new Vector2(1f, 0f);
            Vector2 rel = body.GetRelativePoint(local);
            Vector2 inv = body.GetPoint(local);

            float errRel = Vector2.Distance(rel, new Vector2(ExpectedX, ExpectedY));
            bool verdict = errRel <= 0.001f;

            var sb = new StringBuilder();
            sb.AppendLine("body_pos=(3, 2) rotation_deg=30");
            sb.AppendLine("local_point=(1, 0)");
            sb.AppendLine("expected_world=(" + ExpectedX.ToString(CultureInfo.InvariantCulture) + ", " + ExpectedY.ToString(CultureInfo.InvariantCulture) + ")");
            sb.AppendLine("GetRelativePoint=" + rel);
            sb.AppendLine("GetPoint=" + inv);
            sb.AppendLine("error_GetRelativePoint=" + errRel.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("verdict=" + (verdict ? "PASS" : "FAIL"));

            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), "math_sanity.txt");
            File.WriteAllText(path, sb.ToString());
            Object.DestroyImmediate(go);

            Debug.Log("SpikeBench Math Sanity: " + (verdict ? "PASS" : "FAIL") + " — see " + path);
        }
    }
}