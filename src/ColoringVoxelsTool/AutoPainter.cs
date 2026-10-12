using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 拟人自动涂色引擎（3D 体素版）。
    ///
    /// 拟人策略（贴合游戏「一次只涂一层」的规则）：
    ///   1. 从当前层开始，一种颜色一种颜色地涂 —— 先选定一个颜色，把它的待涂体素处理掉，再换下一个；
    ///   2. 同一种颜色内部按「平面分块 + 块间就近移动 + 块内蛇形扫行」生成笔顺，
    ///      在 x-z 层内一格一格推进，像人拿着刷子扫；
    ///   3. 每完成一笔随机停顿；手速带抖动；
    ///   4. 当前层涂满后借游戏自己的「换层」逻辑升到下一层，直到整张图完成。
    /// 落笔统一走 <see cref="GameApi.PaintVoxel"/>，即游戏自己的 SelectCube，所以计数、存档、
    /// 进化到「换色 / 换层」都和手涂一模一样。
    /// </summary>
    internal class AutoPainter : MonoBehaviour
    {
        public static AutoPainter Instance;

        // ---- 运行状态 ----
        public bool Running { get; private set; }
        public string Status { get; private set; } = "待机";
        public int PaintedThisRun { get; private set; }
        public int CurrentLayer { get; private set; } = -1;
        public int CurrentColour { get; private set; } = -1;
        public float Elapsed => Running ? Time.unscaledTime - _startTime : _lastElapsed;
        public float CellsPerSecondNow => _actualCps;

        // ---- 可调参数 ----
        public float CellsPerSecond = 45f;
        public int BlockSize = 8;
        public int StrokeLength = 30;
        public float PauseChance = 0.4f;
        public float MistakeChance = 0f;
        public bool LargestColourFirst;
        public bool HighlightColour = true;
        public bool AutoSave = true;
        public int RestrictToColour;

        /// <summary>当前正在涂的颜色（0 基）；面板显示用。</summary>
        public int ColourId => CurrentColour;

        private Builder _b;
        private readonly System.Random _rng = new System.Random(Environment.TickCount);

        private List<Vector2Int> _path;   // 当前颜色的笔顺（x,z）
        private int _pathIndex;
        private List<int> _colours;       // 当前层的颜色顺序
        private int _colourIndex;
        private int _layer;
        private int _cursorX = -1, _cursorY = -1;

        private float _timer, _interval, _pauseUntil, _startTime, _lastElapsed;
        private float _cpsWindow, _actualCps;
        private int _cpsCount, _strokeLeft;
        private bool _finished;

        private void Awake()
        {
            Instance = this;
        }

        // ------------------------------------------------------------ 生命周期

        public void StartRun()
        {
            var b = GameApi.B;
            if (!GameApi.InLevel(b))
            {
                Status = "不在关卡内";
                return;
            }

            _b = b;
            Running = true;
            _finished = false;
            PaintedThisRun = 0;
            _timer = 0f;
            _pauseUntil = 0f;
            _strokeLeft = Mathf.Max(1, StrokeLength);
            _cursorX = _cursorY = -1;
            _path = null;
            _pathIndex = 0;
            _colours = null;
            _colourIndex = 0;
            CurrentColour = -1;
            _cpsWindow = 0f;
            _cpsCount = 0;
            _actualCps = 0f;
            _startTime = Time.unscaledTime;
            _lastElapsed = 0f;

            _layer = b.selectedLayer >= 0 ? b.selectedLayer : 0;
            if (b.selectedLayer != _layer) GameApi.SelectLayer(b, _layer);
            CurrentLayer = _layer;

            Running = true;
            Status = "开始涂色";
            LoadNextColour();
        }

        public void StopRun()
        {
            if (!Running && !_finished) return;
            Running = false;
            Status = "已停止";
        }

        public void Toggle()
        {
            if (Running) StopRun();
            else StartRun();
        }

        private void Finish()
        {
            Running = false;
            _finished = true;
            _lastElapsed = Time.unscaledTime - _startTime;
            if (AutoSave) GameApi.SaveNow(_b);
            Status = $"完成！本次涂了 {PaintedThisRun} 格，用时 {_lastElapsed:0.0}s";
            Debug.Log("[CGV] AutoPainter " + Status);
        }

        private void Update()
        {
            if (!Running) return;

            var b = _b;
            if (b == null || !GameApi.InLevel(b))
            {
                Running = false;
                Status = "关卡已关闭";
                return;
            }

            // 游戏自己翻页了（涂满一层自动 SetNextLayer）：同步到新层
            if (b.selectedLayer != _layer)
            {
                _layer = b.selectedLayer;
                CurrentLayer = _layer;
                _colours = null;
                _colourIndex = 0;
                _path = null;
                _pathIndex = 0;
                _cursorX = _cursorY = -1;
            }

            if (Time.unscaledTime < _pauseUntil) return;

            _timer += Time.unscaledDeltaTime;
            if (_timer < _interval) return;
            _timer = 0f;

            // 手速抖动
            float baseInterval = 1f / Mathf.Max(1f, CellsPerSecond);
            _interval = baseInterval * Mathf.Lerp(0.72f, 1.35f, (float)_rng.NextDouble());

            Step();

            _cpsWindow += Time.unscaledDeltaTime;
            _cpsCount++;
            if (_cpsWindow >= 0.5f)
            {
                _actualCps = _cpsCount / _cpsWindow;
                _cpsWindow = 0f;
                _cpsCount = 0;
            }
        }

        // ------------------------------------------------------------ 单步

        private void Step()
        {
            var b = _b;

            if (_path == null || _pathIndex >= _path.Count)
            {
                if (!LoadNextColour())
                {
                    Finish();
                    return;
                }
                // 换层 / 等待时本轮可能还没有可走的笔顺
                if (_path == null || _pathIndex >= _path.Count) return;
            }

            Vector2Int cell = _path[_pathIndex++];
            int target = GameApi.TargetColour(b, cell.x, _layer, cell.y);
            if (target >= 0 && b.voxelProgress[cell.x, _layer, cell.y] != target)
            {
                if (GameApi.PaintVoxel(b, cell.x, _layer, cell.y))
                {
                    PaintedThisRun++;
                    _cursorX = cell.x;
                    _cursorY = cell.y;
                }
            }

            _strokeLeft--;
            if (_strokeLeft <= 0)
            {
                _strokeLeft = Mathf.Max(1, StrokeLength + _rng.Next(-StrokeLength / 3, StrokeLength / 3 + 1));
                if (_rng.NextDouble() < PauseChance)
                {
                    float pause = Mathf.Lerp(0.12f, 0.55f, (float)_rng.NextDouble());
                    if (_rng.NextDouble() < 0.08f) pause += Mathf.Lerp(0.6f, 1.8f, (float)_rng.NextDouble());
                    _pauseUntil = Time.unscaledTime + pause;
                }
            }
        }

        // ------------------------------------------------------------ 换颜色 / 换层

        /// <summary>为当前颜色规划笔顺；当前层涂完就换下一层；都涂完返回 false。</summary>
        private bool LoadNextColour()
        {
            var b = _b;

            while (true)
            {
                if (_colours == null)
                {
                    _colours = GameApi.PendingColours(b, _layer);
                    if (LargestColourFirst)
                        _colours.Sort((a, c) =>
                            GameApi.PendingCells(b, _layer, c).Count.CompareTo(GameApi.PendingCells(b, _layer, a).Count));
                    _colourIndex = 0;
                }

                if (_colourIndex >= _colours.Count)
                {
                    // 只涂当前颜色：涂完这种颜色就收工，不升层
                    if (RestrictToColour > 0) return false;

                    int before = b.selectedLayer;
                    GameApi.NextLayer(b, true);
                    if (b.selectedLayer == before) return false; // 没有下一层了 → 整图完成

                    _layer = b.selectedLayer;
                    CurrentLayer = _layer;
                    _colours = null;
                    _colourIndex = 0;
                    _path = null;
                    _pathIndex = 0;
                    _cursorX = _cursorY = -1;
                    return true;
                }

                int colour = _colours[_colourIndex++];
                if (RestrictToColour > 0 && colour != RestrictToColour) continue;

                List<Vector2Int> cells = GameApi.PendingCells(b, _layer, colour);
                if (cells.Count == 0) continue;

                CurrentColour = colour;
                GameApi.SelectColour(b, colour);

                _path = BuildPath(cells, _cursorX, _cursorY);
                _pathIndex = 0;
                _strokeLeft = Mathf.Max(1, StrokeLength + _rng.Next(-StrokeLength / 3, StrokeLength / 3 + 1));
                return true;
            }
        }

        // ------------------------------------------------------------ 笔顺规划

        /// <summary>分块 → 块间就近移动 → 块内蛇形扫行（在 x-z 平面内）。</summary>
        private List<Vector2Int> BuildPath(List<Vector2Int> cells, int startX, int startY)
        {
            var path = new List<Vector2Int>(cells.Count);
            int bs = Mathf.Max(2, BlockSize);

            var blocks = new Dictionary<long, List<Vector2Int>>();
            foreach (var p in cells)
            {
                long key = (long)(p.x / bs) * 100000L + (p.y / bs);
                if (!blocks.TryGetValue(key, out var l))
                {
                    l = new List<Vector2Int>();
                    blocks[key] = l;
                }
                l.Add(p);
            }

            // 块间最近邻
            var keys = new List<long>(blocks.Keys);
            var ordered = new List<long>(keys.Count);
            float curX = startX < 0 ? 0f : startX / (float)bs;
            float curY = startY < 0 ? 0f : startY / (float)bs;

            while (keys.Count > 0)
            {
                int best = 0;
                float bestD = float.MaxValue;
                for (int i = 0; i < keys.Count; i++)
                {
                    long k = keys[i];
                    float bx = k / 100000f;
                    float by = k % 100000f;
                    float d = (bx - curX) * (bx - curX) + (by - curY) * (by - curY);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = i;
                    }
                }
                long chosen = keys[best];
                keys.RemoveAt(best);
                ordered.Add(chosen);
                curX = chosen / 100000f;
                curY = chosen % 100000f;
            }

            // 块内蛇形扫行
            foreach (long key in ordered)
            {
                var list = blocks[key];
                var rows = new SortedDictionary<int, List<Vector2Int>>();
                foreach (var p in list)
                {
                    if (!rows.TryGetValue(p.y, out var r))
                    {
                        r = new List<Vector2Int>();
                        rows[p.y] = r;
                    }
                    r.Add(p);
                }

                int blockCentreX = (int)((key / 100000L) * bs + bs / 2);
                bool flip = startX >= 0 && startX > blockCentreX;

                foreach (var kv in rows)
                {
                    kv.Value.Sort((a, c) => a.x.CompareTo(c.x));
                    if (flip) kv.Value.Reverse();
                    path.AddRange(kv.Value);
                    flip = !flip;
                }

                if (path.Count > 0)
                {
                    startX = path[path.Count - 1].x;
                    startY = path[path.Count - 1].y;
                }
            }

            return path;
        }
    }
}
