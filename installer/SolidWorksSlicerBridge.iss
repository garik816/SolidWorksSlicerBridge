#define MyAppName "SolidWorks Slicer Bridge"
#define MyAppPublisher "garik816"
#define MyAppURL "https://github.com/garik816/SolidWorksSlicerBridge"
#define MyAppId "{{D51D3347-A8E7-4892-A8BD-391203C2E8A4}"

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf64}\SolidWorksSlicerBridge
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\release
OutputBaseFilename=SolidWorksSlicerBridge-{#MyAppVersion}-Setup-x64
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
UninstallDisplayName={#MyAppName}
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
MinVersion=10.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "..\src\SwAddin.cs"; DestDir: "{app}\src"; Flags: ignoreversion
Source: "..\src\SettingsForm.cs"; DestDir: "{app}\src"; Flags: ignoreversion
Source: "..\Icons\*.png"; DestDir: "{app}\Icons"; Flags: ignoreversion
Source: "..\Build.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Install.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Uninstall.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README_RU.md"; DestDir: "{app}"; Flags: ignoreversion

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Uninstall.ps1"" -Quiet -SkipElevation"; WorkingDir: "{app}"; Flags: runhidden waituntilterminated; RunOnceId: "UnregisterSolidWorksSlicerBridge"

[Code]
function IsSolidWorks2026Present(): Boolean;
var
  PF: String;
begin
  PF := ExpandConstant('{autopf64}');
  Result :=
    FileExists(PF + '\SOLIDWORKS Corp\SOLIDWORKS\api\redist\SolidWorks.Interop.sldworks.dll') or
    FileExists(PF + '\Dassault Systemes\SOLIDWORKS 2026\api\redist\SolidWorks.Interop.sldworks.dll') or
    FileExists(PF + '\SOLIDWORKS Corp\SOLIDWORKS 2026\api\redist\SolidWorks.Interop.sldworks.dll');
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsSolidWorks2026Present() then
  begin
    MsgBox(
      'SOLIDWORKS 2026 API files were not found in the standard locations.' + #13#10 + #13#10 +
      'Setup can continue because the build script also performs a wider search, but installation will fail if SOLIDWORKS 2026 is not installed.',
      mbInformation, MB_OK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  Ok: Boolean;
begin
  if CurStep = ssPostInstall then
  begin
    WizardForm.StatusLabel.Caption := 'Building and registering SolidWorks Slicer Bridge...';
    Ok := Exec(
      ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      '-NoProfile -ExecutionPolicy Bypass -File "' + ExpandConstant('{app}\Install.ps1') + '" -Quiet -SkipElevation',
      ExpandConstant('{app}'),
      SW_HIDE,
      ewWaitUntilTerminated,
      ResultCode);

    if (not Ok) or (ResultCode <> 0) then
    begin
      MsgBox(
        'SolidWorks Slicer Bridge could not be built or registered.' + #13#10 + #13#10 +
        'Make sure SOLIDWORKS 2026 is installed and closed, then run Setup again.' + #13#10 + #13#10 +
        'Detailed log: ' + ExpandConstant('{app}\install.log') + #13#10 +
        'Setup log: ' + ExpandConstant('{log}'),
        mbError, MB_OK);
      RaiseException('Add-In installation failed.');
    end;
  end;
end;
