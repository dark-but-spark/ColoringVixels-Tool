using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 把「当前选中的颜色」在画布上对应的待涂体素高亮出来，方便肉眼快速定位（3D 版）。
    ///
    /// 实现方式：在 OnGUI(Repaint) 阶段把每个待涂体素的世界坐标用主相机投影到屏幕，
    /// 叠加一块半透明色标，不改动游戏数据。只处理当前层（游戏一次也只让涂这一层）。
    /// </summary>
    internal class ColorHighlighter : MonoBehaviour
    {
        public static ColorHighlighter Instance;

        private static Texture2D _px;

        private static Texture2D Px
        {
            get
            {
                if (_px == null)
                {
                    _px = new Texture2D(1, 1, TextureFormat.RGBA32, false)
                    {
                        hideFlags = HideFlags.HideAndDontSave,
                        filterMode = FilterMode.Point
                    };
                    _px.SetPixel(0, 0, Color.white);
                    _px.Apply();
                }
                return _px;
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (Plugin.HighlightMode == null || Plugin.HighlightMode.Value == 0) return;

            var b = GameApi.B;
            if (!GameApi.InLevel(b)) return;

            int colour = b.selectedCol;
            if (colour < 0) return;
            if (GameApi.Hidden(b, colour)) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            int layer = b.selectedLayer;
            if (layer < 0) return;
            if (!GameApi.Size(b, out int xn, out int _, out int zn)) return;

            float alpha = Mathf.Clamp01(Plugin.HighlightAlpha.Value);
            if (Plugin.HighlightMode.Value == 2)
                alpha *= 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 4.5f);

            Color baseCol = Plugin.HighlightColor;
            Color col = new Color(baseCol.r, baseCol.g, baseCol.b, alpha);
            bool onlyPending = Plugin.HighlightOnlyPending.Value;
            short[,,] vp = b.voxelProgress;

            for (int x = 0; x < xn; x++)
            {
                for (int z = 0; z < zn; z++)
                {
                    int t = GameApi.TargetColour(b, x, layer, z);
                    if (t != colour) continue;
                    if (onlyPending && vp[x, layer, z] == t) continue;

                    Vector3 sp = cam.WorldToScreenPoint(new Vector3(x + 0.5f, layer + 0.5f, z + 0.5f));
                    if (sp.z <= 0f) continue;

                    float sx = sp.x;
                    float sy = Screen.height - sp.y;
                    if (sx < -16f || sy < -16f || sx > Screen.width + 16f || sy > Screen.height + 16f) continue;

                    float size = Mathf.Clamp(260f / Mathf.Max(1f, sp.z), 4f, 46f);
                    Fill(new Rect(sx - size * 0.5f, sy - size * 0.5f, size, size), col);
                }
            }
        }

        private static void Fill(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Px);
            GUI.color = prev;
        }
    }
}
