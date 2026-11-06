using System;

namespace EquipmentMonitor.Infrastructure
{
    public class GoogolTechIoController : IIoController
    {
        public int InputCount => throw new NotImplementedException();

        public int OutputCount => throw new NotImplementedException();

        public bool IsInitialize => throw new NotImplementedException();

        public int ControllerCount => throw new NotImplementedException();

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool GetIOCount(int ControllerNo, ref ushort _inCount, ref ushort _outCount)
        {
            throw new NotImplementedException();
        }

        public bool Initialize()
        {
            throw new NotImplementedException();
        }

        public bool ReadIn(int ControllerNo, int ioNo)
        {
            throw new NotImplementedException();
        }

        public bool ReadOut(int ControllerNo, int ioNo)
        {
            throw new NotImplementedException();
        }

        public bool SetOut(int ControllerNo, int ioNo, bool state)
        {
            throw new NotImplementedException();
        }
    }
}