; Directive 47 installer.
;
; Per-user and unelevated, by design rather than by preference. architecture.md §9 names two
; properties never to trade away — no elevation and no runtime prerequisite — and Phase 18's
; controller work is scoped around "the per-user no-elevation install". So this installs to
; %LOCALAPPDATA%\Programs, which the Commander owns and can write to.
;
; The install directory is FIXED rather than versioned. Everything d47 writes lives in data\
; beside the executable (CLAUDE.md), which is what makes the folder portable and what keeps
; settings, DPAPI secrets and several hundred megabytes of speech model together. Update
; frameworks that install into per-version folders — Squirrel, Velopack — move the executable
; on every update and would orphan that folder, so d47 keeps its own in-place updater and this
; installer does first install and clean uninstall only.

#ifndef Version
  #define Version "0.0.0"
#endif

#define Name "Directive 47"
#define ExeName "d47.exe"

[Setup]
; Never regenerate this. It is the identity Windows uses to recognise an existing install and
; to upgrade it in place rather than stacking a second copy in Add/Remove Programs.
AppId={{8B5F6A21-4C7D-4E93-9A18-2D6F3B7C51E4}
AppName={#Name}
AppVersion={#Version}
AppVerName={#Name} {#Version}
AppPublisher=Doug Seelinger
AppSupportURL=https://github.com/dseelinger/d47
AppUpdatesURL=https://github.com/dseelinger/d47/releases
VersionInfoVersion={#Version}

; The whole point: lowest asks for no elevation and cannot silently escalate.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=

DefaultDirName={localappdata}\Programs\d47
DefaultGroupName={#Name}
; One entry directly in the Start Menu rather than a folder holding one shortcut. It also has
; to match StartMenuShortcut.DefaultPath exactly, so the app's own "add a Start Menu entry?"
; offer sees it already there and stands down without needing to know it was installed.
DisableProgramGroupPage=yes
DisableDirPage=auto
DisableReadyPage=no

OutputDir=.
; Versioned, deliberately, and this is the asset that may be (#96). Nothing in src/ reads this
; name and no test pins it - the in-app updater fetches d47.zip and never the installer. So this is
; the one release asset free to say which build it is, which is worth it for the file a person
; downloads and may still have in a folder a year later. d47.zip is the opposite case and must
; never be renamed; see UpdateChecker.ArchiveAsset.
OutputBaseFilename=d47-setup-{#Version}
SetupIconFile=..\assets\directive-47.ico
UninstallDisplayIcon={app}\{#ExeName}
UninstallDisplayName={#Name}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startmenu"; Description: "Add a Start Menu entry"; GroupDescription: "Shortcuts:"
Name: "desktopicon"; Description: "Add a desktop shortcut"; GroupDescription: "Shortcuts:"; Flags: unchecked

[Files]
Source: "..\src\D47.App\bin\Release\publish\{#ExeName}"; DestDir: "{app}"; Flags: ignoreversion
; Whisper's natives, which its loader finds by probing runtimes\win-x64 on disk beside the
; executable. recursesubdirs preserves that layout exactly; flattening it breaks speech.
Source: "..\src\D47.App\bin\Release\publish\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs
; The card still for every hull (#289), 11 MB, so a fresh installation has a fleet with pictures
; on it before anything is fetched. The build's folder, not the Commander's: the large art lands
; in data\ships\, which an install and an update both leave alone.
Source: "..\src\D47.App\bin\Release\publish\ships\*"; DestDir: "{app}\ships"; Flags: ignoreversion

[Icons]
; Named to match StartMenuShortcut.EntryName so the two paths cannot disagree.
Name: "{userprograms}\{#Name}"; Filename: "{app}\{#ExeName}"; WorkingDir: "{app}"; Tasks: startmenu
Name: "{userdesktop}\{#Name}"; Filename: "{app}\{#ExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#ExeName}"; Description: "Start {#Name}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; What an in-place update leaves behind. The Commander's data\ folder is deliberately absent
; from this list — see below.
Type: files; Name: "{app}\{#ExeName}.old"
Type: filesandordirs; Name: "{app}\runtimes"
Type: filesandordirs; Name: "{app}\ships"

[Code]
{ Uninstall keeps data\ unless the Commander says otherwise. It holds their API keys, their
  settings and a speech model that can be several hundred megabytes to fetch again, and none
  of that is recoverable by reinstalling. Asked rather than assumed, in either direction. }
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{app}\data');

    if DirExists(DataDir) then
    begin
      if MsgBox('Remove your D47 settings, saved keys and downloaded speech models?' + #13#10#13#10
                + 'Choose No to keep them for a future install.',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
      begin
        DelTree(DataDir, True, True, True);
      end;
    end;
  end;
end;
