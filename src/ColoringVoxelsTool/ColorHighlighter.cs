using System.Collections.Generic;
using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 把「当前选中的颜色」在画布上对应的待涂体素高亮出来，方便肉眼快速定位（3D 版）。
    ///
    /// 坐标：方块 (x,y,z) 的世界坐标中心就是整数 (x,y,z)（方块跨度 ±0.5）。
    /// 高亮取方块**顶面四角**投影后的屏幕包围盒，保证盖住整块，而不是只点一个中心点。
    ///
    /// 性能：待高亮体素列表按 (关卡:层:颜色) 缓存；屏幕坐标只在画面变化时重算；每帧只叠加缓存矩形。
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

        // 每个待高亮体素的中心世界坐标 (x, layer, z)
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
                Vector3 c = _cells[i];              // 方块中心 (x, layer, z)
                float top = c.y + 0.5f;             // 方块顶面
                float x0 = c.x - 0.5f, x1 = c.x + 0.5f;
                float z0 = c.z - 0.5f, z1 = c.z + 0.5f;

                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;
                bool ok = true;

                ok &= Project(cam, x0, top, z0, ref minX, ref minY, ref maxX, ref maxY, h);
                ok &= Project(cam, x1, top, z0, ref minX, ref minY, ref maxX, ref maxY, h);
                ok &= Project(cam, x1, top, z1, ref minX, ref minY, ref maxX, ref maxY, h);
                ok &= Project(cam, x0, top, z1, ref minX, ref minY, ref maxX, ref maxY, h);

                if (!ok || maxX < -16f || maxY < -16f || minX > w + 16f || minY > h + 16f)
                {
                    _rects.Add(new Rect(0f, 0f, 0f, 0f));
                    continue;
                }

                _rects.Add(new Rect(minX, minY, Mathf.Max(1f, maxX - minX), Mathf.Max(1f, maxY - minY)));
            }
        }

        private static bool Project(Camera cam, float x, float y, float z,
            ref float minX, ref float minY, ref float maxX, ref float maxY, float h)
        {
            Vector3 sp = cam.WorldToScreenPoint(new Vector3(x, y, z));
            if (sp.z <= 0f) return false;
            float sx = sp.x;
            float sy = h - sp.y;
            if (sx < minX) minX = sx;
            if (sx > maxX) maxX = sx;
            if (sy < minY) minY = sy;
            if (sy > maxY) maxY = sy;
            return true;
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

                    _cells.Add(new Vector3(x, layer, z));
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
