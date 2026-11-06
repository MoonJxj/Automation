namespace EquipmentMonitor.Core.Abstractions
{
    public interface IIoController : IControllerBase
    {
        int InputCount { get; }

        int OutputCount { get; }

        bool GetIOCount(int ControllerNo, ref ushort _inCount, ref ushort _outCount);
          
        bool SetOut(int ControllerNo, int ioNo, bool state);

        bool ReadIn(int ControllerNo, int ioNo);

        bool ReadOut(int ControllerNo, int ioNo);
    }
}