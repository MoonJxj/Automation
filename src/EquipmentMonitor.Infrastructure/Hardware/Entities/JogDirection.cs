namespace EquipmentMonitor.Infrastructure
{
    /// <summary>
    /// 点动方向，用于把 <see cref="IAxisController.Jog"/> 的布尔方向参数表达为可读的枚举。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>刻意没有 0 值成员</b>：<c>default(JogDirection)</c> 落在枚举外，
    /// "忘了赋方向"不会被静默当成某个方向执行 —— 方向错了意味着往反方向撞。
    /// </para>
    /// <para>
    /// 与 <c>EquipmentMonitor.Core.Enums.MotionDirection</c>（PLCopen 命令级方向）是两层概念：
    /// 本枚举只描述板卡点动的正/负方向。
    /// </para>
    /// </remarks>
    public enum JogDirection
    {
        /// <summary>负方向。</summary>
        Negative = -1,

        /// <summary>正方向。</summary>
        Positive = 1
    }
}
