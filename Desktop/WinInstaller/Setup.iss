[Setup]
AppName=neonKUBE
AppVersion={#GetEnv("NF_NEONKUBE_VERSION")}
DefaultDirName={pf}\neonKube
DefaultGroupName=neonKube
; UninstallDisplayIcon={app}\neonKUBE.exe
MinVersion=10.0.16299
Compression=lzma2
SolidCompression=no
OutputDir={#GetEnv("NF_BUILD")}
OutputBaseFilename=neonKUBE
; "ArchitecturesAllowed=x64" specifies that Setup cannot run on
; anything but x64.
ArchitecturesAllowed=x64
; "ArchitecturesInstallIn64BitMode=x64" requests that the install be
; done in "64-bit mode" on x64, meaning it should use the native
; 64-bit Program Files directory and the 64-bit view of the registry.
ArchitecturesInstallIn64BitMode=x64
AppPublisher=neonFORGE, LLC
AppPublisherURL=https://neonKUBE.com
ChangesEnvironment=yes

[Files]
; Source: {#GetEnv("NF_CACHE")}\windows\kubectl\{#GetEnv("NF_KUBE_VERSION")}\kubectl.exe; DestDir: {app}; Flags: recursesubdirs replacesameversion

[Icons]
; Name: "{group}\My Program"; Filename: "{app}\neonKUBE.exe"

[Code]

; =========================================================================
; Low level Windows API wrappers
; =============================================================================

function GetPhysicallyInstalledSystemMemory(var physicalRamKBytes: Int64): BOOL;
  external 'GetPhysicallyInstalledSystemMemory@kernel32.dll stdcall';

; =============================================================================
; These functions manage the PATH environment variable.
; =============================================================================

function Replace(Dest, SubStr, Str: string): string;
var
  Position: Integer;
  Ok: Integer;
begin
  Ok := 1;
  while Ok > 0 do
  begin
    Position:=Pos(SubStr, Dest);
    if Position > 0 then
    begin
      Delete(Dest, Position, Length(SubStr));
      Insert(Str, Dest, Position);
    end else
      Ok := 0;
  end;
  Result:=Dest;
end;

procedure AppendToPath();
var
  V: string;
  Str: string;
begin
  RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', V);
  Str := ExpandConstant('{app}');
  V := Replace(V, Str, '');
  V := V + ';' + Str;
  V := Replace(V,';;',';');
  RegWriteStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', V);
end;

procedure RemoveFromPath();
var
  V: string;
  Str: string;
begin
  RegQueryStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', V);
  Str := ExpandConstant('{app}');
  V := Replace(V, Str, '');
  V := Replace(V,';;',';');
  RegWriteStringValue(HKLM, 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment', 'Path', V);
end;

; =============================================================================
; EVENT Handlers
; =============================================================================

function InitializeSetup: Boolean;
var
    physicalRamKBytes: Int64;
begin

    ; Verify that the machine has at least 4GB of RAM

    GetPhysicallyInstalledSystemMemory(physicalRamKBytes);

    if physicalRamKBytes/(1024*1024) < 4 then
    begin
        MsgBox('Cannot install MINIKUBE.  You have less than 4GB physical memory available.', mbError, MB_OK);
        Result := False;
    end;

    Result := True;
end;

procedure DeinitializeSetup();
begin
    
  ; Append the program folder to the PATH

  AppendToPath();
end;

procedure DeinitializeUninstall();
begin

  ; Remove the program folder from the PATH

  RemoveFromPath();
end;