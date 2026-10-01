# Product

<!-- impeccable:product-schema 1 -->

## Platform

windows

## Users

Staff of the Rai newsrooms (TG graphics) who do both halves of the job: they export the map from ArcGIS Pro and then animate it in After Effects with the GEOlayers 3 plugin. The same person exports the tile package, converts it and serves it, several times a day, with a news piece to deliver.

## Product Purpose

Turn an ArcGIS Pro tile package (.tpkx or .tpk) into an XYZ tile folder `{z}/{x}/{y}` and give GEOlayers a URL to load it from: a local server on the same PC or, once it exists, a remote web server. Success means the map appears in GEOlayers at the first attempt, with no command line, no misaligned tiles and no files scattered in the wrong folder.

## Positioning

It replaces a hand-run Python script plus `python -m http.server`. The tool understands ArcGIS tile packages and GEOlayers: it checks the tiling scheme before converting, keeps every project in its own folder, and merges packages with more detailed zoom levels without erasing what a project already contains.

## Operating Context

- Input: ArcGIS Pro "Create Map Tile Package" output, usually .tpkx (Compact Cache V2), sometimes .tpk.
- Output: a project folder used by GEOlayers 3 through a custom map style URL such as `http://localhost:8000/{z}/{x}/{y}.png`.
- Windows 10/11 workstations; projects live on local disks or on network shares (the team has a NAS).
- Several packages of the same map, at different zoom levels or areas, end up merged into one project.
- Work happens under news deadlines: speed and getting it right the first time matter more than options.

## Capabilities and Constraints

- Single portable .exe (WinForms on .NET Framework 4.8), nothing to install, Italian UI.
- Conversion of Compact Cache V2 bundles to `{z}/{x}/{y}.png|jpg`; only the Web Mercator "ArcGIS Online / Bing Maps / Google Maps" scheme with 256 px tiles is accepted, anything else is refused with the fix to apply in ArcGIS Pro.
- Output always goes into a dedicated project folder, never straight into a generic folder such as the Desktop.
- Adding a package to an existing project requires the same image format; tiles already present are kept by default, because a small-area package also carries low zoom levels that would erase the rest of the map. Overwriting is an explicit choice.
- Local server on 127.0.0.1 and ::1 only, default port 8000.
- For GEOlayers the app lists the Raster Source values of the project: URI, min and max zoom, tile size 256 px and bounds (the ArcGIS Pro extent converted to degrees, union of the imported packages).
- Remote storage: a web server URL can be checked against a tile of the local project. Storage type (web server preferred, network folder possible) and upload method are undecided.

## Brand Commitments

- Name: Rai - Package tile manager (repository `rai-tile-package-manager`); beside the Rai logo the header reads "Package tile manager".
- No official Rai assets were supplied. The user asked for a Rai-appropriate design derived from official public sources:
  - rai.it stylesheet: brand blue `#0060E6`, deep blues `#0000BC` / `#000078`, light blues `#14AAFF` / `#1061D4`, slate `#4E6573` / `#22434E`, near-white `#F6F7F9`; typefaces Inter, Inter Tight, Public Sans.
  - rainews.it stylesheet: primary `#000099`, tints `#EEF1FC` / `#F5F7FF`, navy `#10193C` / `#212335`, live and breaking red `#C22C2F` / `#E72F39`, greens `#1F8555` / `#00B46E`, label orange `#FF510C`; typefaces Source Sans Pro, Merriweather.
  - Rai 2016 identity (Fonts In Use): the square as the dominant shape; Futura Bold corporate logotype, ITC Lubalin Graph channel numbers, Neue Haas Grotesk on air.
- The Rai logo appears in the header at the user's request: the square mark published inline on rai.it (SVG, `#00008A`), drawn reversed in white on the blue band. It is a registered trademark and the repository is public.

## Evidence on Hand

- A real test package from the team (PNG32, zoom 0-3) kept in `resources/`, which git ignores. Client data: never commit or publish it, nor screenshots of it; the README images use a synthetic package.
- `explode.py`: the previous script-based workflow.
- No screenshots of GEOlayers, no user research and no Rai brand manual are available; do not invent them.

## Product Principles

1. Never scatter or destroy work: dedicated project folders, no silent overwrite.
2. Right the first time under deadline: working defaults and one obvious next action.
3. Explain every problem in ArcGIS Pro and GEOlayers terms, together with the fix.
4. Local first: nothing leaves the PC unless the user sets up a remote storage.
