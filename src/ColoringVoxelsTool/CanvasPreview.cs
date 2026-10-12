using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 「预览」页用的**当前层**缩略图（3D 版）：直接把游戏自己的体素数据拼成一张 Texture2D。
    ///
    /// 数据来源（全是游戏现成的，不截图、不受相机角度影响）：
    ///   Builder.voxelData[x,y,z].colourIndex —— 目标颜色编号
    ///   Builder.voxelProgress[x,y,z]         —— 已涂颜色编号
    ///   LevelVersionData.colours[t]          —— 颜色真彩
    ///
    /// 一格 = 一个像素，采样用 Point：放大之后就是清楚的方格。只画当前选中的那一层。
    /// 已涂对 = 目标色；没涂 = 压暗的「底稿」（可以关掉）；不属于图案的格子 = 全透明。
    /// </summary>
    internal sealed class CanvasPreview
    {
        public Texture2D Texture { get; private set; }
        public int Cols { get; private set; }
        public int Rows { get; private set; }
        public int Painted { get; private set; }
        public int Total { get; private set; }

        /// <summary>未涂的格子是否显示暗色底稿。</summary>
        public bool ShowGhost { get; set; } = true;

        private const float RebuildInterval = 0.25f;
        private const float HeartbeatInterval = 1.5f;

        private float _cooldown;
        private float _idle;
        private string _key = "";
        private bool _lastGhost;
        private int _lastPainted = -1;

        /// <summary>每帧调用：数据变了才重建贴图（带节流）。</summary>
        public void Refresh(float delta)
        {
            _cooldown -= delta;
            _idle += delta;

            var b = GameApi.B;
            if (!GameApi.InLevel(b) || b.selectedLayer < 0)
            {
                Release();
                _key = "";
                _lastPainted = -1;
                return;
            }

            int painted = GameApi.PaintedVoxels(b);
            string key = GameApi.LevelKey + ":" + b.selectedLayer;
            bool changed = Texture == null
                           || key != _key
                           || ShowGhost != _lastGhost
                           || painted != _lastPainted
                           || _idle >= HeartbeatInterval;

            if (!changed || _cooldown > 0f) return;

            _cooldown = RebuildInterval;
            _idle = 0f;
            Build(b, key, painted);
        }

        /// <summary>释放贴图（离开关卡 / 面板关闭时调用）。</summary>
        public void Release()
        {
            if (Texture == null) return;
            try { UnityEngine.Object.Destroy(Texture); } catch { }
            Texture = null;
        }

        private void Build(Builder b, string key, int painted)
        {
            if (!GameApi.Size(b, out int cols, out int _, out int rows)) { Release(); return; }
            int layer = b.selectedLayer;
            if (layer < 0 || layer >= b.voxelProgress.GetLength(1)) { Release(); return; }

            var px = new Color32[cols * rows];
            Color32 clear = new Color32(0, 0, 0, 0);
            short[,,] vp = b.voxelProgress;

            int paintedCount = 0;
            int total = 0;

            for (int x = 0; x < cols; x++)
            {
                for (int z = 0; z < rows; z++)
                {
                    int t = GameApi.TargetColour(b, x, layer, z);
                    Color32 c = clear;

                    if (t >= 0 && !GameApi.Hidden(b, t))
                    {
                        total++;
                        Color col = GameApi.ColourOf(b, t);
                        if (vp[x, layer, z] == t)
                        {
                            paintedCount++;
                            c = col;
                        }
                        else if (ShowGhost)
                        {
                            c = new Color(col.r * 0.30f + 0.02f, col.g * 0.30f + 0.02f, col.b * 0.30f + 0.02f, 1f);
                        }
                    }

                    px[z * cols + x] = c;
                }
            }

            if (Texture == null || Texture.width != cols || Texture.height != rows)
            {
                Release();
                Texture = new Texture2D(cols, rows, TextureFormat.RGBA32, false);
                Texture.filterMode = FilterMode.Point;
                Texture.wrapMode = TextureWrapMode.Clamp;
                Texture.hideFlags = HideFlags.HideAndDontSave;
            }

            Texture.SetPixels32(px);
            Texture.Apply(false);

            Cols = cols;
            Rows = rows;
            Painted = paintedCount;
            Total = total;
            _key = key;
            _lastGhost = ShowGhost;
            _lastPainted = painted;
        }
    }
}
