#Requires -Version 5.1
$ErrorActionPreference = "Stop"

function Get-PredictorRepoRoot {
    Split-Path -Parent $PSScriptRoot
}

function Get-OllamaConfig {
    $repoRoot = Get-PredictorRepoRoot
    $appDir = Join-Path $repoRoot "src\Predictor"
    $basePath = Join-Path $appDir "appsettings.json"
    if (-not (Test-Path -LiteralPath $basePath)) {
        throw "appsettings.json not found: $basePath"
    }

    $config = Get-Content -LiteralPath $basePath -Raw -Encoding UTF8 | ConvertFrom-Json
    $configHash = ConvertTo-Hashtable $config

    $envName = $env:ASPNETCORE_ENVIRONMENT
    if ([string]::IsNullOrWhiteSpace($envName)) {
        $envName = "Development"
    }

    $overlayPath = Join-Path $appDir ("appsettings.{0}.json" -f $envName)
    if (Test-Path -LiteralPath $overlayPath) {
        $overlay = Get-Content -LiteralPath $overlayPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $configHash = Merge-Hashtable $configHash (ConvertTo-Hashtable $overlay)
    }

    if (-not $configHash.ContainsKey("Ollama")) {
        throw "Section 'Ollama' is missing in appsettings.json"
    }

    $ollama = $configHash["Ollama"]
    if (-not $ollama.ContainsKey("Agents") -or $null -eq $ollama["Agents"]) {
        throw "Section 'Ollama:Agents' is missing in appsettings.json"
    }

    $agentsRaw = $ollama["Agents"]
    $agents = @{}
    foreach ($key in $agentsRaw.Keys) {
        $item = $agentsRaw[$key]
        $baseModel = Get-RequiredJsonString $item["BaseModel"] "Ollama:Agents:$key`:BaseModel"
        $agents[$key] = [pscustomobject]@{
            Key                      = $key
            BaseUrl                  = (Get-JsonString $item["BaseUrl"] "http://localhost:11434").TrimEnd("/")
            BaseModel                = $baseModel
            AgentName                = Get-JsonString $item["AgentName"] $baseModel
            Temperature              = Get-JsonNumber $item["Temperature"] 0.2
            NumCtx                   = [int](Get-JsonNumber $item["NumCtx"] 8192)
            SystemPrompt             = Get-JsonString $item["SystemPrompt"] ""
            AllowUrlFetch            = Get-JsonBool $item["AllowUrlFetch"] $false
            UrlFetchMaxBytes         = [int](Get-JsonNumber $item["UrlFetchMaxBytes"] 65536)
            UrlFetchTimeoutSeconds   = [int](Get-JsonNumber $item["UrlFetchTimeoutSeconds"] 15)
            AllowLocalhostUrlFetch   = Get-JsonBool $item["AllowLocalhostUrlFetch"] $false
        }
    }

    return [pscustomobject]@{
        RepoRoot       = $repoRoot
        AppSettingsDir = $appDir
        Agents         = $agents
    }
}

function Get-OllamaAgent {
    param(
        [Parameter(Mandatory = $true)]$Config,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($Config.Agents.ContainsKey($Name)) {
        return $Config.Agents[$Name]
    }

    foreach ($key in $Config.Agents.Keys) {
        if ([string]::Equals($key, $Name, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $Config.Agents[$key]
        }
    }

    $known = @($Config.Agents.Keys) -join ", "
    throw "Ollama agent '$Name' is not configured. Known agents: $known"
}

function Get-JsonString {
    param($Value, [string]$Default)
    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) {
        return $Default
    }
    return [string]$Value
}

function Get-RequiredJsonString {
    param($Value, [string]$Path)
    $text = Get-JsonString $Value ""
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "Missing required setting: $Path"
    }
    return $text
}

function Get-JsonNumber {
    param($Value, $Default)
    if ($null -eq $Value -or $Value -eq "") {
        return $Default
    }
    return [double]$Value
}

function Get-JsonBool {
    param($Value, [bool]$Default)
    if ($null -eq $Value -or $Value -eq "") {
        return $Default
    }
    if ($Value -is [bool]) {
        return [bool]$Value
    }
    $text = [string]$Value
    if ($text -eq "1" -or $text -eq "true" -or $text -eq "True") {
        return $true
    }
    if ($text -eq "0" -or $text -eq "false" -or $text -eq "False") {
        return $false
    }
    return $Default
}

function ConvertTo-Hashtable {
    param($Object)
    if ($null -eq $Object) { return $null }
    if ($Object -is [hashtable]) { return $Object }
    if ($Object -is [string] -or $Object -is [ValueType]) { return $Object }
    if ($Object -is [System.Collections.IDictionary]) {
        $copy = @{}
        foreach ($key in $Object.Keys) {
            $copy[[string]$key] = ConvertTo-Hashtable $Object[$key]
        }
        return $copy
    }
    if ($Object -is [System.Collections.IEnumerable] -and -not ($Object -is [string])) {
        $items = New-Object System.Collections.Generic.List[object]
        foreach ($item in $Object) {
            [void]$items.Add((ConvertTo-Hashtable $item))
        }
        return $items
    }

    $copy = @{}
    foreach ($property in $Object.PSObject.Properties) {
        $copy[$property.Name] = ConvertTo-Hashtable $property.Value
    }
    return $copy
}

function Merge-Hashtable {
    param([hashtable]$Base, [hashtable]$Overlay)
    $result = @{}
    foreach ($key in $Base.Keys) { $result[$key] = $Base[$key] }
    if ($null -eq $Overlay) { return $result }
    foreach ($key in $Overlay.Keys) {
        if ($result.ContainsKey($key) -and $result[$key] -is [hashtable] -and $Overlay[$key] -is [hashtable]) {
            $result[$key] = Merge-Hashtable $result[$key] $Overlay[$key]
        }
        else {
            $result[$key] = $Overlay[$key]
        }
    }
    return $result
}

function Assert-OllamaCli {
    if (-not (Get-Command ollama -ErrorAction SilentlyContinue)) {
        throw "Ollama CLI not found. Install from https://ollama.com/download"
    }
}

function Wait-OllamaReady {
    param([Parameter(Mandatory = $true)][string]$BaseUrl, [int]$TimeoutSec = 60)

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $tagsUrl = "$BaseUrl/api/tags"
    do {
        try {
            Invoke-RestMethod -Uri $tagsUrl -TimeoutSec 5 | Out-Null
            return
        }
        catch {
            Write-Host "Waiting for Ollama at $BaseUrl ..."
            Start-Sleep -Seconds 2
        }
    } while ((Get-Date) -lt $deadline)

    throw "Ollama is not responding at $BaseUrl. Start the Ollama app and retry."
}

function Invoke-OllamaCli {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string[]]$OllamaArgs
    )

    $previous = [Environment]::GetEnvironmentVariable("OLLAMA_HOST")
    $env:OLLAMA_HOST = $BaseUrl.TrimEnd("/")
    try {
        & ollama @OllamaArgs
        if ($LASTEXITCODE -ne 0) {
            throw "ollama $($OllamaArgs -join ' ') failed (host $env:OLLAMA_HOST)"
        }
    }
    finally {
        if ([string]::IsNullOrWhiteSpace($previous)) {
            Remove-Item Env:OLLAMA_HOST -ErrorAction SilentlyContinue
        }
        else {
            $env:OLLAMA_HOST = $previous
        }
    }
}

function Invoke-OllamaPull {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$Model
    )

    Write-Host "Pulling model '$Model' at $BaseUrl ..."
    Invoke-OllamaCli -BaseUrl $BaseUrl -OllamaArgs @("pull", $Model)
}

function New-OllamaAgent {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$AgentName,
        [Parameter(Mandatory = $true)][string]$BaseModel,
        [string]$SystemPrompt = "",
        [double]$Temperature = 0.2,
        [int]$NumCtx = 8192,
        [switch]$SkipRuntimeParameters
    )

    $safeName = $AgentName -replace "[^a-zA-Z0-9._-]", "_"
    $modelfilePath = Join-Path $env:TEMP ("predictor-{0}.Modelfile" -f $safeName)
    $builder = New-Object System.Text.StringBuilder
    [void]$builder.AppendLine("FROM $BaseModel")
    if (-not $SkipRuntimeParameters) {
        [void]$builder.AppendLine(("PARAMETER temperature {0}" -f $Temperature.ToString([System.Globalization.CultureInfo]::InvariantCulture)))
        if ($NumCtx -gt 0) {
            [void]$builder.AppendLine("PARAMETER num_ctx $NumCtx")
        }
    }
    if (-not [string]::IsNullOrWhiteSpace($SystemPrompt)) {
        $escaped = $SystemPrompt.Replace('"""', '\"\"\"')
        [void]$builder.AppendLine('SYSTEM """')
        [void]$builder.AppendLine($escaped)
        [void]$builder.AppendLine('"""')
    }

    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($modelfilePath, $builder.ToString(), $utf8)

    Write-Host "Creating Ollama agent '$AgentName' from '$BaseModel' at $BaseUrl ..."
    Invoke-OllamaCli -BaseUrl $BaseUrl -OllamaArgs @("create", $AgentName, "-f", $modelfilePath)
}

function Test-OllamaEmbedding {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$Model
    )

    Write-Host "Smoke-testing embedding agent '$Model'..."
    $body = @{ model = $Model; input = "predictor embedding smoke test" } | ConvertTo-Json -Compress
    $response = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/embed" -ContentType "application/json; charset=utf-8" -Body $body -TimeoutSec 120
    $vector = $null
    if ($response.embeddings) {
        $vector = @($response.embeddings)[0]
    }
    if (-not $vector -or @($vector).Count -lt 1) {
        throw "Embedding smoke test failed: empty vector"
    }
    Write-Host ("Embedding OK, dimensions: {0}" -f @($vector).Count)
}

function Test-OllamaChat {
    param(
        [Parameter(Mandatory = $true)][string]$BaseUrl,
        [Parameter(Mandatory = $true)][string]$Model
    )

    Write-Host "Smoke-testing chat agent '$Model'..."
    $payload = @{
        model    = $Model
        stream   = $false
        think    = $false
        messages = @(
            @{ role = "user"; content = "Ответь одним словом: готов." }
        )
    }
    $json = $payload | ConvertTo-Json -Depth 6 -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    $response = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/chat" -ContentType "application/json; charset=utf-8" -Body $bytes -TimeoutSec 180
    $content = $response.message.content
    if ([string]::IsNullOrWhiteSpace($content)) {
        throw "Chat smoke test failed: empty response"
    }
    Write-Host "Chat OK."
}
