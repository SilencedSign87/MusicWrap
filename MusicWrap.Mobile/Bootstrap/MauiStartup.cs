using MusicWrap.Data.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text;

namespace MusicWrap.Mobile.Bootstrap
{
    public static class MauiStartup
    {
        public static async Task InitializeAsync(IServiceProvider services)
        {
            MusicWrapDirectories.EnsureCreated();

            await Task.CompletedTask;
        }
    }
}
