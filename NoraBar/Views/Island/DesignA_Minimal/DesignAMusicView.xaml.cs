using System.Windows.Controls;
using System.Windows.Input;
using NoraBar.ViewModels;

namespace NoraBar.Views.Island.DesignA_Minimal
{
    public partial class DesignAMusicView : UserControl
    {
        public DesignAMusicView()
        {
            InitializeComponent();
        }

        private void UserControl_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DataContext is MainViewModel mainVm && mainVm.Music.HasMultipleSessions)
            {
                if (e.Delta > 0)
                {
                    mainVm.Music.SwitchToPreviousSessionCommand.Execute(null);
                }
                else if (e.Delta < 0)
                {
                    mainVm.Music.SwitchToNextSessionCommand.Execute(null);
                }
                e.Handled = true;
            }
        }
    }
}
