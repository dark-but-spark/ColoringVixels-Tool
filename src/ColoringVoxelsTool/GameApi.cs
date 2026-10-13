using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 《Coloring Voxels》游戏内部数据的统一访问层（3D 体素版）。
    ///
    /// 关键机制（反编译自 Assembly-CSharp.dll）：
    ///   Builder.voxelData[x,y,z].colourIndex —— 目标颜色编号（-1 = 该位置不是需要涂的体素）
    ///   Builder.voxelProgress[x,y,z]         —— 玩家已涂颜色编号（short，-1 = 从未触碰，**public**）
    ///   Builder.colourHidden[c]              —— 该颜色是否被「隐藏非必要体素」整体跳过
    ///   Builder.selectedCol / selectedLayer  —— 当前选中的颜色 / 层（**public**）
    ///   Builder.GetColourCounts()            —— 当前层的每种颜色剩余数（**public**）
    ///   Builder.SelectCube(x,y,z,clickable)  —— 涂一格的完整游戏逻辑（计数、存档、换色/换层）
    ///   Builder.SetLayer / SetCol            —— 切换当前层 / 颜色
    ///   SaveManager.SaveLevelData(name)       —— 写盘
    ///
    /// 一层是 y 轴：游戏一次只让涂 selectedLayer 那一层，涂满自动往上跳。
    /// </summary>
    internal static class GameApi
    {
        private const BindingFlags Flags =
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // Builder 的私有字段（反射访问）
        private static readonly FieldInfo FVoxelData = typeof(Builder).GetField("voxelData", Flags);
        private static readonly FieldInfo FCubes = typeof(Builder).GetField("cubes", Flags);
        private static readonly FieldInfo FColourHidden = typeof(Builder).GetField("colourHidden", Flags);

        // ---------------------------------------------------------------- 单例

        // 用游戏自己缓存的单例，避免面板每帧多次调用时反复做全场景查找（FindObjectOfType 很贵）。
        public static Builder B => Builder.Inst;
        public static CrossLevelStorage St => CrossLevelStorage.Inst;

        // ---------------------------------------------------------------- 关卡状态

        public static bool InLevel(Builder b)
        {
            return b != null && b.voxelProgress != null && b.gameState != Builder.GameState.None;
        }

        public static bool InLevel()
        {
            return InLevel(B);
        }

        /// <summary>体素规模：x = 宽，y = 层数，z = 深。直接读公开的 voxelProgress 维度。</summary>
        public static bool Size(Builder b, out int x, out int y, out int z)
        {
            x = y = z = 0;
            short[,,] vp = b != null ? b.voxelProgress : null;
            if (vp == null) return false;
            x = vp.GetLength(0);
            y = vp.GetLength(1);
            z = vp.GetLength(2);
            return x > 0 && y > 0 && z > 0;
        }

        public static int LayerCount(Builder b)
        {
            Size(b, out int _, out int y, out int _);
            return y;
        }

        public static int CurrentLayer(Builder b) => b != null ? b.selectedLayer : -1;
        public static int CurrentColour(Builder b) => b != null ? b.selectedCol : -1;

        /// <summary>(x,y,z) 的目标颜色编号；-1 表示该位置不需要涂（原始值，含被隐藏的颜色）。</summary>
        public static int TargetColour(Builder b, int x, int y, int z)
        {
            VoxelData[,,] data = FVoxelData?.GetValue(b) as VoxelData[,,];
            if (data == null) return -1;
            if (x < 0 || y < 0 || z < 0) return -1;
            if (x >= data.GetLength(0) || y >= data.GetLength(1) || z >= data.GetLength(2)) return -1;
            return data[x, y, z].colourIndex;
        }

        /// <summary>该颜色是否被「隐藏非必要体素」整体跳过（游戏不计入进度、也不生成可点方块）。</summary>
        public static bool Hidden(Builder b, int colour)
        {
            bool[] arr = FColourHidden?.GetValue(b) as bool[];
            return arr != null && colour >= 0 && colour < arr.Length && arr[colour];
        }

        /// <summary>计入进度的目标色：目标色存在且未被隐藏，否则 -1。</summary>
        private static int CountedColour(Builder b, int x, int y, int z)
        {
            int t = TargetColour(b, x, y, z);
            if (t < 0 || Hidden(b, t)) return -1;
            return t;
        }

        public static bool IsPainted(Builder b, int x, int y, int z)
        {
            short[,,] vp = b?.voxelProgress;
            if (vp == null) return false;
            int t = TargetColour(b, x, y, z);
            return t >= 0 && vp[x, y, z] == t;
        }

        // ---------------------------------------------------------------- 统计

        // 说明：这两个读数原先逐格扫描整张三维网格（O(N)），面板 HUD / 预览 / 计时每帧都调，
        // 大图上会直接把帧率拖垮。改为读游戏自己维护的计数器 / 关卡清单，O(1)。

        public static int TotalVoxels(Builder b)
        {
            if (b == null) return 0;
            CrossLevelStorage st = St;
            if (st == null) return 0;
            try
            {
                List<LevelVersionData> versioned = st.levelData.versionedData;
                if (versioned == null || versioned.Count == 0) return 0;
                int v = st.levelSaveData != null ? st.levelSaveData.levelVersion : 0;
                if (v < 0 || v >= versioned.Count) v = versioned.Count - 1;
                LevelVersionData vd = versioned[v];
                bool hide = st.mainMenuSaveData != null && st.mainMenuSaveData.hideNonEssentialVoxels;
                return hide ? vd.voxelsCountSimplified : vd.voxelsCount;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static int PaintedVoxels(Builder b)
        {
            if (b == null) return 0;
            try { return Mathf.Max(0, Mathf.RoundToInt(b.GetTotalPlacedVoxels())); }
            catch (Exception) { return 0; }
        }

        public static int RemainingVoxels(Builder b) => Mathf.Max(0, TotalVoxels(b) - PaintedVoxels(b));

        public static float Progress(Builder b)
        {
            int total = TotalVoxels(b);
            if (total <= 0) return 0f;
            return Mathf.Clamp01(PaintedVoxels(b) / (float)total);
        }

        /// <summary>指定层还没涂的体素数。</summary>
        public static int LayerRemaining(Builder b, int layer)
        {
            if (!Size(b, out int xn, out int _, out int zn)) return 0;
            if (layer < 0 || layer >= b.voxelProgress.GetLength(1)) return 0;
            short[,,] vp = b.voxelProgress;
            int n = 0;
            for (int x = 0; x < xn; x++)
                for (int z = 0; z < zn; z++)
                {
                    int t = CountedColour(b, x, layer, z);
                    if (t >= 0 && vp[x, layer, z] != t) n++;
                }
            return n;
        }

        public static bool IsLayerComplete(Builder b, int layer)
        {
            bool[] done = b?.layersComplete;
            return done != null && layer >= 0 && layer < done.Length && done[layer];
        }

        /// <summary>当前层的每种颜色剩余数（直接返回游戏自己的数组）。</summary>
        public static int[] ColourCounts(Builder b)
        {
            try { return b?.GetColourCounts(); } catch (Exception) { return null; }
        }

        // ---------------------------------------------------------------- 选择

        public static void SelectColour(Builder b, int colourId)
        {
            if (b == null || colourId < 0) return;
            try { b.SetCol(colourId); } catch (Exception) { }
        }

        /// <summary>切换当前选中颜色（以当前关卡为准）。</summary>
        public static void HighlightColour(int colourId)
        {
            SelectColour(B, colourId);
        }

        /// <summary>当前关卡的颜色总数（调色板大小）。</summary>
        public static int ColourCount(Builder b)
        {
            CrossLevelStorage st = St;
            if (st == null) return 0;
            List<LevelVersionData> versioned = st.levelData.versionedData;
            if (versioned == null || versioned.Count == 0) return 0;
            int v = st.levelSaveData != null ? st.levelSaveData.levelVersion : 0;
            if (v < 0 || v >= versioned.Count) v = versioned.Count - 1;
            List<ColourData> cols = versioned[v].colours;
            return cols == null ? 0 : cols.Count;
        }

        /// <summary>颜色编号对应的真彩（越界或读不到返回透明）。</summary>
        public static Color ColourOf(Builder b, int colourId)
        {
            CrossLevelStorage st = St;
            if (st == null || colourId < 0) return Color.clear;
            List<LevelVersionData> versioned = st.levelData.versionedData;
            if (versioned == null || versioned.Count == 0) return Color.clear;
            int v = st.levelSaveData != null ? st.levelSaveData.levelVersion : 0;
            if (v < 0 || v >= versioned.Count) v = versioned.Count - 1;
            List<ColourData> cols = versioned[v].colours;
            if (cols == null || colourId >= cols.Count) return Color.clear;
            ColourData c = cols[colourId];
            float a = c.a <= 0f ? 1f : c.a;
            return new Color(c.r, c.g, c.b, a);
        }

        public static void SelectLayer(Builder b, int layer)
        {
            if (b == null || layer < 0) return;
            try { b.SetLayer(layer); } catch (Exception) { }
        }

        public static void NextLayer(Builder b, bool forward)
        {
            try { b?.SetNextLayer(forward); } catch (Exception) { }
        }

        public static void NextColour(Builder b, bool forward)
        {
            try { b?.SetNextCol(forward); } catch (Exception) { }
        }

        // ---------------------------------------------------------------- 涂色

        /// <summary>涂满当前层里某一种颜色的所有待涂体素，返回涂了几格。</summary>
        public static int FillLayerColour(Builder b, int colourId)
        {
            if (!InLevel(b) || colourId < 0 || Hidden(b, colourId)) return 0;
            if (!Size(b, out int _, out int _, out int _)) return 0;

            int layer = b.selectedLayer;
            if (layer < 0 || layer >= b.voxelProgress.GetLength(1)) return 0;

            List<Vector2Int> cells = PendingCells(b, layer, colourId);
            if (cells.Count == 0) return 0;

            SelectColour(b, colourId);

            int n = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                if (b.selectedLayer != layer) break; // 这一层已经涂满、游戏自动翻页了
                if (PaintVoxel(b, cells[i].x, layer, cells[i].y)) n++;
            }
            return n;
        }

        /// <summary>涂满当前层所有颜色，返回涂了几格。</summary>
        public static int FillLayer(Builder b)
        {
            if (!InLevel(b)) return 0;
            int layer = b.selectedLayer;
            if (layer < 0) return 0;

            int n = 0;
            foreach (int c in PendingColours(b, layer))
            {
                if (b.selectedLayer != layer) break;
                n += FillLayerColour(b, c);
            }
            return n;
        }

        /// <summary>逐层涂满整张图（依赖游戏自己的层动画，调用方需按帧调度）。</summary>
        public static int FillAll(Builder b)
        {
            if (!InLevel(b)) return 0;
            int n = 0;
            int layers = LayerCount(b);
            for (int layer = 0; layer < layers; layer++)
            {
                if (b.selectedLayer != layer) SelectLayer(b, layer);
                if (b.selectedLayer != layer) continue;
                n += FillLayer(b);
            }
            return n;
        }

        /// <summary>擦掉某一体素的已涂色。</summary>
        public static bool EraseCell(Builder b, int x, int y, int z)
        {
            if (!InLevel(b)) return false;
            try
            {
                b.EraseCube(x, y, z);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>用当前选中的颜色、借游戏自己的涂色逻辑涂一格（自动处理计数 / 存档 / 换色换层）。</summary>
        public static bool PaintVoxel(Builder b, int x, int y, int z)
        {
            if (!InLevel(b)) return false;
            Cube[,,] cubes = FCubes?.GetValue(b) as Cube[,,];
            if (cubes == null) return false;
            if (x < 0 || y < 0 || z < 0) return false;
            if (x >= cubes.GetLength(0) || y >= cubes.GetLength(1) || z >= cubes.GetLength(2)) return false;

            CubeClickable clickable = cubes[x, y, z] as CubeClickable;
            if (clickable == null || clickable.burnt) return false;

            try
            {
                b.SelectCube(x, y, z, clickable);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>指定层里某种颜色还没涂的格子（返回 (x,z) 列表）。</summary>
        public static List<Vector2Int> PendingCells(Builder b, int layer, int onlyColour)
        {
            var list = new List<Vector2Int>();
            if (!Size(b, out int xn, out int _, out int zn)) return list;
            if (layer < 0 || layer >= b.voxelProgress.GetLength(1)) return list;
            short[,,] vp = b.voxelProgress;
            for (int x = 0; x < xn; x++)
                for (int z = 0; z < zn; z++)
                {
                    int t = CountedColour(b, x, layer, z);
                    if (t < 0) continue;
                    if (onlyColour >= 0 && t != onlyColour) continue;
                    if (vp[x, layer, z] == t) continue;
                    list.Add(new Vector2Int(x, z));
                }
            return list;
        }

        /// <summary>指定层里还有待涂格子的颜色编号（按调色板顺序）。</summary>
        public static List<int> PendingColours(Builder b, int layer)
        {
            var set = new HashSet<int>();
            if (!Size(b, out int xn, out int _, out int zn)) return new List<int>();
            if (layer < 0 || layer >= b.voxelProgress.GetLength(1)) return new List<int>();
            short[,,] vp = b.voxelProgress;
            for (int x = 0; x < xn; x++)
                for (int z = 0; z < zn; z++)
                {
                    int t = CountedColour(b, x, layer, z);
                    if (t < 0 || vp[x, layer, z] == t) continue;
                    set.Add(t);
                }
            var list = new List<int>(set);
            list.Sort();
            return list;
        }

        // ---------------------------------------------------------------- 关卡 / 世界

        /// <summary>当前所在世界的索引（-1 表示读不到）。</summary>
        public static int CurrentWorldIndex => St != null && St.mainMenuSaveData != null ? St.mainMenuSaveData.selectedWorld : -1;

        /// <summary>当前关卡在所属世界中的索引（-1 表示读不到）。</summary>
        public static int CurrentLevelIndex
        {
            get
            {
                CrossLevelStorage st = St;
                if (st == null || st.levelData.saveFileName == null) return -1;
                WorldData[] worlds = st.worldDatas;
                int w = CurrentWorldIndex;
                if (worlds == null || w < 0 || w >= worlds.Length) return -1;
                List<LevelData> levels = worlds[w].levels;
                if (levels == null) return -1;
                string cur = st.levelData.saveFileName;
                for (int i = 0; i < levels.Count; i++)
                    if (levels[i].saveFileName == cur) return i;
                return -1;
            }
        }

        /// <summary>用于判断「是否换了一张图」的稳定标识。</summary>
        public static string LevelKey => CurrentWorldIndex + ":" + CurrentLevelIndex;

        public static int WorldCount
        {
            get { WorldData[] w = St?.worldDatas; return w == null ? 0 : w.Length; }
        }

        /// <summary>某个世界的关卡数（读不到返回 0）。</summary>
        public static int LevelCount(int worldIndex)
        {
            WorldData[] worlds = St?.worldDatas;
            if (worlds == null || worldIndex < 0 || worldIndex >= worlds.Length) return 0;
            List<LevelData> levels = worlds[worldIndex].levels;
            return levels == null ? 0 : levels.Count;
        }

        /// <summary>切换/重新加载指定世界与关卡（等价于游戏的关卡按钮 Action）。</summary>
        public static bool LoadLevel(int worldIndex, int levelIndex, out string message)
        {
            message = null;
            CrossLevelStorage st = St;
            if (st == null) { message = "存档尚未就绪"; return false; }

            WorldData[] worlds = st.worldDatas;
            if (worlds == null || worldIndex < 0 || worldIndex >= worlds.Length) { message = "世界索引无效"; return false; }
            List<LevelData> levels = worlds[worldIndex].levels;
            if (levels == null || levelIndex < 0 || levelIndex >= levels.Count) { message = "关卡索引无效"; return false; }

            try
            {
                st.mainMenuSaveData.selectedWorld = worldIndex;
                st.levelData = levels[levelIndex];
                SaveManager.LoadAndSetLevelSaveFile(st.levelData.saveFileName);
                if (ScreenFade.Inst != null) ScreenFade.Inst.TriggerLevelLoad(ScreenFade.MAIN_GAME);
            }
            catch (Exception e)
            {
                message = "切换关卡失败：" + e.Message;
                return false;
            }

            message = (worldIndex + 1) + "-" + (levelIndex + 1) + " · " + st.levelData.saveFileName;
            return true;
        }

        /// <summary>
        /// 切到下一关：同一世界顺着往下走，跳过已完成 / 不可见 / 未拥有的关；
        /// 一个世界走完就进下一个世界。
        /// </summary>
        public static bool LoadNextLevel(out string message, bool skipCompleted = true)
        {
            message = null;
            CrossLevelStorage st = St;
            if (st == null) { message = "存档尚未就绪"; return false; }

            WorldData[] worlds = st.worldDatas;
            if (worlds == null || worlds.Length == 0) { message = "读取不到世界列表"; return false; }

            int w = CurrentWorldIndex;
            if (w < 0 || w >= worlds.Length) { message = "当前世界索引异常"; return false; }

            int l = CurrentLevelIndex + 1;
            if (l < 0) l = 0;

            int guard = 0;
            while (true)
            {
                if (++guard > 8192) { message = "找不到合适的下一关"; return false; }
                if (w >= worlds.Length) { message = "已经是最后一个世界"; return false; }

                WorldData wd = worlds[w];
                List<LevelData> levels = wd != null ? wd.levels : null;

                if (wd == null || !wd.IsVisible() || IsWorldLocked(wd) || levels == null || levels.Count == 0)
                {
                    w++;
                    l = 0;
                    continue;
                }
                if (l >= levels.Count)
                {
                    w++;
                    l = 0;
                    continue;
                }
                if (skipCompleted && IsLevelCompleted(w, levels[l].saveFileName))
                {
                    l++;
                    continue;
                }
                break;
            }

            return LoadLevel(w, l, out message);
        }

        /// <summary>某个世界的某关是否已经标记为完成（读存档摘要）。</summary>
        public static bool IsLevelCompleted(int worldIndex, string saveFileName)
        {
            CrossLevelStorage st = St;
            MainMenuSaveData mm = st?.mainMenuSaveData;
            if (mm == null || mm.worldDatas == null || saveFileName == null) return false;
            if (worldIndex < 0 || worldIndex >= mm.worldDatas.Length) return false;
            List<HighLevelLevelData> list = mm.worldDatas[worldIndex].levelData;
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i].saveFileName == saveFileName && list[i].completed) return true;
            return false;
        }

        /// <summary>DLC 世界未购买时视为上锁（配合解锁补丁使用）。</summary>
        private static bool IsWorldLocked(WorldData wd)
        {
            if (wd == null || wd.worldType != WorldData.WorldType.DLC) return false;
            try
            {
                SteamManager sm = SteamManager.Inst;
                return sm != null && !sm.CheckIfProductOwned(wd.dlc);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---------------------------------------------------------------- 存档

        public static void SaveNow(Builder b)
        {
            CrossLevelStorage st = St;
            if (st == null || st.levelData.saveFileName == null) return;
            try { SaveManager.SaveLevelData(st.levelData.saveFileName); }
            catch (Exception e) { Debug.LogWarning("[CGV] 保存失败：" + e.Message); }
        }

        // ================================================================ 面板兼容层
        // 面板（UI）是从 2D 版沿用下来的，习惯按 ClickTest 的形状读取数据。
        // 这里提供一个只读视图与若干同名方法，让面板 UI 原样保留、底层换成 3D 数据。

        /// <summary>面板沿用的 2D 关卡视图（只读，映射到当前 3D 关卡）。</summary>
        internal sealed class LevelView
        {
            public int xMax;
            public int yMax;
            public int pixelsColored;
            public float yScale = 1f;
            public bool inputDisabled;
            public bool gameOver;
            public int selectedColourID;   // 1 基（与面板显示一致）
            public int[] colourCounts;
        }

        private static string _coloursKey;
        private static List<Color> _colours;

        /// <summary>当前关卡颜色真彩表（面板调色板 / 预览用）。</summary>
        public static List<Color> Colours
        {
            get
            {
                Builder b = B;
                if (b == null || St == null) return null;
                int count = ColourCount(b);
                if (count <= 0) return null;
                string key = LevelKey + ":" + count;
                if (_colours != null && key == _coloursKey) return _colours;

                var list = new List<Color>(count);
                for (int i = 0; i < count; i++) list.Add(ColourOf(b, i));
                _colours = list;
                _coloursKey = key;
                return list;
            }
        }

        private static readonly LevelView _levelView = new LevelView();

        /// <summary>面板沿用的 2D ClickTest 形状视图；不在关卡时返回 null。（复用实例，避免每帧分配）</summary>
        public static LevelView Ct
        {
            get
            {
                Builder b = B;
                if (!InLevel(b)) return null;
                Size(b, out int xn, out int _, out int zn);
                _levelView.xMax = xn;
                _levelView.yMax = zn;
                _levelView.pixelsColored = PaintedVoxels(b);
                _levelView.gameOver = b.gameState == Builder.GameState.Finished;
                _levelView.selectedColourID = b.selectedCol + 1;
                _levelView.colourCounts = ColourCounts(b);
                return _levelView;
            }
        }

        public static bool InLevel(LevelView lv) => lv != null;
        public static int TotalPixels(LevelView lv) => TotalVoxels(B);
        public static int PaintedPixels(LevelView lv) => PaintedVoxels(B);
        public static int RemainingPixels(LevelView lv) => RemainingVoxels(B);
        public static float Progress(LevelView lv) => Progress(B);
        public static int CurrentBookIndex => CurrentWorldIndex;

        public static int FillSelectedColour()
        {
            Builder b = B;
            return InLevel(b) ? FillLayerColour(b, b.selectedCol) : 0;
        }

        /// <summary>面板调色板点击用：涂完指定颜色（1 基，与面板显示一致）。</summary>
        public static int FillColour(int colourId)
        {
            Builder b = B;
            if (!InLevel(b) || colourId < 1) return 0;
            return FillLayerColour(b, colourId - 1);
        }

        public static int InstantFill()
        {
            Builder b = B;
            return InLevel(b) ? FillLayer(b) : 0;
        }

        public static void SaveNow() => SaveNow(B);

        /// <summary>面板「重载关卡 / 清空画布」用；3D 版按体素擦除。</summary>
        public static void EraseAll()
        {
            Builder b = B;
            if (!InLevel(b)) return;
            if (!Size(b, out int xn, out int yn, out int zn)) return;
            for (int y = 0; y < yn; y++)
                for (int x = 0; x < xn; x++)
                    for (int z = 0; z < zn; z++)
                        try { b.EraseCube(x, y, z); } catch (Exception) { }
        }

        /// <summary>面板沿用；3D 版没有等价的「就地重载」，留给关卡切换流程。</summary>
        public static void SoftReset(LevelView lv) { }
    }
}
