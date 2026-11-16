@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

set TARGET=%LOCALAPPDATA%\PhoneCoding

echo ============================================
echo    PhoneCoding - 手机编程模式 安装程序
echo ============================================
echo.

rem ---------- 1. 找可执行文件：优先用仓库里打包好的 ----------
set SRC=
if exist "dist\PhoneCoding.exe" set SRC=dist\PhoneCoding.exe
if exist "build\PhoneCoding.exe" set SRC=build\PhoneCoding.exe

if "%SRC%"=="" (
  echo [!] 没找到 PhoneCoding.exe
  echo.
  echo     正在尝试用本机编译器现场构建...
  echo.
  call build.bat
  if exist "build\PhoneCoding.exe" set SRC=build\PhoneCoding.exe
)

if "%SRC%"=="" (
  echo.
  echo [x] 安装失败：没有可用的 PhoneCoding.exe
  echo     请手动运行 build.bat 看看报什么错。
  echo.
  pause
  exit /b 1
)

echo [1/5] 使用 %SRC%

echo [2/5] 创建目录 %TARGET%
if not exist "%TARGET%" mkdir "%TARGET%"

echo [3/5] 复制程序
taskkill /f /im PhoneCoding.exe >nul 2>&1
ping -n 2 127.0.0.1 >nul
copy /y "%SRC%" "%TARGET%\PhoneCoding.exe" >nul
if errorlevel 1 (
  echo [x] 复制失败，可能程序还在运行
  pause
  exit /b 1
)

echo [4/5] 设置开机自启
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v PhoneCoding ^
  /t REG_SZ /d "\"%TARGET%\PhoneCoding.exe\"" /f >nul

echo [5/5] 创建桌面快捷方式
powershell -NoProfile -Command ^
  "$s=(New-Object -ComObject WScript.Shell).CreateShortcut([Environment]::GetFolderPath('Desktop')+'\手机编程模式.lnk'); $s.TargetPath='%TARGET%\PhoneCoding.exe'; $s.WorkingDirectory='%TARGET%'; $s.Description='一键切换竖屏手机编程模式'; $s.Save()" >nul 2>&1

echo.
echo 启动中...
start "" "%TARGET%\PhoneCoding.exe"
ping -n 3 127.0.0.1 >nul

echo.
echo ============================================
echo    装好了
echo ============================================
echo    程序位置: %TARGET%
echo    开机自启: 已开启
echo    桌面快捷方式: 手机编程模式
echo.
echo    用法: 按 Ctrl+Alt+P 竖过来，再按一次回横屏
echo          （被别的软件占了会自动换键，托盘提示里有）
echo.
echo    卸载: 运行 uninstall.bat
echo.
pause
