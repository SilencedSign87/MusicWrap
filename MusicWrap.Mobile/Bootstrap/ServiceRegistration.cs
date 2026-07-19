using MusicWrap.Core.Services.Contracts;
using MusicWrap.Core.Threading;
using MusicWrap.Mobile.Domain;
using MusicWrap.Mobile.Features.Home.views;
using MusicWrap.Mobile.Features.Library.views;
using MusicWrap.Mobile.Features.Playlists.views;
using MusicWrap.Mobile.Features.Plugins.views;
using MusicWrap.Mobile.Features.Settings.views;
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

            // Pages
            services.AddTransient<MainHostTab>();

            services.AddTransient<HomePage>();
            services.AddTransient<LibraryPage>();
            services.AddTransient<PlaylistsPage>();
            services.AddTransient<PluginsPage>();
            services.AddTransient<SettingsPage>();

            return services;
        }
    }
}
