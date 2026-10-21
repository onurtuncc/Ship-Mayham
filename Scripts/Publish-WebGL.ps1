[CmdletBinding()]
param(
    [string]$UnityPath,
    [string]$Remote = "origin",
    [string]$PagesBranch = "gh-pages",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$versionFile = Join-Path $projectRoot "ProjectSettings\ProjectVersion.txt"
$versionLine = Select-String -LiteralPath $versionFile -Pattern '^m_EditorVersion:\s*(.+)$' | Select-Object -First 1

if (-not $versionLine) {
    throw "Unity version could not be read from $versionFile"
}

$unityVersion = $versionLine.Matches[0].Groups[1].Value.Trim()
if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $UnityPath = "C:\Program Files\Unity\Hub\Editor\$unityVersion\Editor\Unity.exe"
}

$UnityPath = [IO.Path]::GetFullPath($UnityPath)
if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity $unityVersion was not found at '$UnityPath'. Install Web Build Support in Unity Hub or pass -UnityPath."
}

$buildPath = Join-Path $projectRoot "Builds\WebGL"
$logPath = Join-Path $projectRoot "Logs\WebGLBuild.log"

if (-not $SkipBuild) {
    $buildProjectRoot = $projectRoot
    $usesBuildWorkspace = $false
    $projectLock = Join-Path $projectRoot "Temp\UnityLockfile"

    if (Test-Path -LiteralPath $projectLock -PathType Leaf) {
        $buildProjectRoot = Join-Path $projectRoot "Builds\ShipPrototypeValidation"
        New-Item -ItemType Directory -Path $buildProjectRoot -Force | Out-Null
        Write-Host "The main project is locked by Unity; syncing the cached build workspace..."

        foreach ($directoryName in @("Assets", "Packages", "ProjectSettings")) {
            $sourceDirectory = Join-Path $projectRoot $directoryName
            $targetDirectory = Join-Path $buildProjectRoot $directoryName
            & robocopy $sourceDirectory $targetDirectory /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
            if ($LASTEXITCODE -ge 8) {
                throw "Could not synchronize '$directoryName' to the build workspace (robocopy exit $LASTEXITCODE)."
            }
        }
        $usesBuildWorkspace = $true
    }

    New-Item -ItemType Directory -Path (Split-Path -Parent $logPath) -Force | Out-Null
    Write-Host "Building WebGL with Unity $unityVersion..."

    $unityArguments = @(
        "-batchmode",
        "-quit",
        "-nographics",
        "-projectPath", "`"$buildProjectRoot`"",
        "-executeMethod", "ShipMayham.Editor.WebGLBuild.Build",
        "-logFile", "`"$logPath`""
    )
    $unityProcess = Start-Process `
        -FilePath $UnityPath `
        -ArgumentList $unityArguments `
        -WindowStyle Hidden `
        -PassThru

    $unityProcess.WaitForExit()
    $unityExitCode = $unityProcess.ExitCode
    if ($unityExitCode -ne 0) {
        if (Test-Path -LiteralPath $logPath -PathType Leaf) {
            Write-Host "Unity build failed. Last log lines:"
            Get-Content -LiteralPath $logPath -Tail 80
        }
        else {
            Write-Host "Unity build failed before a log file was created."
        }
        throw "Unity exited with code $unityExitCode. Expected log: $logPath"
    }

    if ($usesBuildWorkspace) {
        $workspaceBuildPath = Join-Path $buildProjectRoot "Builds\WebGL"
        if (-not (Test-Path -LiteralPath (Join-Path $workspaceBuildPath "index.html") -PathType Leaf)) {
            throw "The cached Unity build did not produce '$workspaceBuildPath\index.html'."
        }

        if (Test-Path -LiteralPath $buildPath) {
            Remove-Item -LiteralPath $buildPath -Recurse -Force
        }
        New-Item -ItemType Directory -Path (Split-Path -Parent $buildPath) -Force | Out-Null
        Copy-Item -LiteralPath $workspaceBuildPath -Destination (Split-Path -Parent $buildPath) -Recurse -Force
    }
}

$requiredFiles = @(
    (Join-Path $buildPath "index.html"),
    (Join-Path $buildPath ".nojekyll")
)

foreach ($requiredFile in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
        throw "WebGL output is incomplete: '$requiredFile' is missing."
    }
}

if (-not (Get-ChildItem -LiteralPath (Join-Path $buildPath "Build") -Filter "*.loader.js" -File -ErrorAction SilentlyContinue)) {
    throw "WebGL output is incomplete: no Unity loader was found in '$buildPath\Build'."
}

& git -C $projectRoot fetch $Remote $PagesBranch
if ($LASTEXITCODE -ne 0) {
    throw "Could not fetch '$Remote/$PagesBranch'."
}

$worktreePath = Join-Path ([IO.Path]::GetTempPath()) ("ship-mayham-pages-" + [Guid]::NewGuid().ToString("N"))
$worktreeCreated = $false

try {
    & git -C $projectRoot worktree add --detach $worktreePath "$Remote/$PagesBranch"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create a temporary worktree for '$Remote/$PagesBranch'."
    }
    $worktreeCreated = $true

    Get-ChildItem -LiteralPath $worktreePath -Force |
        Where-Object { $_.Name -ne ".git" } |
        Remove-Item -Recurse -Force

    Copy-Item -Path (Join-Path $buildPath "*") -Destination $worktreePath -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $buildPath ".nojekyll") -Destination $worktreePath -Force

    & git -C $worktreePath add --all
    if ($LASTEXITCODE -ne 0) {
        throw "Could not stage the WebGL output."
    }

    & git -C $worktreePath diff --cached --quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "The published WebGL output is already up to date."
        return
    }
    if ($LASTEXITCODE -ne 1) {
        throw "Could not compare the WebGL output with '$PagesBranch'."
    }

    $commitMessage = "Publish WebGL $((Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss')) UTC"
    & git -C $worktreePath commit -m $commitMessage
    if ($LASTEXITCODE -ne 0) {
        throw "Could not commit the WebGL output."
    }

    & git -C $worktreePath push $Remote "HEAD:$PagesBranch"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not push the WebGL output to '$Remote/$PagesBranch'."
    }

    Write-Host "Published successfully: https://onurtuncc.github.io/Ship-Mayham/"
}
finally {
    if ($worktreeCreated) {
        & git -C $projectRoot worktree remove --force $worktreePath
    }
}
