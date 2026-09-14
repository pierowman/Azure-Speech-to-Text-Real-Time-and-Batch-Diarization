<#
.SYNOPSIS
	Deploys the Flask Speech-to-Text app to an Azure App Service (Linux, Python)
	using a zip deploy with Oryx remote build.

.DESCRIPTION
	Packages the application source (excluding virtual environments, secrets and
	caches), ensures the target Web App is configured for an Oryx remote build,
	performs an asynchronous zip deploy, waits for it to finish and then verifies
	the site responds with HTTP 200.

	Requires the Azure CLI (`az`) to be installed and signed in (`az login`).

.PARAMETER AppName
	Name of the target Azure Web App. Defaults to 'cbospeechtotextservice'.

.PARAMETER ResourceGroup
	Resource group containing the Web App. Defaults to 'cbospeechtotextservice_group'.

.PARAMETER SubscriptionId
	Optional. Azure subscription to target. If omitted, the current az default is used.

.PARAMETER EnvFile
	Path to the .env file whose KEY=VALUE pairs are pushed to the Web App as
	application settings. Defaults to '.env' next to this script.

.PARAMETER SkipAppSettings
	Do not sync application settings from the .env file (only the Oryx build
	flags are ensured).

.PARAMETER SkipVerify
	Skip the post-deploy HTTP health check.

.EXAMPLE
	./deploy.ps1

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
function Invoke-Az {
	param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Args)
	$output = & az @Args 2>&1 | Where-Object { $_ -notmatch 'UserWarning|cryptography' }
	if ($LASTEXITCODE -ne 0) {
		throw "az $($Args -join ' ') failed (exit $LASTEXITCODE):`n$($output -join "`n")"
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

# ---------------------------------------------------------------------------
# 0. Pre-flight checks
# ---------------------------------------------------------------------------
Write-Step "Checking prerequisites"

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
	throw "Azure CLI ('az') is not installed or not on PATH. Install it from https://aka.ms/azure-cli."
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
	[void]$settingArgs.Add('SCM_DO_BUILD_DURING_DEPLOYMENT=true')
	[void]$settingArgs.Add('ENABLE_ORYX_BUILD=true')

	$settingArray = $settingArgs.ToArray()
	Invoke-Az webapp config appsettings set `
		--name $AppName --resource-group $ResourceGroup `
		--settings @settingArray -o none | Out-Null
	Write-Ok "Applied $($settingArray.Count) application setting(s)."

	# -----------------------------------------------------------------------
	# 3. Deploy (async so a Kudu restart mid-build doesn't fail the CLI)
	# -----------------------------------------------------------------------
	Write-Step "Deploying to '$AppName' (this can take several minutes)"
	$deployJson = Invoke-Az webapp deploy `
		--name $AppName --resource-group $ResourceGroup `
		--src-path $zip --type zip `
		--track-status false --async true -o json
	$deploy = $deployJson | ConvertFrom-Json

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
