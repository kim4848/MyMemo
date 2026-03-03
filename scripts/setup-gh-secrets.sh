#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PARAM_FILE="${1:-$SCRIPT_DIR/gh-secrets.json}"

if [ ! -f "$PARAM_FILE" ]; then
  echo "Parameter file not found: $PARAM_FILE"
  exit 1
fi

echo "Setting GitHub secrets..."
for name in $(jq -r '.secrets | keys[]' "$PARAM_FILE"); do
  value=$(jq -r --arg k "$name" '.secrets[$k] | if type == "object" then tojson else . end' "$PARAM_FILE")
  if [ -z "$value" ] || [ "$value" = "null" ]; then
    echo "  SKIP $name (empty)"
    continue
  fi
  echo "$value" | gh secret set "$name"
  echo "  SET  $name"
done

echo "Setting GitHub variables..."
for name in $(jq -r '.variables | keys[]' "$PARAM_FILE"); do
  value=$(jq -r --arg k "$name" '.variables[$k]' "$PARAM_FILE")
  if [ -z "$value" ] || [ "$value" = "null" ]; then
    echo "  SKIP $name (empty)"
    continue
  fi
  gh variable set "$name" --body "$value"
  echo "  SET  $name"
done

echo "Done."
