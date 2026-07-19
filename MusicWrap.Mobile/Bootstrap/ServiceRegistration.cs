using MusicWrap.Core.Services.Contracts;
using MusicWrap.Core.Threading;
using MusicWrap.Mobile.Domain;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Mobile.Bootstrap
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddMauiServices(this IServiceCollection services)
        {
            services.AddSingleton<IUIDispatcher, MauiUIDispatcher>();
            services.AddTransient<IImageService, MauiImageService>();

            services.AddTransient<AppShell>();

            // Pages
            services.AddTransient<MainPage>();

            return services;
        }
    }
}
