using System.Windows.Controls;
using System.Windows.Input;
using NoraBar.ViewModels;

namespace NoraBar.Views.Island.DesignB_Productivity
{
    public partial class DesignBMusicView : UserControl
    {
        public DesignBMusicView()
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
