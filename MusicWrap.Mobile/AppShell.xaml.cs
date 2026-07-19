namespace MusicWrap.Mobile
{
    public partial class AppShell : Shell
    {
        public AppShell(IServiceProvider serviceProvider)
        {
            InitializeComponent();

            var mainpage = serviceProvider.GetRequiredService<MainPage>();
            mainShell.Content = mainpage;
        }
    }
}
