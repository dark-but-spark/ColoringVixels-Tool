using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        public string Field;          // 唯一键（对应 MainMenuSaveData 字段名）
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
    /// 游戏自带设置的一份可编程镜像（3D 版）。
    ///
    /// 游戏把这些设置存在 <see cref="MainMenuSaveData"/>（随存档存盘）里；这里把每一项收拢成
    /// 「标题 + 读写委托 + 默认值」，供面板的「游戏预设」页与「推荐预设 / 恢复默认」统一调用。
    /// 写值后统一 <c>SaveManager.SaveMainMenuData()</c> 落盘；带界面副作用的项额外触发对应刷新。
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

        internal static readonly SettingDef[] All = Build();

        private static SettingDef[] Build()
        {
            var list = new List<SettingDef>();

            // ---------------- 界面与无障碍 ----------------
            list.Add(Toggle("darkMode", "暗色模式", "深色底，夜里更省眼", SecAccessibility,
                m => m.darkMode, (m, v) => m.darkMode = v, false, RefreshMaterials));
            list.Add(Toggle("showTimer", "显示计时器", "关卡里显示本图用时", SecAccessibility,
                m => m.showTimer, (m, v) => m.showTimer = v, false));
            list.Add(Toggle("showCompletion", "显示完成度", "关卡里显示完成百分比", SecAccessibility,
                m => m.showCompletion, (m, v) => m.showCompletion = v, false));

            // ---------------- 主菜单 ----------------
            list.Add(Toggle("hideLevelNames", "隐藏关卡名", "未完成的关卡名显示为 ???", SecMainMenu,
                m => m.hideLevelNames, (m, v) => m.hideLevelNames = v, false, RefreshLevelItems));
            list.Add(Toggle("legacyBookSelect", "旧版选书", "用旧式书籍选择界面", SecMainMenu,
                m => m.legacyBookSelect, (m, v) => m.legacyBookSelect = v, false, RefreshWorldSelect));
            list.Add(Toggle("mainMenuBox", "主菜单盒子", "主菜单显示装饰盒子", SecMainMenu,
                m => !m.mainMenuBox, (m, v) => m.mainMenuBox = !v, true));

            // ---------------- 游戏内 ----------------
            list.Add(Toggle("gridDisabled", "显示网格", "地面显示格子网格", SecInGame,
                m => !m.gridDisabled, (m, v) => m.gridDisabled = !v, true));
            list.Add(Toggle("gridFollow", "网格跟随视角", "网格随相机高度移动", SecInGame,
                m => m.gridFollow, (m, v) => m.gridFollow = v, false));
            list.Add(Toggle("removeCompletedLayers", "完成后移除层", "涂满的层自动隐藏", SecInGame,
                m => m.removeCompletedLayers, (m, v) => m.removeCompletedLayers = v, false));
            list.Add(Toggle("disableVoxelNewAnim", "新方块动画", "新涂方块出现时的缩放动画", SecInGame,
                m => !m.disableVoxelNewAnim, (m, v) => m.disableVoxelNewAnim = !v, true));
            list.Add(Toggle("disableVoxelHoverAnim", "悬停动画", "鼠标悬停方块的放大反馈", SecInGame,
                m => !m.disableVoxelHoverAnim, (m, v) => m.disableVoxelHoverAnim = !v, true));
            list.Add(Toggle("disableVoxelCompletedAnim", "完成动画", "涂对时方块的完成动画", SecInGame,
                m => !m.disableVoxelCompletedAnim, (m, v) => m.disableVoxelCompletedAnim = !v, true));
            list.Add(Toggle("toggleClickWhenCompleteColour", "涂完自动跳色", "一种颜色涂完自动切到下一种", SecInGame,
                m => !m.toggleClickWhenCompleteColour, (m, v) => m.toggleClickWhenCompleteColour = !v, true));
            list.Add(Toggle("colorPaletteToLayer", "调色板按层", "调色板只显示当前层的颜色", SecInGame,
                m => !m.colorPaletteToLayer, (m, v) => m.colorPaletteToLayer = !v, true));
            list.Add(Toggle("hideCompletedColours", "隐藏已完成颜色", "把涂完的颜色从调色板移除", SecInGame,
                m => !m.hideCompletedColours, (m, v) => m.hideCompletedColours = !v, true));
            list.Add(Toggle("colorLock", "颜色锁定", "涂错后按目标色显示", SecInGame,
                m => !m.colorLock, (m, v) => m.colorLock = !v, true));
            list.Add(Toggle("hideNonEssentialVoxels", "隐藏非必要体素", "跳过装饰性小体素", SecInGame,
                m => m.hideNonEssentialVoxels, (m, v) => m.hideNonEssentialVoxels = v, false));
            list.Add(Toggle("seasonalEventsDisabled", "季节特效", "显示节日 / 活动特效", SecInGame,
                m => !m.seasonalEventsDisabled, (m, v) => m.seasonalEventsDisabled = !v, true, RefreshSeasonal));

            // ---------------- 音乐 ----------------
            list.Add(Slider("musicVolume", "音乐音量", "背景音乐音量", SecMusic, 0f, 1f, false,
                m => m.musicVolume, (m, v) => m.musicVolume = v, 0.7f, RefreshMusic));
            list.Add(Slider("sfxVolume", "音效音量", "按钮 / 涂色音效音量", SecMusic, 0f, 1f, false,
                m => m.sfxVolume, (m, v) => m.sfxVolume = v, 0.7f, RefreshMusic));

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
        private static void RefreshSeasonal()
        {
            try { EventManager.Inst.FindCurrentEvent(); EventManager.Inst.RefreshAllGraphics(); }
            catch (Exception) { }
        }

        // ---------------------------------------------------------------- 构造

        private static SettingDef Toggle(string field, string label, string desc, string section,
            Func<MainMenuSaveData, bool> get, Action<MainMenuSaveData, bool> set, bool def, Action onChanged = null)
        {
            Getters[field] = () => get(GameApi.St.mainMenuSaveData);
            Setters[field] = v =>
            {
                set(GameApi.St.mainMenuSaveData, Convert.ToBoolean(v, CultureInfo.InvariantCulture));
                if (onChanged != null) onChanged();
            };
            return new SettingDef { Field = field, Label = label, Desc = desc, Section = section, Kind = SettingKind.Toggle, Default = def };
        }

        private static SettingDef Slider(string field, string label, string desc, string section,
            float min, float max, bool integer, Func<MainMenuSaveData, float> get, Action<MainMenuSaveData, float> set,
            float def, Action onChanged = null)
        {
            Getters[field] = () => get(GameApi.St.mainMenuSaveData);
            Setters[field] = v =>
            {
                set(GameApi.St.mainMenuSaveData, Convert.ToSingle(v, CultureInfo.InvariantCulture));
                if (onChanged != null) onChanged();
            };
            return new SettingDef
            {
                Field = field, Label = label, Desc = desc, Section = section,
                Kind = integer ? SettingKind.IntSlider : SettingKind.FloatSlider,
                Min = (int)min, Max = (int)max, MinF = min, MaxF = max, Default = def
            };
        }
    }
}
