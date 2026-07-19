using System;
using System.Collections.Generic;
using OpenWrtProgrammerPro.Services.Interfaces;
using OpenWrtProgrammerPro.Services;

namespace OpenWrtProgrammerPro.Helpers
{
    public class ServiceLocator
    {
        private static readonly ServiceLocator _instance = new();
        public static ServiceLocator Instance => _instance;

        private readonly Dictionary<Type, Func<object>> _services = new();
        private readonly Dictionary<Type, object> _singletons = new();

        private ServiceLocator() 
        {
            RegisterSingleton<ILoggerService, LoggerService>();
            RegisterSingleton<IUbusClient, UbusClient>();
            RegisterSingleton<IUciService, UciService>();
            RegisterSingleton<IDeviceDiscoveryService, DeviceDiscoveryService>();
            RegisterSingleton<INetworkService, NetworkService>();
            RegisterSingleton<IWirelessService, WirelessService>();
            RegisterSingleton<IBackupService, BackupService>();
            RegisterSingleton<ITemplateService, TemplateService>();
            RegisterSingleton<ISavedNetworkService, SavedNetworkService>();
            RegisterSingleton<IProgrammingService, ProgrammingService>();
            RegisterSingleton<ILicenseValidator, OfflineLicenseValidator>();
        }

        public void Register<TInterface, TImplementation>() where TImplementation : TInterface, new()
        {
            var type = typeof(TInterface);
            _services[type] = () => new TImplementation();
        }

        public void RegisterSingleton<TInterface>(TInterface instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            _singletons[typeof(TInterface)] = instance;
        }

        public void RegisterSingleton<TInterface, TImplementation>() where TImplementation : TInterface, new()
        {
            var type = typeof(TInterface);
            _services[type] = () =>
            {
                if (!_singletons.TryGetValue(type, out var instance))
                {
                    instance = new TImplementation();
                    _singletons[type] = instance;
                }
                return instance;
            };
        }

        public T Resolve<T>()
        {
            var type = typeof(T);
            if (_singletons.TryGetValue(type, out var singleton))
            {
                return (T)singleton;
            }

            if (_services.TryGetValue(type, out var factory))
            {
                var resolved = factory();
                return (T)resolved;
            }

            throw new InvalidOperationException($"Service of type {type.Name} is not registered.");
        }
    }
}
