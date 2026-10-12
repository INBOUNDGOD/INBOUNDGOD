#!/usr/bin/env bash
# Stop hook: block the end of a turn when the repo has changes
# (uncommitted, or committed but unpushed) and HANDOFF.md isn't among them.

input=$(cat)
# Don't loop: if this stop was already triggered by this hook, let it through.
if printf '%s' "$input" | grep -q '"stop_hook_active"[[:space:]]*:[[:space:]]*true'; then
  exit 0
fi

cd "${CLAUDE_PROJECT_DIR:-$(dirname "$0")/../..}" 2>/dev/null || exit 0
git rev-parse --is-inside-work-tree >/dev/null 2>&1 || exit 0

changed=$(git status --porcelain --untracked-files=all | sed -E 's/^.{3}//; s/.* -> //')
if git rev-parse --abbrev-ref '@{u}' >/dev/null 2>&1; then
  changed="$changed
$(git diff --name-only '@{u}'..HEAD)"
fi
changed=$(printf '%s\n' "$changed" | sed '/^$/d' | sort -u)

[ -z "$changed" ] && exit 0
printf '%s\n' "$changed" | grep -qx 'HANDOFF.md' && exit 0

files=$(printf '%s\n' "$changed" | head -10 | tr '\n' ' ')
printf '{"decision":"block","reason":"Files changed (%s) but HANDOFF.md was not updated. Update its status, next steps and change log to reflect this work, then commit and push."}\n' "$files"
