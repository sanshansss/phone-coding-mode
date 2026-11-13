@echo off
chcp 65001 >nul
setlocal
set TARGET=%LOCALAPPDATA%\PhoneCoding

echo 正在退出 PhoneCoding...
taskkill /f /im PhoneCoding.exe >nul 2>&1
ping -n 2 127.0.0.1 >nul

echo 删除开机自启...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v PhoneCoding /f >nul 2>&1

echo 删除桌面快捷方式...
del /q "%USERPROFILE%\Desktop\手机编程模式.lnk" >nul 2>&1

echo 删除程序目录...
rmdir /s /q "%TARGET%" 2>nul

echo.
echo 卸载完成。
echo 注意: 如果卸载时屏幕还在竖屏状态，右键托盘退出会先自动回横屏。
echo       万一没还原，去 设置 - 系统 - 屏幕 手动改回横向即可。
echo.
pause
