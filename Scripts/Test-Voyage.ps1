[CmdletBinding()]
param([string]$ExecutablePath, [ValidateRange(60, 300)][int]$TimeoutSeconds = 180)

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Join-Path $projectRoot 'Builds\ShipPrototypeValidation\Builds\ShipPrototype\ShipPrototype.exe'
}
$ExecutablePath = [IO.Path]::GetFullPath($ExecutablePath)
if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) { throw "A current Windows development build is required: $ExecutablePath" }
$runId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
$output = Join-Path $projectRoot "Logs\VoyageSmoke-$runId"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$participants = [Collections.Generic.List[object]]::new()
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)

function Start-Participant([string]$Name, [bool]$HostRole) {
    $role = if ($HostRole) { '--ship-smoke-host' } else { '--ship-smoke-client' }
    $log = Join-Path $output "$Name.log"
    $instance = Start-Process -FilePath $ExecutablePath -ArgumentList @('-batchmode', '-nographics', $role, '-logFile', "`"$log`"") -WindowStyle Hidden -PassThru
    $entry = [pscustomobject]@{ Name = $Name; Process = $instance; Log = $log }
    $participants.Add($entry)
    return $entry
}

function Has-Marker([string]$Log, [string]$Marker) {
    if (-not (Test-Path -LiteralPath $Log)) { return $false }
    return [bool](Select-String -LiteralPath $Log -Pattern $Marker -SimpleMatch -Quiet -ErrorAction SilentlyContinue)
}

try {
    Write-Host "Four-player voyage evidence: $output"
    $hostEntry = Start-Participant 'host' $true
    while (-not (Has-Marker $hostEntry.Log 'SHIP_SMOKE_CONNECTED HOST')) {
        if ($hostEntry.Process.HasExited) { throw "Host exited before opening its test session: $($hostEntry.Log)" }
        if ([DateTime]::UtcNow -gt $deadline) { throw "Host startup timed out: $output" }
        Start-Sleep -Milliseconds 250
    }
    foreach ($name in @('client1', 'client2', 'client3')) { Start-Participant $name $false | Out-Null }
    while (@($participants | Where-Object { -not $_.Process.HasExited }).Count -gt 0) {
        foreach ($entry in $participants) {
            if ($entry.Process.HasExited -and $entry.Process.ExitCode -ne 0) { throw "$($entry.Name) exited with $($entry.Process.ExitCode): $($entry.Log)" }
        }
        if ([DateTime]::UtcNow -gt $deadline) { throw "Voyage test timed out: $output" }
        Start-Sleep -Milliseconds 250
    }
    $supplied = $false
    foreach ($entry in $participants) {
        if ($entry.Process.ExitCode -ne 0 -or -not (Has-Marker $entry.Log 'SHIP_SMOKE_PASS')) {
            throw "Missing successful exit/marker for $($entry.Name): $($entry.Log)"
        }
        if (Has-Marker $entry.Log 'SHIP_SMOKE_AMMO_SUPPLY CLIENT delivered=2') { $supplied = $true }
        Write-Host "$($entry.Name): exit 0, SHIP_SMOKE_PASS"
    }
    if (-not $supplied) { throw "No remote participant completed physical ammunition delivery: $output" }
    Write-Host 'SHIP_VOYAGE_TEST_PASS — 4 processes passed voyage, remote ammunition, combat, passenger and harbor checks.'
}
finally {
    foreach ($entry in $participants) {
        if (-not $entry.Process.HasExited) { $entry.Process.Kill(); $entry.Process.WaitForExit(5000) | Out-Null }
        $entry.Process.Dispose()
    }
}
