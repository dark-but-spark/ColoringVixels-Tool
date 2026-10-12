using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 游戏设置的「自定义预设」读取与套用（3D 版）。
    ///
    /// 预设以「设置键 = 值」的文本形式存放在用户数据目录；套用前先记快照，可「撤销上次改动」。
    /// </summary>
    internal static class GamePreset
    {
        public static string FilePath => AppPaths.InUserData("ColoringVoxelsTool.Preset.txt");

        private static readonly Dictionary<string, string> Values = new Dictionary<string, string>();

        /// <summary>当前载入的预设条目数。</summary>
        public static int Count => Values.Count;

        /// <summary>从磁盘重新载入预设文件。</summary>
        public static void Reload()
        {
            Values.Clear();
            if (!File.Exists(FilePath)) return;
            try
            {
                foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string line = raw == null ? "" : raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    Values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception e) { Debug.LogWarning("[CGV] 读取预设失败：" + e.Message); }
        }

        /// <summary>把预设套用到游戏设置（先记快照）。返回结果说明。</summary>
        public static string Apply()
        {
            if (Values.Count == 0) return "没有可用的预设（先在设置页「把当前设置存为预设」）";

            GameSettings.CaptureSnapshot();
            int n = 0;

            foreach (var d in GameSettings.All)
            {
                string v;
                if (!Values.TryGetValue(d.Field, out v)) continue;

                if (d.Kind == SettingKind.Toggle)
                {
                    bool b = v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
                    if (GameSettings.GetBool(d.Field) != b) { GameSettings.SetValue(d, b); n++; }
                }
                else if (d.Kind == SettingKind.IntSlider)
                {
                    int iv;
                    if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out iv)) continue;
                    if (GameSettings.IntOf(d) != iv) { GameSettings.SetValue(d, iv); n++; }
                }
                else
                {
                    float fv;
                    if (!float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out fv)) continue;
                    if (Mathf.Abs(GameSettings.GetFloat(d.Field) - fv) > 0.0005f) { GameSettings.SetValue(d, fv); n++; }
                }
            }

            return "已套用预设（" + n + " 项）";
        }
    }
}
