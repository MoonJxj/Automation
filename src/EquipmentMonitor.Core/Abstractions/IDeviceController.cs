using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquipmentMonitor.Core.Abstractions
{
    public interface IDeviceController : IControllerBase
    {
        void GetDeviceCount(int ControllerNo, ref uint deviceCount);

        bool GetDeviceName(int ControllerNo, out string name);

        bool GetDeviceType(int ControllerNo, out string type);

        bool SetControlWord(int ControllerNo, string type,ref int value);

        bool ReadStateWord(int ControllerNo, string type, out int state);
    }
}