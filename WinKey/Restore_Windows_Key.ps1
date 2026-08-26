param(
    [Parameter(Mandatory = $true)]
    [string]$ProductKey
)

$ErrorActionPreference = 'Stop'
$slmgr = Join-Path $env:SystemRoot 'System32\slmgr.vbs'

# This script restores only the key explicitly supplied by WinKey.
# It must never fall back to the current PC's embedded/OEM key.
$ProductKey = $ProductKey.Trim()

if ($ProductKey -notmatch '^[A-Za-z0-9]{5}(-[A-Za-z0-9]{5}){4}$') {
    exit 2
}

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
