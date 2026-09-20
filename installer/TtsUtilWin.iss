; TTS Util Win installer. Build it with: scripts\BuildPortable.ps1 -Installer
; Direct use: iscc installer\TtsUtilWin.iss

#ifndef AppVersion

  #define AppVersion "0.1.0-alpha"
  #define FileVersion "0.1.0.1"

  #define SourceDir "..\dist\TtsUtilWin"

#endif

; Which processor this build is for. BuildPortable.ps1 passes it; x64 is the long standing default.
#ifndef TargetArch
  #define TargetArch "x64"
#endif

#if TargetArch == "arm64"
  #define ArchAllowed "arm64"
  #define Arch64Mode "arm64"
#elif TargetArch == "x86"
  #define ArchAllowed "x86compatible"
  #define Arch64Mode ""
#else
  #define ArchAllowed "x64compatible"
  #define Arch64Mode "x64compatible"
#endif

#define AppIdGuid "{8F3C21D6-5A74-4E9B-B0C8-2D1E7A6F4B39}"

#define AppName "TTS Util Win"
#define AppExeName "TtsUtilWin.exe"

#define AppPublisher "TTS Util Win contributors"
#define AppUrl "https://github.com/jsphgei-dot/tts-util-win"

#define VoiceBaseUrl "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/"

[Setup]
AppId={{#AppIdGuid}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}/releases
VersionInfoVersion={#FileVersion}

; Show the install mode page and preselect the per user option.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes

; Reinstalling reuses the previous folder, group and choices without asking again.
DisableDirPage=auto
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
UsePreviousSetupType=yes

; Restart Manager closes a running copy instead of failing on a locked file.
CloseApplications=yes
CloseApplicationsFilter=*.exe,*.dll
RestartApplications=no

LicenseFile=..\LICENSE
OutputDir=..\dist
OutputBaseFilename=TtsUtilWin-{#AppVersion}-{#TargetArch}-setup
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName} {#AppVersion} ({#TargetArch})

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed={#ArchAllowed}
ArchitecturesInstallIn64BitMode={#Arch64Mode}
MinVersion=10.0.14393

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Messages]
SelectComponentsLabel2=The three Microsoft voices that come with Windows are already on this computer and work with {#AppName} straight away, so no voice model has to be downloaded at all. Tick any neural voices you would also like, or none:

[Types]
Name: "recommended"; Description: "Program and the three recommended neural voices"
Name: "minimal";     Description: "Program only, nothing downloaded (the Windows voices still work)"
Name: "custom";      Description: "Choose voices"; Flags: iscustom

[Components]
Name: "app"; Description: "{#AppName}"; Types: recommended minimal custom; Flags: fixed

; Ticked and greyed out: Windows ships these and setup neither downloads nor installs them.
Name: "windows";       Description: "Windows voices, already installed, nothing to download"; Types: recommended minimal custom; Flags: fixed
Name: "windows\david"; Description: "Microsoft David, English (US), on this computer already"; Types: recommended minimal custom; Flags: fixed
Name: "windows\zira";  Description: "Microsoft Zira, English (US), on this computer already"; Types: recommended minimal custom; Flags: fixed
Name: "windows\mark";  Description: "Microsoft Mark, English (US), on this computer already"; Types: recommended minimal custom; Flags: fixed

Name: "voices";          Description: "Neural voice models, downloaded during installation, none of them needed"; Types: recommended custom
Name: "voices\ljspeech"; Description: "English (US), LJ Speech, high quality (110 MB, public domain)"; Types: recommended custom; ExtraDiskSpaceRequired: 115343360
Name: "voices\libritts"; Description: "English (US), LibriTTS-R, 900+ speakers (78 MB, CC BY 4.0)"; Types: recommended custom; ExtraDiskSpaceRequired: 81788928
Name: "voices\kokoro";   Description: "English, Kokoro, 11 voices (305 MB, Apache 2.0)"; Types: recommended custom; ExtraDiskSpaceRequired: 319815680
Name: "voices\alan";     Description: "English (GB), Alan (64 MB)"; ExtraDiskSpaceRequired: 67108864
Name: "voices\amy";      Description: "English (US), Amy, small and fast (64 MB)"; ExtraDiskSpaceRequired: 67108864
Name: "voices\lessac";   Description: "English (US), Lessac (64 MB)"; ExtraDiskSpaceRequired: 67108864

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\{#AppExeName}"; DestDir: "{app}"; Components: app; Flags: ignoreversion
Source: "..\LICENSE";                 DestDir: "{app}"; Components: app; Flags: ignoreversion
Source: "..\NOTICE";                  DestDir: "{app}"; Components: app; Flags: ignoreversion
Source: "..\distribution\README.md"; DestDir: "{app}"; Components: app; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Components: app; Flags: ignoreversion
Source: "..\scripts\FetchVoices.ps1"; DestDir: "{app}\scripts"; Components: app; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Voices downloaded here live under {app} and go with it. Settings stay.
Type: filesandordirs; Name: "{app}\voices"
Type: dirifempty; Name: "{app}\scripts"
Type: dirifempty; Name: "{app}"

[Code]
const
  UninstallKey = 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{#AppIdGuid}_is1';
  ArchValue = 'InstalledArch';

var
  DownloadPage: TDownloadWizardPage;
  VoiceComponents: TArrayOfString;
  VoiceIds: TArrayOfString;
  PreviousVersion: String;
  InstallAction: String;
  OtherVersion: String;
  OtherArch: String;
  OtherUninstaller: String;
  OtherRoot: Integer;

function ArchAt(Root: Integer): String;
var
  DisplayName: String;
begin
  if RegQueryStringValue(Root, UninstallKey, ArchValue, Result) and (Result <> '') then
    Exit;

  { A 0.12.0-beta install recorded no architecture, and its display name ends with one. }
  Result := 'x64';
  if RegQueryStringValue(Root, UninstallKey, 'DisplayName', DisplayName) then
  begin
    if Pos('(arm64)', DisplayName) > 0 then
      Result := 'arm64'
    else if Pos('(x86)', DisplayName) > 0 then
      Result := 'x86';
  end;
end;

{ An install of this architecture lands in PreviousVersion and one of another in Other*.
  HKLM is split by bitness and HKCU is not, so HKCU is read once. }
procedure ScanForInstalls;
var
  Roots: array[0..2] of Integer;
  Count, I: Integer;
  Version, Arch: String;
begin
  PreviousVersion := '';
  OtherVersion := '';
  OtherArch := '';
  OtherUninstaller := '';

  Count := 0;
  if IsX64OS or IsArm64 then
  begin
    Roots[Count] := HKLM64;
    Count := Count + 1;
  end;
  Roots[Count] := HKLM32;
  Count := Count + 1;
  Roots[Count] := HKCU;
  Count := Count + 1;

  for I := 0 to Count - 1 do
  begin
    if RegQueryStringValue(Roots[I], UninstallKey, 'DisplayVersion', Version) then
    begin
      Arch := ArchAt(Roots[I]);

      if Arch = '{#TargetArch}' then
      begin
        if PreviousVersion = '' then
          PreviousVersion := Version;
      end
      else if OtherVersion = '' then
      begin
        OtherVersion := Version;
        OtherArch := Arch;
        OtherRoot := Roots[I];
        RegQueryStringValue(Roots[I], UninstallKey, 'UninstallString', OtherUninstaller);
      end;
    end;
  end;
end;

{ The uninstaller hands the work to a copy of itself and returns early, so the registry
  entry going away is what says the removal finished. }
function RemoveOtherArchitecture: Boolean;
var
  Code, Waited: Integer;
  Version: String;
begin
  Result := False;
  if OtherUninstaller = '' then
    Exit;

  if not Exec(RemoveQuotes(OtherUninstaller), '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '',
              SW_SHOW, ewWaitUntilTerminated, Code) then
    Exit;

  Waited := 0;
  while (Waited < 60000) and
        RegQueryStringValue(OtherRoot, UninstallKey, 'DisplayVersion', Version) do
  begin
    Sleep(500);
    Waited := Waited + 500;
  end;

  Result := not RegQueryStringValue(OtherRoot, UninstallKey, 'DisplayVersion', Version);
end;

{ Every architecture shares one AppId, one program folder and one Start menu entry, so two
  of them installed at once would fight over all three. }
function ResolveArchitectureClash: Boolean;
begin
  Result := True;
  if OtherVersion = '' then
    Exit;

  if SuppressibleMsgBox(
       '{#AppName} ' + OtherVersion + ' is installed as the ' + OtherArch + ' build, and this ' +
       'is the {#TargetArch} build.' + #13#10 + #13#10 +
       'The two share a program folder and a Start menu entry, so they cannot sit side by ' +
       'side. Setup can remove the ' + OtherArch + ' copy first. Your settings are kept, and ' +
       'any voice it installed can be ticked again further on.' + #13#10 + #13#10 +
       'Remove it and carry on? Choosing No ends setup.',
       mbConfirmation, MB_YESNO, IDNO) = IDNO then
  begin
    Result := False;
    Exit;
  end;

  if RemoveOtherArchitecture then
    Exit;

  SuppressibleMsgBox(
    'The ' + OtherArch + ' copy could not be removed. Remove {#AppName} from Installed apps, ' +
    'then run this setup again.',
    mbError, MB_OK, IDOK);
  Result := False;
end;

#include "Version.iss"

function InitializeSetup: Boolean;
var
  Comparison: Integer;
begin
  InstallAction := 'install';
  ScanForInstalls;

  Result := ResolveArchitectureClash;
  if not Result then
    Exit;

  if PreviousVersion = '' then
    Exit;

  Comparison := CompareVersions(PreviousVersion, '{#AppVersion}');

  if Comparison < 0 then
    InstallAction := 'upgrade'
  else if Comparison = 0 then
  begin
    InstallAction := 'repair';

    if SuppressibleMsgBox(
         '{#AppName} ' + PreviousVersion + ' is already installed.' + #13#10 + #13#10 +
         'Setup will repair it: program files are replaced, any voice you tick and do not ' +
         'already have is downloaded, and your settings are kept.' + #13#10 + #13#10 +
         'Continue?',
         mbConfirmation, MB_YESNO, IDYES) = IDNO then
      Result := False;
  end
  else
  begin
    InstallAction := 'downgrade';

    if SuppressibleMsgBox(
         '{#AppName} ' + PreviousVersion + ' is installed, which is newer than this ' +
         'setup (' + '{#AppVersion}' + ').' + #13#10 + #13#10 +
         'Installing anyway replaces it with the older version. Continue?',
         mbConfirmation, MB_YESNO, IDNO) = IDNO then
      Result := False;
  end;
end;

function UpdateReadyMemo(const Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  Headline: String;
begin
  if InstallAction = 'upgrade' then
    Headline := 'Upgrading from ' + PreviousVersion + ' to {#AppVersion}. Settings and ' +
                'installed voices are kept.'
  else if InstallAction = 'repair' then
    Headline := 'Repairing {#AppVersion}. Program files are replaced; settings and ' +
                'installed voices are kept.'
  else if InstallAction = 'downgrade' then
    Headline := 'Replacing ' + PreviousVersion + ' with the older {#AppVersion}.'
  else
    Headline := 'Installing {#AppVersion}.';

  Result := Headline + NewLine + NewLine + MemoDirInfo + NewLine + NewLine +
            MemoTypeInfo + NewLine + NewLine + MemoComponentsInfo + NewLine + NewLine +
            MemoTasksInfo;
end;

procedure BuildVoiceTable;
begin
  SetArrayLength(VoiceComponents, 6);
  SetArrayLength(VoiceIds, 6);

  VoiceComponents[0] := 'voices\ljspeech';
  VoiceIds[0]        := 'vits-piper-en_US-ljspeech-high';

  VoiceComponents[1] := 'voices\libritts';
  VoiceIds[1]        := 'vits-piper-en_US-libritts_r-medium';

  VoiceComponents[2] := 'voices\kokoro';
  VoiceIds[2]        := 'kokoro-en-v0_19';

  VoiceComponents[3] := 'voices\alan';
  VoiceIds[3]        := 'vits-piper-en_GB-alan-medium';

  VoiceComponents[4] := 'voices\amy';
  VoiceIds[4]        := 'vits-piper-en_US-amy-low';

  VoiceComponents[5] := 'voices\lessac';
  VoiceIds[5]        := 'vits-piper-en_US-lessac-medium';
end;

function VoicesDirectory: String;
begin
  Result := ExpandConstant('{app}\voices');
end;

function ArchivePath(const Id: String): String;
begin
  Result := ExpandConstant('{tmp}\') + Id + '.tar.bz2';
end;

function TarPath: String;
begin
  Result := ExpandConstant('{sys}\tar.exe');

  if not FileExists(Result) then
    Result := 'tar.exe';
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if Progress = ProgressMax then
    Log('Downloaded ' + FileName);

  Result := True;
end;

procedure InitializeWizard;
begin
  BuildVoiceTable;

  DownloadPage := CreateDownloadPage(
    'Downloading voices',
    'Setup is downloading the voice models you selected.',
    @OnDownloadProgress);
end;

function QueueSelectedVoices: Integer;
var
  I: Integer;
begin
  Result := 0;
  DownloadPage.Clear;

  for I := 0 to GetArrayLength(VoiceIds) - 1 do
  begin
    if not WizardIsComponentSelected(VoiceComponents[I]) then
      Continue;

    // A voice already on disk is left alone.
    if DirExists(VoicesDirectory + '\' + VoiceIds[I]) then
      Continue;

    DownloadPage.Add('{#VoiceBaseUrl}' + VoiceIds[I] + '.tar.bz2', VoiceIds[I] + '.tar.bz2', '');
    Result := Result + 1;
  end;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;

  if CurPageID <> wpReady then
    Exit;

  if QueueSelectedVoices = 0 then
    Exit;

  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      SuppressibleMsgBox(
        'A voice could not be downloaded, so setup will continue without it.' + #13#10 + #13#10 +
        AddPeriod(GetExceptionMessage) + #13#10 + #13#10 +
        'Run scripts\FetchVoices.ps1 from the install folder to try again.',
        mbError, MB_OK, IDOK);
    end;
  finally
    DownloadPage.Hide;
  end;
end;

procedure ExtractVoices;
var
  I, ResultCode: Integer;
  Archive, Directory: String;
begin
  Directory := VoicesDirectory;

  if not ForceDirectories(Directory) then
    Exit;

  for I := 0 to GetArrayLength(VoiceIds) - 1 do
  begin
    Archive := ArchivePath(VoiceIds[I]);

    if not FileExists(Archive) then
      Continue;

    WizardForm.StatusLabel.Caption := 'Extracting ' + VoiceIds[I] + '...';

    if (not Exec(TarPath, '-xf "' + Archive + '" -C "' + Directory + '"', '', SW_HIDE, ewWaitUntilTerminated, ResultCode))
      or (ResultCode <> 0) then
      SuppressibleMsgBox(
        'Could not extract ' + VoiceIds[I] + '.' + #13#10 + #13#10 +
        'Run scripts\FetchVoices.ps1 from the install folder to install it later.',
        mbError, MB_OK, IDOK);

    DeleteFile(Archive);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    { Read by the next setup, whichever architecture it is built for. }
    RegWriteStringValue(HKA, UninstallKey, ArchValue, '{#TargetArch}');
    ExtractVoices;
  end;
end;
{ Walks the folder rather than trusting the catalog, since a reader may have dropped voices
  in by hand or run FetchVoices.ps1 themselves. }
function VoicesFolderBytes(const Dir: String): Int64;
var
  FindRec: TFindRec;
  Child: String;
begin
  Result := 0;
  if not FindFirst(AddBackslash(Dir) + '*', FindRec) then
    Exit;

  try
    repeat
      if (FindRec.Name <> '.') and (FindRec.Name <> '..') then
      begin
        Child := AddBackslash(Dir) + FindRec.Name;

        if FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0 then
          Result := Result + VoicesFolderBytes(Child)
        else
          Result := Result + Int64(FindRec.SizeHigh) * 4294967296 + Int64(FindRec.SizeLow);
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function DescribeSize(Bytes: Int64): String;
begin
  if Bytes >= 1073741824 then
    Result := Format('%.1f GB', [Bytes / 1073741824.0])
  else
    Result := Format('%d MB', [Bytes div 1048576]);
end;

{ Settings and saved scripts outlive an uninstall and the voices do not, which is a surprise
  worth one prompt when the folder holds gigabytes somebody waited on. The default is Yes, so
  the silent run that swaps one architecture for another is not left waiting on an answer. }
function InitializeUninstall: Boolean;
var
  Dir: String;
  Bytes: Int64;
begin
  Result := True;

  Dir := VoicesDirectory;
  if not DirExists(Dir) then
    Exit;

  Bytes := VoicesFolderBytes(Dir);
  if Bytes = 0 then
    Exit;

  Result := SuppressibleMsgBox(
    'Uninstalling {#AppName} also removes the voices it keeps in' + #13#10 +
    Dir + #13#10 + #13#10 +
    'That is ' + DescribeSize(Bytes) + ' of downloaded models, which would have to be ' +
    'downloaded again. Your settings and any saved scripts are kept.' + #13#10 + #13#10 +
    'Copy that folder somewhere else first if you want to keep the voices.' + #13#10 + #13#10 +
    'Carry on with the uninstall?',
    mbConfirmation, MB_YESNO, IDYES) = IDYES;
end;
