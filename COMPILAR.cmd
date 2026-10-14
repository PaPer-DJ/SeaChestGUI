@echo off
setlocal
cd /d "%~dp0"
set "GUI_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%GUI_CSC%" set "GUI_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%GUI_CSC%" (
  echo No se encontro el compilador C# de .NET Framework.
  pause
  exit /b 1
)
"%GUI_CSC%" /nologo /target:winexe /optimize+ /platform:anycpu /win32manifest:src\app.manifest /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Management.dll /out:SeaChestGUI.exe src\Core.cs src\Program.cs src\WindowsDiskMetadata.cs src\GuideUI.cs src\GuideContent.cs
if errorlevel 1 (
  echo Error al compilar. Revisa el mensaje anterior.
  pause
  exit /b 1
)
echo Compilacion completada: SeaChestGUI.exe
pause
