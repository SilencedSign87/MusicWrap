using Microsoft.Extensions.DependencyInjection;
using MusicWrap.Core.Threading;

namespace MusicWrap.UI.Bootstrap
{
    public sealed class StartupWarmup : IStartupInitializer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Type _serviceType;

        public Type Type => _serviceType;

        public StartupWarmup(IServiceProvider serviceProvider, Type serviceType)
        {
            _serviceProvider = serviceProvider;
            _serviceType = serviceType;
        }

        public void Initialize() => _serviceProvider.GetRequiredService(_serviceType);
    }
}
