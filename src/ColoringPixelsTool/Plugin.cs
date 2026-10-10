using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ColoringPixelsTool
{
    /// <summary>
    /// 《Coloring Voxels》插件入口（3D 体素版）。
    ///
    /// 迁移进度：当前是最小可运行核心 —— 身份 / 热键配置 / Harmony 装配 / DLC 解锁开关。
    /// 面板、自动涂色、高亮、统计等模块会逐步接回来。
    /// </summary>
    [BepInPlugin(Guid, PluginName, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "coloringvoxels.cheatsuite";
        public const string PluginName = "Coloring Voxels Tool";

        /// <summary>插件版本。发版时与仓库根目录的 VERSION 文件一起更新。</summary>
        public const string Version = "3.0.6";

        internal static Plugin Instance;
        internal static Harmony HarmonyInstance;

        // ---- 通用热键 ----
        internal static ConfigEntry<KeyCode> KeyToggle;
        internal static ConfigEntry<KeyCode> KeyFill;
        internal static ConfigEntry<KeyCode> KeyAuto;
        internal static ConfigEntry<KeyCode> KeyErase;
        internal static ConfigEntry<KeyCode> KeySave;
        internal static ConfigEntry<KeyCode> KeyHighlight;
        internal static ConfigEntry<KeyCode> KeyHoldLeft;

        // ---- 解锁 ----
        internal static ConfigEntry<bool> UnlockAllDlc;

        // ---- 功能开关 ----
        internal static ConfigEntry<bool> AutoUnlocked;
        internal static ConfigEntry<bool> GuideShown;

        private void Awake()
        {
            Instance = this;
            Log.Bind(Logger);

            BindConfig();

            UserProfile.Load();
            PaintTimer.Load();
            Stats.Load();

            Unlocker.ForceDlcOwned = UnlockAllDlc.Value;

            HarmonyInstance = new Harmony(Guid);
            try
            {
                HarmonyInstance.PatchAll(typeof(Plugin).Assembly);
                Log.Info("Harmony 补丁已应用");
            }
            catch (System.Exception e)
            {
                Log.Error("Harmony 补丁失败: " + e);
            }

            Log.Info(PluginName + " v" + Version + " 已加载 —— 按 " + KeyToggle.Value + " 打开面板");
        }

        private void Update()
        {
            float dt = UnityEngine.Time.unscaledDeltaTime;
            UserProfile.Tick(dt);
            UserProfile.TickSave(dt);
            PaintTimer.Tick(dt);
            Stats.Tick(dt);
        }

        private void OnDestroy()
        {
            Log.Info("插件正在退出，补写落盘数据……");
            try { PaintTimer.Save(); } catch (System.Exception e) { Log.Error("保存单图计时失败：" + e.Message); }
            try { Stats.Save(); } catch (System.Exception e) { Log.Error("保存使用统计失败：" + e.Message); }
            try { UserProfile.Save(); } catch (System.Exception e) { Log.Error("保存用户资料失败：" + e.Message); }

            HarmonyInstance?.UnpatchSelf();
            Log.Info("插件已退出。");
        }

        /// <summary>
        /// BepInEx 的 config 目录：插件配置（.cfg）与预设放在这里。
        /// 优先问 BepInEx 要（尊重用户自定义的路径），拿不到再回退到 &lt;游戏目录&gt;\BepInEx\config。
        /// </summary>
        internal static string ConfigDirectory()
        {
            try
            {
                if (Instance != null && Instance.Config != null &&
                    !string.IsNullOrEmpty(Instance.Config.ConfigFilePath))
                    return System.IO.Path.GetDirectoryName(Instance.Config.ConfigFilePath);
            }
            catch { /* 忽略 */ }

            return System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "BepInEx", "config");
        }

        private void BindConfig()
        {
            var g = "0-通用";
            KeyToggle = Config.Bind(g, "面板热键", KeyCode.F1, "打开/关闭面板");
            KeyFill = Config.Bind(g, "一键涂完热键", KeyCode.F2, "一键涂完当前层");
            KeyAuto = Config.Bind(g, "拟人涂色热键", KeyCode.F3, "开始/停止拟人自动涂色");
            KeyErase = Config.Bind(g, "清空画布热键", KeyCode.F4, "清空当前层");
            KeySave = Config.Bind(g, "保存热键", KeyCode.F5, "立即保存当前关卡");
            KeyHighlight = Config.Bind(g, "颜色高亮热键", KeyCode.F6, "开关颜色高亮");
            KeyHoldLeft = Config.Bind(g, "长按左键", KeyCode.Q, "按住 = 一直按着鼠标左键，涂色时不用一直压着鼠标");

            var u = "1-解锁";
            UnlockAllDlc = Config.Bind(u, "解锁全部DLC", true, "让所有 DLC / 奖励世界可以直接进入");

            var f = "A-功能开关";
            AutoUnlocked = Config.Bind(f, "自动绘图已解锁", false,
                "自动绘图（一键涂完 / 拟人涂色 / 自动化）默认上锁，在面板上解锁后记成 true");
            GuideShown = Config.Bind(f, "已看过新手指引", false,
                "首次启动会弹出新手指引，点「不再提示」后记成 true");
        }
    }
}
