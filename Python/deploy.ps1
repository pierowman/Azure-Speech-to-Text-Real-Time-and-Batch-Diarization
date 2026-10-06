<#
.SYNOPSIS
	Deploys the Flask Speech-to-Text app to an Azure App Service (Linux, Python).

	By default it builds a fully self-contained ("prebuilt") artifact: all Python
	dependencies are vendored into the zip as Linux wheels, and the Oryx remote
	build is DISABLED so nothing is pulled down at deploy time. Use -RemoteBuild
	to fall back to the classic Oryx remote-build behaviour instead.

.DESCRIPTION
	Prebuilt mode (default):
	  * Stages the application source (excluding virtual environments, secrets and
		caches).
	  * Downloads/installs every requirement as Linux (manylinux) wheels for the
		target Python version into '.python_packages/lib/site-packages' inside the
		package, using pip cross-platform download. No build runs on App Service.
	  * Sets SCM_DO_BUILD_DURING_DEPLOYMENT=false / ENABLE_ORYX_BUILD=false and a
		startup command that puts the vendored packages on PYTHONPATH.

	Remote-build mode (-RemoteBuild):
	  * Sets SCM_DO_BUILD_DURING_DEPLOYMENT=true / ENABLE_ORYX_BUILD=true and lets
		Oryx run 'pip install -r requirements.txt' during the deploy.

	Both modes then perform an asynchronous zip deploy, wait for it to finish and
	verify the site responds with HTTP 200.

	Requires the Azure CLI (`az`) signed in (`az login`). Prebuilt mode also
	requires a local `python` with pip (any recent version; it does not need to
	match the target Python version because wheels are cross-downloaded).

	NOTE: Prebuilt mode only works if every native dependency publishes a
	manylinux wheel for the target Python version/ABI. If a wheel is missing the
	script fails loudly; use -RemoteBuild (or Docker/CI) in that case.

.PARAMETER AppName
	Name of the target Azure Web App. Defaults to 'cbospeechtotextservice'.

.PARAMETER ResourceGroup
	Resource group containing the Web App. Defaults to 'cbospeechtotextservice_group'.

.PARAMETER SubscriptionId
	Optional. Azure subscription to target. If omitted, the current az default is used.

.PARAMETER EnvFile
	Path to the .env file whose KEY=VALUE pairs are pushed to the Web App as
	application settings. Defaults to '.env' next to this script.

.PARAMETER PythonVersion
	Target App Service Python version used to select Linux wheels in prebuilt
	mode (e.g. '3.14'). Must match the Web App's runtime. Ignored with -RemoteBuild.

.PARAMETER RemoteBuild
	Use the classic Oryx remote build (pip install on App Service during deploy)
	instead of shipping a prebuilt, self-contained artifact.

.PARAMETER SkipAppSettings
	Do not sync application settings from the .env file (build/runtime flags and,
	in prebuilt mode, the startup command are still ensured).

.PARAMETER SkipVerify
	Skip the post-deploy HTTP health check.

.EXAMPLE
	# Prebuilt, self-contained deploy (default) - nothing installed at deploy time
	./deploy.ps1

.EXAMPLE
	# Prebuilt for a specific Python runtime
	./deploy.ps1 -PythonVersion 3.12

.EXAMPLE
	# Fall back to Oryx remote build
	./deploy.ps1 -RemoteBuild

.EXAMPLE
	./deploy.ps1 -AppName my-app -ResourceGroup my-rg -SubscriptionId 00000000-0000-0000-0000-000000000000

.EXAMPLE
	./deploy.ps1 -EnvFile .\.env.production
#>

[CmdletBinding()]
param(
	[string]$AppName        = 'cbospeechtotextservice',
	[string]$ResourceGroup  = 'cbospeechtotextservice_group',
	[string]$SubscriptionId,
	[string]$EnvFile,
	[string]$PythonVersion  = '3.14',
	[switch]$RemoteBuild,
	[switch]$SkipAppSettings,
	[switch]$SkipVerify
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
function Write-Step   { param([string]$Message) Write-Host "`n==> $Message" -ForegroundColor Cyan }
function Write-Ok     { param([string]$Message) Write-Host "    $Message" -ForegroundColor Green }
function Write-Warn2  { param([string]$Message) Write-Host "    $Message" -ForegroundColor Yellow }

# Run an az command, filtering out the noisy 32-bit cryptography warning.
# Uses the automatic $args array (a simple, non-advanced function) so that
# short options like '-o json' are passed straight through to az instead of
# colliding with PowerShell common parameters (e.g. -OutVariable/-OutBuffer).
function Invoke-Az {
	$output = & az @args 2>&1 | Where-Object { $_ -notmatch 'UserWarning|cryptography' }
	if ($LASTEXITCODE -ne 0) {
		throw "az $($args -join ' ') failed (exit $LASTEXITCODE):`n$($output -join "`n")"
	}
	return $output
}

# Parse a .env file into an ordered [KEY]=VALUE dictionary.
# Skips blank lines and comments, honours an optional leading 'export ',
# splits on the first '=' and strips surrounding single/double quotes.
function ConvertFrom-EnvFile {
	param([string]$Path)

	$settings = [ordered]@{}
	foreach ($line in (Get-Content -LiteralPath $Path)) {
		$trimmed = $line.Trim()
		if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }
		if ($trimmed -match '^\s*export\s+') { $trimmed = $trimmed -replace '^\s*export\s+', '' }

		$idx = $trimmed.IndexOf('=')
		if ($idx -lt 1) { continue }

		$key   = $trimmed.Substring(0, $idx).Trim()
		$value = $trimmed.Substring($idx + 1).Trim()

		# Strip a single matching pair of surrounding quotes.
		if ($value.Length -ge 2 -and
			(($value.StartsWith('"') -and $value.EndsWith('"')) -or
			 ($value.StartsWith("'") -and $value.EndsWith("'")))) {
			$value = $value.Substring(1, $value.Length - 2)
		}

		if ($key) { $settings[$key] = $value }
	}
	return $settings
}

# Resolve the local Python launcher (python, then the 'py' launcher).
function Get-PythonCommand {
	foreach ($candidate in @('python', 'python3')) {
		$cmd = Get-Command $candidate -ErrorAction SilentlyContinue
		if ($cmd) { return @($cmd.Source) }
	}
	if (Get-Command py -ErrorAction SilentlyContinue) { return @('py', '-3') }
	return $null
}

# Produce a Linux-flavoured requirements file: drop Windows-only packages and
# strip 'platform_system' environment markers so the correct (Linux) packages
# are selected when cross-downloading wheels from a Windows host.
function New-LinuxRequirementsFile {
	param([string]$RequirementsPath, [string]$OutPath)

	$out = New-Object System.Collections.Generic.List[string]
	foreach ($line in (Get-Content -LiteralPath $RequirementsPath)) {
		$t = $line.Trim()
		if ($t -eq '' -or $t.StartsWith('#')) { continue }
		# Skip requirements that only apply on Windows (e.g. python-magic-bin).
		if ($t -match ';\s*platform_system\s*==\s*"?Windows"?') { continue }
		# Keep non-Windows requirements but drop the now-redundant marker.
		$t = ($t -replace ';\s*platform_system\s*!=\s*"?Windows"?\s*$', '').Trim()
		if ($t) { $out.Add($t) }
	}
	Set-Content -LiteralPath $OutPath -Value $out -Encoding ascii
}

# Cross-download every requirement as Linux (manylinux) wheels for the target
# Python version and unpack them into the vendored site-packages directory.
function Install-LinuxPackages {
	param(
		[string[]]$Python,
		[string]$RequirementsFile,
		[string]$TargetDir,
		[string]$PythonVersion
	)

	$abi = 'cp' + ($PythonVersion -replace '\.', '')   # e.g. 3.14 -> cp314
	New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

	$pipArgs = @(
		'-m', 'pip', 'install',
		'-r', $RequirementsFile,
		'--target', $TargetDir,
		'--upgrade',
		'--only-binary=:all:',
		'--python-version', $PythonVersion,
		'--implementation', 'cp',
		'--abi', $abi,
		'--platform', 'manylinux2014_x86_64',
		'--platform', 'manylinux_2_17_x86_64',
		'--platform', 'manylinux_2_28_x86_64'
	)

	# $Python is e.g. @('python') or @('py','-3'); combine so slicing is safe.
	$call = $Python + $pipArgs
	& $call[0] @($call[1..($call.Count - 1)])
	if ($LASTEXITCODE -ne 0) {
		throw @"
Failed to build Linux wheels for Python $PythonVersion.
A dependency is likely missing a manylinux wheel for this Python version/ABI ($abi).
Options:
  * Re-run with -RemoteBuild to let Oryx build on App Service, or
  * Build the artifact in Docker/CI against a matching Linux Python image, or
  * Target a Python version that has published wheels for all dependencies.
"@
	}
}

# ---------------------------------------------------------------------------
# 0. Pre-flight checks
# ---------------------------------------------------------------------------
Write-Step "Checking prerequisites"

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
	throw "Azure CLI ('az') is not installed or not on PATH. Install it from https://aka.ms/azure-cli."
}

# Prebuilt (default) mode cross-downloads Linux wheels, so a local Python+pip
# is required. Remote-build mode does not need Python locally.
$Python = $null
if (-not $RemoteBuild) {
	$Python = Get-PythonCommand
	if (-not $Python) {
		throw "Prebuilt mode needs a local Python (with pip) on PATH. Install Python, or run with -RemoteBuild."
	}
}

# The app source lives in the same directory as this script.
$SourceDir = $PSScriptRoot
if (-not (Test-Path (Join-Path $SourceDir 'app.py'))) {
	throw "Could not find app.py in '$SourceDir'. Run this script from the Python project folder."
}

try {
	$account = Invoke-Az account show -o json | ConvertFrom-Json
} catch {
	throw "You are not signed in to Azure. Run 'az login' first."
}

if ($SubscriptionId) {
	Write-Step "Setting subscription to $SubscriptionId"
	Invoke-Az account set --subscription $SubscriptionId | Out-Null
	$account = Invoke-Az account show -o json | ConvertFrom-Json
}
Write-Ok "Subscription: $($account.name) ($($account.id))"

# ---------------------------------------------------------------------------
# 1. Stage the application files (exclude venvs, secrets and caches)
# ---------------------------------------------------------------------------
Write-Step "Staging application files"

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ("sttdeploy_stage_{0}" -f ([guid]::NewGuid().ToString('N')))
$zip   = Join-Path ([System.IO.Path]::GetTempPath()) ("sttdeploy_{0}.zip"  -f ([guid]::NewGuid().ToString('N')))

$excludeDirs  = @('venv', '.venv', 'antenv', 'env', '__pycache__', '.git',
				  '.pytest_cache', '.mypy_cache', 'node_modules', '.vs', 'coverage')
$excludeFiles = @('.env', '*.pyc')

try {
	New-Item -ItemType Directory -Path $stage | Out-Null

	# robocopy exit codes 0-7 indicate success; 8+ indicate failure.
	$roboArgs = @($SourceDir, $stage, '/E', '/NFL', '/NDL', '/NJH', '/NJS', '/NP',
				  '/XD') + $excludeDirs + @('/XF') + $excludeFiles
	& robocopy @roboArgs | Out-Null
	if ($LASTEXITCODE -ge 8) {
		throw "robocopy failed while staging files (exit $LASTEXITCODE)."
	}

	if (Test-Path (Join-Path $stage '.env')) {
		throw "Safety check failed: .env was staged for deployment. Aborting."
	}

	$fileCount = (Get-ChildItem $stage -Recurse -File).Count
	Write-Ok "Staged $fileCount files"

	# Stamp build metadata so the running app can report exactly which code is
	# deployed (surfaced by the /version endpoint and the startup log line).
	$gitCommit = (& git -C $SourceDir rev-parse --short HEAD 2>$null)
	if (-not $gitCommit) { $gitCommit = 'unknown' }
	$buildInfo = [ordered]@{
		version   = '1.1.0'
		commit    = "$gitCommit".Trim()
		built_utc = (Get-Date).ToUniversalTime().ToString('o')
	} | ConvertTo-Json -Compress
	Set-Content -Path (Join-Path $stage 'build_info.json') -Value $buildInfo -Encoding utf8
	Write-Ok "Stamped build_info.json (commit $gitCommit)"

	# -----------------------------------------------------------------------
	# 1b. Prebuilt mode: vendor all dependencies as Linux wheels so nothing is
	#     pulled down at deploy time. They go into '.python_packages/lib/
	#     site-packages', which the startup command puts on PYTHONPATH.
	# -----------------------------------------------------------------------
	if (-not $RemoteBuild) {
		Write-Step "Vendoring Linux dependencies for Python $PythonVersion"

		$reqPath = Join-Path $SourceDir 'requirements.txt'
		if (-not (Test-Path -LiteralPath $reqPath)) {
			throw "Could not find requirements.txt in '$SourceDir' (required for prebuilt mode)."
		}

		$linuxReq     = Join-Path $stage '.requirements.linux.txt'
		$sitePackages = Join-Path $stage '.python_packages\lib\site-packages'
		New-LinuxRequirementsFile -RequirementsPath $reqPath -OutPath $linuxReq
		Install-LinuxPackages -Python $Python -RequirementsFile $linuxReq `
			-TargetDir $sitePackages -PythonVersion $PythonVersion

		# The transformed requirements file is only needed during the build.
		Remove-Item -LiteralPath $linuxReq -Force -ErrorAction SilentlyContinue

		$pkgCount = (Get-ChildItem $sitePackages -Directory -ErrorAction SilentlyContinue |
			Where-Object { $_.Name -notlike '*.dist-info' -and $_.Name -notlike '*.data' }).Count
		Write-Ok "Vendored dependencies into .python_packages ($pkgCount top-level package(s))."
	}

	Write-Step "Creating deployment package"
	Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
	$zipMb = [math]::Round((Get-Item $zip).Length / 1MB, 2)
	Write-Ok "Package: $zip ($zipMb MB)"

	# -----------------------------------------------------------------------
	# 2. Configure the Web App's application settings
	#    - App config from the .env file (config.py reads everything from the
	#      environment, so these must exist as App Service app settings).
	#    - FLASK_ENV=production so the production config/validation is used.
	#    - Oryx remote build flags so dependencies are installed on deploy.
	# -----------------------------------------------------------------------
	Write-Step "Configuring application settings"

	$settingArgs = [System.Collections.ArrayList]::new()

	# Resolve the .env file (parameter wins, otherwise the one next to the script).
	if (-not $EnvFile) { $EnvFile = Join-Path $SourceDir '.env' }

	if ($SkipAppSettings) {
		Write-Warn2 "Skipping .env sync (-SkipAppSettings)."
	} elseif (Test-Path -LiteralPath $EnvFile) {
		$envSettings = ConvertFrom-EnvFile -Path $EnvFile
		foreach ($key in $envSettings.Keys) {
			[void]$settingArgs.Add("$key=$($envSettings[$key])")
		}
		Write-Ok "Loaded $($envSettings.Count) setting(s) from $EnvFile"
	} else {
		Write-Warn2 "No .env file found at '$EnvFile' - only build/runtime defaults will be set."
	}

	# Defaults that should always be present (these override any .env duplicates
	# because az applies later --settings values last).
	[void]$settingArgs.Add('FLASK_ENV=production')
	if ($RemoteBuild) {
		# Let Oryx install requirements on the App Service during deploy.
		[void]$settingArgs.Add('SCM_DO_BUILD_DURING_DEPLOYMENT=true')
		[void]$settingArgs.Add('ENABLE_ORYX_BUILD=true')
	} else {
		# Prebuilt artifact: disable the remote build and point Python at the
		# vendored packages so imports (e.g. the speech SDK) resolve.
		[void]$settingArgs.Add('SCM_DO_BUILD_DURING_DEPLOYMENT=false')
		[void]$settingArgs.Add('ENABLE_ORYX_BUILD=false')
		[void]$settingArgs.Add('PYTHONPATH=/home/site/wwwroot/.python_packages/lib/site-packages')
	}

	$settingArray = $settingArgs.ToArray()
	Invoke-Az webapp config appsettings set `
		--name $AppName --resource-group $ResourceGroup `
		--settings @settingArray -o none | Out-Null
	Write-Ok "Applied $($settingArray.Count) application setting(s)."

	# In prebuilt mode there is no Oryx-generated startup, so set an explicit
	# startup command that runs gunicorn from the vendored packages via PYTHONPATH.
	if (-not $RemoteBuild -and -not $SkipAppSettings) {
		$startup = 'PYTHONPATH="/home/site/wwwroot/.python_packages/lib/site-packages:$PYTHONPATH" ' +
			'python -m gunicorn --bind=0.0.0.0:8000 --workers=4 --timeout=600 app:app'
		Invoke-Az webapp config set `
			--name $AppName --resource-group $ResourceGroup `
			--startup-file $startup -o none | Out-Null
		Write-Ok "Set startup command to run gunicorn from vendored packages."
	}

	# -----------------------------------------------------------------------
	# 3. Deploy (async so a Kudu restart mid-build doesn't fail the CLI)
	# -----------------------------------------------------------------------
	Write-Step "Deploying to '$AppName' (this can take several minutes)"
	$deployJson = Invoke-Az webapp deploy `
		--name $AppName --resource-group $ResourceGroup `
		--src-path $zip --type zip `
		--track-status false --async true -o json

	# 'az webapp deploy' can emit non-JSON warning/progress lines (e.g. the
	# 32-bit cryptography notice or a deployment status hint) ahead of the JSON
	# payload. Isolate the JSON object before parsing so a stray leading line
	# doesn't break ConvertFrom-Json.
	$deployText  = ($deployJson -join "`n")
	$jsonStart   = $deployText.IndexOfAny([char[]]@('{', '['))
	if ($jsonStart -ge 0) { $deployText = $deployText.Substring($jsonStart) }
	$deploy = $deployText | ConvertFrom-Json

	# Kudu deployment status: 4 = Success, 3 = Failed.
	if ($deploy.status -eq 4 -and $deploy.complete) {
		Write-Ok "Deployment completed successfully."
	} elseif ($deploy.status -eq 3) {
		$errs = if ($deploy.build_summary) { ($deploy.build_summary.errors -join "`n") } else { '' }
		throw "Deployment failed (Kudu status 3).`n$errs"
	} else {
		Write-Warn2 "Deployment reported status=$($deploy.status), complete=$($deploy.complete)."
	}
}
finally {
	# -----------------------------------------------------------------------
	# 4. Clean up local artifacts
	# -----------------------------------------------------------------------
	Remove-Item $zip   -Force            -ErrorAction SilentlyContinue
	Remove-Item $stage -Recurse -Force   -ErrorAction SilentlyContinue
}

# ---------------------------------------------------------------------------
# 5. Verify the site is serving
# ---------------------------------------------------------------------------
if ($SkipVerify) {
	Write-Step "Skipping post-deploy verification (-SkipVerify)"
	return
}

Write-Step "Verifying the site is responding"

$hostName = Invoke-Az webapp show --name $AppName --resource-group $ResourceGroup `
	--query defaultHostName -o tsv
$url = "https://$hostName/"

$maxAttempts = 12
$healthy = $false
for ($i = 1; $i -le $maxAttempts; $i++) {
	try {
		$resp = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 60
		if ($resp.StatusCode -eq 200) {
			Write-Ok "HTTP 200 from $url"
			$healthy = $true
			break
		}
		Write-Warn2 "Attempt $i/$maxAttempts : HTTP $($resp.StatusCode)"
	} catch {
		Write-Warn2 "Attempt $i/$maxAttempts : $($_.Exception.Message)"
	}
	Start-Sleep -Seconds 20
}

if (-not $healthy) {
	throw "Site did not return HTTP 200 after $maxAttempts attempts. Check the container logs:`n" +
		  "  az webapp log tail --name $AppName --resource-group $ResourceGroup"
}

Write-Step "Done"
Write-Ok "App is live at $url"
