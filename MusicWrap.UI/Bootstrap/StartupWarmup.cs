using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Threading;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.UI.Bootstrap
{
    public sealed class StartupWarmup : IStartupInitializer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Type _serviceType;

        public StartupWarmup(IServiceProvider serviceProvider, Type serviceType)
        {
            _serviceProvider = serviceProvider;
            _serviceType = serviceType;
        }

        public void Initialize() => _serviceProvider.GetRequiredService(_serviceType);
    }
}
