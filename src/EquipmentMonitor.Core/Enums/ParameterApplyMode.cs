namespace EquipmentMonitor.Core.Enums;

/// <summary>
/// 参数生效方式：决定改完之后是立即写控制器、下一条命令使用，还是必须重启。
/// </summary>
public enum ParameterApplyMode
{
    /// <summary>立即写控制器/PLC；失败则本次修改不生效。</summary>
    Immediate = 0,

    /// <summary>写控制器后，下一条运动命令使用新值（例如默认速度/加速度）。</summary>
    NextCommand = 1,

    /// <summary>只写配置文件，重启/重新装配后生效（地址、缩放等会重建映射的参数）。</summary>
    Restart = 2
}
