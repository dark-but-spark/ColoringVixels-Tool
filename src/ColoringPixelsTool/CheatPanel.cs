using System;
using UnityEngine;

namespace ColoringPixelsTool
{
    /// <summary>
    /// 游戏内面板（3D 体素版·精简）。
    ///
    /// 目前已接入：关卡进度、拟人涂色、定时自动化、解锁、提醒播报、主题与输入闸门。
    /// 「首页 / 自动完成 / 人工辅助 / 设置 / 调试」等完整页签会在此基础上逐步长回来。
    /// </summary>
    public class CheatPanel : MonoBehaviour
    {
        public static CheatPanel Instance;

        /// <summary>某个输入框正在编辑时，游戏热键要让路（供人工辅助等模块查询）。</summary>
        public static bool TextFieldFocused;

        /// <summary>鼠标是否压在面板上（等价于输入闸门）。</summary>
        public static bool IsMouseOverPanel => InputGate.BlockGameInput;

        private const float Pad = 16f;
        private const float W = 470f;

        private bool _visible;
        private float _showT;
        private Rect _window;

        private Rect _mouseBlockRect;
        private float _lastOverCheck;

        private string _toast = "";
        private float _toastUntil;

        private float _countdownMinutes = 45f;

        private readonly CanvasPreview _preview = new CanvasPreview();

        private void Awake()
        {
            Instance = this;
            _countdownMinutes = Plugin.RemindCountdownMinutes.Value;
            PanelHooks.OnNudgeReveal += OnNudge;
        }

        private void OnDestroy()
        {
            PanelHooks.OnNudgeReveal -= OnNudge;
        }

        private void OnNudge(string reason)
        {
            _visible = true;
        }

        public void Toggle()
        {
            _visible = !_visible;
            if (_visible) _countdownMinutes = Plugin.RemindCountdownMinutes.Value;
        }

        public void Reveal()
        {
            _visible = true;
        }

        public void Toast(string text)
        {
            _toast = text ?? "";
            _toastUntil = Time.unscaledTime + 2.2f;
        }

        private void Update()
        {
            if (Plugin.KeyToggle != null && Input.GetKeyDown(Plugin.KeyToggle.Value)) Toggle();

            _showT = Mathf.MoveTowards(_showT, _visible ? 1f : 0f, Time.unscaledDeltaTime * 9f);

            if (_showT > 0.001f) _preview.Refresh(Time.unscaledDeltaTime);

            // 输入闸门：鼠标压面板时屏蔽游戏点击 / 滚轮（Patches 读 InputGate）
            bool over = _visible && _showT > 0.4f && _window.Contains(GuiMouse());
            InputGate.BlockGameInput = over;
            InputGate.BlockScrollInput = over;
        }

        private static Vector2 GuiMouse()
        {
            Vector3 m = Input.mousePosition;
            return new Vector2(m.x, Screen.height - m.y);
        }

        private void OnGUI()
        {
            if (_showT <= 0.001f) return;

            if (Theme.Current != Plugin.PanelTheme.Value) Theme.Apply(Plugin.PanelTheme.Value);

            Event e = Event.current;
            Ui.Mouse = e.mousePosition;
            Ui.MouseInside = true;

            float h = Mathf.Min(Screen.height - 60f, 610f);
            _window = new Rect((Screen.width - W) * 0.5f, 34f, W, h);

            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _showT);
            DrawPanel(_window);
            GUI.color = prev;

            DrawOverlays();
        }

        // ------------------------------------------------------------ 主窗口

        private void DrawPanel(Rect w)
        {
            Ui.Surface(w, 14f);

            // 顶部渐隐条
            Ui.Fill(new Rect(w.x, w.y, w.width, 50f), Ui.Alpha(Ui.WindowTop, 0.65f));

            float y = w.y + Pad;
            float innerW = w.width - Pad * 2f;

            // 标题栏
            Ui.Text(new Rect(w.x + Pad, y, innerW - 40f, 24f),
                Plugin.PluginName, Ui.Title, Ui.TextCol);
            Ui.Text(new Rect(w.x + Pad, y + 22f, innerW - 40f, 16f),
                "v" + Plugin.Version + " · 3D 体素版", Ui.MutedSmall);

            var close = new Rect(w.xMax - Pad - 24f, y + 2f, 24f, 24f);
            if (Ui.Button(close, "×", Ui.Bad, true)) _visible = false;
            y += 50f;

            DrawLevelSection(w, ref y, innerW);
            DrawPreviewSection(w, ref y, innerW);
            DrawAutoSection(w, ref y, innerW);
            DrawSchedulerSection(w, ref y, innerW);
            DrawUnlockSection(w, ref y, innerW);

            // 底部提示
            if (Time.unscaledTime < _toastUntil && !string.IsNullOrEmpty(_toast))
                Ui.Text(new Rect(w.x + Pad, w.yMax - 26f, innerW, 18f), _toast, Ui.MutedSmall, Ui.Accent2);
        }

        private void DrawLevelSection(Rect w, ref float y, float innerW)
        {
            Ui.Section(new Rect(w.x + Pad, y, innerW, 16f), "关卡");
            y += 22f;

            var b = GameApi.B;
            bool inLevel = GameApi.InLevel(b);

            if (!inLevel)
            {
                Ui.Text(new Rect(w.x + Pad, y, innerW, 18f), "尚未进入关卡（进一张图后这里会显示进度）", Ui.MutedStyle);
                y += 26f;
                return;
            }

            int layer = b.selectedLayer;
            int layers = GameApi.LayerCount(b);
            int total = GameApi.TotalVoxels(b);
            int remain = GameApi.RemainingVoxels(b);
            float prog = GameApi.Progress(b);

            Ui.InfoRow(new Rect(w.x + Pad, y, innerW, 18f), "世界 / 关卡",
                (GameApi.CurrentWorldIndex + 1) + " - " + (GameApi.CurrentLevelIndex + 1), Ui.TextCol);
            y += 20f;
            Ui.InfoRow(new Rect(w.x + Pad, y, innerW, 18f), "当前层",
                (layer + 1) + " / " + layers, Ui.TextCol);
            y += 20f;
            Ui.InfoRow(new Rect(w.x + Pad, y, innerW, 18f), "当前颜色",
                "#" + (b.selectedCol + 1) + " / " + GameApi.ColourCount(b), Ui.TextCol);
            y += 20f;
            Ui.InfoRow(new Rect(w.x + Pad, y, innerW, 18f), "剩余体素",
                remain + " / " + total, remain > 0 ? Ui.Warn : Ui.Good);
            y += 22f;

            Ui.ProgressBar(new Rect(w.x + Pad, y, innerW, 10f), prog, Ui.Accent);
            y += 16f;
            Ui.Text(new Rect(w.x + Pad, y, innerW, 16f), (prog * 100f).ToString("0.0") + "%", Ui.MutedSmall);
            y += 24f;
        }

        private void DrawPreviewSection(Rect w, ref float y, float innerW)
        {
            Ui.Section(new Rect(w.x + Pad, y, innerW, 16f), "预览（当前层）");
            y += 22f;

            Texture2D tex = _preview.Texture;
            if (tex == null)
            {
                Ui.Text(new Rect(w.x + Pad, y, innerW, 18f), "进入关卡后这里显示当前层的缩略图", Ui.MutedStyle);
                y += 24f;
                return;
            }

            float maxH = 140f;
            float ar = (float)_preview.Rows / Mathf.Max(1, _preview.Cols);
            float pw = innerW;
            float ph = pw * ar;
            if (ph > maxH) { ph = maxH; pw = ph / Mathf.Max(0.0001f, ar); }

            var box = new Rect(w.x + Pad + (innerW - pw) * 0.5f, y, pw, ph);
            Ui.Round(box, 6f, Ui.Track);
            GUI.DrawTexture(box, tex, ScaleMode.ScaleToFit, true);
            y += ph + 8f;

            _preview.ShowGhost = Ui.ToggleRow(new Rect(w.x + Pad, y, innerW, 40f),
                _preview.ShowGhost, "显示未涂底稿", "把还没涂的格子用暗色目标色示意");
            y += 44f;
        }

        private void DrawAutoSection(Rect w, ref float y, float innerW)
        {
            Ui.Section(new Rect(w.x + Pad, y, innerW, 16f), "自动完成");
            y += 24f;

            var b = GameApi.B;
            bool inLevel = GameApi.InLevel(b);
            var painter = AutoPainter.Instance;

            float bw = (innerW - 12f) / 3f;
            if (Ui.Button(new Rect(w.x + Pad, y, bw, 34f), "一键涂完本层"))
            {
                if (inLevel) { int n = GameApi.FillLayer(b); Toast("本层涂了 " + n + " 格"); }
                else Toast("还没进关卡");
            }

            bool running = painter != null && painter.Running;
            if (Ui.Button(new Rect(w.x + Pad + bw + 6f, y, bw, 34f), running ? "停止拟人涂色" : "开始拟人涂色",
                    running ? Ui.Bad : Ui.Accent, true))
            {
                if (painter != null)
                {
                    if (!running && Plugin.AutoRestrict.Value)
                        painter.RestrictToColour = inLevel ? b.selectedCol : 0;
                    painter.Toggle();
                }
            }

            if (Ui.Button(new Rect(w.x + Pad + (bw + 6f) * 2f, y, bw, 34f), "立即保存"))
            {
                if (inLevel) { GameApi.SaveNow(b); Toast("已保存"); }
                else Toast("还没进关卡");
            }
            y += 42f;

            Plugin.AutoSpeed.Value = (int)Ui.SliderRow("auto-speed",
                new Rect(w.x + Pad, y, innerW, 34f), Plugin.AutoSpeed.Value, 5f, 220f, "手速", Plugin.AutoSpeed.Value + " 格/秒", true);
            if (painter != null) painter.CellsPerSecond = Plugin.AutoSpeed.Value;
            y += 40f;

            Plugin.AutoRestrict.Value = Ui.ToggleRow(new Rect(w.x + Pad, y, innerW, 40f),
                Plugin.AutoRestrict.Value, "只涂当前颜色", "只处理调色板里高亮的那一种颜色");
            y += 44f;

            if (running && painter != null)
            {
                Ui.Text(new Rect(w.x + Pad, y, innerW, 16f),
                    "拟人：" + painter.Status + " · 已涂 " + painter.PaintedThisRun + " 格", Ui.MutedSmall, Ui.Accent2);
                y += 22f;
            }
        }

        private void DrawSchedulerSection(Rect w, ref float y, float innerW)
        {
            Ui.Section(new Rect(w.x + Pad, y, innerW, 16f), "自动化（定时 / 连涂）");
            y += 24f;

            var s = AutoScheduler.Instance;
            bool running = s != null && s.Running;

            if (Ui.Button(new Rect(w.x + Pad, y, innerW, 34f), running ? "停止自动化" : "开始自动化",
                    running ? Ui.Bad : Ui.Accent2, true))
            {
                if (s != null)
                {
                    if (running) s.StopSession("面板停止");
                    else s.StartSession(Plugin.AutoTotalMinutes.Value, Plugin.AutoContinuous.Value,
                        Plugin.AutoPauseBetweenImages.Value, Plugin.AutoDrawingSpeedPreset.Value);
                }
            }
            y += 40f;

            if (running && s != null)
            {
                Ui.InfoRow(new Rect(w.x + Pad, y, innerW, 18f), "状态", s.Status, Ui.TextCol);
                y += 20f;
                Ui.InfoRow(new Rect(w.x + Pad, y, innerW, 18f), "剩余",
                    PaintTimer.Format(s.RemainingSeconds) + " · 已完成 " + s.ImagesCompleted + " 张", Ui.Muted);
                y += 22f;
            }
        }

        private void DrawUnlockSection(Rect w, ref float y, float innerW)
        {
            Ui.Section(new Rect(w.x + Pad, y, innerW, 16f), "解锁");
            y += 24f;

            float bw = (innerW - 12f) / 3f;
            if (Ui.Button(new Rect(w.x + Pad, y, bw, 34f), "显示全部世界"))
            {
                int n = Unlocker.RevealAllWorlds();
                Toast("解锁了 " + n + " 个世界");
            }
            if (Ui.Button(new Rect(w.x + Pad + bw + 6f, y, bw, 34f), "解锁全部成就"))
            {
                int n = Unlocker.UnlockAllAchievements();
                Toast("尝试解锁 " + n + " 个成就");
            }
            if (Ui.Button(new Rect(w.x + Pad + (bw + 6f) * 2f, y, bw, 34f), "标记全部完成"))
            {
                int n = Unlocker.CompleteAllWorlds();
                Toast("标记完成 " + n + " 关");
            }
            y += 42f;
        }

        // ------------------------------------------------------------ 浮层

        private void DrawOverlays()
        {
            // 提醒弹窗
            if (Reminder.Popup)
            {
                var r = new Rect((Screen.width - 340f) * 0.5f, (Screen.height - 170f) * 0.5f, 340f, 170f);
                Ui.Fill(new Rect(0f, 0f, Screen.width, Screen.height), Ui.Alpha(Ui.Scrim, 0.55f));
                Ui.SurfaceEdge(r, 14f);
                Ui.Text(new Rect(r.x + 18f, r.y + 16f, r.width - 36f, 24f), Reminder.PopupTitle, Ui.Bold, Ui.TextCol);
                GUI.Label(new Rect(r.x + 18f, r.y + 48f, r.width - 36f, 70f), Reminder.PopupBody, Ui.Label);
                if (Ui.Button(new Rect(r.xMax - 110f, r.yMax - 48f, 92f, 34f), "知道了", Ui.Accent, true))
                    Reminder.DismissPopup();
            }

            // 顶部横幅
            if (Reminder.BannerVisible)
            {
                var r = new Rect((Screen.width - 420f) * 0.5f, 12f, 420f, 40f);
                Ui.SurfaceEdge(r, 12f);
                Ui.Text(new Rect(r.x + 16f, r.y, r.width - 32f, r.height), Reminder.BannerText, Ui.Center, Ui.Accent2);
            }
        }
    }
}
