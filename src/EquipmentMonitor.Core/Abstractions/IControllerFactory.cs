

namespace EquipmentMonitor.Core.Abstractions
{
    public interface IControllerFactory
    {
        T Create<T>(string brand) where T : class, IControllerBase;
    }
}