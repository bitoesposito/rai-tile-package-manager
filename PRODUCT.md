# Product

<!-- impeccable:product-schema 1 -->

## Platform

windows

## Users

Rai newsroom graphics staff (TG) who export the map from ArcGIS Pro and animate it in After Effects with GEOlayers 3. The same person exports the tile package, converts it and serves it, several times a day, with a news piece due.

## Product Purpose

Turn an ArcGIS Pro tile package (.tpkx or .tpk) into an XYZ tile folder `{z}/{x}/{y}` and give GEOlayers a URL for it: a local server on the same PC or, once there is one, a remote web server. Success: the map shows up in GEOlayers at the first try, with no command line, no misaligned tiles and no files in the wrong folder.

## Positioning

It replaces a hand-run Python script plus `python -m http.server`. It knows ArcGIS tile packages and GEOlayers: it checks the tiling scheme before converting, keeps each project in its own folder and merges packages with more detailed zoom levels without erasing what a project already has.

## Operating Context

- Input: the output of ArcGIS Pro "Create Map Tile Package", usually .tpkx (Compact Cache V2), sometimes .tpk.
- Output: a project folder that GEOlayers 3 reads through a URL such as `http://localhost:8000/{z}/{x}/{y}.png`.
- Windows 10/11 workstations; projects on local disks or network shares (the team has a NAS).
- Several packages of one map, at different zoom levels or areas, merged into one project.
- News deadlines: speed and getting it right the first time count more than options.

## Capabilities and Constraints

- One portable .exe (WinForms on .NET Framework 4.8), nothing to install, Italian UI.
- Converts Compact Cache V2 bundles to `{z}/{x}/{y}.png|jpg`. Only the Web Mercator "ArcGIS Online / Bing Maps / Google Maps" scheme with 256 px tiles is accepted; anything else is refused with the fix to make in ArcGIS Pro.
- Output always goes into a dedicated project folder, never straight into a folder like the Desktop.
- A package added to a project must have the same image format. Tiles already there are kept by default, because a small-area package also carries low zoom levels that would erase the rest of the map; overwriting is an explicit choice.
- Local server on 127.0.0.1 and ::1 only, port 8000 by default.
- The app lists the project's GEOlayers Raster Source values: URI, min and max zoom, 256 px tile size and bounds (the ArcGIS Pro extent in degrees, union of the imported packages).
- A map preview window shows the project's tiles, hatches where a zoom has none and frames what a Full HD comp takes in, with the zoom it needs.
- Remote storage: a web server URL can be checked against a tile of the local project. Storage type (web server preferred, network folder possible) and upload method are still open.

## Brand Commitments

- Name: Rai - Package tile manager (repository `rai-tile-package-manager`); beside the Rai logo the header reads "Package tile manager".
- No official Rai assets were supplied; the design follows public sources:
  - rai.it stylesheet: brand blue `#0060E6`, deep blues `#0000BC` / `#000078`, light blues `#14AAFF` / `#1061D4`, slate `#4E6573` / `#22434E`, near-white `#F6F7F9`; typefaces Inter, Inter Tight, Public Sans.
  - rainews.it stylesheet: primary `#000099`, tints `#EEF1FC` / `#F5F7FF`, navy `#10193C` / `#212335`, live and breaking red `#C22C2F` / `#E72F39`, greens `#1F8555` / `#00B46E`, label orange `#FF510C`; typefaces Source Sans Pro, Merriweather.
  - Rai 2016 identity (Fonts In Use): the square as the dominant shape; Futura Bold corporate logotype, ITC Lubalin Graph channel numbers, Neue Haas Grotesk on air.
- The Rai logo is in the header at the user's request: the square mark published inline on rai.it (SVG, `#00008A`), reversed in white on the blue band. It is a registered trademark and the repository is public.

## Evidence on Hand

- A real test package from the team (PNG32, zoom 0-3) in `resources/`, which git ignores. It is client data: never commit or publish it, or screenshots of it; the README images use synthetic data.
- `explode.py`: the previous script workflow.
- No GEOlayers screenshots, user research or Rai brand manual are available; do not invent them.

## Product Principles

1. Never scatter or destroy work: dedicated project folders, no silent overwrite.
2. Right the first time under deadline: working defaults and one obvious next action.
3. Explain every problem in ArcGIS Pro and GEOlayers terms, with the fix.
4. Local first: nothing leaves the PC unless the user sets up a remote storage.
