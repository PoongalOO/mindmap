#ifndef MyAppName
  #define MyAppName "Mindmap"
#endif

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
UninstallDisplayIcon={app}\MindMapApp.exe
OutputDir=..\..\dist\windows
OutputBaseFilename=Setup_{#MyAppName}_{#MyAppVersion}_win_x64
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern

[Files]
Source: "..\..\publish\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\MindMapApp.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\MindMapApp.exe"

[Run]
Filename: "{app}\MindMapApp.exe"; Description: "Lancer {#MyAppName}"; Flags: nowait postinstall skipifsilent