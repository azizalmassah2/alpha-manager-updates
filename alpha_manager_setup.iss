[Setup]
AppName=Alpha Manager
AppVerName=Alpha Manager v2.0.5
AppVersion=2.0.5
VersionInfoVersion=2.0.5
AppPublisher=Alpha Manager
DefaultDirName={autopf}\AlphaManager
DefaultGroupName=Alpha Manager
OutputDir=D:\LUXCARD\desktop\Installer
OutputBaseFilename=AlphaManagerSetup_v2.0.5
Compression=lzma2
SolidCompression=yes
SetupIconFile=D:\LUXCARD\desktop\Lux.Management.Console\Resources\img\icon.ico
WizardSmallImageFile=D:\LUXCARD\desktop\Lux.Management.Console\Resources\img\logo2.png
PrivilegesRequired=admin
WizardStyle=modern

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; تضمين جميع ملفات النشر للبرنامج الرئيسي والاعتماديات
Source: "D:\LUXCARD\desktop\Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Alpha Manager"; Filename: "{app}\Lux.Management.Console.exe"
Name: "{autodesktop}\Alpha Manager"; Filename: "{app}\Lux.Management.Console.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Lux.Management.Console.exe"; Description: "{cm:LaunchProgram,Alpha Manager}"; Flags: nowait postinstall skipifsilent
