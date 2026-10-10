namespace ColoringPixelsTool
{
    /// <summary>
    /// 面板与游戏之间的输入闸门。
    ///
    /// 面板（尚未迁移）负责在鼠标压在自己身上时把对应标志置位，
    /// <see cref="Patches"/> 负责在游戏的输入更新里读取并屏蔽穿透。
    /// 这样游戏输入补丁不必依赖面板类型，便于分开迁移。
    /// </summary>
    internal static class InputGate
    {
        /// <summary>鼠标按在面板上时，屏蔽画布射线 / 涂色。</summary>
        public static bool BlockGameInput;

        /// <summary>鼠标悬停在面板上滚动时，屏蔽游戏自身的滚轮缩放。</summary>
        public static bool BlockScrollInput;
    }
}
