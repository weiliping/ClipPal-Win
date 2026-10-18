; ClipPal 安装包脚本（Inno Setup 6+）
; 编译：iscc installer\ClipPal.iss  或在 Inno Setup 里打开本文件按 F9
; 产物：installer\Output\ClipPal-Setup.exe

[Setup]
AppId={{8B7E4C31-2F5A-4C8E-9D3A-CLIPPAL0001}
AppName=ClipPal
AppVersion=1.0.0
AppPublisher=ClipPal
AppPublisherURL=https://github.com/weiliping/ClipPal-Win
DefaultDirName={localappdata}\Programs\ClipPal
DefaultGroupName=ClipPal
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=ClipPal-Setup
SetupIconFile=..\Assets\clippal.ico
UninstallDisplayIcon={app}\ClipPal.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; 装到当前用户目录：不弹 UAC，非程序员也不会被权限卡住
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

[Languages]
Name: "zh"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "开机自动启动 / Launch at startup"; GroupDescription: "启动"; Flags: unchecked

[Files]
; publish\ 目录由 dotnet publish 生成（见 build-installer.ps1）
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\ClipPal"; Filename: "{app}\ClipPal.exe"
Name: "{group}\卸载 ClipPal"; Filename: "{uninstallexe}"
Name: "{autodesktop}\ClipPal"; Filename: "{app}\ClipPal.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
    ValueName: "ClipPal"; ValueData: """{app}\ClipPal.exe"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\ClipPal.exe"; Description: "{cm:LaunchProgram,ClipPal}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM ClipPal.exe /F"; Flags: runhidden skipifdoesntexist
