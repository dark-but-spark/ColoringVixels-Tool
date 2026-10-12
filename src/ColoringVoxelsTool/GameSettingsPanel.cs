using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 定位游戏自带的「设置」界面（3D 版），用于导出层级做排查。
    ///
    /// 3D 版的设置控件是 <see cref="SettingsToggle"/> / <see cref="SettingsSlider"/> /
    /// <see cref="SettingsElement"/>；这里用「带有这些控件的公共父节点」反推设置面板根节点。
    /// </summary>
    internal static class GameSettingsPanel
    {
        private const float CacheSeconds = 0.5f;
        private const int MinControls = 3;

        private static Transform _cached;
        private static float _cacheUntil;

        /// <summary>当前激活的设置面板容器；未打开设置时返回 null。</summary>
        internal static Transform Locate()
        {
            if (_cached != null)
            {
                if (_cached.gameObject.activeInHierarchy) return _cached;
                _cached = null;
            }

            if (Time.unscaledTime < _cacheUntil) return null;
            _cacheUntil = Time.unscaledTime + CacheSeconds;

            // 找一个带有足够设置控件的节点：从一个控件的祖先里往上找。
            var controls = Object.FindObjectsOfType<SettingsElement>();
            for (int i = 0; i < controls.Length && i < 64; i++)
            {
                Transform t = controls[i] != null ? controls[i].transform : null;
                for (int depth = 0; depth < 6 && t != null; depth++)
                {
                    if (Usable(t)) { _cached = t; return _cached; }
                    t = t.parent;
                }
            }
            return null;
        }

        private static bool Usable(Transform t)
        {
            if (t == null || t.parent == null) return false;
            if (t.GetComponent<Canvas>() != null) return false;
            return CountControls(t) >= MinControls;
        }

        internal static int CountControls(Transform root)
        {
            if (root == null) return 0;
            int n = root.GetComponentsInChildren<SettingsToggle>(true).Length;
            if (n >= MinControls) return n;
            n += root.GetComponentsInChildren<SettingsSlider>(true).Length;
            if (n >= MinControls) return n;
            n += root.GetComponentsInChildren<SettingsElement>(true).Length;
            return n;
        }

        /// <summary>导出设置面板的层级结构，用于排查 / 微调注入位置。</summary>
        internal static string Dump(int maxDepth = 6)
        {
            Transform root = Locate();
            if (root == null) return "未找到游戏设置面板（请先打开游戏的设置界面）。";

            var sb = new StringBuilder();
            sb.AppendLine("设置面板根节点: " + Path(root));
            sb.AppendLine("子节点控件数: " + CountControls(root));
            DumpNode(root, 0, maxDepth, sb);
            return sb.ToString();
        }

        /// <summary>导出整个画布层级。</summary>
        internal static string DumpCanvas(int maxDepth = 8)
        {
            var sb = new StringBuilder();

            var canvases = Object.FindObjectsOfType<Canvas>();
            if (canvases == null || canvases.Length == 0) return "当前场景没有 Canvas。";

            foreach (var c in canvases)
            {
                if (c == null) continue;
                sb.AppendLine("Canvas: " + Path(c.transform) + "  [" + c.renderMode + "]");
                DumpNode(c.transform, 0, maxDepth, sb);
            }
            return sb.ToString();
        }

        private static void DumpNode(Transform t, int depth, int maxDepth, StringBuilder sb)
        {
            if (t == null || depth > maxDepth) return;

            sb.Append(new string(' ', depth * 2));
            sb.Append("- ");
            sb.Append(t.gameObject.name);
            if (!t.gameObject.activeSelf) sb.Append(" (inactive)");

            var tag = new List<string>();
            if (t.GetComponent<SettingsToggle>() != null) tag.Add("SettingsToggle");
            if (t.GetComponent<SettingsSlider>() != null) tag.Add("SettingsSlider");
            if (t.GetComponent<SettingsElement>() != null) tag.Add("SettingsElement");
            if (t.GetComponent<UnityEngine.UI.Button>() != null) tag.Add("Button");
            if (t.GetComponent<UnityEngine.UI.Image>() != null) tag.Add("Image");
            if (tag.Count > 0) sb.Append("  <" + string.Join(",", tag.ToArray()) + ">");

            sb.AppendLine();

            for (int i = 0; i < t.childCount; i++)
                DumpNode(t.GetChild(i), depth + 1, maxDepth, sb);
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            int guard = 0;
            while (t != null && guard++ < 12)
            {
                parts.Add(t.gameObject.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }
    }
}
