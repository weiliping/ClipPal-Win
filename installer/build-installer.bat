@echo off
chcp 65001 >nul
setlocal
title ClipPal - 一键打包安装包

echo ============================================
echo   ClipPal 一键打包
echo   全程自动，最后会打开产物所在文件夹
echo ============================================
echo.

cd /d "%~dp0.."

rem ---------- 1. 检查 .NET SDK（注意：只装运行时时也会有 dotnet.exe，必须看 --list-sdks） ----------
set "SDKVER="
for /f "delims=" %%i in ('dotnet --list-sdks 2^>nul') do set "SDKVER=%%i"
if not defined SDKVER (
    echo [1/3] 未检测到 .NET SDK，正在安装 .NET 8 SDK ...
    winget install --id Microsoft.DotNet.SDK.8 --accept-source-agreements --accept-package-agreements
    if errorlevel 1 (
        echo.
        echo 安装失败。请手动安装后重试：https://dotnet.microsoft.com/download/dotnet/8.0
        echo 装完请关掉这个窗口，重新双击本脚本。
        pause
        exit /b 1
    )
    echo 安装完成，请关掉这个窗口重新双击本脚本（新装的 SDK 需要新环境）。
    pause
    exit /b 0
) else (
    echo [1/3] .NET SDK 已就绪：%SDKVER%
)

rem ---------- 2. 检查 Inno Setup ----------
set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist "%ISCC%" (
    echo [2/3] 未检测到 Inno Setup，正在安装 ...
    winget install --id JRSoftware.InnoSetup --accept-source-agreements --accept-package-agreements
    set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
    if not exist "%ISCC%" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
    if not exist "%ISCC%" (
        echo 安装后仍未找到 ISCC.exe，请手动打开 installer\ClipPal.iss 按 F9 编译。
        pause
        exit /b 1
    )
) else (
    echo [2/3] Inno Setup 已就绪
)

rem ---------- 3. 发布 + 打包 ----------
echo [3/3] 正在打包，请稍候（1-2 分钟）...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish
if errorlevel 1 (
    echo.
    echo 发布失败，请把上面的报错发我。
    pause
    exit /b 1
)
if not exist "publish\ClipPal.exe" (
    echo.
    echo 发布没有生成 publish\ClipPal.exe，请把上面的输出发我。
    pause
    exit /b 1
)
echo      发布完成：publish\ClipPal.exe

"%ISCC%" "installer\ClipPal.iss"
if errorlevel 1 (
    echo.
    echo 编译安装包失败。
    pause
    exit /b 1
)

echo.
echo ============================================
echo   完成！安装包在 installer\Output\ 里
echo ============================================
if exist "installer\Output\ClipPal-Setup.exe" (
    for %%F in ("installer\Output\ClipPal-Setup.exe") do echo   文件：%%~fF  (%%~zF 字节)
    start "" "installer\Output"
) else (
    echo 没找到 ClipPal-Setup.exe，请把输出发我。
)
echo.
pause
