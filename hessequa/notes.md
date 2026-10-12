# Hessequa landfill airspace project (Qpoint Group): pointer

This repo is the user's **public** GitHub profile, so this file holds **no
client figures**: no volumes, dates, costs or licence numbers. The full
working notes (results tables, disposal records, closure costs, per-site
open questions, the arithmetic re-check and the Draft-A inconsistency list)
are in `hessequa/private/notes-full.md`, which is **git-ignored**, and were
sent to the user as a file on 2026-10-12. The report PDFs are on the user's
PC. If the user wants the full notes under version control, use a private
repo or the NAS, not this one.

## What the project is

- **Client:** Hessequa Local Municipality, Western Cape. **Consultant:**
  Qpoint Group (www.qpoint.co.za), the user's business.
- **Job:** measure the airspace (remaining fill capacity under the licensed
  final landform) at the municipality's eight licensed landfill sites, the
  year each will be full, and an indicative closure cost per site. Seven
  operating sites: Albertinia, Droëkloof (Heidelberg), Gouritsmond,
  Melkhoutfontein (Stilbaai), Slangrivier, Steynskloof (Riversdale), Witsand
  (Uitsig). One closed: Jongensfontein (desktop study, not flown).
- **Survey:** drone photogrammetry (DJI Air 3S), 21–22 September 2026,
  GNSS-only, 5 cm orthophoto/DSM/DTM, WGS 84 / UTM 34S (EPSG:32734).
- **Reports:** site reports QPG-2026-001 to 007, Jongensfontein desktop
  study QPG-2026-009, consolidated report QPG-2026-008. All Draft A dated
  5 October 2026; sign-off blocks unsigned.

## Documents the user shared with Claude (2026-10-12)

All nine report PDFs (consolidated 46 pages; site reports 14–19 pages each).
Not shared: the A3 maps and the GIS deliverables (GeoTIFFs, point clouds,
contours, footprints, QGIS projects, 3D models).

## Method (as in the reports, no site figures)

1. Licensed footprint placed on the orthophoto (licence corner coordinates
   where usable, otherwise the municipality's 2025 airspace report outline).
2. Drone surface model with vegetation removed.
3. Natural ground under the waste interpolated from the surveyed surround
   (no pre-tipping survey exists); two sites use a boundary-level or
   road-level reference instead.
4. Final landform: 1:4 side slopes, 2 % crown, the licence's height cap
   (or the 2025 airspace report's default where the licence is silent).
5. Remaining airspace = surface-to-landform volume minus material already
   above the landform; used = total minus remaining.
6. Remaining life = remaining ÷ the yearly disposal the municipality
   recorded in IPWIS 2020–2025 (gate estimates, no weighbridges), converted
   at the municipality's t/m³ factor; highest rate = earliest date, median =
   typical; capped at 40 years.
7. Each site report: sensitivity table, zone map to direct tipping,
   cross-sections, heightmap, closure-cost build-up at the municipality's
   2025/26 rates, recommendations, deliverables list.

## What the reports leave open (their own recommendations, no figures)

- A land surveyor to set out the licence boundaries at four sites, and
  written confirmation of the permitted final height at two sites; one
  permit's stated area is a known typing error to be fixed by variation.
- Priced closure designs to replace the indicative costs, critical sites
  first; the IDP's airspace figures to be replaced by these measurements.
- Jongensfontein: closure licence, closure design, fencing, and a short
  drone flight to measure it.
- Pre-tipping surveys or design surfaces for the deep fills.
- Repeat the drone survey annually with a few ground control points; the
  September 2026 data is the baseline.

## Draft-A editorial issues Claude found (for a Draft B)

- Consolidated cover says "seven landfill sites"; the title, document
  control and summary table cover eight. The location map shows seven.
- Rounding follows no single rule: site-report summaries, site tables,
  consolidated tables and consolidated prose round differently, so the same
  quantity appears with different values, and in two summary rows the parts
  don't add to the total.
- One site report labels the partial 2026 disposal as January–July; all the
  others say January–August.
- One site report recommends re-surveying every two to three years; all the
  others and the consolidated report say annually.
- The closed-site report reuses operating-site boilerplate ("when the site
  reaches capacity", "measured in this survey") and lists an item in its cost
  table that its text says is omitted.
- "DJI Mavic Air 3S" should be "DJI Air 3S".
- Sign-off blocks are empty throughout.

## Status and next steps

- 2026-10-12: all nine reports read in full by Claude; arithmetic re-checked
  and found consistent; full notes written to the private file and sent to
  the user. Nothing requested yet. Next: the user says what they want
  (Draft B edits, a dashboard or map, the annual re-survey workflow, or
  something else).
