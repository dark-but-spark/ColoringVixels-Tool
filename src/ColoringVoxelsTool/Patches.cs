using HarmonyLib;

namespace ColoringVoxelsTool
{
    /// <summary>游戏内部逻辑的最小侵入式补丁（3D 体素版）。</summary>
    internal static class Patches
    {
        /// <summary>DLC 拥有判定恒为真 —— 解锁所有 DLC / 奖励世界。</summary>
        [HarmonyPatch(typeof(SteamManager), "CheckIfProductOwned")]
        internal static class SteamManager_CheckIfProductOwned
        {
            private static bool Prefix(ref bool __result)
            {
                if (!Unlocker.ForceDlcOwned) return true;
                __result = true;
                return false;
            }
        }

        /// <summary>鼠标按在面板上时屏蔽画布射线 / 涂色，悬停滚动时屏蔽游戏滚轮缩放。</summary>
        [HarmonyPatch(typeof(ClickManager), "Update")]
        internal static class ClickManager_Update
        {
            private static bool Prefix()
            {
                if (InputGate.BlockGameInput) return false;
                if (InputGate.BlockScrollInput) return false;
                return true;
            }
        }
    }
}
