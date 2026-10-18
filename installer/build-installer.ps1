# 生成 ClipPal 安装包（在 Windows 上运行）
# 前置：winget install JRSoftware.InnoSetup
# 用法：powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    Write-Host '→ 发布目录版到 publish\ ...' -ForegroundColor Cyan
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish

    $iscc = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $iscc) {
        Write-Host '未找到 ISCC.exe（Inno Setup 编译器）' -ForegroundColor Yellow
        Write-Host '请先安装：winget install JRSoftware.InnoSetup' -ForegroundColor Yellow
        Write-Host "安装后也可以直接打开 installer\ClipPal.iss 按 F9 编译。" -ForegroundColor Yellow
        exit 1
    }

    Write-Host "→ 编译安装包（$iscc）..." -ForegroundColor Cyan
    & $iscc 'installer\ClipPal.iss'

    $setup = Join-Path $root 'installer\Output\ClipPal-Setup.exe'
    if (Test-Path $setup) {
        $size = [math]::Round((Get-Item $setup).Length / 1MB, 1)
        Write-Host "✅ 完成：$setup ($size MB)" -ForegroundColor Green
        Get-FileHash $setup -Algorithm SHA256 | Format-List
    }
} finally {
    Pop-Location
}
