@echo off
chcp 65001 >nul
echo ======================================
echo AudioCraft 构建脚本
echo ======================================
echo.

REM 检查 PowerShell 是否可用
where powershell >nul 2>&1
if %errorlevel% neq 0 (
    echo 错误: 未找到 PowerShell
    pause
    exit /b 1
)

REM 运行构建脚本
powershell -ExecutionPolicy Bypass -File build.ps1 %*

pause
