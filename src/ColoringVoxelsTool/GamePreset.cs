using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 游戏设置的「推荐预设 / 撤销 / 自定义预设」管理（3D 版）。
    ///
    /// 预设以「开关标题 / 滑条键 = 值」的文本形式存放，落在用户数据目录，不随游戏目录清理。
    /// 套用前先记一份快照，随时可「撤销上次改动」。
    /// </summary>
    internal static class GamePreset
    {
        public static string PresetPath => AppPaths.InUserData("ColoringVoxelsTool.Preset.txt");
        private static string SnapshotPath => AppPaths.InUserData("ColoringVoxelsTool.Preset.snapshot.txt");

        // 内置推荐值（未列出的项用游戏出厂默认）
        private static readonly Dictionary<string, bool> RecommendedToggles = new Dictionary<string, bool>
        {
            { "显示计时器", true },
            { "显示完成度", true },
            { "涂完自动跳色", true },
            { "调色板按层", true },
            { "隐藏已完成颜色", false },
            { "隐藏非必要体素", true },
            { "完成后移除层", false },
            { "网格跟随视角", false },
            { "颜色锁定", true },
        };

        private static readonly Dictionary<string, float> RecommendedSliders = new Dictionary<string, float>
        {
            { "music", 0.7f },
            { "sfx", 0.7f },
        };

        // ------------------------------------------------------------ 套用

        /// <summary>套用推荐预设（先记快照）。返回改动的项数。</summary>
        public static int ApplyRecommended()
        {
            SaveSnapshot();
            int changed = 0;
            foreach (var d in GameSettings.Toggles)
            {
                bool target;
                if (!RecommendedToggles.TryGetValue(d.Label, out target)) target = d.Default;
                if (GameSettings.ReadToggle(d) != target) { GameSettings.WriteToggle(d, target); changed++; }
            }
            foreach (var d in GameSettings.Sliders)
            {
                float target;
                if (!RecommendedSliders.TryGetValue(d.Key, out target)) target = d.Default;
                if (Mathf.Abs(GameSettings.ReadSlider(d) - target) > 0.001f) { GameSettings.WriteSlider(d, target); changed++; }
            }
            return changed;
        }

        /// <summary>恢复游戏出厂默认（先记快照，可撤销）。</summary>
        public static int ApplyGameDefaults()
        {
            SaveSnapshot();
            return GameSettings.RestoreDefaults();
        }

        /// <summary>套用自定义预设文件（先记快照）。文件不存在时回退到推荐预设。</summary>
        public static int ApplyCustom()
        {
            var toggles = new Dictionary<string, bool>();
            var sliders = new Dictionary<string, float>();
            if (!File.Exists(PresetPath) || !LoadFile(PresetPath, toggles, sliders))
                return ApplyRecommended();

            SaveSnapshot();
            int changed = 0;
            foreach (var d in GameSettings.Toggles)
            {
                bool v;
                if (toggles.TryGetValue(d.Label, out v) && GameSettings.ReadToggle(d) != v)
                {
                    GameSettings.WriteToggle(d, v);
                    changed++;
                }
            }
            foreach (var d in GameSettings.Sliders)
            {
                float v;
                if (sliders.TryGetValue(d.Key, out v) && Mathf.Abs(GameSettings.ReadSlider(d) - v) > 0.001f)
                {
                    GameSettings.WriteSlider(d, v);
                    changed++;
                }
            }
            return changed;
        }

        /// <summary>把当前设置存成自定义预设文件。</summary>
        public static void SaveCurrentAsPreset()
        {
            Write(CurrentLines(), PresetPath);
        }

        /// <summary>撤销上次「套用预设 / 恢复默认」之前的改动。</summary>
        public static bool UndoLast()
        {
            if (!File.Exists(SnapshotPath)) return false;
            var toggles = new Dictionary<string, bool>();
            var sliders = new Dictionary<string, float>();
            if (!LoadFile(SnapshotPath, toggles, sliders)) return false;

            foreach (var d in GameSettings.Toggles)
            {
                bool v;
                if (toggles.TryGetValue(d.Label, out v) && GameSettings.ReadToggle(d) != v)
                    GameSettings.WriteToggle(d, v);
            }
            foreach (var d in GameSettings.Sliders)
            {
                float v;
                if (sliders.TryGetValue(d.Key, out v) && Mathf.Abs(GameSettings.ReadSlider(d) - v) > 0.001f)
                    GameSettings.WriteSlider(d, v);
            }
            return true;
        }

        // ------------------------------------------------------------ 内部

        private static void SaveSnapshot()
        {
            Write(CurrentLines(), SnapshotPath);
        }

        private static string CurrentLines()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# ColoringVoxelsTool 游戏设置（T/toggle S/slider）");
            foreach (var d in GameSettings.Toggles)
                sb.Append("T\t").Append(d.Label).Append('\t').Append(GameSettings.ReadToggle(d) ? "1" : "0").AppendLine();
            foreach (var d in GameSettings.Sliders)
                sb.Append("S\t").Append(d.Key).Append('\t')
                  .Append(GameSettings.ReadSlider(d).ToString("F3", CultureInfo.InvariantCulture)).AppendLine();
            return sb.ToString();
        }

        private static void Write(string text, string path)
        {
            try { AppPaths.WriteAtomic(path, text); }
            catch (Exception e) { Debug.LogWarning("[CGV] 写入预设失败：" + e.Message); }
        }

        private static bool LoadFile(string path, Dictionary<string, bool> toggles, Dictionary<string, float> sliders)
        {
            bool any = false;
            try
            {
                foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    string line = raw == null ? "" : raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    string[] p = line.Split('\t');
                    if (p.Length < 3) continue;

                    if (p[0] == "T")
                    {
                        if (toggles != null) toggles[p[1]] = p[2] == "1";
                        any = true;
                    }
                    else if (p[0] == "S")
                    {
                        float v;
                        if (sliders != null && float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                            sliders[p[1]] = v;
                        any = true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CGV] 读取预设失败：" + e.Message);
            }
            return any;
        }
    }
}
