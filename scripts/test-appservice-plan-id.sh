#!/usr/bin/env bash
# Pruebas deterministas del resolver de plan del preflight F1.
# No llaman a Azure: usan fixtures con las variantes de az webapp show.
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=appservice-plan-id.sh
source "$script_dir/appservice-plan-id.sh"

expected_id="/subscriptions/757e516b-f9da-4ea7-bf37-dda78151d711/resourceGroups/gradex-rg/providers/Microsoft.Web/serverfarms/ASP-gradexrg-8bed"
pass=0
failures=0

pass_case() {
  pass=$((pass + 1))
  printf 'PASS %s\n' "$1"
}

fail_case() {
  failures=$((failures + 1))
  printf 'FAIL %s: %s\n' "$1" "$2" >&2
}

check_match() {
  local name="$1" json="$2" actual
  actual="$(extract_app_plan_id "$json")"
  if plan_id_matches "$actual" "$expected_id"; then
    pass_case "$name"
  else
    fail_case "$name" "plan resuelto='$actual'"
  fi
}

check_no_match() {
  local name="$1" json="$2" actual
  actual="$(extract_app_plan_id "$json")"
  if [ -z "$actual" ] || ! plan_id_matches "$actual" "$expected_id"; then
    pass_case "$name"
  else
    fail_case "$name" "se aceptó un plan divergente='$actual'"
  fi
}

check_consistent() {
  local name="$1" json="$2"
  if plan_ids_consistent "$json"; then
    pass_case "$name"
  else
    fail_case "$name" "los campos appServicePlanId/serverFarmId son inconsistentes"
  fi
}

check_inconsistent() {
  local name="$1" json="$2"
  if ! plan_ids_consistent "$json"; then
    pass_case "$name"
  else
    fail_case "$name" "se aceptaron campos de plan inconsistentes"
  fi
}

# Variante nueva de Azure CLI.
check_match "appServicePlanId válido" \
  '{"appServicePlanId":"/subscriptions/757e516b-f9da-4ea7-bf37-dda78151d711/resourceGroups/gradex-rg/providers/Microsoft.Web/serverfarms/ASP-gradexrg-8bed","serverFarmId":null}'

# Variante anterior de Azure CLI.
check_match "serverFarmId válido" \
  '{"appServicePlanId":null,"serverFarmId":"/subscriptions/757e516b-f9da-4ea7-bf37-dda78151d711/resourceGroups/gradex-rg/providers/Microsoft.Web/serverfarms/ASP-gradexrg-8bed"}'

# La comparación debe ser insensible a mayúsculas/minúsculas.
check_match "appServicePlanId con distinto casing" \
  '{"appServicePlanId":"/SUBSCRIPTIONS/757E516B-F9DA-4EA7-BF37-DDA78151D711/RESOURCEGROUPS/GRADEX-RG/PROVIDERS/MICROSOFT.WEB/SERVERFARMS/asp-GRADEXRG-8BED"}'

# La normalización tolera espacios y una barra final.
check_match "serverFarmId con espacios y barra final" \
  '{"serverFarmId":" /subscriptions/757e516b-f9da-4ea7-bf37-dda78151d711/resourceGroups/gradex-rg/providers/Microsoft.Web/serverfarms/ASP-gradexrg-8bed/ "}'

# Fallback: campo nuevo vacío, campo antiguo correcto.
check_match "fallback de appServicePlanId vacío a serverFarmId" \
  '{"appServicePlanId":"","serverFarmId":"/subscriptions/757e516b-f9da-4ea7-bf37-dda78151d711/resourceGroups/gradex-rg/providers/Microsoft.Web/serverfarms/ASP-gradexrg-8bed"}'

# Divergencias que no deben pasar.
check_no_match "plan distinto" \
  '{"appServicePlanId":"/subscriptions/757e516b-f9da-4ea7-bf37-dda78151d711/resourceGroups/gradex-rg/providers/Microsoft.Web/serverfarms/ASP-otro-plan"}'

check_no_match "resource group distinto" \
  '{"appServicePlanId":"/subscriptions/757e516b-f9da-4ea7-bf37-dda78151d711/resourceGroups/otro-rg/providers/Microsoft.Web/serverfarms/ASP-gradexrg-8bed"}'

check_no_match "suscripción distinta" \
  '{"appServicePlanId":"/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/gradex-rg/providers/Microsoft.Web/serverfarms/ASP-gradexrg-8bed"}'

check_no_match "sin ambos campos" \
  '{"appServicePlanId":null,"serverFarmId":null}'

check_no_match "serverFarmId divergente" \
  '{"appServicePlanId":null,"serverFarmId":"/subscriptions/757e516b-f9da-4ea7-bf37-dda78151d711/resourceGroups/gradex-rg/providers/Microsoft.Web/serverfarms/ASP-otro-plan"}'

# Coherencia entre ambos campos.
check_consistent "ambos campos equivalentes con distinto casing" \
  '{"appServicePlanId":"/subscriptions/757e516b-f9da-4ea7-bf37-dda78151d711/resourceGroups/gradex-rg/providers/Microsoft.Web/serverfarms/ASP-gradexrg-8bed","serverFarmId":"/SUBSCRIPTIONS/757E516B-F9DA-4EA7-BF37-DDA78151D711/resourceGroups/GRADEX-RG/providers/Microsoft.Web/serverfarms/asp-gradexrg-8bed"}'

check_inconsistent "ambos campos con planes distintos" \
  '{"appServicePlanId":"/subscriptions/757e516b-f9da-4ea7-bf37-dda78151d711/resourceGroups/gradex-rg/providers/Microsoft.Web/serverfarms/ASP-gradexrg-8bed","serverFarmId":"/subscriptions/757e516b-f9da-4ea7-bf37-dda78151d711/resourceGroups/gradex-rg/providers/Microsoft.Web/serverfarms/ASP-otro-plan"}'

printf 'Resumen: PASS=%d FAIL=%d\n' "$pass" "$failures"
if [ "$failures" -ne 0 ]; then
  exit 1
fi
