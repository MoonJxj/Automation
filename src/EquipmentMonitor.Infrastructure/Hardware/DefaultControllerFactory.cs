using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EquipmentMonitor.Infrastructure.Hardware
{
    public class DefaultControllerFactory : IControllerFactory
    {
        private readonly IServiceProvider _sp;
        public DefaultControllerFactory(IServiceProvider sp) => _sp = sp;

        T IControllerFactory.Create<T>(string brand)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(brand);
            
            return  _sp.GetKeyedService<T>(brand)?? throw new InvalidOperationException($"No service of type {typeof(T).Name} found for brand {brand}");
        }
    }
}
