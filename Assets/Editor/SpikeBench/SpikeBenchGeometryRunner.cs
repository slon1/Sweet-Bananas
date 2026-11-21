using System.Globalization;
using System.IO;
using System.Text;
using SweetBananas.Data;
using SweetBananas.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweetBananas.Editor.SpikeBench
{
    public static class SpikeBenchGeometryRunner
    {
        static readonly (int w, int h, float expectedSize)[] Resolutions =
        {
            (1080, 1920, 9.6f),
            (1080, 2160, 10.8f),
            (1080, 2340, 11.7f),
            (1080, 2280, 11.4f)
        };

        [MenuItem("Sweet Bananas/SpikeBench/B24.3 — camera geometry")]
        public static void RunFromMenu()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Data/GameConfig.asset");
            var camera = UnityEngine.Object.FindAnyObjectByType<CameraFitter>()?.GetComponent<Camera>();
            var sb = new StringBuilder();
            sb.AppendLine("world_bottom_edge_y=0");
            foreach (var res in Resolutions)
            {
                float size = config.WorldWidthUnits * 0.5f * res.h / (float)res.w;
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0}x{1} orthographicSize={2:F3} expected={3:F1}",
                    res.w, res.h, size, res.expectedSize));
            }

            if (camera != null)
            {
                float bottom = camera.transform.position.y - camera.orthographicSize;
                sb.AppendLine("current_screen orthographicSize=" +
                                camera.orthographicSize.ToString("F3", CultureInfo.InvariantCulture));
                sb.AppendLine("current_bottom_y=" + bottom.ToString("F3", CultureInfo.InvariantCulture));
            }

            string path = Path.Combine(SpikeBenchMethodology.LogDirectory(), "geometry_b243.txt");
            SpikeBenchMethodology.EnsureLogDirectory();
            File.WriteAllText(path, sb.ToString());
            Debug.Log("Geometry written: " + path);
        }
    }
}