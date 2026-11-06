namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 轴参数的作用域。决定“改这个参数需要什么权限”，以及它是设备配置还是工艺配置。
/// </summary>
public enum ParameterScope
{
    /// <summary>设备参数：地址、缩放、软限位、回零方式等。需要 <see cref="Permission.MachineConfig"/>。</summary>
    Device = 0,

    /// <summary>工艺参数：默认速度/加速度/减速度/加加速度。需要 <see cref="Permission.ProcessConfig"/>。</summary>
    Process = 1,

    /// <summary>控制器/驱动器专属参数（增益、编码器分辨率等）。需要 <see cref="Permission.MachineConfig"/>。</summary>
    Controller = 2
}
