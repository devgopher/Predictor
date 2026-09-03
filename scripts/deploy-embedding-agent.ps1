#Requires -Version 5.1
param(
    [string]$Agent = "embedding"
)
$ErrorActionPreference = "Stop"

. "$PSScriptRoot\ollama-common.ps1"

Write-Host "Deploying Predictor embedding agent '$Agent' from appsettings.json"

Assert-OllamaCli
$config = Get-OllamaConfig
$item = Get-OllamaAgent -Config $config -Name $Agent
Wait-OllamaReady -BaseUrl $item.BaseUrl

$baseModel = $item.BaseModel
$agentName = $item.AgentName

Invoke-OllamaPull -BaseUrl $item.BaseUrl -Model $baseModel
New-OllamaAgent -BaseUrl $item.BaseUrl -AgentName $agentName -BaseModel $baseModel -SkipRuntimeParameters
Test-OllamaEmbedding -BaseUrl $item.BaseUrl -Model $agentName

Write-Host "Embedding agent is ready."
Write-Host "  Base URL   : $($item.BaseUrl)"
Write-Host "  Base model : $baseModel"
Write-Host "  Agent name : $agentName"
Write-Host "  App setting: Ollama:Agents:$Agent`:AgentName"
