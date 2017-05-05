; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define MyAppName "Wise40"
#define MyAppVersion "1.0"
#define MyAppPublisher "The Wise Observatory"
#define MyAppExeName "Dash.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{779299FF-1754-4A3B-9F22-12EAD0624C78}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppContact="Arie Blumenzweig <blumzi@013.net>"
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputBaseFilename="Wise40 Setup"
Compression=lzma
SolidCompression=yes
SetupLogging=yes
SetupIconFile=ASCOM.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 0,6.1

[Files]
Source: "{#SolutionDir}\Dash\bin\Debug\Dash.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\Boltwood\bin\Debug\ASCOM.Wise40.Boltwood.ObservingConditions.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\Common\bin\Debug\Common.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\ComputerControl\bin\Debug\MccDaq.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\ComputerControl\bin\Debug\ASCOM.Wise40.ComputerControl.SafetyMonitor.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\VantagePro\bin\Debug\ASCOM.Wise40.VantagePro.ObservingConditions.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\Dome\bin\Debug\ASCOM.Wise40.Dome.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\Focus\bin\Debug\ASCOM.Wise40.Focuser.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\Hardware\bin\Debug\Hardware.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\PIDLibrary\bin\Debug\PID.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\Restore-ASCOM-Profiles\bin\Debug\Restore-ASCOM-Profiles.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\Restore-ASCOM-Profiles\bin\Debug\Restore-ASCOM-Profiles - ACP.lnk"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\Restore-ASCOM-Profiles\bin\Debug\Restore-ASCOM-Profiles - LCOGT.lnk"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\Restore-ASCOM-Profiles\bin\Debug\Restore-ASCOM-Profiles - Wise.lnk"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\SafeToImage\bin\Debug\ASCOM.Wise40.SafeToImage.SafetyMonitor.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\SafeToOpen\bin\Debug\ASCOM.Wise40.SafeToOpen.SafetyMonitor.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\FilterWheel\bin\Debug\ASCOM.Wise40.FilterWheel.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SolutionDir}\Telescope\bin\Debug\ASCOM.Wise40.Telescope.dll"; DestDir: "{app}"; Flags: ignoreversion

Source: "{#SolutionDir}\Boltwood\Sample Files\ClarityII-data.txt"; DestDir: "{app}"; AfterInstall: CopyToTemp('c:\temp\ClarityII-data.txt')
Source: "{#SolutionDir}\DavisVantage\Sample Files\Weather_Wise40_Vantage_Pro.htm"; DestDir: "{app}";  AfterInstall: CopyToTemp('c:\temp\Weather_Wise40_Vantage_Pro.htm')
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

; [Components]
; Name: "Dashboard"; Description: "The Wise40 Dashboard"; Types: full;
; Name: "Telescope"; DEscription: "The Wise40 ASCOM Telescope driver"; Types: full;
; Name: "FilterWeheel"; Description: "The Wise40 ASCOM FilterWheel driver"; Types: full;
; Name: "Focuser"; Description: "The Wise40 ASCOM Focuser driver"; Types: full;
; Name: "Dome"; Description: "The Wise40 ASCOM Dome driver"; Types: full;
; Name: "Computer Control"; Description: "The Wise40 ASCOM ComputerControl SafetyMonitor driver"; Types: full;
; Name: "SafeToOpen"; Description: "The Wise40 ASCOM SafeToOpen SafetyMonitor driver"; Types: full;
; Name: "SafeToImage"; Description: "The Wise40 ASCOM SafeToImage SafetyMonitor driver"; Types: full;
; Name: "Boltwood CloudSensor"; Description: "The Wise40 ASCOM CloudSensor ObservingConditions driver"; Types: full;
; Name: "Davis VantagePro2"; Description: "The Wise40 ASCOM VantagePro2 ObservingConditions driver"; Types: full;

[Run]
Filename: "{dotnet4032}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.Boltwood.ObservingConditions.dll"""; Flags: runhidden 32bit
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.Boltwood.ObservingConditions.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.ComputerControl.SafetyMonitor.dll"""; Flags: runhidden 32bit
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.ComputerControl.SafetyMonitor.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.VantagePro.ObservingConditions.dll"""; Flags: runhidden 32bit
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.VantagePro.ObservingConditions.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.Dome.dll"""; Flags: runhidden 32bit
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.Dome.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.Focuser.dll"""; Flags: runhidden 32bit
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.Focuser.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.SafeToImage.SafetyMonitor.dll"""; Flags: runhidden 32bit
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.SafeToImage.SafetyMonitor.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.SafeToOpen.SafetyMonitor.dll"""; Flags: runhidden 32bit
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.SafeToOpen.SafetyMonitor.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.Telescope.dll"""; Flags: runhidden 32bit
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.Telescope.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.FilterWheel.dll"""; Flags: runhidden 32bit
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.FilterWheel.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.VantagePro.ObservingConditions.dll"""; Flags: runhidden 32bit
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.VantagePro.ObservingConditions.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{app}\Restore-ASCOM-Profiles"; Description: "Initialize ASCOM Profiles according to previous selection."; Parameters: "{code:ProfileType|}"; Flags: postinstall
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,Wise40 Dashboard}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Only for .NET assembly/in-proc drivers
Filename: "{dotnet4032}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.Boltwood.ObservingConditions.dll"""; Flags: runhidden 32bit
; This helps to give a clean uninstall
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.Boltwood.ObservingConditions.dll"""; Flags: runhidden 64bit; Check: IsWin64
Filename: "{dotnet4064}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.Boltwood.ObservingConditions.dll"""; Flags: runhidden 64bit; Check: IsWin64


Filename: "{dotnet4032}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.ComputerControl.SafetyMonitor.dll"""; Flags: runhidden 32bit
; This helps to give a clean uninstall
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.ComputerControl.SafetyMonitor.dll"""; Flags: runhidden 64bit; Check: IsWin64
Filename: "{dotnet4064}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.ComputerControl.SafetyMonitor.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.VantagePro.ObservingConditions.dll"""; Flags: runhidden 32bit
; This helps to give a clean uninstall
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.VantagePro.ObservingConditions.dll"""; Flags: runhidden 64bit; Check: IsWin64
Filename: "{dotnet4064}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.VantagePro.ObservingConditions.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.Dome.dll"""; Flags: runhidden 32bit
; This helps to give a clean uninstall
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.Dome.dll"""; Flags: runhidden 64bit; Check: IsWin64
Filename: "{dotnet4064}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.Dome.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.Focuser.dll"""; Flags: runhidden 32bit
; This helps to give a clean uninstall
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.Focuser.dll"""; Flags: runhidden 64bit; Check: IsWin64
Filename: "{dotnet4064}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.Focuser.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.SafeToImage.SafetyMonitor.dll"""; Flags: runhidden 32bit
; This helps to give a clean uninstall
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.SafeToImage.SafetyMonitor.dll"""; Flags: runhidden 64bit; Check: IsWin64
Filename: "{dotnet4064}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.SafeToImage.SafetyMonitor.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.SafeToOpen.SafetyMonitor.dll"""; Flags: runhidden 32bit
; This helps to give a clean uninstall
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.SafeToOpen.SafetyMonitor.dll"""; Flags: runhidden 64bit; Check: IsWin64
Filename: "{dotnet4064}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.SafeToOpen.SafetyMonitor.dll"""; Flags: runhidden 64bit; Check: IsWin64

Filename: "{dotnet4032}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.Telescope.dll"""; Flags: runhidden 32bit
; This helps to give a clean uninstall
Filename: "{dotnet4064}\regasm.exe"; Parameters: "/codebase ""{app}\ASCOM.Wise40.Telescope.dll"""; Flags: runhidden 64bit; Check: IsWin64
Filename: "{dotnet4064}\regasm.exe"; Parameters: "-u ""{app}\ASCOM.Wise40.Telescope.dll"""; Flags: runhidden 64bit; Check: IsWin64

[CODE]
  
//const
  //WiseDescText = 'Wise Profile'#13'New line';
  //LCODescText  = 'LCO Profile';
  //ACPDescText  = 'ACP Profile';

var
  LCORadioButton: TNewRadioButton;
  WiseRadioButton: TNewRadioButton;
  ACPRadioButton: TNewRadioButton;
  SkipRadioButton: TNewRadioButton;

//
// Before the installer UI appears, verify that the (prerequisite)
// ASCOM Platform 6.2 or greater is installed, including both Helper
// components. Utility is required for all types (COM and .NET)!
//
function InitializeSetup(): Boolean;
var
   U : Variant;
   H : Variant;
begin
   Result := FALSE;  // Assume failure
   // check that the DriverHelper and Utilities objects exist, report errors if they don't
   try
      H := CreateOLEObject('DriverHelper.Util');
   except
      MsgBox('The ASCOM DriverHelper object has failed to load, this indicates a serious problem with the ASCOM installation', mbInformation, MB_OK);
   end;
   try
      U := CreateOLEObject('ASCOM.Utilities.Util');
   except
      MsgBox('The ASCOM Utilities object has failed to load, this indicates that the ASCOM Platform has not been installed correctly', mbInformation, MB_OK);
   end;
   try
      if (U.IsMinimumRequiredVersion(6,2)) then	// this will work in all locales
         Result := TRUE;
   except
   end;
   if(not Result) then
      MsgBox('The ASCOM Platform 6.2 or greater is required for this driver.', mbInformation, MB_OK);

end;

// Code to enable the installer to uninstall previous versions of itself when a new version is installed
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  UninstallExe: String;
  UninstallRegistry: String;
begin
  if (CurStep = ssInstall) then // Install step has started
	begin
      // Create the correct registry location name, which is based on the AppId
      UninstallRegistry := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#SetupSetting("AppId")}' + '_is1');
      // Check whether an extry exists
      if RegQueryStringValue(HKLM, UninstallRegistry, 'UninstallString', UninstallExe) then
        begin // Entry exists and previous version is installed so run its uninstaller quietly after informing the user
          MsgBox('Setup will now remove the previous version.', mbInformation, MB_OK);
          Exec(RemoveQuotes(UninstallExe), ' /SILENT', '', SW_SHOWNORMAL, ewWaitUntilTerminated, ResultCode);
          sleep(1000);    //Give enough time for the install screen to be repainted before continuing
        end;
  end;
end;

procedure CopyToTemp(dst: String);
begin
    if GetComputerNameString <> 'dome-ctlr' then
	begin
       FileCopy(ExpandConstant(CurrentFileName), dst, False);
	end;
end;

procedure InitializeWizard;
var
  CustomPage: TWizardPage;
  IntroLabel, SkipDescLabel: TLabel;
begin
  CustomPage := CreateCustomPage(wpWelcome, 'ASCOM Profile Initialization', '');

  IntroLabel := TLabel.Create(WizardForm);
  IntroLabel.Parent := CustomPage.Surface;
  IntroLabel.Caption := 'The ASCOM Profiles for the various drivers must be initialized.'#13#13'Please select the type of control that will be used.'#13'The main difference is what controls the dome.'#13;

  WiseRadioButton := TNewRadioButton.Create(WizardForm);
  WiseRadioButton.Parent := CustomPage.Surface;
  WiseRadioButton.Checked := True;
  WiseRadioButton.Top := IntroLabel.Top + IntroLabel.Height + 2;
  WiseRadioButton.Width := CustomPage.SurfaceWidth;
  WiseRadioButton.Font.Style := [fsBold];
  WiseRadioButton.Font.Size := 9;
  WiseRadioButton.Caption := 'Wise40 Dashboard'

  LCORadioButton := TNewRadioButton.Create(WizardForm);
  LCORadioButton.Parent := CustomPage.Surface;
  LCORadioButton.Top := WiseRadioButton.Top + WiseRadioButton.Height + 2;
  LCORadioButton.Width := CustomPage.SurfaceWidth;
  LCORadioButton.Font.Style := [fsBold];
  LCORadioButton.Font.Size := 9;
  LCORadioButton.Caption := 'LCO'

  ACPRadioButton := TNewRadioButton.Create(WizardForm);
  ACPRadioButton.Parent := CustomPage.Surface;
  ACPRadioButton.Top := LCORadioButton.Top + LCORadioButton.Height + 2;
  ACPRadioButton.Width := CustomPage.SurfaceWidth;
  ACPRadioButton.Font.Style := [fsBold];
  ACPRadioButton.Font.Size := 9;
  ACPRadioButton.Caption := 'ACP'

  SkipRadioButton := TNewRadioButton.Create(WizardForm);
  SkipRadioButton.Parent := CustomPage.Surface;
  SkipRadioButton.Top := ACPRadioButton.Top + ACPRadioButton.Height + 2;
  SkipRadioButton.Width := CustomPage.SurfaceWidth;
  SkipRadioButton.Font.Style := [fsBold];
  SkipRadioButton.Font.Size := 9;
  SkipRadioButton.Caption := 'Skip'
  SkipDescLabel := TLabel.Create(WizardForm);
  SkipDescLabel.Parent := CustomPage.Surface;
  SkipDescLabel.Left := 20;
  SkipDescLabel.Top := SkipRadioButton.Top + SkipRadioButton.Height + 2;
  SkipDescLabel.Width := CustomPage.SurfaceWidth;
  SkipDescLabel.Height := 40;
  SkipDescLabel.AutoSize := False;
  SkipDescLabel.Wordwrap := True;
  SkipDescLabel.Caption := 'The current ASCOM Profiles, if existent, will not be altered.'#13'NOTE: The Wise40 drivers will not work without initialized profiles!!!';
end;

function ProfileType(Param: String): String;
begin
	if ACPRadioButton.Checked then
		Result := 'ACP'
	else if WiseRadioButton.Checked then
		Result := 'Wise'
	else if LCORadioButton.Checked then
		Result := 'LCO'
	else
		Result := 'Skip'
end;
