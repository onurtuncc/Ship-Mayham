[CmdletBinding()]
param(
    [string]$ExecutablePath,
    [ValidateRange(60, 300)][int]$TimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
if ([string]::IsNullOrWhiteSpace($ExecutablePath)) {
    $ExecutablePath = Join-Path $projectRoot "Builds\ShipPrototypeValidation\Builds\ShipPrototype\ShipPrototype.exe"
}
$ExecutablePath = [IO.Path]::GetFullPath($ExecutablePath)
if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
    throw "A current Windows development build is required: '$ExecutablePath'."
}
$runId = (Get-Date -Format "yyyyMMdd-HHmmss") + "-" + [Guid]::NewGuid().ToString("N").Substring(0, 6)
$output = Join-Path $projectRoot "Logs\RelayMigration-$runId"
New-Item -ItemType Directory -Path $output -Force | Out-Null
$processes = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$clients = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)

function Start-Participant([string]$Name, [bool]$HostRole) {
    $role = if ($HostRole) { "--ship-online-host" } else { "--ship-online-client" }
    $profile = "qa-" + $Name + "-" + $runId.Substring($runId.Length - 6)
    $arguments = @(
        "-batchmode", "-nographics", $role,
        "--ship-online-participant", $Name,
        "--ship-auth-profile", $profile,
        "--ship-online-output", "`"$output`"",
        "-logFile", "`"$(Join-Path $output ($Name + '.log'))`""
    )
    $instance = Start-Process -FilePath $ExecutablePath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $processes.Add($instance)
    return $instance
}

function Assert-WithinDeadline {
    if ([DateTime]::UtcNow -gt $deadline) { throw "Relay migration test timed out. Evidence: $output" }
}

try {
    Write-Host "Relay/WSS migration evidence: $output"
    $originalHost = Start-Participant "host" $true
    while (-not (Test-Path -LiteralPath (Join-Path $output "join-code.txt"))) {
        Assert-WithinDeadline
        if ($originalHost.HasExited) { throw "Original host exited before opening its Relay session. See host.log." }
        Start-Sleep -Milliseconds 250
    }
    foreach ($name in @("client1", "client2", "client3")) { $clients.Add((Start-Participant $name $false)) }
    $signals = @("host-ready.txt", "client1.ready", "client2.ready", "client3.ready")
    while (@($signals | Where-Object { -not (Test-Path -LiteralPath (Join-Path $output $_)) }).Count -gt 0) {
        Assert-WithinDeadline
        foreach ($instance in $processes) {
            if ($instance.HasExited) { throw "A participant exited before fault injection. See participant logs in $output." }
        }
        Start-Sleep -Milliseconds 250
    }

    # Only this script's original test process is terminated. No editor or user process is selected.
    Write-Host "All four players observed the probe. Terminating original test host PID $($originalHost.Id)."
    $originalHost.Kill()
    $originalHost.WaitForExit()
    Write-Host "Injected host exit code: $($originalHost.ExitCode) (expected forced termination)"

    $resultNames = @("client1.json", "client2.json", "client3.json")
    while (@($resultNames | Where-Object { -not (Test-Path -LiteralPath (Join-Path $output $_)) }).Count -gt 0) {
        Assert-WithinDeadline
        foreach ($instance in $clients) {
            if ($instance.HasExited) { throw "A client exited before reporting migration success. See logs in $output." }
        }
        Start-Sleep -Milliseconds 250
    }
    $results = @($resultNames | ForEach-Object { Get-Content -LiteralPath (Join-Path $output $_) -Raw | ConvertFrom-Json })
    if (@($results | Where-Object NewHost).Count -ne 1) { throw "Exactly one surviving player must become host." }
    foreach ($result in $results) {
        if ($result.OriginalSlot -ne $result.RestoredSlot -or $result.Migrations -lt 1 -or
            $result.Wallet -ne 321 -or $result.EventSerial -ne 777 -or $result.ActiveCrew -ne 3) {
            throw "Migration assertion failed for $($result.Participant)."
        }
    }
    foreach ($instance in $clients) {
        if (-not $instance.WaitForExit(15000) -or $instance.ExitCode -ne 0) { throw "A surviving client did not exit cleanly." }
    }
    $results | Format-Table Participant, NewHost, OriginalSlot, RestoredSlot, Migrations, Wallet, EventSerial, ActiveCrew -AutoSize
    Write-Host "SHIP_RELAY_MIGRATION_PASS — 3 surviving clients passed; original host was deliberately terminated."
}
finally {
    foreach ($instance in $processes) {
        if (-not $instance.HasExited) { $instance.Kill(); $instance.WaitForExit(5000) | Out-Null }
        $instance.Dispose()
    }
}
