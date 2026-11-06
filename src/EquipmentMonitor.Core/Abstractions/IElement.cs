using EquipmentMonitor.Common;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions;

public interface IElement
{
    ElementMetadata ElementMetadata { get; }

    // 元件自身的状态（比如：Unknown, Ready, Running, Faulted）
    ElementState State { get; }

    bool IsInitialized { get; }

    Task<Result> InitializeAsync(CancellationToken cancellationToken = default);

    Task<Result> RefreshAsync(CancellationToken cancellationToken = default);

    // 复位元件
    Task<Result> ResetAsync(CancellationToken cancellationToken = default);
}
