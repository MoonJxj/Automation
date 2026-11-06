using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Core.Abstractions
{
    /// <summary>
    /// 同步轴控制器契约：厂商 DLL / PLC 适配层实现。
    /// </summary>
    /// <remarks>
    /// 命令返回 <c>true</c> 表示“已下发”，不表示“已到位”；失败原因写入
    /// <see cref="GetLastError"/>。无法用 <c>bool</c> 表达的错误（离线/超时/从站异常）
    /// 可抛 <see cref="AxisControllerException"/>，由 <c>AxisElement</c> 统一转换。
    /// </remarks>
    public interface IAxisController : IControllerBase
    {
        int AxisCount { get; }

        void GetAxisCount(int ControllerNo, ref uint axisCount);

        bool ServeOn(int ControllerNo, int axisNo);

        bool ServeOff(int ControllerNo, int axisNo);

        bool Home(int ControllerNo, int axisNo);

        bool MoveAbs(int ControllerNo, int axisNo, double position);

        bool MoveRel(int ControllerNo, int axisNo, double distance);

        bool MoveInPos(int ControllerNo, int axisNo);

        bool Jog(int ControllerNo, int axisNo, bool direction);

        bool Stop(int ControllerNo, int axisNo);

        bool ResetAxis(int ControllerNo, int axisNo);

        bool ResetAll();

        double GetPos(int ControllerNo, int axisNo);

        double GetVel(int ControllerNo, int axisNo);

        double GetTorque(int ControllerNo, int axisNo);

        bool ReadStateWord(int ControllerNo, int axisNo, string type, out int state);

        /// <summary>
        /// 获取该轴最近一次失败的详细信息。
        /// </summary>
        /// <returns>没有失败记录时返回 <c>null</c>。</returns>
        /// <remarks>
        /// 厂商标识的 DLL 通常只返回 <c>bool</c>，实现类应在失败时记录本对象。
        /// 默认返回 <c>null</c>，让尚未实现的厂商适配器保持兼容。
        /// </remarks>
        AxisOperationError? GetLastError(int ControllerNo, int axisNo) => null;
    }
}