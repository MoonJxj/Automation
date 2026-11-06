using EquipmentMonitor.Infrastructure;

namespace EquipmentMonitor.Infrastructure
{
    public class LeadShineIoController : IIoController
    {
        public int InputCount => _inputCount;

        public int OutputCount => _outputCount;

        public bool IsInitialize => throw new NotImplementedException();

        public int ControllerCount => throw new NotImplementedException();

        public IAxisController _leadShineAxisControl;
        private int _inputCount;
        private int _outputCount;

        public LeadShineIoController(IAxisController leadShineAxisControl)
        {
            _leadShineAxisControl = leadShineAxisControl;
            Initialize();
        }

        public bool Initialize()
        {
            if (!_leadShineAxisControl.IsInitialize)
            {
                _leadShineAxisControl.Initialize();
            }
            return _leadShineAxisControl.IsInitialize;
        }

        public bool GetIOCount(int cardNo, ref ushort inCount, ref ushort outCount)
        {
            if (_leadShineAxisControl.IsInitialize &&
                LTDMC.dmc_get_total_ionum((ushort)cardNo, ref inCount, ref outCount) == 0)
            {
                _inputCount = inCount;
                _outputCount = outCount;
                return true;
            }

            return false;
        }

        public bool SetOut(int cardNo, int ioNo, bool state)
        {
            return LTDMC.dmc_write_outbit((ushort)cardNo, (ushort)ioNo, (ushort)(state ? 1 : 0)) == 0;
        }

        public bool ReadIn(int cardNo, int ioNo)
        {
            return LTDMC.dmc_read_inbit((ushort)cardNo, (ushort)ioNo) == 0; //npn
        }

        public bool ReadOut(int cardNo, int ioNo)
        {
            return LTDMC.dmc_read_outbit((ushort)cardNo, (ushort)ioNo) == 0;
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}