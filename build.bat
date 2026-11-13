@echo off
chcp 65001 >nul
setlocal

rem ============================================================
rem  PhoneCoding 一键编译脚本
rem  只需要 Windows 自带的 .NET Framework 编译器，无需装 SDK
rem ============================================================

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [x] 找不到 C# 编译器 csc.exe
  echo     需要 .NET Framework 4.x（Win10/11 默认自带）
  pause
  exit /b 1
)

cd /d "%~dp0"
if not exist build mkdir build

echo [1/2] 编译中...
"%CSC%" -nologo -target:winexe -optimize+ ^
  -out:build\PhoneCoding.exe ^
  -r:System.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll ^
  src\Core.cs src\Display.cs src\PhoneMode.cs src\UI.cs src\Program.cs

if errorlevel 1 (
  echo.
  echo [x] 编译失败
  pause
  exit /b 1
)

echo [2/2] 完成 -^> build\PhoneCoding.exe
echo.
echo 双击 build\PhoneCoding.exe 就会出现在系统托盘
echo 快捷键默认 Ctrl+Alt+P（被占用时会自动换键，托盘提示里能看到）
echo.
pause
