@echo off
rem Build the Windows desktop app with the C# compiler shipped in Windows.
rem No .NET SDK / Visual Studio needed.
cd /d "%~dp0"

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo [x] csc.exe not found. Need .NET Framework 4.x on this machine.
  exit /b 1
)

if not exist holidays.json (
  echo [x] holidays.json missing. Run: python tools\build_data.py
  exit /b 1
)

if not exist build mkdir build

echo [1/2] compiling
"%CSC%" -nologo -target:winexe -out:build\JiaQi.exe -optimize+ ^
  -resource:holidays.json -resource:sources_seed.json ^
  -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Web.Extensions.dll ^
  Model.cs Theme.cs App.cs Source.cs Plan.cs SlideForm.cs PageEditor.cs PlanGen.cs PlanGenForm.cs PlanDialog.cs MediaOpen.cs RingTrim.cs PlanReminder.cs RingSetting.cs WmpPanel.cs WmpForm.cs
if errorlevel 1 (
  echo [x] compile failed
  exit /b 1
)

echo [2/2] done
echo     build\JiaQi.exe
