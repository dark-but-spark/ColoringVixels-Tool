using System;
using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 提供一份带中文字形的动态字体。
    ///
    /// 游戏自带的是像素字体（Silkscreen 等），没有中文字形；把中文塞进去会显示成方框，
    /// 所以注入到游戏界面里的中文按钮都要换成一个系统中文字体。
    /// </summary>
    internal static class CjkFont
    {
        private static Font _font;
        private static bool _failed;
        private static string _name;

        /// <summary>当前使用的字体名（未就绪时为空）。</summary>
        internal static string Name { get { return _name; } }

        internal static Font Get()
        {
            if (_font != null) return _font;
            if (_failed) return null;

            string[] candidates =
            {
                "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑",
                "SimHei", "黑体", "SimSun", "宋体", "NSimSun",
                "Microsoft JhengHei", "Noto Sans CJK SC", "Source Han Sans SC",
                "Arial Unicode MS"
            };

            // 直接按优先级尝试创建：避免 Font.GetOSInstalledFontNames() 枚举全部系统字体
            // （很慢，首次调用常常正好卡在进游戏 / 界面绘制那一帧）。
            for (int i = 0; i < candidates.Length; i++)
            {
                Font f = Font.CreateDynamicFontFromOSFont(candidates[i], 24);
                if (f != null)
                {
                    _font = f;
                    _name = candidates[i];
                    Log.Info("中文字体：" + candidates[i]);
                    return _font;
                }
            }

            _failed = true;
            Log.Warn("未找到可用的中文字体，注入的中文按钮可能显示为方块。");
            return null;
        }

        /// <summary>文本里是否含中日韩字符。</summary>
        internal static bool HasCjk(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= 0x2E80 && c <= 0x9FFF) return true;
                if (c >= 0xF900) return true;
            }
            return false;
        }
    }
}
