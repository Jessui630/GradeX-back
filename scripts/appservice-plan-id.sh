#!/usr/bin/env bash
# Funciones compartidas por el preflight F1 y sus pruebas.
# Este archivo no ejecuta acciones: solo define funciones para poder probar
# la normalización y la resolución del plan sin llamar a Azure.

normalize_plan_id() {
  printf '%s' "$1" | tr '[:upper:]' '[:lower:]' | tr -d '[:space:]' | sed 's:/*$::'
}

primary_app_plan_id() {
  jq -r '.appServicePlanId // empty' <<<"$1"
}

secondary_app_plan_id() {
  jq -r '.serverFarmId // empty' <<<"$1"
}

# Azure CLI reciente devuelve appServicePlanId; versiones anteriores
# devolvían serverFarmId. Se acepta cualquiera de los dos, con prioridad
# para appServicePlanId cuando está presente.
extract_app_plan_id() {
  local primary secondary
  primary="$(primary_app_plan_id "$1")"
  if [ -n "$primary" ]; then
    printf '%s' "$primary"
    return 0
  fi

  secondary="$(secondary_app_plan_id "$1")"
  printf '%s' "$secondary"
}

plan_id_matches() {
  [ "$(normalize_plan_id "$1")" = "$(normalize_plan_id "$2")" ]
}

# Si la CLI devuelve ambos campos y no coinciden, no se acepta el PASS.
plan_ids_consistent() {
  local primary secondary
  primary="$(primary_app_plan_id "$1")"
  secondary="$(secondary_app_plan_id "$1")"
  if [ -z "$primary" ] || [ -z "$secondary" ]; then
    return 0
  fi
  plan_id_matches "$primary" "$secondary"
}
