[Setup]
AppId={{9F588B98-96BC-4A2D-A58F-3A9B266474CD}
AppName=NoraBar
AppVersion=1.0.2
AppPublisher=mtmtyu
DefaultDirName={autopf}\NoraBar
DefaultGroupName=NoraBar
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=NoraBar-Setup
LicenseFile=LICENSE
SetupIconFile=NoraBar\Assets\AppIcon.ico
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\NoraBar.exe
AppMutex=NoraBar.AppMutex
CloseApplications=force
CloseApplicationsFilter=NoraBar.exe

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "out\portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\NoraBar"; Filename: "{app}\NoraBar.exe"
Name: "{autodesktop}\NoraBar"; Filename: "{app}\NoraBar.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\NoraBar.exe"; Description: "{cm:LaunchProgram,NoraBar}"; Flags: nowait postinstall skipifsilent
