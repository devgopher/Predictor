#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
APP_DIR="$REPO_ROOT/src/Predictor"
BASE_SETTINGS="$APP_DIR/appsettings.json"

python_bin() {
  if command -v python3 >/dev/null 2>&1; then
    echo python3
  elif command -v python >/dev/null 2>&1; then
    echo python
  else
    echo "Python is required to read appsettings.json" >&2
    exit 1
  fi
}

read_ollama_agent() {
  local agent_name="${1:?agent name required}"
  local env_name="${ASPNETCORE_ENVIRONMENT:-Development}"
  "$(python_bin)" - "$BASE_SETTINGS" "$APP_DIR" "$env_name" "$agent_name" <<'PY'
import json, os, sys

base_path, app_dir, env_name, agent_name = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
if not os.path.isfile(base_path):
    raise SystemExit(f"appsettings.json not found: {base_path}")

def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)

def merge(base, overlay):
    if not isinstance(base, dict) or not isinstance(overlay, dict):
        return overlay
    out = dict(base)
    for key, value in overlay.items():
        out[key] = merge(out[key], value) if key in out else value
    return out

config = load(base_path)
overlay_path = os.path.join(app_dir, f"appsettings.{env_name}.json")
if os.path.isfile(overlay_path):
    config = merge(config, load(overlay_path))

ollama = config.get("Ollama")
if not ollama:
    raise SystemExit("Section 'Ollama' is missing in appsettings.json")

agents = ollama.get("Agents") or {}
if not agents:
    raise SystemExit("Section 'Ollama:Agents' is missing in appsettings.json")

key = next((k for k in agents if k.lower() == agent_name.lower()), None)
if key is None:
    known = ", ".join(agents) or "(none)"
    raise SystemExit(f"Ollama agent '{agent_name}' is not configured. Known agents: {known}")

agent = agents[key] or {}
if not agent.get("BaseModel"):
    raise SystemExit(f"Missing required setting: Ollama:Agents:{key}:BaseModel")

payload = {
    "key": key,
    "baseUrl": (agent.get("BaseUrl") or "http://localhost:11434").rstrip("/"),
    "baseModel": agent["BaseModel"],
    "agentName": agent.get("AgentName") or agent["BaseModel"],
    "temperature": agent.get("Temperature", 0.2),
    "numCtx": agent.get("NumCtx", 8192),
    "systemPrompt": agent.get("SystemPrompt") or "",
}
print(json.dumps(payload, ensure_ascii=False))
PY
}

agent_field() {
  local json="$1"
  local field="$2"
  "$(python_bin)" -c 'import json,sys; print(json.loads(sys.argv[1])[sys.argv[2]])' "$json" "$field"
}

assert_ollama_cli() {
  if ! command -v ollama >/dev/null 2>&1; then
    echo "Ollama CLI not found. Install from https://ollama.com/download" >&2
    exit 1
  fi
}

wait_ollama_ready() {
  local base_url="$1"
  local deadline=$((SECONDS + 60))
  while (( SECONDS < deadline )); do
    if curl -fsS "$base_url/api/tags" >/dev/null 2>&1; then
      return 0
    fi
    echo "Waiting for Ollama at $base_url ..."
    sleep 2
  done
  echo "Ollama is not responding at $base_url. Start Ollama and retry." >&2
  exit 1
}

pull_model() {
  local base_url="$1"
  local model="$2"
  echo "Pulling model '$model' at $base_url ..."
  OLLAMA_HOST="$base_url" ollama pull "$model"
}

create_agent() {
  local base_url="$1"
  local agent_name="$2"
  local base_model="$3"
  local temperature="$4"
  local num_ctx="$5"
  local system_prompt="$6"
  local modelfile
  modelfile="$(mktemp)"
  {
    echo "FROM $base_model"
    if [[ -n "$temperature" ]]; then
      echo "PARAMETER temperature $temperature"
    fi
    if [[ -n "$num_ctx" && "$num_ctx" != "0" ]]; then
      echo "PARAMETER num_ctx $num_ctx"
    fi
    if [[ -n "$system_prompt" ]]; then
      printf 'SYSTEM """\n%s\n"""\n' "$system_prompt"
    fi
  } > "$modelfile"
  echo "Creating Ollama agent '$agent_name' from '$base_model' at $base_url ..."
  OLLAMA_HOST="$base_url" ollama create "$agent_name" -f "$modelfile"
  rm -f "$modelfile"
}

test_embedding() {
  local base_url="$1"
  local model="$2"
  echo "Smoke-testing embedding agent '$model'..."
  curl -fsS "$base_url/api/embed" \
    -H "Content-Type: application/json" \
    -d "{\"model\":\"$model\",\"input\":\"predictor embedding smoke test\"}" >/dev/null
  echo "Embedding OK."
}

test_chat() {
  local base_url="$1"
  local model="$2"
  echo "Smoke-testing chat agent '$model'..."
  curl -fsS "$base_url/api/chat" \
    -H "Content-Type: application/json" \
    -d "{\"model\":\"$model\",\"stream\":false,\"think\":false,\"messages\":[{\"role\":\"user\",\"content\":\"Ответь одним словом: готов.\"}]}" >/dev/null
  echo "Chat OK."
}
