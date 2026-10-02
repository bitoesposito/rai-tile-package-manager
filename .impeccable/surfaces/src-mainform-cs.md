---
version: 1
slug: "src-mainform-cs"
primary_target: "src/MainForm.cs"
related_targets: ["src/Ui.cs", "src/MapPreview.cs"]
---

Scope: the main window (src/MainForm.cs) with its components in src/Ui.cs, plus the map preview window (src/MapPreview.cs). Mode: Operate.

Audience and job: Rai TG graphics staff who export from ArcGIS Pro and animate in After Effects with GEOlayers 3, several times a day on deadline. They drop a .tpkx/.tpk, convert it into a dedicated project folder or add it to an existing project, put the project on air with the local server or check a remote web server, and copy the URL into GEOlayers.

Structure: three tabs in the header band. Importa: drop zone as a program monitor, destination choice, convert. In onda: current project and its map preview, port, server tally, URL and Raster Source values. Storage remoto: address, connection check, URL.

Constraints: WinForms on .NET Framework 4.8, single exe; standard Windows controls where users expect them (radio buttons, text boxes, file dialogs); keyboard reachable; high contrast falls back to system colours; tiles never go straight into a generic folder.

Direction: TG sottopancia (on-air lower thirds), candidate 5 of 7 grounded directions, seed 224c4e9a. Every state is a lower third and the running server is IN ONDA; no stacked grey form, no dashed-dropzone dashboard. Rai News blue #000099 bands with white Inter Tight, a full-height tab with the state as a drawn glyph, live red #C22C2F only for on air and errors, rai.it blue #0060E6 for actions and focus, a light #F5F7FF desk under the dark on-air elements, square corners.

Story: the operator drops the package on the program monitor, reads in one strap whether it lines up, converts it into its own folder, puts it on air and copies the URL into GEOlayers. First viewport: the header band with the Rai logo, the name, three tabs and the window buttons; the navy monitor with "Trascina qui il tile package" across the top of Importa; the destination choice below; the primary action in a bar that never scrolls away. Memorable moment: each status strap wipes in like a lower third, and the running server shows a red IN ONDA tally.

Finish: a build is done after the finish review, its verdict and DESIGN.md.

Unresolved: remote storage type and upload method.
