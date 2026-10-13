using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>游戏设置项的类型。</summary>
    internal enum SettingKind
    {
        Toggle = 0,
        IntSlider = 1,
        FloatSlider = 2
    }

    /// <summary>一条游戏设置项（3D 版）。</summary>
    internal sealed class SettingDef
    {
        public string Field;
        public string Label;
        public string Desc;
        public string Section;
        public SettingKind Kind = SettingKind.Toggle;
        public int Min;
        public int Max = 1;
        public float MinF;
        public float MaxF = 1f;
        public string[] Options;
        public float DisplayScale = 1f;
        public string SliderName;
        public object Default;
    }

    /// <summary>
    /// 游戏自带设置的完整镜像（3D 版）。
    ///
    /// 设置分散在 <see cref="MainMenuSaveData"/>（随存档）与 <see cref="DisplaySettings"/>
    /// （分辨率 / 垂直同步 / 帧率，存 PlayerPrefs）里；这里收拢成统一的「标题 + 读写委托」，
    /// 供面板「游戏预设」页与「推荐预设 / 恢复默认」调用。
    /// </summary>
    internal static class GameSettings
    {
        internal const string SecAccessibility = "界面与无障碍（ACCESSIBILITY）";
        internal const string SecMainMenu = "主菜单（MAIN MENU）";
        internal const string SecInGame = "游戏内（IN GAME）";
        internal const string SecMusic = "音乐（SETTINGS）";

        private static readonly Dictionary<string, Func<object>> Getters = new Dictionary<string, Func<object>>();
        private static readonly Dictionary<string, Action<object>> Setters = new Dictionary<string, Action<object>>();
        private static Dictionary<string, object> _snapshot;

        public static bool Ready
        {
            get { return GameApi.St != null && GameApi.St.mainMenuSaveData != null; }
        }

        private static SettingDef[] _all;

        /// <summary>全部设置项。**懒构建**：只有真正打开设置页时才枚举分辨率等（否则会卡启动那一帧）。</summary>
        internal static SettingDef[] All
        {
            get
            {
                if (_all == null) _all = Build();
                return _all;
            }
        }

        // ---------------------------------------------------------------- 访问器

        private static MainMenuSaveData MM { get { return GameApi.St != null ? GameApi.St.mainMenuSaveData : null; } }
        private static DisplaySettings DS { get { return GameApi.St.DisplaySettingsNew; } }

        private static void SaveDS(DisplaySettings d)
        {
            try { d.Save(); } catch (Exception) { }
            try { GameApi.St.DisplaySettingsNew = d; } catch (Exception) { }
        }

        private static int MaxFps()
        {
            int max = 30;
            try
            {
                max = Mathf.Max(max, Screen.currentResolution.refreshRate);
                Resolution[] rs = Screen.resolutions;
                for (int i = 0; i < rs.Length; i++) max = Mathf.Max(max, rs[i].refreshRate);
            }
            catch (Exception) { }
            return max + 1;
        }

        private static string[] ResOptions()
        {
            var list = new List<string>();
            try
            {
                Resolution[] rs = Screen.resolutions;
                for (int i = 0; i < rs.Length; i++)
                {
                    string t = rs[i].width + "x" + rs[i].height;
                    if (!list.Contains(t)) list.Add(t);
                }
            }
            catch (Exception) { }
            if (list.Count == 0) list.Add("当前");
            return list.ToArray();
        }

        private static int ResIndex()
        {
            DisplaySettings d = DS;
            Resolution[] rs = Screen.resolutions;
            int idx = 0, n = 0;
            var seen = new HashSet<string>();
            for (int i = 0; i < rs.Length; i++)
            {
                string t = rs[i].width + "x" + rs[i].height;
                if (!seen.Add(t)) continue;
                if (rs[i].width == d.resWidth && rs[i].height == d.resHeight) { idx = n; break; }
                n++;
            }
            return idx;
        }

        // ---------------------------------------------------------------- 表

        private static SettingDef[] Build()
        {
            var list = new List<SettingDef>();
            var fsNames = Enum.GetNames(typeof(FullScreenMode));
            var tlNames = Enum.GetNames(typeof(SettingsTimelapse));
            var statNames = Enum.GetNames(typeof(SettingsLevelItemStat));
            var smallNames = Enum.GetNames(typeof(SettingsLevelSmall));
            string[] resOpts = ResOptions();
            int maxFps = MaxFps();

            // ---------------- 界面与无障碍 ----------------
            list.Add(Toggle("vSync", "垂直同步", "开启后帧率跟随显示器刷新率", SecAccessibility,
                () => DS.vSync == 1, v => { DisplaySettings d = DS; d.vSync = v ? 1 : 0; SaveDS(d); }, false));
            list.Add(Slider("targetFrameRate", "帧率上限", "未开垂直同步时的目标帧率", SecAccessibility,
                24f, maxFps, false, () => { DisplaySettings d = DS; return d.unlimitedFrameRate ? maxFps : d.targetFrameRate; },
                v => { DisplaySettings d = DS; d.targetFrameRate = (int)v; d.unlimitedFrameRate = false; SaveDS(d); }, maxFps));
            list.Add(Slider("fullScreenMode", "屏幕模式", "全屏 / 无边框 / 窗口", SecAccessibility,
                0f, fsNames.Length - 1, true, () => (int)DS.fullScreenMode,
                v => { DisplaySettings d = DS; d.fullScreenMode = (FullScreenMode)Mathf.Clamp((int)v, 0, fsNames.Length - 1); SaveDS(d); }, 1f,
                fsNames));
            list.Add(Slider("resolution", "分辨率", "窗口分辨率", SecAccessibility,
                0f, resOpts.Length - 1, true, () => ResIndex(),
                v => {
                    int i = Mathf.Clamp((int)v, 0, resOpts.Length - 1);
                    string t = resOpts[i];
                    int x = t.IndexOf('x');
                    if (x > 0)
                    {
                        int w, h;
                        if (int.TryParse(t.Substring(0, x), out w) && int.TryParse(t.Substring(x + 1), out h))
                        {
                            DisplaySettings d = DS; d.resWidth = w; d.resHeight = h; SaveDS(d);
                        }
                    }
                }, 0f, resOpts));
            list.Add(Slider("bloom", "泛光", "画面泛光强度", SecAccessibility,
                0f, 4f, false, () => MM.bloom, v => { MM.bloom = v; RefreshVolume(); }, 0f));
            list.Add(Toggle("darkMode", "暗色模式", "深色底，夜里更省眼", SecAccessibility,
                () => MM.darkMode, v => { MM.darkMode = v; RefreshMaterials(); }, false));

            // ---------------- 主菜单 ----------------
            list.Add(Toggle("hideLevelNames", "隐藏关卡名", "未完成的关卡名显示为 ???", SecMainMenu,
                () => MM.hideLevelNames, v => { MM.hideLevelNames = v; RefreshLevelItems(); }, false));
            list.Add(Toggle("legacyBookSelect", "旧版选书", "用旧式书籍选择界面", SecMainMenu,
                () => MM.legacyBookSelect, v => { MM.legacyBookSelect = v; RefreshWorldSelect(); }, false));
            list.Add(Toggle("mainMenuBox", "主菜单盒子", "主菜单显示装饰盒子", SecMainMenu,
                () => !MM.mainMenuBox, v => { MM.mainMenuBox = !v; }, true));
            list.Add(Slider("mainMenuScroll", "主菜单滚动速度", "主菜单滚动手感", SecMainMenu,
                0f, 4f, false, () => MM.mainMenuScroll, v => MM.mainMenuScroll = v, 0f));
            list.Add(Slider("hideCompletedWorlds", "隐藏已完成世界", "完成度达到多少后隐藏该世界", SecMainMenu,
                0f, 4f, true, () => MM.hideCompletedWorlds, v => { MM.hideCompletedWorlds = (int)v; RefreshWorldSelect(); }, 0f));
            list.Add(Slider("levelItemCompleteStat", "关卡完成信息", "已完成关卡的补充信息", SecMainMenu,
                0f, statNames.Length - 1, true, () => MM.levelItemCompleteStat, v => { MM.levelItemCompleteStat = (int)v; RefreshLevelItems(); }, 0f, statNames));
            list.Add(Slider("levelItemLeft", "关卡页左栏", "关卡条目左侧显示的信息", SecMainMenu,
                0f, smallNames.Length - 1, true, () => MM.levelItemLeft - 1, v => { MM.levelItemLeft = (int)v + 1; RefreshLevelItems(); }, 1f, smallNames));
            list.Add(Slider("levelItemRight", "关卡页右栏", "关卡条目右侧显示的信息", SecMainMenu,
                0f, smallNames.Length - 1, true, () => MM.levelItemRight - 1, v => { MM.levelItemRight = (int)v + 1; RefreshLevelItems(); }, 2f, smallNames));

            // ---------------- 游戏内 ----------------
            list.Add(Toggle("showTimer", "显示计时器", "关卡里显示本图用时", SecInGame,
                () => MM.showTimer, v => MM.showTimer = v, false));
            list.Add(Toggle("showCompletion", "显示完成度", "关卡里显示完成百分比", SecInGame,
                () => MM.showCompletion, v => MM.showCompletion = v, false));
            list.Add(Toggle("toggleClick", "点击切换涂色", "点一下切换按住 / 松开，不用一直压着鼠标", SecInGame,
                () => MM.toggleClick, v => MM.toggleClick = v, false));
            list.Add(Toggle("gridDisabled", "显示网格", "地面显示格子网格", SecInGame,
                () => !MM.gridDisabled, v => MM.gridDisabled = !v, true));
            list.Add(Toggle("gridFollow", "网格跟随视角", "网格随相机高度移动", SecInGame,
                () => MM.gridFollow, v => MM.gridFollow = v, false));
            list.Add(Toggle("removeCompletedLayers", "完成后移除层", "涂满的层自动隐藏", SecInGame,
                () => MM.removeCompletedLayers, v => MM.removeCompletedLayers = v, false));
            list.Add(Toggle("disableVoxelNewAnim", "新方块动画", "新涂方块出现时的缩放动画", SecInGame,
                () => !MM.disableVoxelNewAnim, v => MM.disableVoxelNewAnim = !v, true));
            list.Add(Toggle("disableVoxelHoverAnim", "悬停动画", "鼠标悬停方块的放大反馈", SecInGame,
                () => !MM.disableVoxelHoverAnim, v => MM.disableVoxelHoverAnim = !v, true));
            list.Add(Toggle("disableVoxelCompletedAnim", "完成动画", "涂对时方块的完成动画", SecInGame,
                () => !MM.disableVoxelCompletedAnim, v => MM.disableVoxelCompletedAnim = !v, true));
            list.Add(Toggle("toggleClickWhenCompleteColour", "涂完自动跳色", "一种颜色涂完自动切到下一种", SecInGame,
                () => !MM.toggleClickWhenCompleteColour, v => MM.toggleClickWhenCompleteColour = !v, true));
            list.Add(Toggle("colorPaletteToLayer", "调色板按层", "调色板只显示当前层的颜色", SecInGame,
                () => !MM.colorPaletteToLayer, v => MM.colorPaletteToLayer = !v, true));
            list.Add(Toggle("hideCompletedColours", "隐藏已完成颜色", "把涂完的颜色从调色板移除", SecInGame,
                () => !MM.hideCompletedColours, v => MM.hideCompletedColours = !v, true));
            list.Add(Toggle("colorLock", "颜色锁定", "涂错后按目标色显示", SecInGame,
                () => !MM.colorLock, v => MM.colorLock = !v, true));
            list.Add(Toggle("hideNonEssentialVoxels", "隐藏非必要体素", "跳过装饰性小体素", SecInGame,
                () => MM.hideNonEssentialVoxels, v => MM.hideNonEssentialVoxels = v, false));
            list.Add(Toggle("seasonalEventsDisabled", "季节特效", "显示节日 / 活动特效", SecInGame,
                () => !MM.seasonalEventsDisabled, v => { MM.seasonalEventsDisabled = !v; RefreshSeasonal(); }, true));
            list.Add(Slider("timelapseTime", "延时动画速度", "关卡回放动画的时长", SecInGame,
                0f, 20f, false, () => MM.timelapseTime, v => MM.timelapseTime = v, 0f));
            list.Add(Slider("timelapseMode", "延时动画模式", "何时播放关卡回放", SecInGame,
                0f, tlNames.Length - 1, true, () => MM.timelapseMode, v => MM.timelapseMode = (int)v, 0f, tlNames));
            list.Add(Slider("keyboardSpeed", "键盘视角速度", "键盘移动视角的速度", SecInGame,
                0f, 4f, false, () => MM.keyboardSpeed, v => MM.keyboardSpeed = v, 0f));
            list.Add(Slider("mouseSpeed", "鼠标视角速度", "鼠标拖动旋转视角的速度", SecInGame,
                0f, 4f, false, () => MM.mouseSpeed, v => MM.mouseSpeed = v, 0f));

            // ---------------- 音乐 ----------------
            list.Add(Slider("musicVolume", "音乐音量", "背景音乐音量", SecMusic,
                0f, 1f, false, () => MM.musicVolume, v => { MM.musicVolume = v; RefreshMusic(); }, 0.7f));
            list.Add(Slider("sfxVolume", "音效音量", "按钮 / 涂色音效音量", SecMusic,
                0f, 1f, false, () => MM.sfxVolume, v => { MM.sfxVolume = v; RefreshMusic(); }, 0.7f));

            return list.ToArray();
        }

        // ---------------------------------------------------------------- 读写

        public static bool GetBool(string field)
        {
            Func<object> g;
            if (!Getters.TryGetValue(field, out g)) return false;
            try { return (bool)g(); } catch (Exception) { return false; }
        }

        public static float GetFloat(string field)
        {
            Func<object> g;
            if (!Getters.TryGetValue(field, out g)) return 0f;
            try { return Convert.ToSingle(g(), CultureInfo.InvariantCulture); } catch (Exception) { return 0f; }
        }

        public static int IntOf(SettingDef d)
        {
            return (int)Mathf.Round(GetFloat(d.Field));
        }

        public static void SetValue(SettingDef d, object value)
        {
            if (d == null) return;
            Action<object> s;
            if (Setters.TryGetValue(d.Field, out s))
            {
                try { s(value); } catch (Exception e) { Debug.LogWarning("[CGV] 写入设置失败：" + e.Message); }
            }
            try { SaveManager.SaveMainMenuData(); } catch (Exception) { }
        }

        public static void Range(SettingDef d, out float min, out float max)
        {
            if (d == null) { min = 0f; max = 1f; return; }
            if (d.Kind == SettingKind.FloatSlider) { min = d.MinF; max = d.MaxF; }
            else { min = d.Min; max = d.Max; }
        }

        public static string DisplayOf(SettingDef d, float cur)
        {
            if (d == null) return "-";
            if (d.Options != null)
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(cur), 0, d.Options.Length - 1);
                return d.Options[i];
            }
            if (d.Kind == SettingKind.FloatSlider)
                return (cur * d.DisplayScale).ToString("0.##", CultureInfo.InvariantCulture);
            return ((int)Mathf.Round(cur)).ToString(CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------- 快照 / 撤销 / 预设

        private static object Current(SettingDef d)
        {
            if (d.Kind == SettingKind.Toggle) return GetBool(d.Field);
            if (d.Kind == SettingKind.IntSlider) return IntOf(d);
            return GetFloat(d.Field);
        }

        public static void CaptureSnapshot()
        {
            _snapshot = new Dictionary<string, object>();
            foreach (var d in All)
            {
                try { _snapshot[d.Field] = Current(d); } catch (Exception) { }
            }
        }

        public static bool UndoAvailable
        {
            get { return _snapshot != null && _snapshot.Count > 0; }
        }

        public static string UndoLastApply()
        {
            if (_snapshot == null) return "没有可撤销的快照";
            int n = 0;
            foreach (var d in All)
            {
                object v;
                if (!_snapshot.TryGetValue(d.Field, out v)) continue;
                if (!Equals(Current(d), v)) { SetValue(d, v); n++; }
            }
            return "已撤销上次改动（" + n + " 项）";
        }

        public static int DiffFromDefaults()
        {
            int n = 0;
            foreach (var d in All)
                if (!Equals(Current(d), d.Default)) n++;
            return n;
        }

        public static string RestoreDefaults()
        {
            int n = 0;
            foreach (var d in All)
            {
                if (!Equals(Current(d), d.Default)) { SetValue(d, d.Default); n++; }
            }
            return "已恢复 " + n + " 项到游戏默认";
        }

        public static string SaveAsPreset()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# ColoringVoxelsTool 游戏设置预设（键 = 值）");
            foreach (var d in All)
            {
                if (d.Kind == SettingKind.Toggle)
                    sb.Append(d.Field).Append('=').Append(GetBool(d.Field) ? "1" : "0").AppendLine();
                else if (d.Kind == SettingKind.IntSlider)
                    sb.Append(d.Field).Append('=').Append(IntOf(d)).AppendLine();
                else
                    sb.Append(d.Field).Append('=')
                      .Append(GetFloat(d.Field).ToString("F3", CultureInfo.InvariantCulture)).AppendLine();
            }
            try { AppPaths.WriteAtomic(GamePreset.FilePath, sb.ToString()); return "已存为预设：" + GamePreset.FilePath; }
            catch (Exception e) { return "保存预设失败：" + e.Message; }
        }

        public static void Flush()
        {
            try { SaveManager.SaveMainMenuData(); } catch (Exception) { }
        }

        /// <summary>面板每帧调用；目前无需周期任务。</summary>
        public static void Tick() { }

        // ---------------------------------------------------------------- 副作用刷新

        private static void RefreshMaterials() { try { MaterialManager.Inst?.RefreshMaterials(); } catch (Exception) { } }
        private static void RefreshLevelItems() { try { WorldVisualiser.Inst?.RefreshLevelItems(); } catch (Exception) { } }
        private static void RefreshWorldSelect() { try { WorldSelectPopulator.Inst?.InitialiseButtons(); } catch (Exception) { } }
        private static void RefreshMusic() { try { MusicManager.Inst.UpdateVolume(); } catch (Exception) { } }
        private static void RefreshVolume() { try { GlobalVolumeManager.Inst.RefreshValues(); } catch (Exception) { } }
        private static void RefreshSeasonal()
        {
            try { EventManager.Inst.FindCurrentEvent(); EventManager.Inst.RefreshAllGraphics(); }
            catch (Exception) { }
        }

        // ---------------------------------------------------------------- 构造

        private static SettingDef Toggle(string field, string label, string desc, string section,
            Func<bool> get, Action<bool> set, bool def, Action onChanged = null)
        {
            Getters[field] = () => get();
            Setters[field] = v =>
            {
                set(Convert.ToBoolean(v, CultureInfo.InvariantCulture));
                if (onChanged != null) onChanged();
            };
            return new SettingDef { Field = field, Label = label, Desc = desc, Section = section, Kind = SettingKind.Toggle, Default = def };
        }

        private static SettingDef Slider(string field, string label, string desc, string section,
            float min, float max, bool integer, Func<float> get, Action<float> set, float def,
            string[] options = null, float displayScale = 1f)
        {
            Getters[field] = () => get();
            Setters[field] = v => set(Convert.ToSingle(v, CultureInfo.InvariantCulture));
            return new SettingDef
            {
                Field = field, Label = label, Desc = desc, Section = section,
                Kind = integer ? SettingKind.IntSlider : SettingKind.FloatSlider,
                Min = (int)min, Max = (int)max, MinF = min, MaxF = max,
                Options = options, DisplayScale = displayScale, Default = integer ? (object)(int)def : (object)def
            };
        }
    }
}
