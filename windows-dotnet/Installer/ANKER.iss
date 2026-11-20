#ifndef PayloadDir
  #error Supply /DPayloadDir with the self-contained program directory
#endif
#ifndef PrerequisiteDir
  #error Supply /DPrerequisiteDir with the Microsoft offline runtime installer directory
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif

[Setup]
AppId={{96F34F43-297D-4948-B2C2-AAD889371017}
AppName=ANKER
AppVersion=0.5.1-win.31
AppVerName=ANKER 0.5.1-win.31
AppPublisher=ANKER Windows community port
VersionInfoVersion=0.5.1.31
DefaultDirName={localappdata}\Programs\ANKER
DefaultGroupName=ANKER
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.19041
AppMutex=Local\Charker.Windows.0.5.1
CloseApplications=no
RestartApplications=no
SetupIconFile=..\UI\assets\AppIcon.ico
UninstallDisplayIcon={app}\ANKER.exe
OutputDir={#OutputDir}
OutputBaseFilename=ANKER-0.5.1-win.31-Setup-x64
Compression=lzma2/normal
SolidCompression=yes
DiskSpanning=no
WizardStyle=modern
SetupLogging=yes
UsePreviousTasks=yes
DisableWelcomePage=no

[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
chinesesimp.DesktopShortcut=创建桌面快捷方式
english.DesktopShortcut=Create a desktop shortcut
chinesesimp.LaunchApp=打开 ANKER
english.LaunchApp=Launch ANKER
chinesesimp.RuntimeInstalling=正在准备 WebView2 离线运行库，请稍候...
english.RuntimeInstalling=Preparing the offline WebView2 runtime. Please wait...
chinesesimp.RuntimeFailed=WebView2 运行库安装未完成（错误码 %1）。请重新运行安装程序；如电脑有安装限制，请联系管理员。
english.RuntimeFailed=WebView2 runtime installation did not finish (code %1). Run Setup again or contact your administrator if installation is restricted.

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopShortcut}"

[Files]
Source: "{#PrerequisiteDir}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe"; Flags: dontcopy
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ANKER"; Filename: "{app}\ANKER.exe"; WorkingDir: "{app}"; IconFilename: "{app}\ANKER.exe"
Name: "{autodesktop}\ANKER"; Filename: "{app}\ANKER.exe"; WorkingDir: "{app}"; IconFilename: "{app}\ANKER.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ANKER.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent unchecked

[Code]
function ValidRuntimeVersion(Root: Integer; Key: String): Boolean;
var
  V: String;
begin
  Result := RegQueryStringValue(Root, Key, 'pv', V) and (V <> '') and (V <> '0.0.0.0');
end;

function HasWebView2: Boolean;
begin
  Result := ValidRuntimeVersion(HKLM32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') or
    ValidRuntimeVersion(HKCU32, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}') or
    ValidRuntimeVersion(HKCU64, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
  Started: Boolean;
begin
  Result := '';
  if HasWebView2 then begin
    Log('WebView2 runtime already present; offline prerequisite skipped.');
    exit;
  end;
  WizardForm.PreparingLabel.Caption := CustomMessage('RuntimeInstalling');
  ExtractTemporaryFile('MicrosoftEdgeWebView2RuntimeInstallerX64.exe');
  Code := -1;
  Started := Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebView2RuntimeInstallerX64.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Log(Format('Offline WebView2 install: started=%d; exit=%d', [Ord(Started), Code]));
  if not HasWebView2 then
    Result := FmtMessage(CustomMessage('RuntimeFailed'), [IntToStr(Code)]);
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Existing, Installed: String;
begin
  if CurStep = ssPostInstall then begin
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ANKER.Windows', Existing) then begin
      if (Pos('\anker.exe" --background', Lowercase(Existing)) > 0) then begin
        Installed := '"' + ExpandConstant('{app}\ANKER.exe') + '" --background';
        RegWriteStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ANKER.Windows', Installed);
      end;
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Existing, Installed: String;
begin
  if CurUninstallStep = usUninstall then begin
    Installed := '"' + ExpandConstant('{app}\ANKER.exe') + '" --background';
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ANKER.Windows', Existing) then
      if CompareText(Existing, Installed) = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ANKER.Windows');
  end;
end;
