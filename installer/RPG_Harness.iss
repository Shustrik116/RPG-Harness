#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#define MyAppName "RPG Harness"
#define MyAppExeName "RPG_Harness.exe"
#define PublishDir "..\artifacts\win-x64"

[Setup]
AppId={{D1B79771-A024-4A01-99A3-10A689FE25A2}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=RPG Harness contributors
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=RPG-Harness-Setup-win-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\assets\RPG_Harness.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
