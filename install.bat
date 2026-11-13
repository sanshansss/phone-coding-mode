@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

set TARGET=%LOCALAPPDATA%\PhoneCoding

echo ============================================
echo   PhoneCoding 安装
echo ============================================
echo.

if not exist build\PhoneCoding.exe (
  echo [!] 还没编译，先跑 build.bat
  pause
  exit /b 1
)

echo [1/5] 创建目录 %TARGET%
if not exist "%TARGET%" mkdir "%TARGET%"

echo [2/5] 复制程序
copy /y build\PhoneCoding.exe "%TARGET%\PhoneCoding.exe" >nul
if errorlevel 1 (
  echo [x] 复制失败 -^> 如果托盘里还开着旧版本，先右键退出
  pause
  exit /b 1
)

echo [3/5] 关掉正在运行的旧实例
taskkill /f /im PhoneCoding.exe >nul 2>&1
ping -n 2 127.0.0.1 >nul

echo [4/5] 写入开机自启
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
echo   装好了
echo ============================================
echo   程序位置: %TARGET%
echo   开机自启: 已开启
echo   桌面快捷方式: 手机编程模式
echo.
echo   用法: 按 Ctrl+Alt+P 竖过来 / 再按一次回横屏
echo         或者双击托盘的图标
echo         被别的软件占了键的话，右键托盘图标 - 选项 - 换键
echo.
echo   卸载: 跑 uninstall.bat
echo.
pause
