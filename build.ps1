param(
    [ValidateSet("build", "publish")]
    [string]$Action = "publish"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $Root "JRBSI\JRBSI.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error ".NET SDK is required. Install from https://aka.ms/dotnet/download"
}

function Test-DotNetSingleFileBundle {
    param([string]$ExePath)

    $signature = [byte[]]@(
        0x8B, 0x12, 0x02, 0xB9, 0x6A, 0x61, 0x20, 0x38,
        0x72, 0x7B, 0x93, 0x02, 0x14, 0xD7, 0xA0, 0x32,
        0x13, 0xF5, 0xB9, 0xE6, 0xEF, 0xAE, 0x33, 0x18,
        0xEE, 0x3B, 0x2D, 0xCE, 0x24, 0xB3, 0x6A, 0xAE
    )

    $fs = [IO.File]::OpenRead($ExePath)
    try {
        $buffer = New-Object byte[] 65536
        $window = New-Object byte[] ($signature.Length + 8)
        $filled = 0
        $absolute = [int64]0
        while ($true) {
            $read = $fs.Read($buffer, 0, $buffer.Length)
            if ($read -le 0) {
                break
            }

            for ($i = 0; $i -lt $read; $i++) {
                if ($filled -lt $window.Length) {
                    $window[$filled] = $buffer[$i]
                    $filled++
                }
                else {
                    [Array]::Copy($window, 1, $window, 0, $window.Length - 1)
                    $window[$window.Length - 1] = $buffer[$i]
                }

                $absolute++
                if ($filled -lt $window.Length) {
                    continue
                }

                $match = $true
                for ($j = 0; $j -lt $signature.Length; $j++) {
                    if ($window[8 + $j] -ne $signature[$j]) {
                        $match = $false
                        break
                    }
                }

                if ($match) {
                    return [BitConverter]::ToInt64($window, 0) -ne 0
                }
            }
        }
    }
    finally {
        $fs.Close()
    }

    return $false
}

Push-Location $Root
try {
    if ($Action -eq "build") {
        dotnet build $Project -c Release
    }
    else {
        $publishDir = Join-Path $Root "JRBSI\bin\Release\net8.0-windows\win-x64\publish"
        if (Test-Path $publishDir) {
            Remove-Item -Path $publishDir -Recurse -Force
        }

        dotnet publish $Project -c Release -r win-x64 --self-contained true `
            /p:PublishSingleFile=true `
            /p:IncludeNativeLibrariesForSelfExtract=true `
            /p:EnableCompressionInSingleFile=true `
            /p:TraceSingleFileBundler=true

        $output = Join-Path $publishDir "JRBSI.exe"
        $dest = Join-Path $Root "JRBSI_v1.4.exe"
        if (-not (Test-Path $output)) {
            Write-Error "Publish succeeded but $output was not created."
        }

        if (-not (Test-DotNetSingleFileBundle $output)) {
            Write-Error "Published $output is not a valid single-file bundle. After UAC it would look for JRBSI.dll and exit."
        }

        Copy-Item -Path $output -Destination $dest -Force
        $sizeMb = [math]::Round((Get-Item $dest).Length / 1MB, 1)
        Write-Host ""
        Write-Host "Published: $dest ($sizeMb MB)" -ForegroundColor Green
        Write-Host "Launching will require a single UAC elevation (requireAdministrator manifest)."
    }
}
finally {
    Pop-Location
}
