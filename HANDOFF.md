# HANDOFF

The single source of truth for picking up this project mid-stream. Any Claude
session (any model) that opens this repo should read this file first, then
keep it current. See `CLAUDE.md` for the update rules.

_Last updated: 2026-10-02_

---

## 1. Active project: Ben 10 watch face

**Goal:** a personal Ben 10 / Omnitrix-themed watch face for the user's
Samsung Galaxy Watch.

**Status (2026-10-02, later):** WFS build complete on the user's PC
(background, seconds bar, overlay, time, date; Always-On static + green time
+ minute marker). **Install to the watch: attempted, result unknown.** Run on
Device didn't connect at first; the user was walked through Developer
options, Wireless debugging, adb pair/connect and WFS's own "Add device"
dialog (IP 192.168.0.20, pairing port 34365 at the time). The user then moved
on to other projects without saying whether it worked. Also still open: the
user said their WFS build "looks nothing like the v2 preview" but never sent
the comparison screenshots.

Earlier status: user approved **Ultimatrix v2** and the **build kit is made**
(2026-10-02): `watch-faces/ultimatrix/` (layers, fonts, previews, SVG
sources and `WFS-BUILD.md`). Waiting on the user to build it in Watch Face
Studio, install it on the watch and report back.

### Build kit: `watch-faces/ultimatrix/`
- `WFS-BUILD.md`: the step-by-step Watch Face Studio guide (layer order,
  positions, fonts, colours, Always-On set, install, phone battery setup,
  checklist).
- `layers/*.png`: full-canvas 450 × 450 images, all placed at X 0, Y 0.
  `01_background`, `03_bezel_overlay`, `aod_01_static`,
  `aod_02_seconds_tick`. (`05_phone_icon_label` was deleted when the
  battery was dropped.)
- **Seconds-ring technique:** a WFS circular **progress bar** (r 202,
  thickness 30, #62E83C, value = seconds, range 0–60, start angle −3°) sits
  between the background and `03_bezel_overlay.png`. The overlay is the bezel
  with see-through slots, so the green bar only shows inside slots. The −3°
  start makes the bar stop between slots.
- Time, date and battery are WFS text, clock and complication components (not
  images), in Oxanium (`fonts/`, static Bold and SemiBold cut from Google's
  variable font with fonttools; OFL licence).
- In `aod_01_static` the hourglass lines are cut out behind the time box
  (x 92–358, y 166–262), because WFS text can't have the black halo the demo used.
- `render-layers.js` regenerates all images (Playwright, run from the folder).
- **Phone battery: dropped by the user.** For the record: WFS faces can't read
  the phone battery directly; it would need a complication slot and a
  phone-battery provider app.
- **Always-On seconds:** included as a seconds-hand layer for testing.
  Google and Samsung sources say Always-On only redraws once a minute, so it
  will probably freeze or be rejected by WFS. The user has been told.

### Ultimatrix v2 (current design)
Live preview: https://claude.ai/artifact/ToMBV5b9tmr8zoFQ3WkC7q
Source: `watch-faces/demos/ultimatrix-v2.html`

Wrist up (active):
- Thick black bezel with 60 seconds segments (lit one per second), inner
  green rim, dark green radial background, hourglass outline.
- Date (`FRI · OCT 02`) above; big glowing `HH:MM` (y 214, size 88).
- **No readouts.** The user removed the left and right complications, then
  (while building in WFS, 2026-10-02) the phone battery too: "I don't want
  it anymore". The face is just time, date and the seconds ring.
- The four diagonal corner notches from earlier demos were **removed**.
  They were decoration only, and the user asked to drop anything that's
  just style.

Always-On (user request 2026-10-02: "everything fades away, leave only the
ticking and the Ben 10 theme"):
- **Stays:** time in **solid green** (`#3fbf28`, light-green edge
  `#b8ff9a`), bright hourglass outline (`#62e83c`, 3.5 px, no fill), a thin
  dim-green rim ring (r 175, `#2f8f22`, 2 px) and **one bright seconds mark
  ticking round the edge** (`#9dff6e`).
- **Brightness (user request 2026-10-02):** the first Always-On version
  looked too dim, close to a blank black screen. It was brightened and
  measured at about **9.5% lit pixels**, under Samsung's guideline of about
  15%. Filling the hourglass pushed it to 27%, so the hourglass stays
  outline-only.
- **Goes:** date, phone battery, full seconds ring, bezel, glow.
- **Seconds in Always-On: confirmed not possible in WFS** (user test,
  2026-10-02). An Analog Clock in Always-On only moves the minute hand. The
  user says an older Ben 10 face of theirs had a "spinning circle" set to
  seconds; that may only have animated in the WFS preview. Also tested
  an image rotated by `[SEC]*6`: it doesn't move even in WFS's Always-On
  preview (which steps per minute, so `[SEC]` stays 0). Seconds are
  impossible in Always-On. **Final (user-confirmed):** the tick image is a
  **Radial indicator synced to Minute in Hour** (value 0–60, 360° CW), so it
  orbits the rim one step per minute.
- WFS reported an Always-On **On Pixel Ratio of 3.1%** for the user's build.
- A black halo behind the digits stops the hourglass lines crossing them.
- The demo fades over 0.9 s. On the watch, the switch to Always-On is handled
  by the system.

### Earlier: Ultimatrix readout options (superseded by v2)
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
1. ~~Pick a design~~ (Ultimatrix v2). ~~Make layers and build guide~~ (done).
2. **User builds in WFS following `watch-faces/ultimatrix/WFS-BUILD.md`,
   installs with Run on device, and reports back** (photos help): layout,
   seconds ring, Always-On brightness, whether the Always-On seconds mark
   moves, whether phone battery shows.
3. Fix whatever comes back, by editing `render-layers.js`, re-rendering and
   updating the guide.
4. Later (user request): when the Forgejo move is done, make a **PDF**
   covering everything from this project for the user's local Claude.

### History
- An earlier attempt happened in Claude Code session
  `session_01DzeuacYjquSzcXWwEwjdEb` ("desktop-npieb66-calm-star", 2026-09-30).
  It stalled while waiting for a style choice, and nothing was built there.

---

## 1b. Active project: Crush site (started 2026-10-02)

**Goal:** a cute website where someone asks another person to be their
crush. Two sides: the **asker** makes a request and gets a link; the
**crush** opens it and answers yes or no; the asker sees the answer.

**Decisions (user, 2026-10-02):**
- **Plan: demo first, then a real site.** A clickable demo of all screens
  is built as a claude.ai artifact to perfect the look. Then it becomes a
  real website anyone can open, hosted on the user's Synology NAS or a free
  host.
- Why not a claude.ai artifact for the live version: shared data in
  artifacts only works for signed-in claude.ai users given access, and a
  crush usually won't have that.
- **Vibe: "Playful game"**: cute characters, a "No" button that runs away,
  confetti and a happy dance on yes.
- Kindness rule (Claude's design choice): after a few dodges the No button
  stops running so the crush can genuinely say no, and the "no" screens
  stay gentle on both sides.

**Files:** `crush-site/demo.html` (the clickable demo of all three screens).
Live demo: https://claude.ai/artifact/PqofEdiz8WRLsfNHNou8SL

**Demo contents:** one page with three steps:
1. **Ask:** your name, their name, an optional note, and a messenger colour
   (Strawberry, Sunny, Minty, Grape) → "Make my link" → a placeholder link
   with a Copy button.
2. **Their page:** a blob "messenger" character (hopeful/happy/sad moods)
   asks "Will you be [name]'s crush?". The **No** button dodges on hover/tap
   up to 5 times (teasing lines, Yes grows), then stays still and reads
   "No, sorry" so they can really decline. Yes → confetti + dance.
3. **Your answer:** status chips (link made / opened / answer). Yes →
   celebration; no → a gentle, kind message; none yet → waiting.
- The messenger character is **interactive** (user request): it slowly
  drifts toward the cursor and its eyes follow it; it can be dragged and
  springs back on release; a click/tap squishes it and pops a heart. The
  "Demo" label and the demo footnote were removed (user request); the step
  tabs stay so the demo can be navigated.
- Fonts Grandstander + Nunito; candy palette (berry #ff3d6e, sun #ffcf3f,
  mint #4fcfa3, ink #3b1f2b on pink dots). No capabilities; state stays in
  memory, nothing is sent.

**Status:** demo published. Next: user feedback on the demo, then the
real site (choose host: NAS vs free host; storage per request with a
private result link for the asker).

**Note:** this repo is the user's public GitHub profile repo. Don't commit
real names or answers from people.

---

## 1c. Active project: RTX 3060 Ti repair (started 2026-10-02)

**Goal:** the user's "RTX3060TI DUAL OC 8GB 256BIT 3 DP HDMI V1" artifacts
heavily and the NVIDIA driver never installs properly. They suspect VRAM chip
"A1" and want the board schematic/boardview to attempt a repair.

**Brand confirmed: Palit** (from the user's recovered ChatGPT history). The
Palit Dual uses NVIDIA reference board PG142 SKU 20 (TechPowerUp). ASUS
files are irrelevant. Full notes and the MODS excerpt: `gpu-repair/notes.md`.
**Diagnosis so far:** MODS shows FBIOA1 with 205,298 write errors, failing
bits A032–A039 = one byte lane of the FBIOA1 chip. Physical chip for FBIOA1
not yet identified (needs boardview or a freeze-spray test).

Earlier note: the name was ambiguous. "Dual OC … V1" matches
the **Palit GeForce RTX 3060 Ti Dual OC V1** exactly, but ASUS also sells a
**Dual RTX 3060 Ti OC (DUAL-RTX3060TI-O8G)**. A ChatGPT chat the user shared
(https://chatgpt.com/share/6abfcd1d-9f40-83e9-a013-eb39fd9ce8ad) assumed ASUS.
Photos of the bare PCB (user promised) settle it: the PCB code is printed on
the board (ASUS: CG190P / CG190PI / CG142S; Palit/Gainward: a V-number).

**Files found (2026-10-02):**
- elvikom.pl thread "Schemat Asus RTX3060TI-8G MINI CG190P" (free after forum
  registration): boardviews (*.cad) for ASUS RTX3060TI-8G MINI CG190P r1.00
  and DUAL-RTX3060TI-8G-MINI-I3S CG190P_HYN r1.00.
  https://www.elvikom.pl/schemat-asus-rtx3060ti-8g-mini-cg190p-t73599.html?lang=en
- badcaps.net thread 3474108 (premium download): Asus_TUF_RTX3060TI_O8G_GAMING
  .fz + .pdf, ASUS CG142S schematic PDF + boardview PDF, Gigabyte PG190-A02
  PDF, and V397_10/20/40/50/51/60/61/70 .cad boardviews.
- pkbiosfix.com thread 10444 (VIP): the same file set.
- realschematic.com: paid ASUS TUF CG190PI and CG142S packs.
- A YouTube video claims a free download of the TUF CG190PI schematic+boardview
  (https://www.youtube.com/watch?v=58wiEcPtYJU); not verified.
- No file was downloaded into the repo (paywalled/registration; not needed
  until the board is identified).

**How "A1" maps to a chip:** NVIDIA MATS/MODS reports memory by partition
(FBPA A–D) and sub-partition (0/1): A0, A1, B0, B1, C0, C1, D0, D1 = the 8
GDDR6 chips on a 256-bit card. The physical position of each is board-specific;
the boardview's net names (FBPA…) give the mapping. Don't guess from another
board.

**Repair reality (told to the user):** replacing GDDR6 needs a BGA rework
station (hot air/IR + preheater), a reballing stencil, a matching replacement
chip (same part number, e.g. Samsung K4Z80325BC or Hynix H56C8H24AIR), and
practice. Diagnose first (MATS/MODS run, rail resistance checks) before
removing anything.

**Status:** PCB photos received (front only; shrunk copies in
`gpu-repair/photos/`, layout map in `gpu-repair/notes.md`). PCB silkscreen
"SP019-U"; Hynix GDDR6 (part number to confirm). User believes FBIOA1 = chip
**M1** (bottom-left, above PCIe), unverified. Next: confirm with a
freeze-spray MODS test, confirm the chip part number, check tools, then plan
the replacement.

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

- **2026-10-02**: GPU: user sent bare-PCB photos and says they ran MODS
  themselves; they believe FBIOA1 is chip M1. Saved shrunk photos and the
  chip layout map in `gpu-repair/`.

- **2026-10-02**: GPU: confirmed Palit (PG142 SKU 20). Saved the MODS excerpt
  and interpretation in `gpu-repair/notes.md`.

- **2026-10-02**: New project 1c: RTX 3060 Ti repair. Researched schematic
  and boardview sources; brand (ASUS vs Palit) still unconfirmed. Watch:
  recorded install attempt as result unknown. Session model switched to
  Fable 5.1 by the user; handoff continued without issue.

- **2026-10-02**: Crush demo: removed the demo label and footnote; made the
  character follow the cursor, draggable and pokeable.

- **2026-10-02**: Built and published the crush site demo
  (`crush-site/demo.html`). Waiting for user feedback.

- **2026-10-02**: New project: crush site (section 1b). The watch face is
  paused ("fine as it is for now"); the user still has to send
  screenshots of how their WFS build differs from the v2 preview.

- **2026-10-02**: The user found the WFS **Radial indicator** option. Synced
  to seconds it doesn't move in Always-On; synced to **Minute in Hour** it
  works. That's now the Always-On marker. `WFS-BUILD.md` AOD 3 updated.
  Next: Run on device.

- **2026-10-02**: The `[SEC]*6` rotation test also failed in Always-On.
  Seconds in Always-On are closed as not possible; the tick layer is to be
  deleted.

- **2026-10-02**: User built the Always-On layers in WFS (static image +
  green HH:MM, 3.1% on-pixel ratio). Confirmed that Always-On seconds don't
  move (only the minute hand). Updated `WFS-BUILD.md`. Next: Run on device.

- **2026-10-02**: User dropped the phone battery. Removed it from the WFS
  build (user deleting the SmallBox complication), from `WFS-BUILD.md`,
  `render-layers.js` and the previews (`05_phone_icon_label` layer deleted),
  and from the v2 demo. WFS progress: background, seconds bar, bezel
  overlay, time (Oxanium-Bold 88) and date (ICU date, default
  "Wed, Oct 28" format) done. Next: Always-On layers.

- **2026-10-02**: Learned from the user's WFS: fonts must be **installed in
  Windows** (WFS lists system fonts), and font settings are under
  **Properties**, not Style. Recorded the Add Component menu names in
  `WFS-BUILD.md`. The user is adding the Digital Clock now.

- **2026-10-02**: In WFS, the user has the background, seconds progress bar
  and bezel overlay in, and the seconds slots tick correctly in WFS's
  preview. Recorded the exact working progress-bar settings in
  `WFS-BUILD.md` (Range uses Start 357 and Angular distance 360; the preset
  icons break it). Next: time, date, phone icon, battery slot.

- **2026-10-02**: User started in WFS (screenshot: empty Circle project
  "ultimatrix", "No layers"). Their WFS version has no digital/analog
  template choice, so `WFS-BUILD.md` section 1 was corrected. Walked them
  through adding the first layers with **+ Add Component**. Their WFS also
  has a **Mask** tool, a possible alternative to the bezel-overlay trick.

- **2026-10-02**: User approved Ultimatrix v2. Built the kit in
  `watch-faces/ultimatrix/` (layers, fonts, previews, SVG, `render-layers.js`,
  `WFS-BUILD.md`). Researched: phone battery needs a complication provider;
  Always-On seconds likely limited by the OS.

- **2026-10-02**: Brightened Ultimatrix v2's Always-On mode (solid green
  digits, brighter hourglass, rim ring); measured at 9.5% lit.
- **2026-10-02**: Ultimatrix v2 updated (same artifact URL, version 2):
  side readouts removed (phone battery only), decorative corner notches
  removed, ticking seconds mark added to Always-On.
- **2026-10-02**: Built Ultimatrix v2 (phone battery in the middle;
  Always-On fades to just the clock, hourglass and notches) in
  `watch-faces/demos/ultimatrix-v2.html` and published it.
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
