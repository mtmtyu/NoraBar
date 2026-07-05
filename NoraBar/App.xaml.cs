using System.Threading;
using System.Windows;

namespace NoraBar
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string AppMutexName = "NoraBar.AppMutex";

        private Mutex? _appMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            _appMutex = new Mutex(false, AppMutexName);
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _appMutex?.Dispose();
            base.OnExit(e);
        }
    }
}
