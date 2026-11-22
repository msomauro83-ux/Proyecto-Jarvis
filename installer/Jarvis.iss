#ifdef VerificationBuild
  #define AppName "Jarvis - Vale Verification 0.4.3"
  #define AppMutexName "Local\Jarvis.Vale.InstallerVerification"
#else
  #define AppName "Jarvis - Vale"
  #define AppMutexName "Local\Jarvis.Vale.Desktop"
#endif
#define AppVersion "0.4.3"

[Setup]
#ifdef VerificationBuild
AppId={{7B479844-6D3D-45A8-ACCB-A68156303A4D}
#else
AppId={{0A59645C-4D26-4BFB-911C-0C8D88DD3346}
#endif
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion} Pre release
AppPublisher=Proyecto Jarvis
#ifdef VerificationBuild
DefaultDirName={localappdata}\Programs\Jarvis-Verification-0.4.3
#else
DefaultDirName={localappdata}\Programs\Jarvis
#endif
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=..\artifacts
#ifdef VerificationBuild
OutputBaseFilename=Jarvis-Vale-Verification-{#AppVersion}
#else
OutputBaseFilename=Jarvis-Vale-Setup-{#AppVersion}
#endif
SetupIconFile=..\assets\jarvis.ico
UninstallDisplayIcon={app}\Jarvis.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
AppMutex={#AppMutexName}
CloseApplications=no
RestartApplications=no
UninstallDisplayName={#AppName}
VersionInfoVersion={#AppVersion}.0

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"; Flags: unchecked

[Files]
Source: "..\dist\Jarvis.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\Jarvis.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; DestName: "LEEME.md"; Flags: ignoreversion

[InstallDelete]
; Remove known files shipped by earlier releases. Keep data/settings.json
; and unrelated files; startup registration is not changed during an upgrade.
Type: files; Name: "{app}\Microsoft.Web.WebView2.Core.dll"
Type: files; Name: "{app}\Microsoft.Web.WebView2.WinForms.dll"
Type: files; Name: "{app}\WebView2Loader.dll"
Type: files; Name: "{app}\WebView2-LICENSE.txt"
Type: files; Name: "{app}\WebView2-NOTICE.txt"
Type: files; Name: "{app}\docs\CHATGPT.md"

[Icons]
Name: "{group}\Jarvis - Vale"; Filename: "{app}\Jarvis.exe"; WorkingDir: "{app}"
Name: "{group}\Desinstalar Jarvis - Vale"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Jarvis - Vale"; Filename: "{app}\Jarvis.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Jarvis.exe"; WorkingDir: "{app}"; Description: "Abrir Jarvis - Vale (micrófono en pausa)"; Flags: nowait postinstall skipifsilent

[Code]
function HasRequiredFramework: Boolean;
var
  Release: Cardinal;
begin
  Result := (RegQueryDWordValue(HKLM32, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040));
  if not Result then
    Result := (RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) and (Release >= 528040));
end;

function InitializeSetup: Boolean;
begin
  Result := HasRequiredFramework;
  if not Result then
    MsgBox('Jarvis necesita .NET Framework 4.8. Actívalo o instálalo en Windows antes de continuar.', mbError, MB_OK);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if CheckForMutexes('{#AppMutexName}') then
    Result := 'Jarvis está abierto. Elige «Salir de Jarvis» en el icono V de la bandeja y vuelve a intentarlo.';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Command: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'JarvisVale', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\Jarvis.exe') + '" --tray') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'JarvisVale');
end;
