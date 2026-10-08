#ifndef AppVersion
  #error AppVersion is required
#endif
#ifndef SourceExe
  #error SourceExe is required
#endif
#ifndef OutputDirectory
  #error OutputDirectory is required
#endif

[Setup]
AppId=AutoDark.Desktop
AppName=AutoDark
AppVersion={#AppVersion}
AppPublisher=Danilo Stoletović
DefaultDirName={localappdata}\Programs\AutoDark
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
OutputDir={#OutputDirectory}
OutputBaseFilename=AutoDark-Setup-win-x64
SetupIconFile=..\Assets\AutoDark.ico
UninstallDisplayIcon={app}\AutoDark.exe
UninstallDisplayName=AutoDark
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\AutoDark"; Filename: "{app}\AutoDark.exe"

[Run]
Filename: "{app}\AutoDark.exe"; Description: "Open AutoDark"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  if not UninstallSilent then
    if MsgBox('Close AutoDark before continuing. Uninstall will turn automatic switching OFF, remove its scheduled task, and restore your previous theme. Continue?', mbConfirmation, MB_YESNO) <> IDYES then
      Exit;
  if not Exec(ExpandConstant('{app}\AutoDark.exe'), '--disable', ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    MsgBox('AutoDark cleanup could not start. Repair the installation and try again. ' + SysErrorMessage(ResultCode), mbError, MB_OK);
    Exit;
  end;
  if ResultCode <> 0 then
  begin
    MsgBox('AutoDark could not disable automatic switching. Open AutoDark, turn it OFF, resolve the displayed error, and retry uninstalling. Your installation has been kept.', mbError, MB_OK);
    Exit;
  end;
  Result := True;
end;
