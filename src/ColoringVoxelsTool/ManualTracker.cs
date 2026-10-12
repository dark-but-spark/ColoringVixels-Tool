using UnityEngine;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 记录「人工涂色」行为：左键按下到抬起之间，画面上新涂对了多少格。
    /// 这个数字就是等级系统里说的「涂色率」（每击平均涂到的格数），经验记在「人工辅助」那条线上。
    ///
    /// 面板上的点击不算。
    /// （人工辅助扫描引擎替你涂的部分，等辅助模块迁移后在同一处按增量补记。）
    /// </summary>
    internal sealed class ManualTracker : MonoBehaviour
    {
        public static ManualTracker Instance;

        private bool _down;
        private int _startPainted;
        private float _downTime;

        public static void Ensure(Transform parent)
        {
            if (Instance != null) return;
            var go = new GameObject("ColoringVoxelsTool.ManualTracker");
            if (parent != null) go.transform.SetParent(parent, false);
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<ManualTracker>();
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            var b = GameApi.B;
            if (!GameApi.InLevel(b))
            {
                _down = false;
                return;
            }

            if (Input.GetMouseButtonDown(0) && !CheatPanel.IsMouseOverPanel)
            {
                _down = true;
                _startPainted = GameApi.PaintedVoxels(b);
                _downTime = Time.unscaledTime;
                return;
            }

            if (_down && Input.GetMouseButtonUp(0))
            {
                _down = false;
                int now = GameApi.PaintedVoxels(b);
                int delta = now - _startPainted;
                if (delta < 0) delta = 0;
                if (delta > 3000) delta = 3000;   // 防止把自动模块的成果算进来
                UserProfile.RecordManualClick(delta, Time.unscaledTime - _downTime);

                // 手工把最后几格涂完 → 这张图的「完成」成果记在人工辅助轨上
                if (delta > 0 && GameApi.RemainingVoxels(b) <= 0)
                    UserProfile.RecordImageCompleted(GameApi.TotalVoxels(b), XpTrack.Manual);
            }
        }
    }
}
