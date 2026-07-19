using System;
using Lux.Management.Console.Modules.Broadcasting.QuickConfig.Services.Interfaces;

namespace Lux.Management.Console.Modules.Broadcasting.QuickConfig.Helpers
{
    /// <summary>
    /// موصل الخدمات (ServiceLocator) لتسهيل جلب الخدمات من حاوية DI الخاصة بالتطبيق دون تغيير كود YAZ-WART الأصلي.
    /// </summary>
    public class ServiceLocator
    {
        private static readonly ServiceLocator _instance = new();
        public static ServiceLocator Instance => _instance;

        private IServiceProvider? _serviceProvider;

        private ServiceLocator() { }

        public void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public T Resolve<T>()
        {
            if (_serviceProvider == null)
            {
                throw new InvalidOperationException("ServiceLocator is not initialized with a service provider.");
            }

            var service = _serviceProvider.GetService(typeof(T));
            if (service == null)
            {
                // محاولة البحث عن خدمة تناسب الاسم الكامل أو النوع
                throw new InvalidOperationException($"Service of type {typeof(T).Name} is not registered.");
            }

            return (T)service;
        }
    }
}
