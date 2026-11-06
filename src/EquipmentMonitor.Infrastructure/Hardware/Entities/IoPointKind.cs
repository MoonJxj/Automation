namespace EquipmentMonitor.Infrastructure;

/// <summary>
/// IO 点类别：输入或输出。
/// </summary>
/// <remarks>
/// 配置实体 <c>IoPointConfig.Kind</c> 与运行时快照 <c>IoPointState.Type</c> 共用本枚举，
/// 避免出现两个语义相同、名字不同、值也相同的枚举（旧代码里曾同时有
/// <c>IoPointType</c> 与 <c>IoPointKind</c>）。
/// </remarks>
public enum IoPointKind
{
    /// <summary>输入点（只读）。</summary>
    Input = 0,

    /// <summary>输出点（可写、可回读）。</summary>
    Output = 1
}
