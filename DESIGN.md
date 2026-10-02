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

The app uses the on-air language of a Rai News bulletin. Every status is a lower third: a Rai News blue band, a full-height coloured tab with the state, a bold white title and a softer detail line. The running server is IN ONDA, a red tally like the one in a studio gallery. The package is dropped on a "program monitor", a dark navy field ruled with the tile grid the app writes. Headings, fields, radio buttons and hints sit quietly on a pale blue desk, so the eye reads the dark on-air elements first.

It is a native Windows tool (WinForms, GDI/GDI+), dense and calm, in Italian. Standard controls stay standard: radio buttons, text boxes, file dialogs, the system body font. The brand lives in a few drawn components: the header band with the Rai mark, the straps, the monitor, the square buttons, the URL button and the map preview.

Brand sources are the public rai.it and rainews.it stylesheets and the Rai 2016 identity, with the square as the dominant shape (see PRODUCT.md). The official Rai square mark is in the header, reversed in white, at the user's request.

**Key Characteristics:**
- Rai News blue bands with white Inter Tight on a light blue-white desk.
- State shown by the colour of a full-height tab plus a drawn glyph or a short uppercase label, never by colour alone.
- Square corners, flat surfaces, no shadows.
- One entrance motion: the lower-third wipe from the left.
- Full fallback to system colours in Windows high contrast.

## Colors

Two Rai blues with separate jobs, three signal colours kept to the state tabs, and a cool blue-grey neutral family.

### Primary
- **Rai News Blue** (band): the header band, every strap and lower third, the pressed primary button. It is the surface the on-air elements are made of.
- **Pale Band Blue** (band-soft): secondary text on the band: strap details, unselected tabs, the tab hover bar, the preview window's subtitle.

### Secondary
- **rai.it Action Blue** (action): everything the operator can act on: primary button fill, secondary button text and outline, the focused field frame, the progress fill, links, the `{z}/{x}/{y}` tokens in the URL, the copy mark, the info and drop strap tab.
- **Action Hover Blue** (action-hover): primary button hover.
- **Action Tint** (action-tint): hover wash of secondary buttons and the URL button.

### Tertiary (signal tabs)
- **Live Red** (live): only the IN ONDA tally (the strap tab and the dot beside the "In onda" tab) and error tabs.
- **Signal Green** (ok): success tabs, a valid drag over the monitor, the copied URL button.
- **Label Orange** (warn): warning tabs, an invalid drag over the monitor, the comp frame of a missing zoom; the rainews.it label colour.

### Neutral
- **Gallery Navy** (navy): ink for text and fields, and the ground of the monitor and the map preview.
- **Monitor Grid** (monitor-grid) and **Monitor Grid Lit** (monitor-grid-hover): the monitor's 1 px tile grid at rest and under the pointer; Monitor Grid also hatches the preview where tiles are missing.
- **Slate** (slate): hints, the version line, disabled secondary text, the FUORI ONDA tab, the preview's status line.
- **Rule** (rule): 1 px field frames, disabled button outlines, the progress track, the rules above the action bar and footer, the pressed wash of light surfaces.
- **Disabled Blue-Grey** (disabled): the fill of a disabled primary button.
- **Desk** (desk): the window background.
- **Panel White** (panel): fields, the URL button, secondary button fill, text on blue.

### Named Rules
**The Two Blues Rule.** Rai News Blue is a surface (bands, straps); rai.it Action Blue is an affordance (buttons, focus, links, tokens). A filled action-blue rectangle means "press me", a band means "read me". Never swap them.

**The Red Is On Air Rule.** Live Red appears only for the IN ONDA tally and for errors; warnings are orange, success is green. The red hover of the close button is the Windows colour.

**The High-Contrast Fallback Rule.** Every colour comes from the theme and resolves to a system colour in Windows high contrast. No colour is hard-coded where it is used.

## Typography

**Display Font:** Inter Tight (Regular, SemiBold, Bold), embedded in the exe under the SIL OFL; fallback the system message-box font.
**Body Font:** Segoe UI through the system message-box font (radio buttons, field text, hints, labels).

**Character:** Inter Tight, rai.it's display face, gives the straps their broadcast voice; the native body font keeps the standard controls feeling like Windows.

### Hierarchy
- **Broadcast title** (Bold, 15.5pt): only the program monitor's lower third, the main action.
- **App name** (Bold, 12.5pt): the window title beside the Rai mark.
- **URL** (Regular, 12.5pt): the GEOlayers address in the URL button.
- **Heading** (SemiBold, 11.5pt): section headings on the desk, navy, 20 px above and 6 px below.
- **Tab** (SemiBold, 10.5pt): header tabs, white when selected, pale band blue otherwise.
- **Broadcast detail** (Regular, 10.5pt): the monitor's lower-third detail and the preview window's subtitle.
- **Strap title** (Bold, 10pt): strap titles and the IN ONDA / FUORI ONDA labels, the only uppercase.
- **Button** (SemiBold, 9.75pt): all buttons, in sentence case, and the map preview's chips.
- **Strap detail** (Regular, 9.25pt): strap details and the URL button's note.
- **Body** (Regular, 9pt system): controls, hints and labels.

### Named Rules
**The Padding Follows The Title Rule.** A lower third's paddings derive from its title line height (0.8x horizontal, 0.5x vertical), so the broadcast-size monitor strap keeps the proportions of a status strap. Strap padding is never a fixed pixel value.

**The Sentence Case Rule.** Headings, buttons and strap titles are Italian sentence case. Uppercase is only for the on-air tally labels.

## Layout

One 800 x 700 window (at least 80% of the default width and 480 px tall) with a 56 px header band, three tab pages and a 34 px footer with the version and the guide link. Each page is one column with 28 px side gutters, 14 px top and 18 px bottom padding, scrolling only when the window is too small. The primary action sits in an action bar docked at the bottom above a 1 px rule (padding 28/6/28/12), so it never scrolls away; there the result strap takes the spare width and the full height of the actions, which stack on its right at one fixed width, primary on top.

Rows are tables where one control takes the spare width (a field) and the others size to content (buttons). Fields and buttons share a 32 px height, so a row reads as one line. Straps take the full column width with 6 px above and below. A row that starts hidden keeps its place when it appears. Every metric scales with DPI (value x DeviceDpi / 96).

## Elevation & Depth

Flat, with no shadows. Depth is tonal: the navy monitor, the blue band and the straps sit forward of the pale desk; white panels (fields, URL button) sit on the desk behind 1 px rules. The header band is the title bar.

### Named Rules
**The Flat Desk Rule.** Separation comes from tone and 1 px rules, never from shadows or lifts.

## Shapes

Square corners (0 px) everywhere, after the dominant square of the Rai 2016 identity: buttons, fields, straps, the monitor, the URL button, even the copy mark (two overlapping squares). The one round shape is the on-air dot beside the In onda tab, like a recording light. Borders are 1 px at rest and 2 px when focused. State glyphs are 2 px round-capped strokes: check, cross, exclamation, info, download.

## Components

### Buttons
Square, sized by their longest possible label, so a text change never moves the row.
- Shape: 0 px corners, 32 px tall, width max(112 px, longest label + 32 px), 6 px left margin.
- Primary: Action Blue fill, white text; hover Action Hover Blue; pressed Rai News Blue.
- Secondary: white fill, 1 px Action Blue outline and text; hover Action Tint; pressed Rule.
- Disabled: primary in Disabled Blue-Grey with white text; secondary white with a Rule outline and Slate text.
- Focus: a 2 px inner ring inset 3 px, white on primary and navy on secondary, shown with keyboard cues.

### Inputs / Fields
- Style: a native borderless text box in a 32 px square white frame with a 1 px Rule border and 9 px side padding.
- Placeholder: every field shows one while empty, the native cue banner when editable, painted in Slate over read-only boxes.
- Focus: the frame turns 2 px Action Blue while typing; read-only path fields stay unlit.
- Disabled: the frame takes the system disabled colour, like the box.
- Read-only: path and URL fields stay out of the Tab order; the button beside them takes the focus.

### Navigation
Header tabs on the band, right-aligned before the window buttons: SemiBold 10.5pt, 56 px tall, touching, 8 px side padding, label and on-air dot centred. The selected tab is white with a 3 px white bar across it; hover shows a 1 px pale bar, focus a dotted white rectangle. While the server is on air, the "In onda" tab shows a 10 px Live Red dot with a 1 px white edge before its label, whatever tab is open.

### Status Strap (signature)
The sottopancia: a Rai News Blue band, a full-height tab in the state colour (Action Blue info, green ok, orange warning, Live Red error) with a white glyph, a Bold white title and an optional pale band-blue detail. Its height follows its text, wrapped to the column. The tally variant has a text tab instead: IN ONDA on Live Red, FUORI ONDA on Slate. It enters with a wipe from the left, an exponential ease-out over 240 ms from a third visible; ticking counters do not replay it, and Windows with animations off skips it.

### Program Monitor (signature)
The drop target: a 214 px Gallery Navy field ruled with a 44 px tile grid, with its lower third at broadcast size, bottom-left, inset 24 px and at most 660 px wide. The grid lights up under the pointer. A valid drag turns the tab and a 3 px frame green; anything else turns them orange. Click, Enter or Space opens the file dialog; focus shows a 2 px white frame.

### URL Button (signature)
A 92 px white panel with a 1 px Rule border: a project tile as a square thumbnail (a 3x3 grid on navy when there is none), the URL in Inter Tight 12.5pt with `{z}`, `{x}`, `{y}` in Action Blue, a Slate note line and a copy mark on the right. Click, Enter or Space copies it; for two seconds the copy mark fills green and the note turns green.

### GEOlayers Fields
The Raster Source values after the URI, attached under the URL button with no top edge, so the two read as one card. Names in Slate, values as Action Blue links in Inter Tight SemiBold 10.5pt, in GEOlayers' order: Min Zoom and Max Zoom, Tile Size, the four Bounds in degrees (west, south, east, north). A click or Enter copies the bare number, and for 2.5 s the note turns green with what was copied. Unknown bounds read "non disponibili", with the fix in the note.

### Map Preview (signature)
A separate window with the same header band and the project name as subtitle. The tiles at their real size, one tile pixel per screen pixel, on Gallery Navy; where a zoom has no tile, a Monitor Grid diagonal hatch, with a legend chip at the bottom-left while any is on screen. In the middle, the comp frame: a 2 px white line over a 4 px navy shade, halved one zoom level at a time until it fits. Above it a Rai News Blue chip in white SemiBold 9.75pt gives the zoom a Full HD comp needs, the one for 4K and the width. A zoom the project lacks reads "(manca)"; when it is the Full HD one, the frame turns orange. Drag pans; the wheel, + and − or a double click change the zoom one level at a time; a Slate status line gives the zoom and, under the pointer, the coordinates and the tile.

### Progress Line
A 4 px Rule track filled with Action Blue.

### Header Band
56 px of Rai News Blue: the Rai square mark at 30 px in white, 24 px from the left, the window title 12 px after it (the preview adds the project in pale band blue), the tabs, then native-style window buttons (46 x 56) drawn on the band. The empty band drags the window.

## Do's and Don'ts

### Do:
- Do draw every status as a strap: band, full-height state tab, drawn glyph, Bold title, softer detail.
- Do keep Action Blue for what can be pressed or focused and Rai News Blue for what is read.
- Do keep fields and buttons in a row at one 32 px height, the field taking the spare width.
- Do dock the page's primary action in the bottom action bar.
- Do take every colour from the theme, so high contrast falls back to system colours.
- Do scale every metric by DeviceDpi / 96.

### Don't:
- Don't round the corners of a drawn component (native radio buttons keep their own shape).
- Don't use shadows or lifted cards.
- Don't use Live Red outside the on-air tally and errors.
- Don't signal state by colour alone; the tab always carries a glyph or a label.
- Don't use font glyphs or icon fonts for state icons.
- Don't draw the drop target as a dashed outline box.
- Don't replace native radio buttons, text boxes or file dialogs with look-alikes.
