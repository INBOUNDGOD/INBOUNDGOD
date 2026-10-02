# Palit RTX 3060 Ti Dual OC V1 repair notes

## Card
- Palit GeForce RTX 3060 Ti Dual OC V1, 8 GB GDDR6, 256-bit, 3× DP + HDMI.
- Palit product code family: NE6306T019P2-190AD (Dual / Dual V1).
- TechPowerUp lists the Palit Dual as NVIDIA reference board **PG142 SKU 20**.
  The exact PCB code is printed on the board itself; confirm from photos.

## Symptoms
- Heavy artifacting; NVIDIA driver never installs properly.

## MODS result (excerpt recovered from the user's ChatGPT history; original TXT lost)
```
=== MEMORY ERRORS BY SUBPARTITION ===
SUBPART READ ERRORS WRITE ERRORS UNKNOWN ERRS
FBIOA0            0           32            0
FBIOA1            0       205298            0
FBIOB0            0           32            0
FBIOB1            0           32            0
FBIOC0            0           32            0
FBIOC1            0           32            0
FBIOD0            0           40            0
FBIOD1            0           40            0
```
- Failing bit range: A032–A039.

## Reading it
- GA104 has 4 frame-buffer partitions (A–D), each 64-bit, split into two
  32-bit sub-partitions (x0 = bits 0–31, x1 = bits 32–63). Each sub-partition
  is one GDDR6 chip. 8 chips × 32-bit = 256-bit.
- A032–A039 = bits 32–39 of partition A = the **first byte lane (DQ0–DQ7) of
  the FBIOA1 chip**. One bad byte lane in one chip, everything else fine.
- Cause is one of: that chip's DQ0–7 group (bad chip), the 8 solder balls
  under it (cracked joints), or those 8 traces/vias. A reflow sometimes fixes
  the solder case temporarily; a replacement chip is the proper fix.
- The 32/40 write errors on all other sub-partitions are a uniform baseline,
  likely a test artefact, not seven more bad chips.

## PCB (from the user's bare-board photos, 2026-10-02; copies in `photos/`)
- Silkscreen next to the GPU: **SP019-U**. A "PG" silkscreen sits beside the
  barcode sticker (B21OB009 2+00156). No "PG142" text seen; the exact Palit
  PCB code may be on the back (not photographed yet).
- GPU: NVIDIA **GA104-202-A1**, date code 2139A1 (SA1HKN.M3P).
- VRAM: 8 × SK hynix GDDR6, read as **H56G32CS4D X005 130A** (needs
  confirming under magnification; the correct part number is the shopping
  list for a replacement). Lot line under it reads like "ATDDH264W23".
- VRM: 6 phases of ON Semi 3020 power stages along the bracket side (one
  phase pad looks unpopulated), uP9512 controller (U8001 area), inductors
  1R0 2134/2136, 4R7 2128. Blue FP5K polymer caps.
- Power: single 8-pin at top-right. Fan header J15, 4-pin J10.

### Memory chip layout (orientation: bracket/display outputs LEFT, PCIe
connector at the BOTTOM, 8-pin power top-right)
```
            [M2] [M5]           <- above the GPU, toward the 8-pin
                      [M6]
   (VRM)    [ GPU ]   [M7]      <- right column
                      [M8]
        [M1] [M3] [M4]          <- bottom row, right above the PCIe fingers
```
No chips on the bracket side of the GPU. Reference designators M1..M8 are
printed on the PCB next to each chip.

- **User's belief: FBIOA1 = M1** (bottom-left chip, above the PCIe
  connector). Basis not yet stated; treat as unverified until a freeze-spray
  test or the boardview confirms it.
- Condition seen in photos: white thermal-pad residue around the GPU and
  memory; old paste on the die; a few small copper/orange specks between M1
  and M3 along their edges (check and clean before anything else).

## Which physical chip is FBIOA1?
- The user thinks M1 (see layout). Not yet verified. Needs the PG142/Palit boardview (net names FBPA_A_DQ32..39)
  or a physical test: run MODS and chill one chip at a time with freeze
  spray (or warm it with a heat gun on low); the chip whose cooling/heating
  changes the FBIOA1 error count is the one.

## File sources checked (2026-10-02)
- badcaps.net thread 3706166 "Boardview rtx 3060ti Palit": request only, no
  file posted.
- badcaps.net / pkbiosfix: V397_xx.cad boardviews in the RTX 3060 packs; it is
  not confirmed which board "V397" is.
- Gigabyte PG142-B00 schematic exists (YouTube/Badcaps); same NVIDIA reference
  family, not the same board.
- Nothing downloaded yet.
