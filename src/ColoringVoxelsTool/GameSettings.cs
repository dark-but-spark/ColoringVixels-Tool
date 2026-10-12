using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 游戏自带设置的一份可编程镜像（3D 版）。
    ///
    /// 游戏把这些设置存在 <see cref="MainMenuSaveData"/>（随存档存盘）里，开关与滑条由
    /// <see cref="SettingsToggle"/> / <see cref="SettingsSlider"/> 读写。这里把每一项收拢成
    /// 「标题 + 读/写委托 + 默认值」，供面板与「推荐预设 / 恢复默认」统一调用。
    ///
    /// 写值后统一 <c>SaveManager.SaveMainMenuData()</c> 落盘；带界面副作用的项额外触发对应刷新。
    /// </summary>
    internal static class GameSettings
    {
        internal sealed class ToggleDef
        {
            public string Label;
            public string Desc;
            public Func<bool> Get;
            public Action<bool> Set;
            public bool Default;
            public Action OnChanged;
        }

        internal sealed class SliderDef
        {
            public string Key;
            public string Label;
            public string Desc;
            public Func<float> Get;
            public Action<float> Set;
            public float Min;
            public float Max;
            public bool Integer;
            public float Default;
            public Action OnChanged;
        }

        internal static readonly ToggleDef[] Toggles =
        {
            T("暗色模式", "深色底，夜里更省眼", MM(m => m.darkMode), MM((m, v) => m.darkMode = v), false, () => RefreshMaterials()),
            T("显示计时器", "关卡里显示本图用时", MM(m => m.showTimer), MM((m, v) => m.showTimer = v), false),
            T("显示完成度", "关卡里显示完成百分比", MM(m => m.showCompletion), MM((m, v) => m.showCompletion = v), false),
            T("隐藏关卡名", "未完成的关卡名显示为 ???", MM(m => m.hideLevelNames), MM((m, v) => m.hideLevelNames = v), false, () => RefreshLevelItems()),
            T("点击切换涂色", "点一下切换按住 / 松开，不用一直压着鼠标", MM(m => m.toggleClick), MM((m, v) => m.toggleClick = v), false),
            T("显示网格", "地面显示格子网格", MM(m => !m.gridDisabled), MM((m, v) => m.gridDisabled = !v), true),
            T("网格跟随视角", "网格随相机高度移动", MM(m => m.gridFollow), MM((m, v) => m.gridFollow = v), false),
            T("完成后移除层", "涂满的层自动隐藏", MM(m => m.removeCompletedLayers), MM((m, v) => m.removeCompletedLayers = v), false),
            T("新方块动画", "新涂方块出现时的缩放动画", MM(m => !m.disableVoxelNewAnim), MM((m, v) => m.disableVoxelNewAnim = !v), true),
            T("悬停动画", "鼠标悬停方块的放大反馈", MM(m => !m.disableVoxelHoverAnim), MM((m, v) => m.disableVoxelHoverAnim = !v), true),
            T("完成动画", "涂对时方块的完成动画", MM(m => !m.disableVoxelCompletedAnim), MM((m, v) => m.disableVoxelCompletedAnim = !v), true),
            T("主菜单盒子", "主菜单显示装饰盒子", MM(m => !m.mainMenuBox), MM((m, v) => m.mainMenuBox = !v), true),
            T("涂完自动跳色", "一种颜色涂完自动切到下一种", MM(m => !m.toggleClickWhenCompleteColour), MM((m, v) => m.toggleClickWhenCompleteColour = !v), true),
            T("调色板按层", "调色板只显示当前层的颜色", MM(m => !m.colorPaletteToLayer), MM((m, v) => m.colorPaletteToLayer = !v), true),
            T("隐藏已完成颜色", "把涂完的颜色从调色板移除", MM(m => !m.hideCompletedColours), MM((m, v) => m.hideCompletedColours = !v), true),
            T("颜色锁定", "涂错后按目标色显示", MM(m => !m.colorLock), MM((m, v) => m.colorLock = !v), true),
            T("隐藏非必要体素", "跳过装饰性小体素", MM(m => m.hideNonEssentialVoxels), MM((m, v) => m.hideNonEssentialVoxels = v), false),
            T("季节特效", "显示节日 / 活动特效", MM(m => !m.seasonalEventsDisabled), MM((m, v) => m.seasonalEventsDisabled = !v), true, () => RefreshSeasonal()),
            T("旧版选书", "用旧式书籍选择界面", MM(m => m.legacyBookSelect), MM((m, v) => m.legacyBookSelect = v), false, () => RefreshWorldSelect()),
        };

        internal static readonly SliderDef[] Sliders =
        {
            S("music", "音乐音量", "背景音乐音量", MM(m => m.musicVolume), MM((m, v) => m.musicVolume = v), 0f, 1f, false, 0.7f, () => RefreshMusic()),
            S("sfx", "音效音量", "按钮 / 涂色音效音量", MM(m => m.sfxVolume), MM((m, v) => m.sfxVolume = v), 0f, 1f, false, 0.7f, () => RefreshMusic()),
        };

        // ------------------------------------------------------------ 读写

        public static bool ReadToggle(ToggleDef d)
        {
            try { return d.Get(); } catch (Exception) { return d.Default; }
        }

        public static void WriteToggle(ToggleDef d, bool value)
        {
            try
            {
                d.Set(value);
                if (d.OnChanged != null) d.OnChanged();
                SaveManager.SaveMainMenuData();
            }
            catch (Exception e) { Debug.LogWarning("[CGV] 写入设置失败：" + e.Message); }
        }

        public static float ReadSlider(SliderDef d)
        {
            try { return d.Get(); } catch (Exception) { return d.Default; }
        }

        public static void WriteSlider(SliderDef d, float value)
        {
            try
            {
                d.Set(value);
                if (d.OnChanged != null) d.OnChanged();
                SaveManager.SaveMainMenuData();
            }
            catch (Exception e) { Debug.LogWarning("[CGV] 写入设置失败：" + e.Message); }
        }

        /// <summary>全部恢复成出厂默认。</summary>
        public static int RestoreDefaults()
        {
            int n = 0;
            foreach (ToggleDef d in Toggles) { if (ReadToggle(d) != d.Default) { WriteToggle(d, d.Default); n++; } }
            foreach (SliderDef d in Sliders) { if (Mathf.Abs(ReadSlider(d) - d.Default) > 0.001f) { WriteSlider(d, d.Default); n++; } }
            return n;
        }

        // ------------------------------------------------------------ 副作用刷新

        private static void RefreshMaterials() { try { MaterialManager.Inst?.RefreshMaterials(); } catch (Exception) { } }
        private static void RefreshLevelItems() { try { WorldVisualiser.Inst?.RefreshLevelItems(); } catch (Exception) { } }
        private static void RefreshWorldSelect() { try { WorldSelectPopulator.Inst?.InitialiseButtons(); } catch (Exception) { } }
        private static void RefreshMusic() { try { MusicManager.Inst.UpdateVolume(); } catch (Exception) { } }

        private static void RefreshSeasonal()
        {
            try { EventManager.Inst.FindCurrentEvent(); EventManager.Inst.RefreshAllGraphics(); }
            catch (Exception) { }
        }

        // ------------------------------------------------------------ 构造委托

        private static ToggleDef T(string label, string desc, Func<bool> get, Action<bool> set, bool def, Action onChanged = null)
        {
            return new ToggleDef { Label = label, Desc = desc, Get = get, Set = set, Default = def, OnChanged = onChanged };
        }

        private static SliderDef S(string key, string label, string desc, Func<float> get, Action<float> set,
            float min, float max, bool integer, float def, Action onChanged = null)
        {
            return new SliderDef { Key = key, Label = label, Desc = desc, Get = get, Set = set, Min = min, Max = max, Integer = integer, Default = def, OnChanged = onChanged };
        }

        // MainMenuSaveData 便捷读/写委托（存档实例由 GameApi.St 提供）
        private static Func<bool> MM(Func<MainMenuSaveData, bool> get)
        {
            return () => get(GameApi.St.mainMenuSaveData);
        }

        private static Action<bool> MM(Action<MainMenuSaveData, bool> set)
        {
            return v => set(GameApi.St.mainMenuSaveData, v);
        }

        private static Func<float> MM(Func<MainMenuSaveData, float> get)
        {
            return () => get(GameApi.St.mainMenuSaveData);
        }

        private static Action<float> MM(Action<MainMenuSaveData, float> set)
        {
            return v => set(GameApi.St.mainMenuSaveData, v);
        }
    }
}
