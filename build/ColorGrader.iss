#define MyAppName "Color Grader"
#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

[Setup]
AppId={{8F6111C0-3897-4B24-A2E9-A233B6A0C5A8}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=hotrog2
DefaultDirName={localappdata}\Programs\ColorGrader
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\ColorGrader.App.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#AppPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Color Grader"; Filename: "{app}\ColorGrader.App.exe"
Name: "{autodesktop}\Color Grader"; Filename: "{app}\ColorGrader.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ColorGrader.App.exe"; Description: "Launch Color Grader"; Flags: nowait postinstall skipifsilent
