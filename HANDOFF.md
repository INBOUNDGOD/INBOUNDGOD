# HANDOFF

The single source of truth for picking up this project mid-stream. Any Claude
session (any model) that opens this repo should read this file first, then
keep it current. See `CLAUDE.md` for the update rules.

_Last updated: 2026-10-02_

---

## 1. Active project: Ben 10 watch face

**Goal:** a personal Ben 10 / Omnitrix-themed watch face for the user's
Samsung Galaxy Watch.

**Status:** user picked **Demo 2, Ultimatrix** (2026-10-02). They want the
battery and steps readouts replaced with something more useful. Suggested
replacements were shown in an options demo; waiting on their pick.

### Ultimatrix readout options (shown 2026-10-02)
Live preview: https://claude.ai/artifact/7xFk2CoHNcQPC4HA4m9aST
Source: `watch-faces/demos/ultimatrix-options.html`

| Option | Left slot | Right slot |
|---|---|---|
| A | Weather (temp + condition) | Heart rate (BPM) |
| B | Next calendar event (time + title) | Sunset time |
| C | Rain chance | Next alarm |

Recommendation given: build both slots as Watch Face Studio
**complications**, with the chosen pair as defaults. The user can then swap
either slot on the watch (touch and hold the face, then Customize) without
a rebuild. In this layout the time moved up (y 214, size 88) to make room for
an icon, value and label stack at x 175 and x 275.

### Target device
| Item | Value |
|---|---|
| Model | Samsung Galaxy Watch4, **SM-R870** (44 mm, Bluetooth) |
| User-supplied code | `j7bk` (meaning unconfirmed; may be a serial/batch prefix) |
| Screen | 450 × 450 px, round, Super AMOLED |
| OS | Wear OS (Samsung One UI Watch). Watch Face Format (WFF) based faces |

### Build and install path (decided)
- The user has **Samsung Watch Face Studio (WFS)** on their PC. They build the
  face there and install it with WFS's **Run on device** over Wi-Fi.
- Claude's job: produce the artwork (450 × 450 layers as PNG/SVG) and a
  step-by-step WFS build sheet (which layer goes where, which WFS component to
  use for each element, tap targets, Always-On settings).
- Claude **cannot** drive WFS directly from a cloud session. It only could if
  the user links their computer (computer-use) in a desktop-app session.
- An APK **cannot** be built in the cloud container: `dl.google.com` (the
  Android SDK and Google Maven) is blocked by the network policy. Don't retry
  that; use WFS.

### The three demos
Live preview: https://claude.ai/artifact/4mKzh5tksjy4cCN8tLb8Dz
Source: `watch-faces/demos/omnitrix-faces.html` (self-contained HTML/SVG, all
drawn in a 450 × 450 viewBox, so geometry maps 1:1 to the watch).

| # | Name | What's on it | Always-On version |
|---|---|---|---|
| 1 | Classic Omnitrix | Grey bezel with 4 green slots; green hourglass dial; hours in left black wedge, minutes in right; date on top green, battery and steps on bottom green; green seconds dot orbiting the dark ring | Hourglass outline plus green digits only |
| 2 | Ultimatrix | Thick black ring with 60 segments lit one per second (quarter marks brighter); big glowing HH:MM; date above; battery and steps below; faint hourglass outline behind | Outlined digits, 4 quarter marks, hourglass outline |
| 3 | Omnitrix Analog | 12 glowing hour slots on the bezel (larger at 12/3/6/9); hourglass dial; day in left wedge, date window in right wedge; dark hands with green stripe; **red** second hand (Omnitrix time-out red) | Outlined hands, 4 quarter slots, hourglass outline |

Key geometry (clock degrees, 0 = 12 o'clock, clockwise, centre 225,225):
- Hourglass dial radius 160 to 165; black side wedges span 52° to 128° and 232° to 308°.
- Bezel ring centred at r = 206, width about 38.
- Palette: green `#62e83c`, highlight `#9dff6e`, deep green `#2f8f22`,
  wedge black `#090c0a`, red `#ff3b30`.
- Font in demos: Oxanium (Google Fonts, OFL licence, so it's fine to bundle in WFS).
- Battery (82%) and steps (6,214) in the demo are placeholders. In WFS they
  must be bound to the real battery and step-count data.

### Constraints and notes
- **Personal use only.** Ben 10 is Cartoon Network IP. The artwork is
  original, Omnitrix-*inspired*, and must not be published to the Galaxy Store
  or Google Play. Don't use official Ben 10 logos or screenshots.
- Always-On mode should stay mostly black. Samsung recommends a low lit-pixel
  share in AOD, so use outlines rather than filled shapes.

### Next steps
1. User picks a design (1, 2, 3, or a mix).
2. Export full-resolution 450 × 450 layers (PNG with transparency, plus SVG
   sources) into `watch-faces/<design-name>/assets/`.
3. Write `watch-faces/<design-name>/WFS-BUILD.md`: layer order, positions,
   WFS components (digital clock, analog hands, progress/index images,
   complications), AOD layer set, tap actions.
4. User builds in WFS, runs on the watch, and reports back. Iterate.

### History
- An earlier attempt happened in Claude Code session
  `session_01DzeuacYjquSzcXWwEwjdEb` ("desktop-npieb66-calm-star", 2026-09-30).
  It stalled while waiting for a style choice, and nothing was built there.

---

## 2. User preferences (apply to all work here)
- Likes seeing visual demos of options before committing to one.
- Uses claude.ai cloud sessions and the Claude desktop app, and switches
  sessions and models. Keep this file complete enough that a new session
  needs nothing else.
- Repo: `INBOUNDGOD/INBOUNDGOD`. Work happens on branch
  `claude/keen-newton-hcb1bu`; pull requests go into `main`.
- **This is the user's GitHub profile repo** (repo name = account name,
  description "Config files for my GitHub profile."). A `README.md` at the
  repo root on the default branch is shown publicly on their GitHub profile
  page, so don't add or change a root `README.md` unless the user asks.
  Project docs go in subfolders (e.g. `watch-faces/<design>/WFS-BUILD.md`).
- Open PR: https://github.com/INBOUNDGOD/INBOUNDGOD/pull/1
  (`claude/keen-newton-hcb1bu` → `main`). Every push to the branch updates
  it, so don't open a second PR for this work.
- `main` was created on 2026-10-01 as an empty "Initial commit" because the
  repo had no base branch and PR creation failed ("no base branch ... tried
  'main'"). The work branch was joined to it with a merge
  (`--allow-unrelated-histories`), not a rebase, so nothing was force-pushed.
- GitHub's **default branch** was still `claude/keen-newton-hcb1bu` (the
  first branch ever pushed becomes the default). The user should switch it to
  `main` under GitHub repo Settings, then General, then Default branch. Claude
  has no tool to change this setting.

---

## 3. Moving off GitHub to Forgejo on the NAS (decided 2026-10-02)
- **Decision:** the user wants all their GitHub repos moved to their own
  **Forgejo** server on their Synology NAS, and GitHub dropped entirely
  ("Move off GitHub entirely" was chosen over mirroring).
- **Repos to move:** `INBOUNDGOD/INBOUNDGOD` (this project; it's
  **public** on GitHub) and `INBOUNDGOD/claude-phone-` (private, README only).
- **Where Forgejo details live:** the user says their Forgejo setup
  instructions (URL, access) are in a `CLAUDE.md` on their own machine or NAS.
  It's not in either GitHub repo, so cloud sessions can't see it. Ask
  the user or read it from a local session.
- **How (recommended):** use Forgejo's own **New Migration → GitHub** import,
  run from the NAS. It copies full history (plus PRs, issues and releases if
  ticked) using a GitHub token. It needs no cloud-to-NAS access and no NAS
  passwords in the cloud.
- **Consequence:** Claude Code cloud sessions (claude.ai/code) clone from
  GitHub only. Once GitHub is gone, continue this project with Claude Code
  running locally (PC or NAS) against the Forgejo remote. `HANDOFF.md` and
  `CLAUDE.md` travel with the repo, so the handoff system keeps working.
- **Status: on hold (user, 2026-10-02).** Keep working on GitHub for now.
  When the move is done, make a **PDF** with all the info and instructions
  from this project, so the user can hand it to Claude Code running locally.
- Steps already given to the user. Not yet done. Recommended merging PR #1
  into `main` first so `main` holds all the work before migrating. Recommended
  archiving the GitHub repos (reversible) and deleting them only after the
  Forgejo copies are checked.

---

## 4. Environment notes (cloud container)
- The container is temporary. Anything not committed **and pushed** is lost.
- Blocked: `dl.google.com` (no Android SDK, no Google Maven).
- Available: Java, Gradle, Node, Playwright with Chromium (useful for
  rendering SVG to PNG and for screenshotting demos).
- Published pages (Artifacts) survive the container and can be re-read by
  URL from any later session.

---

## 5. Change log
Newest first. One line per meaningful change: date, what changed, where.

- **2026-10-02**: Built and published the Ultimatrix readout options demo
  (`watch-faces/demos/ultimatrix-options.html`).
- **2026-10-02**: User picked the Ultimatrix design and asked to replace
  battery and steps. The Forgejo move is on hold; make a PDF handoff guide later.

- **2026-10-02**: User decided to move all repos off GitHub to Forgejo on
  their NAS. Added section 3 with the plan and its consequences.

- **2026-10-01**: Noted that this is the user's GitHub profile repo (a root
  `README.md` would appear on their public profile). PR #1 status: no CI
  configured, no reviews, mergeable.

- **2026-10-01**: User opened PR INBOUNDGOD/INBOUNDGOD#1
  (https://github.com/INBOUNDGOD/INBOUNDGOD/pull/1) from the Claude Code UI.

- **2026-10-01**: Created the `main` branch (empty initial commit) so PRs
  can target it, and merged it into `claude/keen-newton-hcb1bu`. The PR from
  the work branch into `main` now shows all project files as additions.

- **2026-10-01**: Created this handoff system: `HANDOFF.md`, `CLAUDE.md`
  rules, and a Stop hook (`.claude/hooks/handoff-check.sh`) that blocks ending
  a turn when files changed but `HANDOFF.md` wasn't updated.
- **2026-10-01**: Built three demo faces (Classic Omnitrix, Ultimatrix,
  Omnitrix Analog) with Always-On previews. Published the artifact and saved
  the source to `watch-faces/demos/omnitrix-faces.html`.
- **2026-10-01**: Confirmed device (SM-R870, Galaxy Watch4 44 mm) and build
  path (Watch Face Studio). Found that APK builds are impossible in the cloud
  container.
