using System.Collections.Generic;
using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 把「当前选中的颜色」在画布上对应的待涂体素高亮出来，方便肉眼快速定位（3D 版）。
    ///
    /// 性能：
    ///   · 待高亮的体素列表按 (关卡:层:颜色) 缓存，每 0.5s 或键变化才重扫一层；
    ///   · 屏幕坐标只在**画面变化时**（相机移动 / 旋转 / 缩放 / 分辨率变化）才重算，静止时直接复用；
    ///   · 每帧只做「按缓存矩形叠加半透明色块」，并设上限，避免大图卡顿。
    /// IMGUI 是立即模式，叠加层仍需每帧重画，但上面的重活都只在必要时做。
    /// </summary>
    internal class ColorHighlighter : MonoBehaviour
    {
        public static ColorHighlighter Instance;

        private const int MaxMarkers = 3000;
        private const float RebuildInterval = 0.5f;

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

        private readonly List<Vector3> _cells = new List<Vector3>();
        private readonly List<Rect> _rects = new List<Rect>();
        private string _cacheKey = "";
        private float _rebuildAt;
        private bool _rectsValid;

        private Vector3 _camPos;
        private Quaternion _camRot;
        private float _camFov;
        private int _sw, _sh;

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

            bool onlyPending = Plugin.HighlightOnlyPending.Value;

            string key = GameApi.LevelKey + ":" + layer + ":" + colour + ":" + (onlyPending ? 1 : 0);
            if (key != _cacheKey || Time.unscaledTime >= _rebuildAt)
            {
                Rebuild(b, layer, colour, onlyPending);
                _cacheKey = key;
                _rebuildAt = Time.unscaledTime + RebuildInterval;
                _rectsValid = false;
            }
            if (_cells.Count == 0) return;

            if (!_rectsValid || ViewChanged(cam))
            {
                RecomputeRects(cam);
                _rectsValid = true;
            }

            float alpha = Mathf.Clamp01(Plugin.HighlightAlpha.Value);
            if (Plugin.HighlightMode.Value == 2)
                alpha *= 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 4.5f);

            Color baseCol = Plugin.HighlightColor;
            Color col = new Color(baseCol.r, baseCol.g, baseCol.b, alpha);

            for (int i = 0; i < _rects.Count; i++)
            {
                Rect r = _rects[i];
                if (r.width <= 0f) continue;
                Fill(r, col);
            }
        }

        private bool ViewChanged(Camera cam)
        {
            return cam.transform.position != _camPos
                   || cam.transform.rotation != _camRot
                   || !Mathf.Approximately(cam.fieldOfView, _camFov)
                   || Screen.width != _sw || Screen.height != _sh;
        }

        private void RecomputeRects(Camera cam)
        {
            _rects.Clear();
            _camPos = cam.transform.position;
            _camRot = cam.transform.rotation;
            _camFov = cam.fieldOfView;
            _sw = Screen.width;
            _sh = Screen.height;

            float w = Screen.width, h = Screen.height;
            for (int i = 0; i < _cells.Count; i++)
            {
                Vector3 sp = cam.WorldToScreenPoint(_cells[i]);
                if (sp.z <= 0f) { _rects.Add(new Rect(0f, 0f, 0f, 0f)); continue; }

                float sx = sp.x;
                float sy = h - sp.y;
                if (sx < -16f || sy < -16f || sx > w + 16f || sy > h + 16f)
                {
                    _rects.Add(new Rect(0f, 0f, 0f, 0f));
                    continue;
                }

                float size = Mathf.Clamp(260f / Mathf.Max(1f, sp.z), 4f, 46f);
                _rects.Add(new Rect(sx - size * 0.5f, sy - size * 0.5f, size, size));
            }
        }

        private void Rebuild(Builder b, int layer, int colour, bool onlyPending)
        {
            _cells.Clear();
            if (!GameApi.Size(b, out int xn, out int _, out int zn)) return;

            short[,,] vp = b.voxelProgress;
            for (int x = 0; x < xn; x++)
            {
                for (int z = 0; z < zn; z++)
                {
                    if (GameApi.TargetColour(b, x, layer, z) != colour) continue;
                    if (onlyPending && vp[x, layer, z] == colour) continue;

                    _cells.Add(new Vector3(x + 0.5f, layer + 0.5f, z + 0.5f));
                    if (_cells.Count >= MaxMarkers) return;
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
