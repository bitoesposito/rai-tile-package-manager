---
version: 1
slug: "src-mainform-cs"
primary_target: "src/MainForm.cs"
related_targets: ["src/Ui.cs"]
---

Scope: the whole main window of the desktop app (src/MainForm.cs plus the components in src/Ui.cs). Mode: Operate.

Audience and job: Rai TG graphics staff who export from ArcGIS Pro and animate in After Effects with GEOlayers 3, several times a day under an edition deadline. Task: drop a .tpkx/.tpk, convert it into a dedicated project folder (or add it to an existing project), put the project on air with the local server or check a remote web server, copy the URL into GEOlayers.

Structure: three tabs in the header band. Importa (drop zone as a program monitor, destination choice, convert), In onda (current project, port, server tally, URL), Storage remoto (address, connection check, URL).

Constraints: WinForms on .NET Framework 4.8, single exe; standard Windows controls where users expect them (radio buttons, text boxes, file dialogs); keyboard reachable; high-contrast mode falls back to system colours; never write tiles straight into a generic folder.

Direction: TG sottopancia (on-air lower thirds). Memorable moment: each status strap wipes in like a lower third; the running server shows a red IN ONDA tally.

Unresolved: remote storage type and upload method; official Rai logo not used.
