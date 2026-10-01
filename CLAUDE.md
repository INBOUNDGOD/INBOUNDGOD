# Instructions for Claude

## Read first
Before doing anything else in this repo, read `HANDOFF.md`. It holds the
current project, decisions already made, the user's preferences, environment
limits and the next steps. Don't re-ask questions it already answers.

## Keep HANDOFF.md current (required)
`HANDOFF.md` is how work moves between sessions and models. After every
meaningful change (new files, design decisions, user answers, blockers,
finished steps), update it in the same turn:

1. Edit the relevant sections (status, decisions, next steps) so they describe
   the project **as it is now**. Don't just append.
2. Add a dated line at the top of **Change log** saying what changed and where.
3. Update the `_Last updated:` date.
4. Commit and push, because the cloud container is temporary:
   `git add -A && git commit -m "..." && git push -u origin <current branch>`

A Stop hook (`.claude/hooks/handoff-check.sh`) blocks ending a turn when files
changed but `HANDOFF.md` didn't. If it fires, update the file; don't work
around it.

## Writing rules for HANDOFF.md
- Write for a reader with zero context: full names, links, file paths, ids.
- Record **why** a decision was made, not just what.
- Never put secrets, passwords, API keys or the user's personal contact
  details in it.
- Keep it accurate. Delete or correct anything that's no longer true.
