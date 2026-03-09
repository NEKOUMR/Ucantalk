#define MyAppName "Ucantalk"
#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "NEKO_UMR"
#define MyAppExeName "Ucantalk.exe"
#define MySourceDir "..\..\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"

[Setup]
AppId={{2A55B6B3-8C8C-4D0A-9DE7-08A3E67A1C77}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\..\artifacts\installer
OutputBaseFilename=Ucantalk_Setup_x64
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
ShowLanguageDialog=yes
UsePreviousLanguage=no
UsePreviousAppDir=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Dirs]
Name: "{app}\logs"; Permissions: users-modify

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Excludes: "logs\*"; Flags: recursesubdirs createallsubdirs ignoreversion
Source: "..\prereqs\windowsdesktop-runtime-8-x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
Source: "..\prereqs\vc_redist.x64.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{tmp}\windowsdesktop-runtime-8-x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing .NET Desktop Runtime 8..."; Flags: runhidden waituntilterminated skipifdoesntexist
Filename: "{tmp}\vc_redist.x64.exe"; Parameters: "/install /quiet /norestart"; StatusMsg: "Installing Microsoft Visual C++ Runtime..."; Flags: runhidden waituntilterminated skipifdoesntexist
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure BackupLegacyConfig();
var
  OldInstallConfigPath: string;
  OldUserConfigPath: string;
  UserConfigDir: string;
  UserConfigPath: string;
begin
  try
    OldInstallConfigPath := ExpandConstant('{oldapp}\config.json');
    OldUserConfigPath := ExpandConstant('{userappdata}\VRC_cantalkcn\config.json');

    UserConfigDir := ExpandConstant('{userappdata}\Ucantalk');
    UserConfigPath := UserConfigDir + '\config.json';

    if not DirExists(UserConfigDir) then
      ForceDirectories(UserConfigDir);

    if FileExists(UserConfigPath) then
      exit;

    if FileExists(OldUserConfigPath) then
    begin
      CopyFile(OldUserConfigPath, UserConfigPath, False);
      exit;
    end;

    if FileExists(OldInstallConfigPath) then
      CopyFile(OldInstallConfigPath, UserConfigPath, False);
  except
    // Ignore backup failures to avoid blocking setup.
  end;
end;

function InitializeSetup(): Boolean;
begin
  BackupLegacyConfig();
  Result := True;
end;
