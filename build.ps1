[CmdletBinding()]
param(
    [string]$SephiriaDir = $env:SEPHIRIA_DIR
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Find-SephiriaDir {
    $candidates = @()
    if ($env:SEPHIRIA_DIR) { $candidates += $env:SEPHIRIA_DIR }
    if ($SephiriaDir) { $candidates += $SephiriaDir }

    try {
        $steam = Get-ItemProperty -Path "HKCU:\Software\Valve\Steam" -Name SteamPath -ErrorAction SilentlyContinue
        if ($steam -and $steam.SteamPath) {
            $steamPath = $steam.SteamPath
            $candidates += (Join-Path $steamPath "steamapps\common\Sephiria")
            $libraryFile = Join-Path $steamPath "steamapps\libraryfolders.vdf"
            if (Test-Path $libraryFile) {
                foreach ($line in Get-Content $libraryFile) {
                    if ($line -match '"path"\s+"([^"]+)"') {
                        $library = $matches[1].Replace("\\", "\")
                        $candidates += (Join-Path $library "steamapps\common\Sephiria")
                    }
                }
            }
        }
    } catch {
    }

    $candidates += "C:\Program Files (x86)\Steam\steamapps\common\Sephiria"
    $candidates += "D:\Steam\steamapps\common\Sephiria"
    $candidates += "D:\SteamLibrary\steamapps\common\Sephiria"

    foreach ($candidate in $candidates) {
        if ($candidate -and (Test-Path (Join-Path $candidate "Sephiria_Data\Managed\Assembly-CSharp.dll"))) {
            return $candidate
        }
    }
    return $null
}

if (-not $SephiriaDir) {
    $SephiriaDir = Find-SephiriaDir
}
if (-not $SephiriaDir -or -not (Test-Path (Join-Path $SephiriaDir "Sephiria_Data\Managed\Assembly-CSharp.dll"))) {
    throw "找不到 Sephiria 游戏目录。请设置环境变量 SEPHIRIA_DIR，例如：`n  `$env:SEPHIRIA_DIR = 'D:\Steam\steamapps\common\Sephiria'`n然后重新运行 build.ps1。"
}

$managed = Join-Path $SephiriaDir "Sephiria_Data\Managed"
Write-Host "[build] Game: $SephiriaDir"

$csc = $env:CSC_PATH
if (-not $csc -or -not (Test-Path $csc)) {
    $csc = Join-Path $root "tools\roslyn\tasks\net472\csc.exe"
}
if (-not (Test-Path $csc)) {
    $tools = Join-Path $root "tools"
    New-Item -ItemType Directory -Force -Path $tools | Out-Null
    $nupkg = Join-Path $tools "compilers.nupkg"
    if (-not (Test-Path $nupkg)) {
        Write-Host "[build] Downloading Roslyn compiler toolset ..."
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/Microsoft.Net.Compilers.Toolset" -OutFile $nupkg -UseBasicParsing
    }
    $roslyn = Join-Path $tools "roslyn"
    if (-not (Test-Path (Join-Path $roslyn "tasks\net472\csc.exe"))) {
        if (Test-Path $roslyn) { Remove-Item -Recurse -Force $roslyn }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($nupkg, $roslyn)
    }
    $csc = Join-Path $roslyn "tasks\net472\csc.exe"
}
if (-not (Test-Path $csc)) {
    throw "找不到 csc.exe。可以安装 .NET SDK，或设置环境变量 CSC_PATH 指向 Roslyn 的 csc.exe。"
}

$outDir = Join-Path $root "dist\SephiriaDamageMeter"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$references = @(
    "mscorlib.dll",
    "System.dll",
    "System.Core.dll",
    "netstandard.dll",
    "UnityEngine.dll",
    "UnityEngine.CoreModule.dll",
    "UnityEngine.UIModule.dll",
    "UnityEngine.UI.dll",
    "UnityEngine.TextRenderingModule.dll",
    "UnityEngine.JSONSerializeModule.dll",
    "Unity.InputSystem.dll",
    "Unity.TextMeshPro.dll",
    "Assembly-CSharp.dll",
    "Mirror.dll"
)

$lines = @(
    "/nologo",
    "/target:library",
    "/langversion:7.3",
    "/nostdlib+",
    "/optimize+",
    ('/out:"' + (Join-Path $outDir "SephiriaDamageMeter.dll") + '"')
)
foreach ($reference in $references) {
    $path = Join-Path $managed $reference
    if (-not (Test-Path $path)) {
        throw "缺少游戏程序集：$path"
    }
    $lines += ('/reference:"' + $path + '"')
}
Get-ChildItem (Join-Path $root "src\SephiriaDamageMeter") -Filter "*.cs" |
    Sort-Object Name |
    ForEach-Object { $lines += ('"' + $_.FullName + '"') }

$rspPath = Join-Path $root "build.rsp"
[System.IO.File]::WriteAllLines($rspPath, $lines, (New-Object System.Text.UTF8Encoding($false)))

& $csc "@$rspPath"
if ($LASTEXITCODE -ne 0) {
    throw "编译失败（csc 退出码 $LASTEXITCODE）。"
}

Copy-Item (Join-Path $root "metadata.json") (Join-Path $outDir "metadata.json") -Force
Copy-Item (Join-Path $root "config.example.json") (Join-Path $outDir "config.json") -Force
Copy-Item (Join-Path $root "README.md") (Join-Path $outDir "README.md") -Force
Copy-Item (Join-Path $root "README.zh-CN.md") (Join-Path $outDir "README.zh-CN.md") -Force
Copy-Item (Join-Path $root "使用指南.md") (Join-Path $outDir "使用指南.md") -Force
Copy-Item (Join-Path $root "USER_GUIDE.en.md") (Join-Path $outDir "USER_GUIDE.en.md") -Force

Write-Host "[build] OK: $outDir\SephiriaDamageMeter.dll"
Write-Host "[build] 把 $outDir 复制到 Sephiria\AddOns\SephiriaDamageMeter 即可。"
