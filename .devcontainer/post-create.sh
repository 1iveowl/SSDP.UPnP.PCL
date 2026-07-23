#!/usr/bin/env bash
set -euo pipefail

dotnet restore src/main/SSDP.UPnP.PCL.sln

# The original root-based setup created these named volumes. Keep their state,
# including authentication, while making it available to the vscode user.
sudo chown -R "$(id -u):$(id -g)" "${CLAUDE_CONFIG_DIR}" "${CODEX_HOME}" "${COPILOT_HOME}"

npm install -g --allow-scripts=@anthropic-ai/claude-code @anthropic-ai/claude-code @openai/codex

# The named tool-state volumes persist these settings across container rebuilds.
claude_settings="${CLAUDE_CONFIG_DIR}/settings.json"
mkdir -p "${CLAUDE_CONFIG_DIR}" "${CODEX_HOME}"

node -e '
const fs = require("fs");
const path = process.argv[1];
let settings = {};
try { settings = JSON.parse(fs.readFileSync(path, "utf8")); } catch (error) {
  if (error.code !== "ENOENT") throw error;
}
settings.defaultMode = "bypassPermissions";
fs.writeFileSync(path, `${JSON.stringify(settings, null, 2)}\n`);
' "${claude_settings}"

codex_config="${CODEX_HOME}/config.toml"
touch "${codex_config}"
sed -i -E '/^(approval_policy|sandbox_mode)[[:space:]]*=/d' "${codex_config}"
printf '\napproval_policy = "never"\nsandbox_mode = "danger-full-access"\n' >> "${codex_config}"
