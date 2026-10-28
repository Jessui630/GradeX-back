#!/usr/bin/env bash
# Verifica un artifact gradex-api antes de desplegarlo o revertirlo.
# Solo lectura: comprueba checksum, manifiesto, ZIP, runtime y contenido prohibido.
set -euo pipefail

fail() {
  echo "::error::$*" >&2
  exit 1
}

for name in \
  EXPECTED_APP EXPECTED_RESOURCE_GROUP EXPECTED_PLAN EXPECTED_LOCATION \
  EXPECTED_RUNTIME EXPECTED_WORKFLOW EXPECTED_REF EXPECTED_COMMIT EXPECTED_RUN_ID; do
  value="${!name:-}"
  if [ -z "$value" ]; then
    fail "Falta la variable obligatoria $name para verificar el artifact."
  fi
done

if ! [[ "$EXPECTED_RUN_ID" =~ ^[0-9]+$ ]]; then
  fail "EXPECTED_RUN_ID debe ser numérico."
fi
if [ -n "${EXPECTED_RUN_ATTEMPT:-}" ] && ! [[ "$EXPECTED_RUN_ATTEMPT" =~ ^[0-9]+$ ]]; then
  fail "EXPECTED_RUN_ATTEMPT debe ser numérico cuando se informa."
fi
if ! [[ "$EXPECTED_COMMIT" =~ ^[0-9a-f]{40}$ ]]; then
  fail "EXPECTED_COMMIT debe ser un SHA-1 de 40 caracteres en minúsculas."
fi
if [ "$EXPECTED_REF" != "refs/heads/release" ]; then
  fail "EXPECTED_REF debe ser refs/heads/release."
fi

ARTIFACT_DIRECTORY="${ARTIFACT_DIRECTORY:-artifact}"
ARTIFACT_NAME="${ARTIFACT_NAME:-gradex-api}"
cd "$ARTIFACT_DIRECTORY"

for file in gradex-api.zip gradex-api.sha256 gradex-api.manifest.json; do
  [ -f "$file" ] || fail "El artifact no contiene '$file'."
done

sha256sum -c gradex-api.sha256 >/dev/null || fail "El checksum SHA-256 del ZIP no coincide."
unzip -t gradex-api.zip >/dev/null || fail "El ZIP está corrupto."

zip_sha="$(sha256sum gradex-api.zip | awk '{print $1}')"
zip_bytes="$(stat -c%s gradex-api.zip)"
entry_count="$(unzip -Z1 gradex-api.zip | wc -l | tr -d ' ')"

if ! jq -e \
  --arg artifactName "$ARTIFACT_NAME" \
  --arg app "$EXPECTED_APP" \
  --arg resourceGroup "$EXPECTED_RESOURCE_GROUP" \
  --arg plan "$EXPECTED_PLAN" \
  --arg location "$EXPECTED_LOCATION" \
  --arg runtime "$EXPECTED_RUNTIME" \
  --arg workflow "$EXPECTED_WORKFLOW" \
  --arg ref "$EXPECTED_REF" \
  --arg commit "$EXPECTED_COMMIT" \
  --arg runId "$EXPECTED_RUN_ID" \
  --arg runAttempt "${EXPECTED_RUN_ATTEMPT:-}" \
  --arg zipSha "$zip_sha" \
  --argjson zipBytes "$zip_bytes" \
  --argjson entryCount "$entry_count" \
  '.schemaVersion == 1
   and .artifactName == $artifactName
   and .app == $app
   and .resourceGroup == $resourceGroup
   and .plan == $plan
   and .location == $location
   and .runtime == $runtime
   and .workflow == $workflow
   and .ref == $ref
   and .commit == $commit
   and .runId == $runId
   and ($runAttempt == "" or .runAttempt == $runAttempt)
   and .zipSha256 == $zipSha
   and .zipBytes == $zipBytes
   and .entryCount == $entryCount' \
  gradex-api.manifest.json >/dev/null; then
  fail "El manifiesto no coincide con el ZIP, el run o el destino esperado."
fi

entries="$(unzip -Z1 gradex-api.zip)"
for required in GradeX.Api.dll GradeX.Api.deps.json GradeX.Api.runtimeconfig.json; do
  if ! grep -qx "$required" <<<"$entries"; then
    fail "El ZIP no contiene '$required' en la raíz."
  fi
done

if grep -Eq '(^|/)\.\.(/|$)|^/' <<<"$entries"; then
  fail "El ZIP contiene rutas absolutas o con '..'."
fi
if grep -Eq '^\./' <<<"$entries"; then
  fail "El ZIP contiene entradas con prefijo './'; se exigen nombres relativos a la raíz."
fi
if grep -q '[\\]' <<<"$entries"; then
  fail "El ZIP contiene separadores de ruta con barra invertida."
fi
if grep -Eq '(^|/)\.git/' <<<"$entries"; then
  fail "El ZIP contiene metadatos de Git."
fi
if grep -Eiq '(^|/)(.*\.(cs|csproj|sln|slnx|go|mod|sum|pfx|snk|pdb)|web\.config|appsettings\.Development\.json|secrets?\.json|\.env|dockerfile)$' <<<"$entries"; then
  fail "El ZIP contiene archivos fuente, de desarrollo o material sensible."
fi

duplicates="$(sort <<<"$entries" | uniq -d)"
if [ -n "$duplicates" ]; then
  fail "El ZIP contiene entradas duplicadas."
fi

if ! unzip -p gradex-api.zip GradeX.Api.runtimeconfig.json | jq -e '.runtimeOptions.tfm == "net10.0"' >/dev/null; then
  fail "El runtime del ZIP no es net10.0."
fi

if [ -n "${GITHUB_OUTPUT:-}" ]; then
  {
    echo "zip_sha256=$zip_sha"
    echo "zip_bytes=$zip_bytes"
    echo "entry_count=$entry_count"
  } >> "$GITHUB_OUTPUT"
fi

echo "Artifact verificado: $ARTIFACT_NAME sha256=$zip_sha bytes=$zip_bytes entradas=$entry_count destino=$EXPECTED_RESOURCE_GROUP/$EXPECTED_APP plan=$EXPECTED_PLAN"
