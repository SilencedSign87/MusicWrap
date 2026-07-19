using Microsoft.Extensions.DependencyInjection;

namespace MusicWrap.Mobile
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var shell = MauiProgram.Services.GetRequiredService<MainHostTab>();

            return new Window(shell);
        }
    }
}