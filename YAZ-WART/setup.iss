[Setup]
AppName=لوكس كارد ( مودمات )
AppVerName=لوكس كارد ( مودمات ) v1.0
AppVersion=1.0
AppPublisher=م/عزيز المساح
DefaultDirName={autopf}\LuxCardModem
DefaultGroupName=لوكس كارد ( مودمات )
OutputDir=d:\tools\alamlaqAhmed3_770936309\Installer
OutputBaseFilename=LuxCardModemSetup
Compression=lzma2
SolidCompression=yes
SetupIconFile=d:\tools\alamlaqAhmed3_770936309\OpenWrtProgrammerPro\Resources\app_icon.ico
PrivilegesRequired=admin
WizardStyle=modern

[Languages]
Name: "arabic"; MessagesFile: "compiler:Languages\Arabic.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "d:\tools\alamlaqAhmed3_770936309\OpenWrtProgrammerPro\bin\Release\publish\OpenWrtProgrammerPro.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\لوكس كارد ( مودمات )"; Filename: "{app}\OpenWrtProgrammerPro.exe"
Name: "{autodesktop}\لوكس كارد ( مودمات )"; Filename: "{app}\OpenWrtProgrammerPro.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\OpenWrtProgrammerPro.exe"; Description: "{cm:LaunchProgram,لوكس كارد ( مودمات )}"; Flags: nowait postinstall skipifsilent
