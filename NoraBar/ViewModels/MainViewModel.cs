using System.Collections.Generic;
using System.Net.Http.Json;
using System.Windows.Input;
using NoraBar.Models;
using NoraBar.Services;

namespace NoraBar.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        public sealed class LanguageOption
        {
            public LanguageOption(AppLanguage language, string displayName)
            {
                Language = language;
                DisplayName = displayName;
            }

            public AppLanguage Language { get; }
            public string DisplayName { get; }
        }

        public sealed class ScrollModeOption : ViewModelBase
        {
            public ScrollModeOption(TextScrollMode mode, string displayName)
            {
                Mode = mode;
                _displayName = displayName;
            }

            public TextScrollMode Mode { get; }

            private string _displayName;
            public string DisplayName
            {
                get => _displayName;
                set => SetProperty(ref _displayName, value);
            }
        }

        private DesignVariant _currentVariant;
        public DesignVariant CurrentVariant
        {
            get => _currentVariant;
            set
            {
                if (SetProperty(ref _currentVariant, value))
                {
                    SaveSettings();
                    OnPropertyChanged(nameof(IsMinimalVariant));
                    OnPropertyChanged(nameof(IsProductivityVariant));
                    OnPropertyChanged(nameof(IsLyricsVariant));
                }
            }
        }

        private IslandState _currentState = IslandState.Idle;
        public IslandState CurrentState
        {
            get => _currentState;
            set => SetProperty(ref _currentState, value);
        }

        private bool _showProgressBar;
        public bool ShowProgressBar
        {
            get => _showProgressBar;
            set
            {
                if (SetProperty(ref _showProgressBar, value))
                {
                    SaveSettings();
                }
            }
        }

        private bool _showLyrics;
        public bool ShowLyrics
        {
            get => _showLyrics;
            set
            {
                if (SetProperty(ref _showLyrics, value))
                {
                    SaveSettings();
                    Music.ShowLyrics = value;
                }
            }
        }

        private TextScrollMode _textScrollMode;
        public TextScrollMode TextScrollMode
        {
            get => _textScrollMode;
            set
            {
                if (SetProperty(ref _textScrollMode, value))
                {
                    SaveSettings();
                    Music.TextScrollMode = value;
                }
            }
        }

        private AppLanguage _selectedLanguage;
        public AppLanguage SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value))
                {
                    SaveSettings();
                    RefreshLocalizedText();
                }
            }
        }

        private bool _hasCustomPosition;
        public bool HasCustomPosition
        {
            get => _hasCustomPosition;
            set
            {
                if (SetProperty(ref _hasCustomPosition, value))
                {
                    SaveSettings();
                }
            }
        }

        private double _windowLeft;
        public double WindowLeft
        {
            get => _windowLeft;
            set
            {
                if (SetProperty(ref _windowLeft, value))
                {
                    SaveSettings();
                }
            }
        }

        private double _windowTop;
        public double WindowTop
        {
            get => _windowTop;
            set
            {
                if (SetProperty(ref _windowTop, value))
                {
                    SaveSettings();
                }
            }
        }

        private bool _isPositionEditMode;
        public bool IsPositionEditMode
        {
            get => _isPositionEditMode;
            set => SetProperty(ref _isPositionEditMode, value);
        }

        public IReadOnlyList<LanguageOption> AvailableLanguages { get; } =
        [
            new LanguageOption(AppLanguage.Japanese, LocalizationService.GetText(AppLanguage.Japanese, LocalizationKey.Japanese)),
            new LanguageOption(AppLanguage.English, LocalizationService.GetText(AppLanguage.English, LocalizationKey.English))
        ];

        public IReadOnlyList<ScrollModeOption> AvailableScrollModes { get; }

        public bool IsMinimalVariant
        {
            get => CurrentVariant == DesignVariant.MinimalFloatingPill;
            set
            {
                if (value) CurrentVariant = DesignVariant.MinimalFloatingPill;
            }
        }

        public bool IsProductivityVariant
        {
            get => CurrentVariant == DesignVariant.ProductivityCommandIsland;
            set
            {
                if (value) CurrentVariant = DesignVariant.ProductivityCommandIsland;
            }
        }

        public bool IsLyricsVariant
        {
            get => CurrentVariant == DesignVariant.LyricsFocusedSidebar;
            set
            {
                if (value) CurrentVariant = DesignVariant.LyricsFocusedSidebar;
            }
        }

        public bool IsStartupEnabled
        {
            get => StartupService.IsStartupEnabled();
            set
            {
                if (StartupService.IsStartupEnabled() != value)
                {
                    StartupService.SetStartup(value);
                    OnPropertyChanged(nameof(IsStartupEnabled));
                }
            }
        }

        private bool _checkUpdateOnStartup;
        public bool CheckUpdateOnStartup
        {
            get => _checkUpdateOnStartup;
            set
            {
                if (SetProperty(ref _checkUpdateOnStartup, value))
                {
                    SaveSettings();
                }
            }
        }

        private bool _disableExpandOnFullscreen;
        public bool DisableExpandOnFullscreen
        {
            get => _disableExpandOnFullscreen;
            set
            {
                if (SetProperty(ref _disableExpandOnFullscreen, value))
                {
                    SaveSettings();
                }
            }
        }

        private string _currentPage = "General";
        public string CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        public string CurrentVersion => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.3";

        public string SettingsWindowTitle => T(LocalizationKey.WindowTitle);
        public string AppSubtitleText => T(LocalizationKey.AppSubtitle);
        public string GeneralSettingsText => T(LocalizationKey.GeneralSettings);
        public string HudSettingsText => T(LocalizationKey.HudSettings);
        public string AboutText => T(LocalizationKey.About);
        public string HudSelectionText => T(LocalizationKey.HudSelection);
        public string HudSelectionDescriptionText => T(LocalizationKey.HudSelectionDescription);
        public string SharedDisplaySettingsText => T(LocalizationKey.SharedDisplaySettings);
        public string SelectedHudSettingsText => T(LocalizationKey.SelectedHudSettings);
        public string DesignStyleText => T(LocalizationKey.DesignStyle);
        public string DesignStyleDescriptionText => T(LocalizationKey.DesignStyleDescription);
        public string ProgressBarText => T(LocalizationKey.ProgressBar);
        public string ProgressBarDescriptionText => T(LocalizationKey.ProgressBarDescription);
        public string ShowLyricsText => T(LocalizationKey.ShowLyrics);
        public string ShowLyricsDescriptionText => T(LocalizationKey.ShowLyricsDescription);
        public string TextScrollModeText => T(LocalizationKey.TextScrollMode);
        public string TextScrollModeDescriptionText => T(LocalizationKey.TextScrollModeDescription);
        public string StartupText => T(LocalizationKey.Startup);
        public string StartupDescriptionText => T(LocalizationKey.StartupDescription);
        public string CheckUpdateOnStartupText => T(LocalizationKey.CheckUpdateOnStartup);
        public string CheckUpdateOnStartupDescriptionText => T(LocalizationKey.CheckUpdateOnStartupDescription);
        public string DisableExpandOnFullscreenText => T(LocalizationKey.DisableExpandOnFullscreen);
        public string DisableExpandOnFullscreenDescriptionText => T(LocalizationKey.DisableExpandOnFullscreenDescription);
        public string LanguageText => T(LocalizationKey.Language);
        public string LanguageDescriptionText => T(LocalizationKey.LanguageDescription);
        public string VersionText => T(LocalizationKey.Version);
        public string CheckUpdatesText => T(LocalizationKey.CheckUpdates);
        public string CheckUpdatesDescriptionText => T(LocalizationKey.CheckUpdatesDescription);
        public string CheckUpdatesButtonText => T(LocalizationKey.CheckUpdatesButton);
        public string UpdateAvailableButtonText => T(LocalizationKey.UpdateAvailableButton);
        public string GitHubRepositoryText => T(LocalizationKey.GitHubRepository);
        public string GitHubRepositoryDescriptionText => T(LocalizationKey.GitHubRepositoryDescription);
        public string OpenRepositoryText => T(LocalizationKey.OpenRepository);
        public string OpenSourceLicensesText => T(LocalizationKey.OpenSourceLicenses);
        public string OpenSourceLicensesDescriptionText => T(LocalizationKey.OpenSourceLicensesDescription);
        public string ShowLicensesText => T(LocalizationKey.ShowLicenses);
        public string LivePreviewText => T(LocalizationKey.LivePreview);
        public string CloseText => T(LocalizationKey.Close);
        public string ThirdPartyLicensesText => T(LocalizationKey.ThirdPartyLicenses);
        public string UpdateText => T(LocalizationKey.Update);
        public string DownloadText => T(LocalizationKey.Download);
        public string ThirdPartyTabTitle => T(LocalizationKey.ThirdPartyTab);

        public string WindowPositionText => T(LocalizationKey.WindowPosition);
        public string WindowPositionDescriptionText => T(LocalizationKey.WindowPositionDescription);
        public string ChangePositionText => T(LocalizationKey.ChangePosition);
        public string ResetPositionText => T(LocalizationKey.ResetPosition);
        public string FinishPositionEditText => T(LocalizationKey.FinishPositionEdit);

        public string ResetSettingsText => T(LocalizationKey.ResetSettings);
        public string ResetSettingsDescriptionText => T(LocalizationKey.ResetSettingsDescription);
        public string ResetAllSettingsText => T(LocalizationKey.ResetAllSettings);
        public string ResetConfirmTitleText => T(LocalizationKey.ResetConfirmTitle);
        public string ResetConfirmMessageText => T(LocalizationKey.ResetConfirmMessage);
        public string ResetConfirmYesText => T(LocalizationKey.ResetConfirmYes);
        public string ResetConfirmNoText => T(LocalizationKey.ResetConfirmNo);

        public string RestartVisualizerText => T(LocalizationKey.RestartVisualizer);
        public string RestartVisualizerDescriptionText => T(LocalizationKey.RestartVisualizerDescription);
        public string RestartVisualizerButtonText => T(LocalizationKey.RestartVisualizerButton);

        private bool _isLicenseDialogOpen;
        public bool IsLicenseDialogOpen
        {
            get => _isLicenseDialogOpen;
            set => SetProperty(ref _isLicenseDialogOpen, value);
        }

        private bool _isNoraBarLicenseTab = true;
        public bool IsNoraBarLicenseTab
        {
            get => _isNoraBarLicenseTab;
            set
            {
                if (SetProperty(ref _isNoraBarLicenseTab, value))
                {
                    OnPropertyChanged(nameof(CurrentLicenseText));
                }
            }
        }

        private bool _isUpdateDialogOpen;
        public bool IsUpdateDialogOpen
        {
            get => _isUpdateDialogOpen;
            set => SetProperty(ref _isUpdateDialogOpen, value);
        }

        private bool _isResetDialogOpen;
        public bool IsResetDialogOpen
        {
            get => _isResetDialogOpen;
            set => SetProperty(ref _isResetDialogOpen, value);
        }

        private bool _isCheckingUpdates;
        public bool IsCheckingUpdates
        {
            get => _isCheckingUpdates;
            set => SetProperty(ref _isCheckingUpdates, value);
        }

        private string _updateStatus = string.Empty;
        public string UpdateStatus
        {
            get => _updateStatus;
            set => SetProperty(ref _updateStatus, value);
        }

        private bool _hasUpdate;
        public bool HasUpdate
        {
            get => _hasUpdate;
            set => SetProperty(ref _hasUpdate, value);
        }

        private string _latestReleaseUrl = string.Empty;
        public string LatestReleaseUrl
        {
            get => _latestReleaseUrl;
            set => SetProperty(ref _latestReleaseUrl, value);
        }

        public ICommand NavigateCommand { get; }
        public ICommand CheckUpdateCommand { get; }
        public ICommand OpenGitHubCommand { get; }
        public ICommand OpenReleasePageCommand { get; }
        public ICommand ShowLicenseCommand { get; }
        public ICommand CloseLicenseCommand { get; }
        public ICommand SelectNoraBarLicenseCommand { get; }
        public ICommand SelectThirdPartyLicenseCommand { get; }
        public ICommand CloseUpdateDialogCommand { get; }
        public ICommand ResetPositionCommand { get; }
        public ICommand ResetAllSettingsCommand { get; }
        public ICommand ShowResetDialogCommand { get; }
        public ICommand CloseResetDialogCommand { get; }
        public ICommand RestartVisualizerCommand { get; }

        public MusicViewModel Music { get; } = new MusicViewModel();

        public ICommand SetVariantCommand { get; }
        public ICommand SetStateCommand { get; }

        public MainViewModel()
        {
            var settings = SettingsService.Load();
            _currentVariant = settings.Variant;
            _showProgressBar = settings.ShowProgressBar;
            _showLyrics = settings.ShowLyrics;
            _textScrollMode = settings.TextScrollMode;
            _selectedLanguage = settings.Language;
            _hasCustomPosition = settings.HasCustomPosition;
            _windowLeft = settings.WindowLeft;
            _windowTop = settings.WindowTop;
            _checkUpdateOnStartup = settings.CheckUpdateOnStartup;
            _disableExpandOnFullscreen = settings.DisableExpandOnFullscreen;

            AvailableScrollModes = new[]
            {
                new ScrollModeOption(Models.TextScrollMode.Disabled, T(LocalizationKey.TextScrollDisabled)),
                new ScrollModeOption(Models.TextScrollMode.Always, T(LocalizationKey.TextScrollAlways)),
                new ScrollModeOption(Models.TextScrollMode.HoverOnly, T(LocalizationKey.TextScrollHoverOnly))
            };

            Music.ShowLyrics = _showLyrics;
            Music.TextScrollMode = _textScrollMode;

            SetVariantCommand = new RelayCommand(ExecuteSetVariant);
            SetStateCommand = new RelayCommand(ExecuteSetState);

            _availableHuds = new[] { T(LocalizationKey.MusicHudName) };

            NavigateCommand = new RelayCommand(p => CurrentPage = p as string ?? "General");
            CheckUpdateCommand = new RelayCommand(async _ => await CheckForUpdatesAsync());
            OpenGitHubCommand = new RelayCommand(_ =>
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/mtmtyu/NoraBar") { UseShellExecute = true });
                }
                catch
                {
                }
            });
            OpenReleasePageCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrEmpty(LatestReleaseUrl))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LatestReleaseUrl) { UseShellExecute = true });
                    }
                    catch
                    {
                    }
                }
            });
            ShowLicenseCommand = new RelayCommand(_ => IsLicenseDialogOpen = true);
            CloseLicenseCommand = new RelayCommand(_ => IsLicenseDialogOpen = false);
            SelectNoraBarLicenseCommand = new RelayCommand(_ => IsNoraBarLicenseTab = true);
            SelectThirdPartyLicenseCommand = new RelayCommand(_ => IsNoraBarLicenseTab = false);
            CloseUpdateDialogCommand = new RelayCommand(_ => IsUpdateDialogOpen = false);
            ResetPositionCommand = new RelayCommand(_ =>
            {
                HasCustomPosition = false;
                IsPositionEditMode = false;
            });
            ShowResetDialogCommand = new RelayCommand(_ => IsResetDialogOpen = true);
            CloseResetDialogCommand = new RelayCommand(_ => IsResetDialogOpen = false);
            ResetAllSettingsCommand = new RelayCommand(_ => ResetAllSettings());
            RestartVisualizerCommand = new RelayCommand(_ => Music.RestartVisualizer());
        }

        private void ResetAllSettings()
        {
            IsResetDialogOpen = false;
            
            var defaultSettings = new UserSettings();

            // Notify UI by setting properties
            CurrentVariant = defaultSettings.Variant;
            ShowProgressBar = defaultSettings.ShowProgressBar;
            ShowLyrics = defaultSettings.ShowLyrics;
            TextScrollMode = defaultSettings.TextScrollMode;
            SelectedLanguage = defaultSettings.Language;
            CheckUpdateOnStartup = defaultSettings.CheckUpdateOnStartup;
            DisableExpandOnFullscreen = defaultSettings.DisableExpandOnFullscreen;
            
            // Explicitly set startup to true as requested
            IsStartupEnabled = true;

            // Reset positions
            HasCustomPosition = false;
            WindowLeft = 0;
            WindowTop = 0;
            IsPositionEditMode = false;
            
            // Save current settings correctly
            SaveSettings();
        }

        private void SaveSettings()
        {
            SettingsService.Save(new UserSettings
            {
                Variant = CurrentVariant,
                ShowProgressBar = ShowProgressBar,
                ShowLyrics = ShowLyrics,
                TextScrollMode = TextScrollMode,
                Language = SelectedLanguage,
                HasCustomPosition = HasCustomPosition,
                WindowLeft = WindowLeft,
                WindowTop = WindowTop,
                CheckUpdateOnStartup = CheckUpdateOnStartup,
                DisableExpandOnFullscreen = DisableExpandOnFullscreen
            });
        }

        private void ExecuteSetVariant(object? parameter)
        {
            if (parameter is DesignVariant variant)
            {
                CurrentVariant = variant;
            }
            else if (parameter is string variantStr && System.Enum.TryParse(variantStr, out DesignVariant parsedVariant))
            {
                CurrentVariant = parsedVariant;
            }
        }

        private void ExecuteSetState(object? parameter)
        {
            if (parameter is IslandState state)
            {
                CurrentState = state;
            }
            else if (parameter is string stateStr && System.Enum.TryParse(stateStr, out IslandState parsedState))
            {
                CurrentState = parsedState;
            }
        }

        private string LoadLicenseResource()
        {
            try
            {
                var uri = new System.Uri("pack://application:,,,/Assets/LICENSE");
                var info = System.Windows.Application.GetResourceStream(uri);
                if (info != null)
                {
                    using var reader = new System.IO.StreamReader(info.Stream);
                    return reader.ReadToEnd();
                }
            }
            catch (System.Exception)
            {
                // Fallback in case loading from resources fails
            }
            return "GNU AFFERO GENERAL PUBLIC LICENSE Version 3, 19 November 2007 (Please refer to the LICENSE file in the installation directory.)";
        }

        public string NoraBarLicenseText => LoadLicenseResource();

        public string ThirdPartyLicenseText =>
            T(LocalizationKey.LicenseIntroTitle) + "\n\n" +
            T(LocalizationKey.LicensePackageVersion) + "\n" +
            T(LocalizationKey.LicenseLabel) + "\n" +
            T(LocalizationKey.LicenseDetails) + "\n\n" +
            "========================================================================\n" +
            "CSCore - Microsoft Public License (MS-PL)\n" +
            "========================================================================\n\n" +
            "This license governs use of the accompanying software. If you use the software, you accept this license. If you do not accept the license, do not use the software.\n\n" +
            "1. Definitions\n" +
            "The terms \"reproduce,\" \"reproduction,\" \"derivative works,\" and \"distribution\" have the same meaning here as under U.S. copyright law.\n" +
            "A \"contribution\" is the original software, or any additions or changes to the software.\n" +
            "A \"contributor\" is any person that distributes its contribution under this license.\n" +
            "\"Licensed patents\" are a contributor's patent claims that read directly on its contribution.\n\n" +
            "2. Grant of Rights\n" +
            "(A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.\n" +
            "(B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.\n\n" +
            "3. Conditions and Limitations\n" +
            "(A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.\n" +
            "(B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, your patent license from such contributor to the software ends automatically.\n" +
            "(C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution notices that are present in the software.\n" +
            "(D) If you distribute any portion of the software in source code form, you may do so only under this license by including a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object code form, you may only do so under a license that complies with this license.\n" +
            "(E) The software is licensed \"as-is.\" You bear the risk of using it. The contributors give no express warranties, guarantees or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular purpose and non-infringement.\n\n" +
            "========================================================================\n" +
            "Material.Icons.WPF - MIT License\n" +
            "========================================================================\n\n" +
            "Copyright (c) 2021 SKProCH\n\n" +
            "Permission is hereby granted, free of charge, to any person obtaining a copy\n" +
            "of this software and associated documentation files (the \"Software\"), to deal\n" +
            "in the Software without restriction, including without limitation the rights\n" +
            "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell\n" +
            "copies of the Software, and to permit persons to whom the Software is\n" +
            "furnished to do so, subject to the following conditions:\n\n" +
            "The above copyright notice and this permission notice shall be included in all\n" +
            "copies or substantial portions of the Software.\n\n" +
            "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR\n" +
            "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,\n" +
            "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE\n" +
            "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER\n" +
            "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,\n" +
            "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE\n" +
            "SOFTWARE.\n\n" +
            "========================================================================\n" +
            "LRCLIB API\n" +
            "========================================================================\n\n" +
            "Lyrics provided by LRCLIB (https://lrclib.net/).\n" +
            "The data from LRCLIB is licensed under the MIT License.\n" +
            "Please visit their website for more information on their license and terms of service.";

        public string CurrentLicenseText => IsNoraBarLicenseTab ? NoraBarLicenseText : ThirdPartyLicenseText;

        private class GitHubRelease
        {
            [System.Text.Json.Serialization.JsonPropertyName("tag_name")]
            public string TagName { get; set; } = string.Empty;

            [System.Text.Json.Serialization.JsonPropertyName("html_url")]
            public string HtmlUrl { get; set; } = string.Empty;
        }

        private async System.Threading.Tasks.Task<GitHubRelease?> GetAvailableUpdateAsync()
        {
            using var client = new System.Net.Http.HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NoraBar-App-Update-Checker");

            var release = await client.GetFromJsonAsync<GitHubRelease>("https://api.github.com/repos/mtmtyu/NoraBar/releases/latest");
            if (release == null || string.IsNullOrEmpty(release.TagName))
            {
                return null;
            }

            var latestVersionText = release.TagName.TrimStart('v');
            var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            return System.Version.TryParse(latestVersionText, out var latestVersion) &&
                   currentVersion != null &&
                   latestVersion > currentVersion
                ? release
                : null;
        }

        private void ApplyAvailableUpdate(GitHubRelease release)
        {
            UpdateStatus = string.Format(T(LocalizationKey.UpdateAvailable), release.TagName);
            HasUpdate = true;
            LatestReleaseUrl = release.HtmlUrl;
        }

        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            IsCheckingUpdates = true;
            UpdateStatus = T(LocalizationKey.CheckingUpdates);
            HasUpdate = false;
            IsUpdateDialogOpen = true;

            try
            {
                var release = await GetAvailableUpdateAsync();
                if (release != null)
                {
                    ApplyAvailableUpdate(release);
                    return;
                }

                UpdateStatus = T(LocalizationKey.UpToDate);
            }
            catch (System.Exception ex)
            {
                UpdateStatus = string.Format(T(LocalizationKey.UpdateFailed), ex.Message);
            }
            finally
            {
                IsCheckingUpdates = false;
            }
        }

        public async System.Threading.Tasks.Task<bool> CheckForUpdatesSilentlyAsync()
        {
            try
            {
                var release = await GetAvailableUpdateAsync();
                if (release != null)
                {
                    ApplyAvailableUpdate(release);
                    IsUpdateDialogOpen = true;
                    return true;
                }
            }
            catch (System.Exception)
            {
                // Fail silently
            }
            return false;
        }

        private string T(LocalizationKey key)
        {
            return LocalizationService.GetText(SelectedLanguage, key);
        }

        private void RefreshLocalizedText()
        {
            var oldSelectedHudIndex = -1;
            if (AvailableHuds != null && SelectedHud != null)
            {
                oldSelectedHudIndex = Array.IndexOf(_availableHuds, SelectedHud);
            }

            AvailableHuds = new[] { T(LocalizationKey.MusicHudName) };

            if (oldSelectedHudIndex >= 0 && oldSelectedHudIndex < _availableHuds.Length)
            {
                SelectedHud = _availableHuds[oldSelectedHudIndex];
            }
            else if (_availableHuds.Length > 0)
            {
                SelectedHud = _availableHuds[0];
            }

            OnPropertyChanged(nameof(SettingsWindowTitle));
            OnPropertyChanged(nameof(AppSubtitleText));
            OnPropertyChanged(nameof(GeneralSettingsText));
            OnPropertyChanged(nameof(HudSelectionText));
            OnPropertyChanged(nameof(HudSelectionDescriptionText));
            OnPropertyChanged(nameof(HudSettingsText));
            OnPropertyChanged(nameof(SharedDisplaySettingsText));
            OnPropertyChanged(nameof(SelectedHudSettingsText));
            OnPropertyChanged(nameof(AboutText));
            OnPropertyChanged(nameof(DesignStyleText));
            OnPropertyChanged(nameof(DesignStyleDescriptionText));
            OnPropertyChanged(nameof(ProgressBarText));
            OnPropertyChanged(nameof(ProgressBarDescriptionText));
            OnPropertyChanged(nameof(ShowLyricsText));
            OnPropertyChanged(nameof(ShowLyricsDescriptionText));
            OnPropertyChanged(nameof(TextScrollModeText));
            OnPropertyChanged(nameof(TextScrollModeDescriptionText));
            if (AvailableScrollModes != null)
            {
                AvailableScrollModes[0].DisplayName = T(LocalizationKey.TextScrollDisabled);
                AvailableScrollModes[1].DisplayName = T(LocalizationKey.TextScrollAlways);
                AvailableScrollModes[2].DisplayName = T(LocalizationKey.TextScrollHoverOnly);
            }
            OnPropertyChanged(nameof(StartupText));
            OnPropertyChanged(nameof(StartupDescriptionText));
            OnPropertyChanged(nameof(CheckUpdateOnStartupText));
            OnPropertyChanged(nameof(CheckUpdateOnStartupDescriptionText));
            OnPropertyChanged(nameof(DisableExpandOnFullscreenText));
            OnPropertyChanged(nameof(DisableExpandOnFullscreenDescriptionText));
            OnPropertyChanged(nameof(LanguageText));
            OnPropertyChanged(nameof(LanguageDescriptionText));
            OnPropertyChanged(nameof(VersionText));
            OnPropertyChanged(nameof(CheckUpdatesText));
            OnPropertyChanged(nameof(CheckUpdatesDescriptionText));
            OnPropertyChanged(nameof(CheckUpdatesButtonText));
            OnPropertyChanged(nameof(UpdateAvailableButtonText));
            OnPropertyChanged(nameof(GitHubRepositoryText));
            OnPropertyChanged(nameof(GitHubRepositoryDescriptionText));
            OnPropertyChanged(nameof(OpenRepositoryText));
            OnPropertyChanged(nameof(OpenSourceLicensesText));
            OnPropertyChanged(nameof(OpenSourceLicensesDescriptionText));
            OnPropertyChanged(nameof(ShowLicensesText));
            OnPropertyChanged(nameof(LivePreviewText));
            OnPropertyChanged(nameof(CloseText));
            OnPropertyChanged(nameof(ThirdPartyLicensesText));
            OnPropertyChanged(nameof(UpdateText));
            OnPropertyChanged(nameof(DownloadText));
            OnPropertyChanged(nameof(ThirdPartyTabTitle));
            OnPropertyChanged(nameof(CurrentLicenseText));
            OnPropertyChanged(nameof(WindowPositionText));
            OnPropertyChanged(nameof(WindowPositionDescriptionText));
            OnPropertyChanged(nameof(ChangePositionText));
            OnPropertyChanged(nameof(ResetPositionText));
            OnPropertyChanged(nameof(FinishPositionEditText));
            OnPropertyChanged(nameof(ResetSettingsText));
            OnPropertyChanged(nameof(ResetSettingsDescriptionText));
            OnPropertyChanged(nameof(ResetAllSettingsText));
            OnPropertyChanged(nameof(ResetConfirmTitleText));
            OnPropertyChanged(nameof(ResetConfirmMessageText));
            OnPropertyChanged(nameof(ResetConfirmYesText));
            OnPropertyChanged(nameof(ResetConfirmNoText));
            OnPropertyChanged(nameof(RestartVisualizerText));
            OnPropertyChanged(nameof(RestartVisualizerDescriptionText));
            OnPropertyChanged(nameof(RestartVisualizerButtonText));
        }

        private string[] _availableHuds;
        public IReadOnlyList<string> AvailableHuds
        {
            get => _availableHuds;
            private set => SetProperty(ref _availableHuds, value as string[]);
        }
        
        private string _selectedHud;
        public string SelectedHud
        {
            get
            {
                if (!string.IsNullOrEmpty(_selectedHud)) return _selectedHud;
                if (_availableHuds != null && _availableHuds.Length > 0) return _availableHuds[0];
                return "音楽HUD";
            }
            set
            {
                if (string.IsNullOrEmpty(value)) return;
                if (SetProperty(ref _selectedHud, value))
                {
                    SaveSettings();
                }
            }
        }
    }
}
