#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=ollama-common.sh
source "$SCRIPT_DIR/ollama-common.sh"

AGENT_KEY="${1:-chat}"
echo "Deploying Predictor chat/conclusions agent '$AGENT_KEY' from appsettings.json"

assert_ollama_cli
config_json="$(read_ollama_agent "$AGENT_KEY")"
base_url="$(agent_field "$config_json" baseUrl)"
base_model="$(agent_field "$config_json" baseModel)"
agent_name="$(agent_field "$config_json" agentName)"
temperature="$(agent_field "$config_json" temperature)"
num_ctx="$(agent_field "$config_json" numCtx)"
system_prompt="$(agent_field "$config_json" systemPrompt)"

wait_ollama_ready "$base_url"
pull_model "$base_url" "$base_model"
create_agent "$base_url" "$agent_name" "$base_model" "$temperature" "$num_ctx" "$system_prompt"
test_chat "$base_url" "$agent_name"

echo "Chat agent is ready."
echo "  Base URL   : $base_url"
echo "  Base model : $base_model"
echo "  Agent name : $agent_name"
echo "  App setting: Ollama:Agents:${AGENT_KEY}:AgentName"
