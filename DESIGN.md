---
name: Rai - Package tile manager
description: A Windows tool for TG graphics staff that speaks the Rai News on-air language; every state is a lower third.
colors:
  band: "#000099"
  band-soft: "#D6DDFF"
  action: "#0060E6"
  action-hover: "#1061D4"
  action-tint: "#EEF1FC"
  live: "#C22C2F"
  ok: "#1F8555"
  warn: "#FF510C"
  navy: "#10193C"
  monitor-grid: "#1E2A5C"
  monitor-grid-hover: "#2E4196"
  slate: "#4E6573"
  rule: "#D3DAEE"
  disabled: "#B9C3E0"
  desk: "#F5F7FF"
  panel: "#FFFFFF"
typography:
  broadcast-title:
    fontFamily: "Inter Tight, Segoe UI, sans-serif"
    fontSize: "15.5pt"
    fontWeight: 700
  app-name:
    fontFamily: "Inter Tight, Segoe UI, sans-serif"
    fontSize: "12.5pt"
    fontWeight: 700
  url:
    fontFamily: "Inter Tight, Segoe UI, sans-serif"
    fontSize: "12.5pt"
    fontWeight: 400
  heading:
    fontFamily: "Inter Tight SemiBold, Segoe UI, sans-serif"
    fontSize: "11.5pt"
    fontWeight: 600
  tab:
    fontFamily: "Inter Tight SemiBold, Segoe UI, sans-serif"
    fontSize: "10.5pt"
    fontWeight: 600
  broadcast-detail:
    fontFamily: "Inter Tight, Segoe UI, sans-serif"
    fontSize: "10.5pt"
    fontWeight: 400
  strap-title:
    fontFamily: "Inter Tight, Segoe UI, sans-serif"
    fontSize: "10pt"
    fontWeight: 700
  button:
    fontFamily: "Inter Tight SemiBold, Segoe UI, sans-serif"
    fontSize: "9.75pt"
    fontWeight: 600
  strap-detail:
    fontFamily: "Inter Tight, Segoe UI, sans-serif"
    fontSize: "9.25pt"
    fontWeight: 400
  body:
    fontFamily: "Segoe UI (SystemFonts.MessageBoxFont)"
    fontSize: "9pt"
    fontWeight: 400
rounded:
  none: "0px"
spacing:
  xs: "2px"
  sm: "6px"
  md: "14px"
  lg: "20px"
  gutter: "28px"
  control: "32px"
  header: "56px"
components:
  button-primary:
    backgroundColor: "{colors.action}"
    textColor: "{colors.panel}"
    typography: "{typography.button}"
    rounded: "{rounded.none}"
    height: "{spacing.control}"
    width: "max(112px, longest label + 32px)"
  button-primary-hover:
    backgroundColor: "{colors.action-hover}"
  button-primary-pressed:
    backgroundColor: "{colors.band}"
  button-primary-disabled:
    backgroundColor: "{colors.disabled}"
    textColor: "{colors.panel}"
  button-secondary:
    backgroundColor: "{colors.panel}"
    textColor: "{colors.action}"
    typography: "{typography.button}"
    rounded: "{rounded.none}"
    height: "{spacing.control}"
  button-secondary-hover:
    backgroundColor: "{colors.action-tint}"
  button-secondary-pressed:
    backgroundColor: "{colors.rule}"
  button-secondary-disabled:
    backgroundColor: "{colors.panel}"
    textColor: "{colors.slate}"
  field:
    backgroundColor: "{colors.panel}"
    textColor: "{colors.navy}"
    typography: "{typography.body}"
    rounded: "{rounded.none}"
    height: "{spacing.control}"
    padding: "0 9px"
  strap:
    backgroundColor: "{colors.band}"
    textColor: "{colors.panel}"
    typography: "{typography.strap-title}"
    rounded: "{rounded.none}"
  strap-tally-on-air:
    backgroundColor: "{colors.live}"
    textColor: "{colors.panel}"
    typography: "{typography.strap-title}"
  strap-tally-off-air:
    backgroundColor: "{colors.slate}"
    textColor: "{colors.panel}"
  header-band:
    backgroundColor: "{colors.band}"
    textColor: "{colors.panel}"
    height: "{spacing.header}"
  tab:
    backgroundColor: "{colors.band}"
    textColor: "{colors.band-soft}"
    typography: "{typography.tab}"
    height: "{spacing.header}"
  tab-selected:
    textColor: "{colors.panel}"
  program-monitor:
    backgroundColor: "{colors.navy}"
    rounded: "{rounded.none}"
    height: "214px"
  url-button:
    backgroundColor: "{colors.panel}"
    textColor: "{colors.navy}"
    typography: "{typography.url}"
    rounded: "{rounded.none}"
    height: "92px"
    padding: "14px"
  url-button-hover:
    backgroundColor: "{colors.action-tint}"
---

# Design System: Rai - Package tile manager

## Overview

**Creative North Star: "The TG Sottopancia"**

The app speaks the on-air language of a Rai News bulletin. Every status the operator needs to read is a lower third: a Rai News blue band, a full-height coloured tab on its left carrying the state, a bold white title and a softer detail line. The running local server is IN ONDA, a red tally exactly like the one in a studio gallery. The package is dropped on a "program monitor", a dark navy field ruled with the tile grid the app writes. Everything else (headings, fields, radio buttons, hints) sits quietly on a pale blue desk, so the dark on-air elements are what the eye reads first.

It is a native Windows 11 tool (WinForms, GDI/GDI+), dense and calm, in Italian. Standard controls stay standard where users expect them (radio buttons, text boxes, file dialogs, a system body font); the brand shows in a small set of drawn components: the header band with the Rai mark, the straps, the monitor, the square buttons, the URL button. The world refuses the stacked grey form and the generic dashed-dropzone dashboard.

Brand sources are the public rai.it and rainews.it stylesheets and the Rai 2016 identity (the square as the dominant shape); see PRODUCT.md. The official Rai square mark is used in the header, reversed in white, at the user's request.

**Key Characteristics:**
- Rai News blue bands with white Inter Tight on a light blue-white desk.
- State carried by the colour of a full-height tab plus a drawn glyph or a short uppercase label, never by colour alone.
- Square corners everywhere; flat, no shadows.
- One entrance motion: the lower-third wipe from the left.
- Full fallback to system colours in Windows high-contrast mode.

## Colors

A deep broadcast blue world: two Rai blues with distinct jobs, three signal colours confined to state tabs, and a cool blue-grey neutral family.

### Primary
- **Rai News Blue** (band): the header band, every strap and lower third, the pressed state of primary buttons. It is the surface the on-air elements are made of, not an accent.
- **Pale Band Blue** (band-soft): text that sits on the band but is secondary: strap detail lines, unselected tab labels, the tab hover underline.

### Secondary
- **rai.it Action Blue** (action): everything the operator can act on: primary button fill, secondary button text and outline, the focused field frame, the progress line fill, links, the `{z}/{x}/{y}` placeholders in the URL, the copy mark, the neutral "info" and "drop" strap tab.
- **Action Hover Blue** (action-hover): primary button hover.
- **Action Tint** (action-tint): hover wash of secondary buttons and the URL button.

### Tertiary (signal tabs)
- **Live Red** (live): only the IN ONDA tally (strap tab and the small square beside the "In onda" tab label) and error tabs.
- **Signal Green** (ok): success tabs, a valid drag over the monitor, the copied state of the URL button.
- **Label Orange** (warn): warning tabs and an invalid drag over the monitor; the rainews.it label colour.

### Neutral
- **Gallery Navy** (navy): body and heading ink, field text, and the program monitor's field. One value, two jobs: ink on the light desk, ground for the dark monitor.
- **Monitor Grid** (monitor-grid) and **Monitor Grid Lit** (monitor-grid-hover): the 1 px tile grid of the monitor at rest and under the pointer.
- **Slate** (slate): hints, version line, secondary-button disabled text, and the FUORI ONDA / disabled tab of a strap.
- **Rule** (rule): 1 px field frames, button outlines when disabled, the progress track, the top rules of the action bar and footer, the pressed wash of light surfaces.
- **Disabled Blue-Grey** (disabled): fill of a disabled primary button.
- **Desk** (desk): the window background under everything.
- **Panel White** (panel): field and URL-button surfaces, secondary button fill, text on blue.

### Named Rules
**The Two Blues Rule.** Rai News Blue is a surface (bands, straps); rai.it Action Blue is an affordance (buttons, focus, links, tokens). A filled action-blue rectangle means "press me"; a navy-blue band means "read me". Never swap them.

**The Red Is On Air Rule.** Live Red appears only for the IN ONDA tally and for errors. Warnings are orange, success is green. (The Windows close-button hover red in the title bar is the platform's own colour, not Live Red.)

**The High-Contrast Fallback Rule.** Every colour role is read through the theme and resolves to a system colour when Windows high contrast is on. No colour is hard-coded at the point of use.

## Typography

**Display Font:** Inter Tight (Regular, SemiBold, Bold), embedded in the exe, SIL OFL; fallback the system message-box font.
**Body Font:** Segoe UI via the system message-box font (radio buttons, field text, hints, labels).

**Character:** Inter Tight is rai.it's display face and gives the straps their broadcast voice; the native body font keeps the standard controls feeling like Windows.

### Hierarchy
- **Broadcast title** (Bold, 15.5pt): the lower third of the program monitor only, because dropping the package is the main action.
- **App name** (Bold, 12.5pt): "Package tile manager" beside the Rai mark in the header band.
- **URL** (Regular, 12.5pt): the GEOlayers address in the URL button.
- **Heading** (SemiBold, 11.5pt): section headings on the desk ("Dove salvare le tile", "Server locale"), navy ink, 20 px above and 6 px below.
- **Tab** (SemiBold, 10.5pt): header tabs, white when selected, pale band blue otherwise.
- **Broadcast detail** (Regular, 10.5pt): monitor lower-third detail.
- **Strap title** (Bold, 10pt): status strap titles and the IN ONDA / FUORI ONDA tab labels (uppercase only there).
- **Button** (SemiBold, 9.75pt): all buttons, sentence case.
- **Strap detail** (Regular, 9.25pt): strap detail lines and the URL button's note line.
- **Body** (Regular, 9pt system): controls, hints and labels.

### Named Rules
**The Padding Follows The Title Rule.** A lower third's paddings derive from its title line height (horizontal 0.8x, vertical 0.5x), so a broadcast-size monitor strap keeps the proportions of a status strap. Never set strap padding in fixed pixels.

**The Sentence Case Rule.** Headings, buttons and strap titles are sentence case Italian. Uppercase is reserved for the on-air tally labels.

## Layout

A single 800 x 700 window (minimum 80% of the default width, at least 480 tall), header band 56 px, three tab pages, a 34 px footer with the version and the guide link. Each page is one vertical column with 28 px side gutters, 14 px top and 18 px bottom padding; it scrolls only when the window is too small. The primary action lives in an action bar docked at the bottom of the page above a 1 px rule (28/6/28/12 padding), so it never scrolls away. There the result strap takes the spare width and the full height of the actions, which stack on its right at one fixed width, primary on top.

Rows are tables where one control takes the spare width (a field) and the rest size to content (buttons). Text fields and buttons share one 32 px height so a row reads as one line. Straps take the full column width, 6 px above and below. A row that starts hidden keeps its place when it appears. All metrics scale with DPI (value x DeviceDpi / 96).

## Elevation & Depth

Flat. There are no shadows anywhere. Depth is tonal: the dark navy monitor and the blue band and straps sit forward of the pale desk, white panels (fields, URL button) sit on the desk behind 1 px rules. The header band is continuous with the custom title bar.

### Named Rules
**The Flat Desk Rule.** Separation comes from tone and 1 px rules, never from shadows or lifts.

## Shapes

Square corners everywhere (0 px), after the Rai 2016 identity's dominant square: buttons, fields, straps, the monitor, the URL button, even the copy mark (two overlapping squares). The one round shape is the on-air dot beside the In onda tab, like a recording light. Borders are 1 px rules at rest and 2 px when focused. Glyphs are drawn: one 2 px round-capped stroke (check, cross, exclamation, info, download), never a font glyph.

## Components

### Buttons
Square, sized by their longest possible label so changing text never moves the row.
- **Shape:** square (0 px), 32 px tall, width max(112 px, longest label + 32 px), 6 px left margin.
- **Primary:** Action Blue fill, white text; hover Action Hover Blue; pressed Rai News Blue.
- **Secondary:** white fill, 1 px Action Blue outline, Action Blue text; hover Action Tint; pressed Rule.
- **Disabled:** primary turns Disabled Blue-Grey with white text; secondary white with Rule outline and Slate text.
- **Focus:** 2 px inner ring inset 3 px, white on primary, navy on secondary, shown with keyboard cues.

### Inputs / Fields
- **Style:** native borderless text box inside a 32 px square frame, white, 1 px Rule border, 9 px side padding.
- **Placeholder:** every field shows one while empty: the native cue banner when editable, painted in Slate over read-only boxes (which draw none).
- **Focus:** frame turns 2 px Action Blue while typing (read-only path fields do not light up).
- **Disabled:** the frame fills with the system disabled colour, following the box.
- **Read-only:** path and URL fields stay out of the Tab order; the button beside them is the stop.

### Navigation
Header tabs on the blue band, right-aligned before the window buttons: SemiBold 10.5pt, 56 px tall, 6 px side padding, so labels sit 12 px apart. Selected is white with a 3 px white bar under the label; hover shows a 1 px pale bar; focus a dotted white rectangle. While the server is on air the "In onda" tab carries a 10 px Live Red dot with a 1 px white edge before its label, on every tab.

### Status Strap (signature)
The sottopancia: Rai News Blue band, a full-height tab on the left in the state colour (Action Blue info, green ok, orange warning, Live Red error) carrying a drawn white glyph, a Bold white title and an optional pale band-blue detail. It sizes its height to its text and wraps to the column. The tally variant replaces the glyph with a text tab: IN ONDA on Live Red, FUORI ONDA on Slate. Entrance: a wipe from the left, exponential ease-out over 240 ms starting a third visible; ticking detail counters do not replay it; skipped when Windows animations are off.

### Program Monitor (signature)
The drop target: a 214 px Gallery Navy field ruled with a 44 px tile grid, its lower third drawn at broadcast scale, inset 24 px, at most 660 px wide, bottom-left. Grid lights up under the pointer. A valid drag turns the tab green with a 3 px green frame; anything else turns it orange with an orange frame. Clicking, Enter or Space opens the file dialog; focus shows a 2 px white frame.

### URL Button (signature)
A 92 px white panel with a 1 px Rule border: a real tile of the project as a square thumbnail (a 3x3 grid on navy when none), the URL in Inter Tight 12.5pt with `{z}`, `{x}`, `{y}` in Action Blue, a note line in Slate, and a copy mark at the right. Click, Enter or Space copies; for two seconds the copy mark fills green and the note turns green.

### GEOlayers Fields
The Raster Source values after the URI, attached under the URL button so the two read as one card (it draws no top edge). Names in Slate, values as Action Blue links in Inter Tight SemiBold 10.5pt, in GEOlayers' order: Min Zoom and Max Zoom, Tile Size, then the four Bounds in degrees (west, south, east, north). A click or Enter copies the bare number; the note line turns green with what was copied for 2.5 s. Unknown bounds read "non disponibili" with the fix in the note.

### Progress Line
A 4 px Rule track filled with Action Blue.

### Header Band
56 px Rai News Blue: the Rai square mark at 30 px reversed in white, 24 px from the left, the app name 12 px after it, the tabs, then native-style window buttons (46 x 56) drawn on the band. The empty band drags the window.

## Do's and Don'ts

### Do:
- **Do** draw every status as a strap: Rai News Blue band, full-height state tab, drawn glyph, Bold title, softer detail.
- **Do** keep Action Blue for things you can press or focus, and Rai News Blue for things you read.
- **Do** keep text fields and buttons in a row at the same 32 px height, with the field taking the spare width.
- **Do** dock the page's primary action in the bottom action bar.
- **Do** resolve every colour through the theme so high-contrast mode falls back to system colours.
- **Do** scale every metric by DeviceDpi / 96.

### Don't:
- **Don't** round the corners of any drawn component (native radio buttons keep their own shape).
- **Don't** use shadows or lifted cards; the desk is flat.
- **Don't** use Live Red outside the on-air tally and errors.
- **Don't** signal state by colour alone; the tab always carries a glyph or a label.
- **Don't** use font glyphs or icon fonts for state icons; draw them as 2 px round-capped strokes.
- **Don't** draw the drop target as a dashed outline box; it is the program monitor.
- **Don't** replace native radio buttons, text boxes or file dialogs with custom look-alikes.
