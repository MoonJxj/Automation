using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

/// <summary>
/// 元件参数配置的持久化端口。
/// </summary>
/// <remarks>
/// <para>实现可以是 JSON 配置文件、SQLite 或远端配置服务；接口只承诺
/// “按稳定 ElementId 读写完整参数集”。</para>
/// <para><b>保存必须原子</b>：先写临时文件再替换，避免掉电/崩溃留下半个 JSON。
/// 读取到旧版本时通过 <see cref="ElementParameterSet.Version"/> 做乐观并发。</para>
/// <para><b>生命周期</b>：通常为单例（文件/数据库连接/缓存是进程级资源）；
/// 与按 PLC 连接创建的参数控制器不同。</para>
/// </remarks>
public interface IElementParameterStore
{
    /// <summary>读取一个元件的参数配置；不存在返回失败。</summary>
    Task<Result<ElementParameterSet>> LoadAsync(
        string elementId,
        CancellationToken cancellationToken = default);

    /// <summary>读取全部元件的参数配置。</summary>
    Task<Result<IReadOnlyList<ElementParameterSet>>> LoadAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存一个元件的参数配置。
    /// </summary>
    /// <remarks>
    /// 若 <see cref="ElementParameterSet.Version"/> 非空且与当前存储版本不一致，应返回冲突失败，
    /// 不得静默覆盖。
    /// </remarks>
    Task<Result> SaveAsync(
        ElementParameterSet values,
        CancellationToken cancellationToken = default);
}
