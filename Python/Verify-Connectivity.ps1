<#
.SYNOPSIS
	Verifies connectivity and that the correct permissions (RBAC) are configured
	across the three Azure resources this app depends on: the App Service, the
	Speech (Cognitive Services) resource, and the Blob Storage account.

.DESCRIPTION
	This is a READ-ONLY diagnostic. It makes no changes to any resource. It
	reads its configuration from the same .env file the app uses (so it checks
	exactly what the app will use at runtime) and then validates, for each
	resource:

	  * Existence / provisioning state of the resource.
	  * Network reachability of the data-plane endpoint (TCP 443 + optional HTTP).
	  * The RBAC role assignments the app relies on:
		  - App Service managed identity  -> Speech  : "Cognitive Services Speech User"
		  - App Service managed identity  -> Storage : "Storage Blob Data Contributor"
		  - Speech managed identity        -> Storage : "Storage Blob Data Reader"

	It prints a summary table and exits non-zero if any CRITICAL check fails,
	so it can be used in CI / smoke tests.

	Requires the Azure CLI ('az') signed in ('az login'). Works against both
	'AzureCloud' and 'AzureUSGovernment' (driven by AZURE_CLOUD in the .env).

.PARAMETER EnvFile
	Path to the .env file to read configuration from. Defaults to '.env' next
	to this script.

.PARAMETER AppName
	Name of the App Service (Web App). Defaults to 'cbospeechtotextservice'.

.PARAMETER ResourceGroup
	Resource group containing the App Service. Defaults to
	'cbospeechtotextservice_group'. (The Speech and Storage resources are looked
	up in the resource group from the .env file, i.e. AZURE_RESOURCE_GROUP.)

.PARAMETER SubscriptionId
	Optional. Azure subscription to target. If omitted the .env
	AZURE_SUBSCRIPTION_ID is used, otherwise the current az default.

.PARAMETER SkipAppService
	Skip all App Service checks (useful when running locally before deploy).

.PARAMETER SkipHttpCheck
	Skip the public HTTP 200 check against the App Service home page.

.EXAMPLE
	./Verify-Connectivity.ps1

.EXAMPLE
	./Verify-Connectivity.ps1 -EnvFile .\.env.production -AppName my-app -ResourceGroup my-rg

.EXAMPLE
	# Validate configuration locally before the app is deployed
	./Verify-Connectivity.ps1 -SkipAppService
#>

[CmdletBinding()]
param(
	[string]$EnvFile,
	[string]$AppName       = 'cbospeechtotextservice',
	[string]$ResourceGroup = 'cbospeechtotextservice_group',
	[string]$SubscriptionId,
	[switch]$SkipAppService,
	[switch]$SkipHttpCheck
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ---------------------------------------------------------------------------
# Cloud-specific suffixes / audiences (mirrors Python/config.py _CLOUD_CONFIG)
# ---------------------------------------------------------------------------
$CloudConfig = @{
	'AzureCloud' = @{
		storage_suffix            = 'blob.core.windows.net'
		storage_audience          = 'https://storage.azure.com'
		cognitive_audience        = 'https://cognitiveservices.azure.com'
		cognitive_endpoint_suffix = 'cognitiveservices.azure.com'
		speech_host_suffix        = 'stt.speech.microsoft.com'
	}
	'AzureUSGovernment' = @{
		storage_suffix            = 'blob.core.usgovcloudapi.net'
		storage_audience          = 'https://storage.azure.us'
		cognitive_audience        = 'https://cognitiveservices.azure.us'
		cognitive_endpoint_suffix = 'cognitiveservices.azure.us'
		speech_host_suffix        = 'stt.speech.azure.us'
	}
}

# Role sets that actually grant the required DATA-plane access.
$SpeechDataRoles  = @('Cognitive Services Speech User', 'Cognitive Services User')
$StorageWriteRoles = @('Storage Blob Data Contributor', 'Storage Blob Data Owner')
$StorageReadRoles  = @('Storage Blob Data Reader', 'Storage Blob Data Contributor', 'Storage Blob Data Owner')
# Management-plane roles that are commonly (but incorrectly) assumed to grant
# data-plane access. We surface these as WARN rather than PASS.
$MgmtOnlyRoles = @('Owner', 'Contributor')

# ---------------------------------------------------------------------------
# Output helpers + result collection
# ---------------------------------------------------------------------------
$script:Results = [System.Collections.Generic.List[object]]::new()

function Write-Step { param([string]$Message) Write-Host "`n==> $Message" -ForegroundColor Cyan }

function Add-Result {
	param(
		[string]$Category,
		[string]$Check,
		[ValidateSet('PASS', 'FAIL', 'WARN', 'SKIP')][string]$Status,
		[string]$Detail = '',
		[switch]$Critical
	)
	$color = switch ($Status) {
		'PASS' { 'Green' }; 'FAIL' { 'Red' }; 'WARN' { 'Yellow' }; 'SKIP' { 'DarkGray' }
	}
	$tag = "[$Status]".PadRight(7)
	Write-Host "    $tag $Check" -ForegroundColor $color
	if ($Detail) { Write-Host "            $Detail" -ForegroundColor DarkGray }
	$script:Results.Add([pscustomobject]@{
		Category = $Category; Check = $Check; Status = $Status
		Detail = $Detail; Critical = [bool]$Critical
	})
}

# Run an az command, filtering the noisy 32-bit cryptography warning. Returns
# stdout lines; throws on non-zero exit.
function Invoke-Az {
	$output = & az @args 2>&1 | Where-Object { $_ -notmatch 'UserWarning|cryptography' }
	if ($LASTEXITCODE -ne 0) {
		throw "az $($args -join ' ') failed (exit $LASTEXITCODE):`n$($output -join "`n")"
	}
	return $output
}

# Same as Invoke-Az but returns $null instead of throwing (for existence probes).
function Try-Az {
	$output = & az @args 2>&1 | Where-Object { $_ -notmatch 'UserWarning|cryptography' }
	if ($LASTEXITCODE -ne 0) { return $null }
	return $output
}

# Parse a .env file into a hashtable. Skips blanks/comments, honours 'export ',
# splits on first '=', strips surrounding quotes.
function ConvertFrom-EnvFile {
	param([string]$Path)
	$settings = @{}
	foreach ($line in (Get-Content -LiteralPath $Path)) {
		$trimmed = $line.Trim()
		if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }
		if ($trimmed -match '^\s*export\s+') { $trimmed = $trimmed -replace '^\s*export\s+', '' }
		$idx = $trimmed.IndexOf('=')
		if ($idx -lt 1) { continue }
		$key = $trimmed.Substring(0, $idx).Trim()
		$value = $trimmed.Substring($idx + 1).Trim()
		if ($value.Length -ge 2 -and
			(($value.StartsWith('"') -and $value.EndsWith('"')) -or
			 ($value.StartsWith("'") -and $value.EndsWith("'")))) {
			$value = $value.Substring(1, $value.Length - 2)
		}
		if ($key) { $settings[$key] = $value }
	}
	return $settings
}

# Portable TCP port reachability test (works on Windows PowerShell and pwsh/Linux).
function Test-TcpPort {
	param([string]$ComputerName, [int]$Port = 443, [int]$TimeoutMs = 5000)
	$client = [System.Net.Sockets.TcpClient]::new()
	try {
		$iar = $client.BeginConnect($ComputerName, $Port, $null, $null)
		if (-not $iar.AsyncWaitHandle.WaitOne($TimeoutMs)) { return $false }
		$client.EndConnect($iar)
		return $true
	} catch {
		return $false
	} finally {
		$client.Close()
	}
}

# Return the list of role definition names assigned to $PrincipalId at (or above,
# via --include-inherited) the given $Scope. Empty array if none / on error.
function Get-AssignedRoles {
	param([string]$PrincipalId, [string]$Scope)
	if (-not $PrincipalId -or -not $Scope) { return @() }
	$json = Try-Az role assignment list --assignee $PrincipalId --scope $Scope `
		--include-inherited --query "[].roleDefinitionName" -o json
	if (-not $json) { return @() }
	try { return @(($json -join "`n") | ConvertFrom-Json) } catch { return @() }
}

# Evaluate role assignments for a data-plane need and record a result.
function Test-DataPlaneRoles {
	param(
		[string]$Category, [string]$Check,
		[string]$PrincipalId, [string]$PrincipalDesc, [string]$Scope,
		[string[]]$AcceptRoles
	)
	if (-not $PrincipalId) {
		Add-Result -Category $Category -Check $Check -Status 'WARN' `
			-Detail "Could not determine the $PrincipalDesc principal ID; cannot verify the role."
		return
	}
	$assigned = Get-AssignedRoles -PrincipalId $PrincipalId -Scope $Scope
	$match = $assigned | Where-Object { $AcceptRoles -contains $_ }
	if ($match) {
		Add-Result -Category $Category -Check $Check -Status 'PASS' `
			-Detail "$PrincipalDesc has: $($match -join ', ')"
		return
	}
	$mgmt = $assigned | Where-Object { $MgmtOnlyRoles -contains $_ }
	if ($mgmt) {
		Add-Result -Category $Category -Check $Check -Status 'WARN' -Critical `
			-Detail ("$PrincipalDesc has management role(s) '$($mgmt -join ', ')' but NOT a data-plane role " +
					 "($($AcceptRoles -join ' / ')). Management roles do NOT grant data access.")
		return
	}
	$have = if ($assigned.Count) { "Found only: $($assigned -join ', ')" } else { 'No role assignments found at this scope.' }
	Add-Result -Category $Category -Check $Check -Status 'FAIL' -Critical `
		-Detail ("$PrincipalDesc is missing a required role ($($AcceptRoles -join ' / ')) at scope $Scope. $have")
}

# ===========================================================================
# 0. Load configuration
# ===========================================================================
Write-Step 'Loading configuration'

if (-not $EnvFile) { $EnvFile = Join-Path $PSScriptRoot '.env' }
if (-not (Test-Path -LiteralPath $EnvFile)) {
	throw "Config file not found: '$EnvFile'. Pass -EnvFile or create one from .env.example."
}
$cfg = ConvertFrom-EnvFile -Path $EnvFile
Write-Host "    Using config: $EnvFile" -ForegroundColor DarkGray

function Get-Cfg { param([string]$Key, [string]$Default = '') if ($cfg.ContainsKey($Key) -and $cfg[$Key]) { return $cfg[$Key] } return $Default }

$cloudName       = Get-Cfg 'AZURE_CLOUD' 'AzureCloud'
if (-not $CloudConfig.ContainsKey($cloudName)) { $cloudName = 'AzureCloud' }
$cloud           = $CloudConfig[$cloudName]

$speechRegion    = Get-Cfg 'AZURE_SPEECH_REGION'
$subFromEnv      = Get-Cfg 'AZURE_SUBSCRIPTION_ID'
$speechRg        = Get-Cfg 'AZURE_RESOURCE_GROUP'
$speechName      = Get-Cfg 'AZURE_SPEECH_RESOURCE_NAME'
$storageAccount  = Get-Cfg 'AZURE_STORAGE_ACCOUNT_NAME'
$containerName   = Get-Cfg 'AZURE_STORAGE_CONTAINER_NAME' 'speech-transcriptions'
$enableBlob      = (Get-Cfg 'ENABLE_BLOB_STORAGE' 'false').ToLower() -eq 'true'
$appClientId     = Get-Cfg 'AZURE_CLIENT_ID'   # set only for user-assigned MI

if (-not $SubscriptionId) { $SubscriptionId = $subFromEnv }

Write-Host "    Cloud: $cloudName | Speech region: $speechRegion" -ForegroundColor DarkGray
Write-Host "    Speech resource: $speechName (rg: $speechRg)" -ForegroundColor DarkGray
Write-Host "    Storage account: $(if($storageAccount){$storageAccount}else{'<none>'}) (blob enabled: $enableBlob)" -ForegroundColor DarkGray

# ===========================================================================
# 1. Prerequisites: az present + signed in + subscription selected
# ===========================================================================
Write-Step 'Checking prerequisites'

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
	throw "Azure CLI ('az') is not installed or not on PATH. Install it from https://aka.ms/azure-cli."
}
$acct = Try-Az account show -o json
if (-not $acct) { throw "You are not signed in to Azure. Run 'az login' first." }

if ($SubscriptionId) {
	Invoke-Az account set --subscription $SubscriptionId | Out-Null
	$acct = Invoke-Az account show -o json
}
$account = ($acct -join "`n") | ConvertFrom-Json
Add-Result -Category 'Prereq' -Check 'Azure CLI signed in' -Status 'PASS' `
	-Detail "Subscription: $($account.name) ($($account.id))"
$subId = $account.id

# ===========================================================================
# 2. App Service
# ===========================================================================
$appPrincipalId = $null
if ($SkipAppService) {
	Add-Result -Category 'App Service' -Check 'App Service checks' -Status 'SKIP' -Detail '-SkipAppService specified.'
} else {
	Write-Step "Checking App Service '$AppName'"

	$appJson = Try-Az webapp show --name $AppName --resource-group $ResourceGroup -o json
	if (-not $appJson) {
		Add-Result -Category 'App Service' -Check 'App Service exists' -Status 'FAIL' -Critical `
			-Detail "Web App '$AppName' not found in resource group '$ResourceGroup'."
	} else {
		$app = ($appJson -join "`n") | ConvertFrom-Json
		Add-Result -Category 'App Service' -Check 'App Service exists' -Status 'PASS' `
			-Detail "State: $($app.state); Host: $($app.defaultHostName)"

		if ($app.state -ne 'Running') {
			Add-Result -Category 'App Service' -Check 'App Service running' -Status 'WARN' `
				-Detail "State is '$($app.state)' (expected 'Running')."
		}

		# ---- Managed identity used by the app ----
		if ($appClientId) {
			# User-assigned MI: its service principal objectId == principalId.
			$spId = Try-Az ad sp show --id $appClientId --query id -o tsv
			if ($spId) {
				$appPrincipalId = ($spId | Select-Object -First 1).Trim()
				Add-Result -Category 'App Service' -Check 'App identity (user-assigned)' -Status 'PASS' `
					-Detail "AZURE_CLIENT_ID=$appClientId -> principalId $appPrincipalId"
			} else {
				Add-Result -Category 'App Service' -Check 'App identity (user-assigned)' -Status 'FAIL' -Critical `
					-Detail "AZURE_CLIENT_ID=$appClientId does not resolve to a managed identity service principal."
			}
			# Confirm it is actually assigned to the web app.
			$assignedIds = Try-Az webapp identity show --name $AppName --resource-group $ResourceGroup `
				--query "userAssignedIdentities" -o json
			if (-not $assignedIds -or (($assignedIds -join '') -notmatch [regex]::Escape($appClientId))) {
				Add-Result -Category 'App Service' -Check 'User-assigned identity attached' -Status 'WARN' `
					-Detail "The user-assigned identity for AZURE_CLIENT_ID may not be attached to the Web App."
			}
		} else {
			# System-assigned MI.
			$saPrincipal = Try-Az webapp identity show --name $AppName --resource-group $ResourceGroup `
				--query principalId -o tsv
			if ($saPrincipal) {
				$appPrincipalId = ($saPrincipal | Select-Object -First 1).Trim()
				Add-Result -Category 'App Service' -Check 'App identity (system-assigned)' -Status 'PASS' `
					-Detail "principalId $appPrincipalId"
			} else {
				Add-Result -Category 'App Service' -Check 'App identity (system-assigned)' -Status 'FAIL' -Critical `
					-Detail "No system-assigned managed identity enabled and no AZURE_CLIENT_ID set. The app cannot authenticate to Azure."
			}
		}

		# ---- Public HTTP reachability ----
		if ($SkipHttpCheck) {
			Add-Result -Category 'App Service' -Check 'HTTP home page' -Status 'SKIP' -Detail '-SkipHttpCheck specified.'
		} elseif ($app.defaultHostName) {
			$url = "https://$($app.defaultHostName)/"
			try {
				$resp = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 60
				if ($resp.StatusCode -eq 200) {
					Add-Result -Category 'App Service' -Check 'HTTP home page' -Status 'PASS' -Detail "HTTP 200 from $url"
				} else {
					Add-Result -Category 'App Service' -Check 'HTTP home page' -Status 'WARN' -Detail "HTTP $($resp.StatusCode) from $url"
				}
			} catch {
				Add-Result -Category 'App Service' -Check 'HTTP home page' -Status 'WARN' `
					-Detail "Could not reach $url : $($_.Exception.Message)"
			}
		}
	}
}

# ===========================================================================
# 3. Speech (Cognitive Services) resource
# ===========================================================================
Write-Step "Checking Speech resource '$speechName'"

$speechPrincipalId = $null
$speechScope = $null

if (-not $speechName -or -not $speechRg) {
	Add-Result -Category 'Speech' -Check 'Speech config present' -Status 'FAIL' -Critical `
		-Detail 'AZURE_SPEECH_RESOURCE_NAME and/or AZURE_RESOURCE_GROUP missing from the .env file.'
} else {
	$speechJson = Try-Az cognitiveservices account show --name $speechName --resource-group $speechRg -o json
	if (-not $speechJson) {
		Add-Result -Category 'Speech' -Check 'Speech resource exists' -Status 'FAIL' -Critical `
			-Detail "Cognitive Services account '$speechName' not found in resource group '$speechRg'."
	} else {
		$speech = ($speechJson -join "`n") | ConvertFrom-Json
		$speechScope = $speech.id
		$prov = $speech.properties.provisioningState
		Add-Result -Category 'Speech' -Check 'Speech resource exists' -Status $(if ($prov -eq 'Succeeded') { 'PASS' } else { 'WARN' }) `
			-Detail "Kind: $($speech.kind); provisioningState: $prov; endpoint: $($speech.properties.endpoint)"

		# Region sanity vs .env
		if ($speechRegion -and $speech.location -and ($speech.location -ne $speechRegion)) {
			Add-Result -Category 'Speech' -Check 'Region matches .env' -Status 'WARN' `
				-Detail "Resource location '$($speech.location)' != AZURE_SPEECH_REGION '$speechRegion'."
		}

		# The Speech resource's own managed identity (used to READ blobs for batch).
		if ($speech.PSObject.Properties.Name -contains 'identity' -and $speech.identity -and $speech.identity.principalId) {
			$speechPrincipalId = $speech.identity.principalId
			Add-Result -Category 'Speech' -Check 'Speech managed identity' -Status 'PASS' `
				-Detail "principalId $speechPrincipalId (needed to read batch audio from storage)."
		} else {
			Add-Result -Category 'Speech' -Check 'Speech managed identity' -Status 'WARN' `
				-Detail 'Speech resource has no system-assigned managed identity; batch transcription cannot read blobs via Entra ID.'
		}

		# ---- Endpoint connectivity ----
		$speechEndpointHost = "$speechName.$($cloud.cognitive_endpoint_suffix)"
		if (Test-TcpPort -ComputerName $speechEndpointHost -Port 443) {
			Add-Result -Category 'Speech' -Check 'Speech endpoint reachable' -Status 'PASS' -Detail "TCP 443 open to $speechEndpointHost"
		} else {
			Add-Result -Category 'Speech' -Check 'Speech endpoint reachable' -Status 'FAIL' -Critical `
				-Detail "Cannot reach $speechEndpointHost:443 (DNS/firewall/private-endpoint?)."
		}

		# ---- Real-time Speech host connectivity (used by ConversationTranscriber) ----
		if ($speechRegion) {
			$rtHost = "$speechRegion.$($cloud.speech_host_suffix)"
			if (Test-TcpPort -ComputerName $rtHost -Port 443) {
				Add-Result -Category 'Speech' -Check 'Real-time speech host reachable' -Status 'PASS' -Detail "TCP 443 open to $rtHost"
			} else {
				Add-Result -Category 'Speech' -Check 'Real-time speech host reachable' -Status 'FAIL' -Critical `
					-Detail "Cannot reach real-time host $rtHost:443. Real-time transcription will fail to connect."
			}
		}
	}
}

# ---- RBAC: App identity must be a Speech data-plane user ----
if ($speechScope) {
	Test-DataPlaneRoles -Category 'Speech' -Check 'App identity -> Speech role' `
		-PrincipalId $appPrincipalId -PrincipalDesc 'App Service identity' `
		-Scope $speechScope -AcceptRoles $SpeechDataRoles
}

# ===========================================================================
# 4. Blob Storage
# ===========================================================================
Write-Step 'Checking Blob Storage'

if (-not $enableBlob -and -not $storageAccount) {
	Add-Result -Category 'Storage' -Check 'Blob storage checks' -Status 'SKIP' `
		-Detail 'ENABLE_BLOB_STORAGE=false and no storage account configured (batch transcription disabled).'
} elseif (-not $storageAccount) {
	Add-Result -Category 'Storage' -Check 'Storage config present' -Status 'FAIL' -Critical `
		-Detail 'ENABLE_BLOB_STORAGE=true but AZURE_STORAGE_ACCOUNT_NAME is missing from the .env file.'
} else {
	# Locate the storage account (search the sub; it may be in a different RG).
	$stgJson = Try-Az storage account show --name $storageAccount -o json
	if (-not $stgJson) {
		$stgId = Try-Az resource list --name $storageAccount --resource-type 'Microsoft.Storage/storageAccounts' --query "[0].id" -o tsv
		if ($stgId) { $stgJson = Try-Az storage account show --ids ($stgId.Trim()) -o json }
	}

	if (-not $stgJson) {
		Add-Result -Category 'Storage' -Check 'Storage account exists' -Status 'FAIL' -Critical `
			-Detail "Storage account '$storageAccount' not found in subscription."
	} else {
		$stg = ($stgJson -join "`n") | ConvertFrom-Json
		$storageScope = $stg.id
		Add-Result -Category 'Storage' -Check 'Storage account exists' -Status 'PASS' `
			-Detail "Kind: $($stg.kind); provisioningState: $($stg.provisioningState)"

		# ---- Endpoint connectivity ----
		$blobHost = "$storageAccount.$($cloud.storage_suffix)"
		if (Test-TcpPort -ComputerName $blobHost -Port 443) {
			Add-Result -Category 'Storage' -Check 'Blob endpoint reachable' -Status 'PASS' -Detail "TCP 443 open to $blobHost"
		} else {
			Add-Result -Category 'Storage' -Check 'Blob endpoint reachable' -Status 'FAIL' -Critical `
				-Detail "Cannot reach $blobHost:443 (DNS/firewall/private-endpoint?)."
		}

		# ---- Container existence (data-plane, via the signed-in principal) ----
		$exists = Try-Az storage container exists --account-name $storageAccount --name $containerName `
			--auth-mode login --query exists -o tsv
		if ($null -ne $exists) {
			if ("$exists".Trim() -eq 'true') {
				Add-Result -Category 'Storage' -Check "Container '$containerName'" -Status 'PASS' -Detail 'Container exists.'
			} else {
				Add-Result -Category 'Storage' -Check "Container '$containerName'" -Status 'WARN' `
					-Detail 'Container does not exist yet; the app creates it on first batch upload (needs write role).'
			}
		} else {
			Add-Result -Category 'Storage' -Check "Container '$containerName' (data-plane access)" -Status 'WARN' `
				-Detail 'Could not query the container with your sign-in. This checks YOUR access, not the app identity; verify your own data role if needed.'
		}

		# ---- RBAC: App identity must be able to WRITE blobs ----
		Test-DataPlaneRoles -Category 'Storage' -Check 'App identity -> Storage (write)' `
			-PrincipalId $appPrincipalId -PrincipalDesc 'App Service identity' `
			-Scope $storageScope -AcceptRoles $StorageWriteRoles

		# ---- RBAC: Speech identity must be able to READ blobs (batch) ----
		Test-DataPlaneRoles -Category 'Storage' -Check 'Speech identity -> Storage (read)' `
			-PrincipalId $speechPrincipalId -PrincipalDesc 'Speech resource identity' `
			-Scope $storageScope -AcceptRoles $StorageReadRoles
	}
}

# ===========================================================================
# 5. Summary
# ===========================================================================
Write-Step 'Summary'

$script:Results | Select-Object Category, Check, Status, Detail |
	Format-Table -AutoSize -Wrap | Out-Host

$pass = @($script:Results | Where-Object Status -eq 'PASS').Count
$warn = @($script:Results | Where-Object Status -eq 'WARN').Count
$fail = @($script:Results | Where-Object Status -eq 'FAIL').Count
$skip = @($script:Results | Where-Object Status -eq 'SKIP').Count
$criticalFailures = @($script:Results | Where-Object { $_.Critical -and ($_.Status -eq 'FAIL' -or $_.Status -eq 'WARN') }).Count

Write-Host ''
Write-Host ("PASS: {0}   WARN: {1}   FAIL: {2}   SKIP: {3}" -f $pass, $warn, $fail, $skip) -ForegroundColor White

if ($criticalFailures -gt 0) {
	Write-Host "`nResult: FAILED - $criticalFailures critical issue(s) found. See FAIL/WARN rows above." -ForegroundColor Red
	exit 1
} elseif ($warn -gt 0) {
	Write-Host "`nResult: PASSED WITH WARNINGS - review WARN rows above." -ForegroundColor Yellow
	exit 0
} else {
	Write-Host "`nResult: PASSED - connectivity and permissions look correctly configured." -ForegroundColor Green
	exit 0
}
