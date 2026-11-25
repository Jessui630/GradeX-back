#!/usr/bin/env bash
# Rechaza cualquier publicación que no provenga de main y cuyo commit no
# pertenezca al historial publicado de main.
set -euo pipefail

expected_ref="refs/heads/main"
candidate_sha="${PUBLICATION_SHA:-${GITHUB_SHA:-}}"

if [ "${GITHUB_REF:-}" != "$expected_ref" ]; then
  echo "::error::Publicación rechazada: GITHUB_REF='${GITHUB_REF:-}' no es '$expected_ref'." >&2
  exit 1
fi

case "${GITHUB_EVENT_NAME:-}" in
  workflow_dispatch|push) ;;
  *)
    echo "::error::Evento no autorizado para publicar: '${GITHUB_EVENT_NAME:-}'." >&2
    exit 1
    ;;
esac

if ! [[ "$candidate_sha" =~ ^[0-9a-f]{40}$ ]]; then
  echo "::error::SHA inválido o ausente: '$candidate_sha'." >&2
  exit 1
fi

if ! git rev-parse --verify refs/remotes/origin/main >/dev/null 2>&1; then
  echo "::error::No está disponible origin/main; use actions/checkout con fetch-depth: 0." >&2
  exit 1
fi

if ! git merge-base --is-ancestor "$candidate_sha" refs/remotes/origin/main; then
  echo "::error::El commit $candidate_sha no pertenece a main." >&2
  exit 1
fi

echo "Publicación autorizada: ref=$GITHUB_REF commit=$candidate_sha pertenece a main."
