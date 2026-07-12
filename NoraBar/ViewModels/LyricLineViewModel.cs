using System;
using NoraBar.Services;

namespace NoraBar.ViewModels
{
    public class LyricLineViewModel : ViewModelBase
    {
        private readonly LyricLine _model;
        private bool _isCurrent;

        public LyricLineViewModel(LyricLine model)
        {
            _model = model;
        }

        public TimeSpan StartTime => _model.StartTime;
        public string Text => _model.Text;

        public bool IsCurrent
        {
            get => _isCurrent;
            set => SetProperty(ref _isCurrent, value);
        }
    }
}
