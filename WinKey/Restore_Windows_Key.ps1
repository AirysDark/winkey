param(
    [string]$ProductKey
)

$ErrorActionPreference = 'Stop'
$slmgr = Join-Path $env:SystemRoot 'System32\slmgr.vbs'

function Get-EmbeddedWindowsKey {
    try {
        $key = (Get-CimInstance -ClassName SoftwareLicensingService -ErrorAction Stop).OA3xOriginalProductKey
        if ($key -and $key.Trim().Length -ge 25) {
            return $key.Trim()
        }
    }
    catch {
    }

    return $null
}

if ([string]::IsNullOrWhiteSpace($ProductKey)) {
    $ProductKey = Get-EmbeddedWindowsKey
}

if ([string]::IsNullOrWhiteSpace($ProductKey)) {
    exit 2
}

$ProductKey = $ProductKey.Trim()

try {
    & cscript.exe //NoLogo $slmgr /ipk $ProductKey | Out-Null
    if ($LASTEXITCODE -ne 0) {
        exit 10
    }

    & cscript.exe //NoLogo $slmgr /ato | Out-Null
    if ($LASTEXITCODE -ne 0) {
        exit 11
    }

    # Show the final activation result in Windows Script Host without opening CMD.
    Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\wscript.exe') -ArgumentList ('"{0}" /xpr' -f $slmgr) -Wait
    exit 0
}
catch {
    exit 12
}
