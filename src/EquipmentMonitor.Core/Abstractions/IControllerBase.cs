using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquipmentMonitor.Core.Abstractions
{
    public interface IControllerBase : IDisposable
    {
        bool IsInitialize { get; }

        int ControllerCount { get; }

        bool Initialize();
    }
}
