#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage:
  tools/scaffold-state-machine.sh TARGET_DIR [--logic-name NAME] [--state-name NAME] [--states A,B,C] [--namespace NAME] [--force]

Examples:
  tools/scaffold-state-machine.sh src/map_movement --states Idle,Moving
  tools/scaffold-state-machine.sh src/game --logic-name GameLogic --state-name GameState --states MainMenu,InGame
USAGE
}

pascal_case() {
  local value="$1"
  local result=""
  local part

  value="${value//[^A-Za-z0-9]/ }"
  for part in $value; do
    first="${part:0:1}"
    rest="${part:1}"
    result+="${first^^}${rest}"
  done

  if [[ -z "$result" ]]; then
    echo "Could not derive a name from '$1'." >&2
    exit 1
  fi

  printf '%s' "$result"
}

write_file() {
  local path="$1"
  local content="$2"

  if [[ -f "$path" && "$force" != "true" ]]; then
    echo "Skipped existing file: $path"
    return
  fi

  mkdir -p "$(dirname "$path")"
  printf '%s' "$content" > "$path"
  echo "Wrote: $path"
}

if [[ $# -lt 1 ]]; then
  usage
  exit 1
fi

target_dir="${1%/}"
shift

logic_name=""
state_name=""
states_csv="Idle"
namespace="Labyrinth"
force="false"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --logic-name)
      logic_name="$2"
      shift 2
      ;;
    --state-name)
      state_name="$2"
      shift 2
      ;;
    --states)
      states_csv="$2"
      shift 2
      ;;
    --namespace)
      namespace="$2"
      shift 2
      ;;
    --force)
      force="true"
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage
      exit 1
      ;;
  esac
done

feature_name="$(basename "$target_dir")"
feature_pascal="$(pascal_case "$feature_name")"

if [[ -z "$logic_name" ]]; then
  logic_name="${feature_pascal}Logic"
fi

state_file_base="${logic_name}State"

if [[ -z "$state_name" ]]; then
  if [[ "$feature_name" == "game" && "$logic_name" == "GameLogic" ]]; then
    state_name="GameState"
  else
    state_name="$state_file_base"
  fi
fi

IFS=',' read -r -a raw_states <<< "$states_csv"
state_names=()
for raw_state in "${raw_states[@]}"; do
  raw_state="${raw_state#"${raw_state%%[![:space:]]*}"}"
  raw_state="${raw_state%"${raw_state##*[![:space:]]}"}"
  [[ -z "$raw_state" ]] && continue
  state_names+=("$(pascal_case "$raw_state")")
done

if [[ ${#state_names[@]} -eq 0 ]]; then
  echo "At least one state name is required." >&2
  exit 1
fi

logic_path="$target_dir/$logic_name.cs"
state_dir="$target_dir/state"
states_dir="$state_dir/states"
base_state_path="$state_dir/$state_file_base.cs"
input_path="$state_dir/${state_file_base}Input.cs"
output_path="$state_dir/${state_file_base}Output.cs"

set_lines=""
for state in "${state_names[@]}"; do
  set_lines+="        Set(new $state_name.$state());"$'\n'
done
set_lines="${set_lines%$'\n'}"

write_file "$logic_path" "namespace $namespace;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

public interface I$logic_name : ILogicBlock;

[Meta]
public partial class $logic_name : LogicBlock, I$logic_name
{
    public $logic_name()
    {
$set_lines
    }
}
"

write_file "$base_state_path" "namespace $namespace;

using Chickensoft.Introspection;
using Chickensoft.LogicBlocks;

[Meta, StateDiagram]
public abstract partial record $state_name : LogicBlockState;
"

write_file "$input_path" "namespace $namespace;

public partial record $state_name
{
    public static class Input
    {

    }
}
"

write_file "$output_path" "namespace $namespace;

public partial record $state_name
{
    public static class Output
    {

    }
}
"

for state in "${state_names[@]}"; do
  write_file "$states_dir/$state.cs" "namespace $namespace;

using Chickensoft.LogicBlocks;

public partial record $state_name
{
    public record $state : $state_name
    {
        public $state()
        {

        }
    }
}
"
done

echo ""
echo "Scaffolded $logic_name in $target_dir"
