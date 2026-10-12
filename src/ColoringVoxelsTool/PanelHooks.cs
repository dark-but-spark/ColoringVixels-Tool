using System;

namespace ColoringVoxelsTool
{
    /// <summary>
    /// 面板与游戏无关件之间的回调挂钩。
    ///
    /// 提醒中心 / 退出确认这些模块需要在「有事情要告诉用户」时把面板唤出来，
    /// 但它们不该直接依赖尚未迁移的面板类型。面板迁移完成后订阅这里即可。
    /// </summary>
    internal static class PanelHooks
    {
        /// <summary>请求把面板调到可见（reason 仅用于日志 / 调试）。</summary>
        public static Action<string> OnNudgeReveal;

        public static void NudgeReveal(string reason)
        {
            try { OnNudgeReveal?.Invoke(reason); }
            catch (Exception) { }
        }
    }
}
