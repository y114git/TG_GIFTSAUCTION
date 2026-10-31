# UX standards

Status: proposed. These rules apply to every screen in [shell.md](shell.md), [onboarding.md](onboarding.md) and [editors/](editors/). Colours, type and radii come from the token set in [../research/g3m-design.md](../research/g3m-design.md#proposed-g3mstudio-token-set); the visual rules above the tokens are in [../design.md](../design.md). This page adds the behaviour rules and the numbers an implementer needs.

Standards followed: WCAG 2.2 level AA (contrast, keyboard, focus, target size, dragging alternatives), Nielsen's ten heuristics, Windows, macOS and GNOME/KDE platform conventions where they do not break the G3M look, VS Code interaction conventions (command registry, palette, tabs, Problems, Output) and GameMaker conventions (Inspector, resource creation, Go To, room editor keys).

Terms follow [../l10n/glossary.md](../l10n/glossary.md): data file, resource, texture page, texture group, audio group, general info, game options, local history, patch, mod, modpack, launch game, command palette, quick open, start page. The glossary has no entry for the left panel; this spec calls it Explorer (open question 1 in [shell.md](shell.md#18-open-design-questions)).

## 1. Spacing, size and density

Spacing scale (px): 2, 4, 6, 8, 12, 16, 24. Nothing else. Use it for padding, margins, gaps and icon-to-label distance.

| Use | Value |
| --- | --- |
| Gap between icon and label | 6 |
| Gap between related controls in a row | 4 |
| Gap between form rows | 8 |
| Gap between form groups | 16 |
| Pane padding | 8 |
| Dialog padding | 16 |
| Dialog button gap | 8 |
| Section gap on the start page and in Settings | 24 |

Two densities, one setting (`ui.density`, default `standard`):

| Element | Standard | Compact |
| --- | --- | --- |
| Pane control height | 24 | 22 |
| Dialog control height | 28 | 24 |
| Tree row | 24 | 22 |
| List and table row | 24 | 22 |
| Tab | 28 | 26 |
| Title bar | 32 | 32 |
| Status bar | 24 | 22 |
| Icon in toolbars | 16 | 16 |
| Scrollbar thickness | 10 (14 on hover) | 10 (14 on hover) |

Standard rows are 24 px, not the 22 px the token doc proposes, because WCAG 2.2 success criterion 2.5.8 asks for pointer targets of at least 24 x 24 px. Compact is opt-in and documented in Settings as below that minimum.

Hit targets: every icon button, tab close button, tree chevron, checkbox and splitter handle has a hit area of at least 24 x 24 px even when the drawn glyph is 16 px or the splitter line is 2 px. Splitters get a 6 px grab zone centred on the 2 px line.

Borders: exactly 2 px everywhere (design.md rule 2). Structural lines use `Border.Subtle`. A control that must be identified as a control (button, field, combo box, checkbox, tab) uses `Border` (4.5:1 on `Bg.Surface`, WCAG 1.4.11 needs 3:1). `Border.Subtle` (1.7:1) is decoration only and never the only edge of an input.

Radii: `Radius.Control` 4, `Radius.Panel` 6, `Radius.Window` 7, always clamped to half the smaller side.

## 2. Colour use and contrast

Tokens come from the proposed set in [research/g3m-design.md](../research/g3m-design.md#proposed-g3mstudio-token-set). Every colour used in design/ is one of these names; no spec writes a raw hex. "New" means the token is not part of G3M's own palette; the text in brackets names the source of the value.

| Token | Hex | New | Use |
| --- | --- | --- | --- |
| `Bg.Window` | `#282828` | no (G3M `background`) | Window, docking area |
| `Bg.Surface` | `#222222` | no (G3M `elements`) | Controls, panels |
| `Bg.Sunken` | `#1B1B1B` | yes | Code editor, hex view, dense lists |
| `Bg.Raised` | `#2B3431` | yes (from G3MTool's button) | Pane headers, toolbars, tab strip |
| `Bg.Overlay` | `#E6282828` | no (G3M tooltip background) | Tooltips, popups, flyouts |
| `Border` | `#039D5B` | no (G3M `border`) | Interactive control outline |
| `Border.Subtle` | `#3A4A43` | yes (from G3MTool's hover) | Splitters, grid lines, structural dividers |
| `Hover` | `#5A6370` | no (G3M `#616B78`, darkened for contrast) | Hover fill |
| `Pressed` | `#4E5763` | yes | Pressed fill |
| `Select` | `#ECEDEF` | no (G3M `select`) | Selection outline, checked fill, focus ring |
| `Selection.Bg` | `#14523A` | yes | Selected row fill in dense lists and trees |
| `Text` | `#E8E9EB` | no (G3M `main_text`) | Body text, icons |
| `Text.Accent` | `#6DE985` | no (G3M `secondary_text`) | Versions, metadata, links |
| `Text.Muted` | `#9CA3AF` | yes (from G3MTool's muted) | Hints, captions, placeholders |
| `Text.OnFill` | `#0F0F0F` | yes | Text on `Accent.Fill` and `Danger.Fill` |
| `Accent.Fill` | `#039D5B` | yes (from G3MTool's primary) | Primary button fill |
| `Danger` | `#FF9F9F` | yes (from G3M-MCE's danger) | Error text and error outline |
| `Danger.Fill` | `#F44336` | no (G3M uninstall button) | Destructive button fill |
| `Warning` | `#FFB74D` | yes | Warning text and outline |
| `Success` | `#6DE985` | no (G3M `secondary_text`) | Success text |
| `Info` | `#5599FF` | no (G3M rich text blue) | Info text |
| `Disabled.Bg` | `#333333` | no | Disabled control fill |
| `Disabled.Text` | `#888888` | no | Disabled control text |
| `Disabled.Border` | `#555555` | no | Disabled control border |
| `Grid.Light` | `#3A3A3A` | yes | Light square of the transparency checkerboard |
| `Grid.Dark` | `#2B2B2B` | yes | Dark square of the transparency checkerboard |

`Grid.Light` and `Grid.Dark` are the defaults of `appearance.transparencyGrid` ([settings.md](settings.md) section 5.1). Type, spacing and radius tokens are in section 1.

Contrast ratios below are measured with the WCAG relative luminance formula on these hexes.

| Pair | Ratio | Rule |
| --- | --- | --- |
| `Text` on `Bg.Window` / `Bg.Surface` / `Bg.Sunken` / `Bg.Raised` | 12.1 / 13.1 / 14.2 / 10.6 | Body text everywhere |
| `Text` on `Hover` / `Pressed` / `Selection.Bg` | 5.0 / 6.0 / 7.5 | Allowed |
| `Text.Accent` on `Bg.Surface` / `Bg.Sunken` / `Bg.Raised` | 10.3 / 11.2 / 8.3 | Links, versions, metadata |
| `Text.Accent` on `Selection.Bg` | 5.9 | Allowed |
| `Text.Muted` on `Bg.Window` / `Bg.Surface` / `Bg.Sunken` / `Bg.Raised` | 5.8 / 6.3 / 6.8 / 5.0 | Hints, captions, placeholders |
| `Text.Muted` on `Hover` | 2.4 | Forbidden. Use `Text` on hovered rows. |
| `Text.Muted` on `Selection.Bg` | 3.6 | Forbidden for text. Use `Text` or `Text.Accent` on selected rows. |
| `Danger` on `Bg.Surface` / `Bg.Sunken` | 8.1 / 8.8 | Error text and outline |
| `Warning` on `Bg.Surface` / `Bg.Sunken` | 9.2 / 10.0 | Warning text and outline |
| `Info` on `Bg.Surface` / `Bg.Sunken` | 5.6 / 6.1 | Info text |
| `Text.OnFill` on `Accent.Fill` | 5.5 | Primary button text |
| `Text.OnFill` on `Danger.Fill` | 5.2 | Destructive button text. White on `Danger.Fill` is 3.7 and is forbidden. |
| `Border` on `Bg.Surface` / `Bg.Window` / `Bg.Sunken` | 4.5 / 4.2 / 4.9 | Control outline (needs 3:1) |
| `Select` on `Bg.Surface` / `Hover` / `Selection.Bg` | 13.6 / 5.2 / 7.8 | Focus ring, checked fill, selection bar |
| `Selection.Bg` on `Bg.Surface` | 1.7 | Not enough alone. See selected rows. |

Disabled controls are exempt from contrast rules (WCAG 1.4.3) but stay legible: `Disabled.Text` on `Disabled.Bg` is 3.6:1.

Rules:

1. Meaning is never carried by colour alone. Severity has an icon and a word (Error, Warning, Info) as well as a colour. Git status has a letter (M, A, U, D, C). Changed values have a marker dot and an Undo/Reset action, not only a tint.
2. Selected row (tree, list, table): `Selection.Bg` fill plus a 2 px `Select` bar inside the leading edge of the row, drawn in the row's left padding so text does not move. A fill alone is 1.7:1 and fails WCAG 1.4.11. The list that has focus also draws the 2 px `Select` focus ring around the row. This adds the bar to the "fill only" rule in the token doc.
3. Hovered row: `Hover` fill, no border change.
4. Invalid field: 2 px `Danger` border plus message text in `Danger` with an error icon below the field.
5. Custom G3M theme import can break these ratios. After import, a check lists failing pairs in Settings > Appearance and offers "Fix contrast" (nudges the failing token to the nearest passing value). It never blocks the import.
6. Windows high contrast (contrast themes) and macOS "Increase contrast" switch to the system colours and keep the 2 px borders; the image background is hidden.

## 3. Focus and keyboard

1. Every interactive element is reachable with Tab and Shift+Tab in reading order (left dock, editor area, right dock, bottom panel, status bar). F6 and Shift+F6 jump between regions: title bar menu, left dock, editor area, right dock, bottom panel, status bar.
2. Focus indicator: 2 px `Select` outline with 2 px offset, shown for keyboard focus only (`:focus-visible`). Never removed. It must not be clipped by a parent: panes keep 2 px of padding so rings stay visible (WCAG 2.4.11).
3. Composite controls (tree, list, table, tab strip, toolbar, menu) are one tab stop. Arrow keys move inside. Home and End go to the first and last item. Type-ahead jumps to an item by name.
4. Escape closes the topmost popup, then clears the focused search field, then returns focus to the editor.
5. A dialog traps focus, focuses its first field (or the primary button for message dialogs), and returns focus to the opener on close. Enter presses the primary button unless focus is in a multi-line field. Escape cancels.
6. Every command is in the command palette (N-UI-3). A feature reachable only by mouse is a bug.
7. Every drag has an alternative that needs no dragging (WCAG 2.5.7): move buttons, numeric fields, cut and paste, or a Move To menu. Each editor spec names it.
8. No shortcut fires while an IME composition is active. Enter and Escape belong to the IME until the composition commits.
9. Shortcuts use the platform modifier: Ctrl on Windows and Linux, Cmd on macOS. Shortcut text in menus and tooltips is drawn from the active platform and the user's keymap, never typed into a string.
10. Keymaps: the default keymap, plus built-in presets VS Code, GameMaker and UTMT (F-UI-5, the UTMT-KEY items). Presets change bindings only; every command exists in all of them.
11. Chords (for example Ctrl+K Ctrl+S) show the pending first key in the status bar and time out after 2 seconds.

## 4. Platform conventions

| Topic | Windows | Linux | macOS |
| --- | --- | --- | --- |
| Menu bar | In the custom title bar, left | Same as Windows | Native menu bar at the top of the screen (Avalonia `NativeMenu`); app menu holds About, Settings (Cmd+,), Hide, Quit |
| Window buttons | Extended client area, system caption buttons kept (snap layouts, Aero Snap) | Own buttons; side follows the desktop setting | System traffic lights, unified title bar |
| Quit | Alt+F4 closes the window; Ctrl+Q quits all windows | Ctrl+Q | Cmd+Q |
| Settings | Ctrl+, | Ctrl+, | Cmd+, |
| File dialogs | System dialogs through Avalonia `StorageProvider` | Portal dialogs where available | System sheets |
| Context menu key | Menu key and Shift+F10 | Same | Control+click, two-finger click |
| Text size | Follows Windows text size scale, then `ui.zoom` | Follows font DPI | `ui.zoom` only |
| Reduce motion | Follows "Show animations" | Follows `gtk-enable-animations` | Follows "Reduce motion" |
| Open with | File associations from onboarding | `.desktop` file and MIME types | `Info.plist` document types |
| Delete key | Delete | Delete | Cmd+Backspace and Delete (fn+Backspace) |
| Reveal in file manager | "Show in Explorer" | "Open Containing Folder" | "Reveal in Finder" |

Labels such as the last row use the platform name for the file manager. Key names on macOS use symbols (Cmd, Option, Shift, Control).

## 5. Motion

1. Hover and border colour: 120 ms. Tooltip fade: 150 ms. Expand and collapse: 200 ms ease-in-out. Progress value: 180 ms. Dock and card moves: 160 ms.
2. Motion explains a change (a panel slides to where it docks). It is never decoration. Animate opacity and transform, not width or height.
3. Setting `ui.reduceMotion`: `system` (default), `on`, `off`. When on, all durations are 0 and spinners become a static "Working" label with the elapsed seconds.
4. Nothing moves for more than 5 seconds unless the user started it (WCAG 2.2.2). Sprite, sequence and particle previews start paused. Pressing Play starts them; each preview has a visible Pause button.
5. No flashing above 3 flashes per second (room effect previews that strobe show a still frame until enabled).

## 6. Copy style

1. English is the source language. Plain, specific, active voice. No filler words, no exclamation marks, no emoji, no em dashes.
2. Case: Title Case for menu items, buttons, dialog titles, panel titles and tab names. Sentence case for field labels, tooltips, messages, hints and empty states.
3. Tooltips, errors and statuses are full sentences ending in a period. Status text that shows ongoing work ends in three ASCII periods ("Saving data.win...").
4. A command that opens a dialog ends its label with three ASCII periods ("Export to G3M...").
5. Placeholders use `{name}` and keep English names in every language. Resource and file names in text are in single quotes: 'obj_player'.
6. No concatenated sentences. Each message is one string with placeholders. Plurals use the language's plural rules (`{count} resources` has one, few, many, other forms).
7. Terms come from the glossary. One term per concept: "resource" for an entry in the data file, "texture page" never "atlas", "patch" never "diff file", "launch game" never "start game".
8. Buttons say the result: "Save", "Apply Patch", "Delete 3 Resources". Never "OK" for a dialog that does something specific. "Cancel" always cancels without side effects.
9. Tooltips name the command and its shortcut: "Save (Ctrl+S)".
10. Numbers and sizes: exact, with unit and a thousands separator from the OS culture ("92.4 MB", "1,204 resources"). Offsets and hashes are monospaced and can be copied.
11. Never blame the user. "The file is not a GameMaker data file." not "You picked a wrong file."

## 7. Confirmations, undo and destructive actions

Order of preference (Nielsen: user control and freedom, error prevention):

1. Make the action undoable and do it without asking. Almost every edit works this way (F-RES-12). A notification with an Undo link follows batch changes ("Deleted 12 resources. Undo").
2. If it cannot be undone, confirm once and say exactly what changes.
3. If it destroys data outside the data file, require a typed or clearly separate confirmation.

| Action | Undoable | Behaviour |
| --- | --- | --- |
| Edit a field, move an element, paint, reorder | Yes | No prompt |
| Delete resources | Yes | Reference scan first; if references exist, the dialog lists them and offers "Delete and Leave Broken References" or "Cancel"; otherwise no prompt |
| Replace in code, project-wide | Yes (one undo step) | Preview list with per-hit checkboxes, then Apply |
| Import over existing frames or audio | Yes | No prompt; notification with Undo |
| Save | Not applicable | Writes a new file, validates, replaces; rotating backups of the previous file are kept. No prompt unless the validation read fails |
| Discard Git changes | No | Confirm; lists file names and counts |
| Restore from a snapshot | Yes | Becomes one undo step; the current state is added to the snapshots first |
| Close or restart with changes not in the data file | Not applicable | Prompt: Sync changes (Save in Direct mode), Do it later, Cancel (shell 14.2 item 8). Do it later leaves the journal, which restores the edits on next launch with a "Restored unsaved changes" notification and a Discard link |
| Reset layout, reset settings | Yes (layout) / No (settings) | Layout: no prompt. Settings: confirm, name the page |
| Delete a backup, clear caches | No | Confirm with size freed |
| Run a script or plugin for the first time | Not applicable | Trust prompt (once per plugin, once per script path) |

Every confirmation dialog has an "Always choose this" checkbox that is stored in Settings > Confirmations as an automatic response (ask, always yes, always no) and stays reversible there (GameMaker pattern, GM Confirm Dialogs).

Dialog anatomy for confirmations: title (the action), one sentence naming the affected items and count, optional expandable list (first 50 items), buttons right-aligned on Windows and Linux, with the primary action last; macOS puts the primary action on the right as well and Cancel to its left. The destructive primary button uses `Danger.Fill`. Focus starts on Cancel for destructive dialogs and on the primary button otherwise.

## 8. Error messages

Pattern: what happened, why, what to do next, and a "Copy Details" button that puts the full text, stack and context on the clipboard (design.md). Three parts in this order, each one sentence.

```
Could not save 'data.win'.
The drive is full (0 bytes free, 92 MB needed).
Free some space or choose another folder with Save As, then try again.
[Save As...]  [Copy Details]  [Close]
```

Rules:

1. Inline errors sit below the field they belong to and are linked to it for screen readers. Typing fixes them live; they are checked on edit, not on every keystroke, and again on Apply or Save.
2. Dialogs with several fields show an error summary at the top after a failed submit: title "There is a problem", one link per error that moves focus to the field. Focus moves to the summary once, not on every blur.
3. Errors from background jobs go to the Problems panel and a persistent notification (errors stay until dismissed). Warnings are notifications that fade after 8 seconds and stay in the history.
4. Severity vocabulary: Error (cannot continue or data would be lost), Warning (can continue, result may be wrong), Info.
5. Codes: every message has a stable code (`G3MS-SAVE-001`) shown after Copy Details and in the Output channel, so users can search and reports are exact.
6. Never show a raw exception message as the only text. Show it under "Details".
7. A message that offers a fix has a button for it ("Disable GameMaker debugger flag", "Unlink missing file").
8. Optional services (crash reporting, the update check) never show an error or crash the app when they fail: no key, no network, an invalid key or an exhausted quota is logged to Output and ignored (F-PRIV-1).

## 9. Progress and cancellation

1. All work over 100 ms runs as a job through the job service (N-PERF-2). The UI stays interactive unless the job takes the workspace lock.
2. Under 300 ms: no indicator. 300 ms to 2 s: spinner in the status bar item. Over 2 s: determinate bar when the total is known, with text "{operation}: {done} of {total}". Indeterminate bars show elapsed time.
3. Status bar jobs item shows the newest job and "+N more". Click opens the Jobs list (a tab of the Output panel) with each job, its progress, Cancel, and its log channel.
4. Loading a data file shows a progress bar inside the Explorer area (not a modal) with Cancel; the shell is usable at once. Save, apply patch and launch game with changes take the workspace lock and show a blocking progress dialog with Cancel only when the job can truly stop. Otherwise they show "Finishing..." and disable Cancel with a tooltip that says why.
5. Cancel stops at the next safe point, rolls back partial output (temp files deleted, workspace unchanged), and reports "Cancelled. No changes were made." Jobs that cannot roll back say so before starting.
6. A finished job ends with a notification only if the user is not looking at its result: a result for an action they triggered shows "Done" with an action ("Show in Explorer", "Open Report"). Info notifications hide after 5 seconds.
7. Long operations log to their Output channel with timestamps.

## 10. Notifications

1. Toast stack bottom right, one visible at a time, merged when identical, newest on top, max 3 queued.
2. Each toast: icon, one-sentence text, up to two actions, close button. Errors stay. Info hides after 5 s, warnings after 8 s. Hover or keyboard focus pauses the timer.
3. The bell in the status bar shows the unread count and opens the history (last 100). Do Not Disturb silences info and warning toasts and never hides errors.
4. Toasts are announced to screen readers as polite live regions (errors as assertive).
5. No promotional or feedback toasts. Release notes after an update appear as one toast with "What's New", never as a tab opened by surprise.

## 11. Shared controls

These are built once and reused. Each editor spec names them by this table. Parity: UTMT-CTRL-01 to 06, UTMT-APP-13, UTMT-APP-15.

| Control | Behaviour |
| --- | --- |
| Reference field (UTMT-CTRL-01) | A box that shows a thumbnail (when the type has one), the name and the type icon. Click selects it in the Explorer; double-click, Enter or the "Open" button opens the editor; middle-click or Ctrl+Enter opens in a new tab; the "Choose..." button opens a picker (searchable list filtered to the allowed type, "None" first); "Clear" (X) sets none; dropping a resource of the allowed type from the Explorer sets it; a dropped wrong type shows "Only sprites can be used here." Keyboard: Enter opens the picker, Delete clears. The field is invalid (red border) when it points at an index that does not exist. |
| String reference field (UTMT-CTRL-02) | A text box bound to a shared string in the STRG table. Editing a string used by more than one place asks once: "Change only this value", "Change all {count} uses", "Cancel" (UTMT-AST-08), with a "Show uses" link. "..." opens the string resource; dropping a string resource relinks. |
| Texture page item display (UTMT-CTRL-03) | Image on a checkerboard (colours from `appearance.transparencyGrid`, default `Grid.Dark` and `Grid.Light`) with the bounding box drawn in `Info`; zoom 25% to 3200%; pixel grid at 800% and above. Alt text: "Texture page item {index}, {w} by {h}". |
| Flags box (UTMT-CTRL-04) | A collapsible group with one checkbox per flag, in two columns, each with a tooltip explaining the flag. Unknown bits show as "Unknown bit {n}" and round-trip untouched. |
| Colour field (UTMT-CTRL-05) | Swatch (24 x 24) plus hex text `AARRGGBB` or `RRGGBB` per field type and a picker flyout with ARGB sliders, HSV square, eye dropper and the last 8 colours. The swatch has a text label for screen readers ("Colour #FF8800"). |
| List grid (UTMT-CTRL-06) | A virtualized table with an always-present last row "Add..." (replaces "double-click to add"; the row is a button and a keyboard target), per-row delete, drag handle plus Alt+Up/Alt+Down to move, multi-select, in-cell edit with F2 or typing, Tab to next cell, Enter to commit, Escape to revert. Column headers sort when sorting is meaningful. |
| Number field | Text box with spin buttons, mouse-wheel step only when focused, arrow keys step by 1, Shift+arrow by 10, Ctrl+arrow by 0.1 for floats. Clamps on commit and shows "Value must be between {min} and {max}." Empty reverts to the last value. |
| Index field | Number field showing a resource or array index with a "Go" button that opens the target when the index resolves ("Resolves to 'spr_player'"). |
| Offset field | Hex text field, monospaced, accepts `0x` prefix, copy button. Read-only in most places. |
| Name field | Resource name rules: starts with a letter or underscore, then letters, digits or underscore, 1 to 64 characters, unique across all resource names of the data file (GML scope). Warning (not error) when the name hides a built-in function. Error text: "Names can use letters, digits and underscores and cannot start with a digit." and "'{name}' is already used by a {type}." |
| Size and point editors | W/H and X/Y pairs, optional lock-ratio toggle, tab order X then Y. |
| Text input dialog (UTMT-APP-13) | Title, optional prompt, single-line or multiline box, validation hook with inline message, custom button captions, used by commands and scripts. |
| Message dialog (UTMT-APP-15) | Question, warning, error: icon, one-sentence title, optional details expander, "Copy Details", buttons from section 7. |
| Result list | One component renders Search results, Problems, References and Changes: grouped tree (resource > hits), icon, severity, text with the match highlighted, location path ("objects/obj_player > Create > line 12"), Open, Open in New Tab, Copy, Copy All, Export. |
| Property grid | The Inspector body. Categories collapse, search filters rows, differing multi-select values show "-", a changed-from-original marker, per-row "Reset to Original". |

## 12. Accessibility checklist

A screen is not done until every line is true. This list is the test script for the headless UI tests and the manual pass.

1. All text and icons meet section 2. A test walks every `Text`/background pair in the token set.
2. Keyboard only: every action in the screen is reachable and every region has a visible focus ring. No keyboard trap other than modal dialogs.
3. Tab order follows reading order. F6 cycles regions.
4. Names and roles: every control sets `AutomationProperties.Name` (from the localized label) and the right control type. Icon buttons have a name equal to their tooltip. Custom canvases (room, sprite, path, sequence, curve, texture page) expose an automation peer that names the focused element and has a parallel list or table (element list, point table, track panel) that offers the same actions.
5. State is exposed: expanded, selected, checked, invalid (with the message), busy, read-only, modified.
6. Live regions announce: job finished, error added to Problems, notification shown, search result count.
7. No information by colour alone (section 2 rule 1).
8. Text scales to 200% (`ui.zoom`) without clipped text or lost controls; the layout reflows by collapsing docks. Minimum window size 960 x 600 at 100%.
9. Pointer targets at least 24 x 24 px at standard density.
10. Dragging has an alternative (section 3 rule 7). Pointer actions have single-pointer alternatives (no path-based gestures required).
11. Previews that move can be paused (section 5).
12. Focus is never hidden under a floating toolbar, toast or sticky header; floating elements shift or the view scrolls the focused control into view.
13. Time limits: none. Toasts that carry the only copy of an error also write it to the Problems panel.
14. Errors identify the field, describe the problem in text and suggest a fix (WCAG 3.3.1, 3.3.3).
15. Consistent help: the Help menu, the command palette and the status bar are in the same place on every screen (WCAG 3.2.6).
16. Redundant entry: values already entered in a dialog or earlier onboarding step are prefilled (WCAG 3.3.7).
17. Screen reader platforms: UI Automation on Windows, NSAccessibility on macOS, AT-SPI on Linux through Avalonia. Verified with Narrator and VoiceOver before the beta; Orca is best effort.

## 13. Localization rules

1. No literal UI text in code or AXAML (F-L10N-1). Every string is a key; keys are `area.screen.element` ("explorer.menu.rename").
2. Text expansion: layouts are built for 40% longer text than English for ru, es and 30% for others; short labels (under 10 characters) get up to 200% room. Buttons size to content with a minimum width of 72 px; no fixed-width labels. Tabs, status bar items and column headers truncate with an end ellipsis and a tooltip with the full text, in that order of priority.
3. Test pass: every screen is checked in ru (long words), ja (no spaces, vertical metrics), zh_tw and with `ui.zoom` 150%, plus a pseudo-locale (accented, 40% longer) in debug builds.
4. Fonts: Noto Sans for en, es, ru; Noto Sans JP, KR, SC, TC chosen by UI language, with the others as fallbacks so mixed text renders (a Japanese resource name in an English UI). Code font Noto Sans Mono with CJK fallback to the language's Noto Sans CJK; the code editor never mixes widths in a line without the fallback (CJK glyphs take two cells).
5. CJK: no italics, no letter-spacing, no uppercase transforms; line height 1.4 minimum; word wrap breaks at character boundaries; the UI font weight Regular is thin at 13 px on CJK, so Strong text uses Bold and tab labels use Medium where the family provides it.
6. Numbers, dates, sizes and sort order follow the OS culture, with an override in Settings > Language (`ui.formatCulture`). Sorting the Explorer by name uses ordinal, case-insensitive order so the order matches GameMaker.
7. Plural forms per language: ru has one, few, many, other; ja, ko and zh have only other. Strings that take a count are written with plural forms for every language.
8. Shortcuts shown in menus follow the platform and the keymap. Shortcut hints in tooltips come from the command registry.
9. Never build a sentence from fragments; never embed a number inside a translated phrase without a placeholder.
10. Right-to-left languages are not in the beta set. Layout uses logical start and end alignment and `FlowDirection` so adding one later does not need a redesign.
11. Resource names, file paths, hex and code are never translated and always render left-to-right and monospaced where they are code.
12. Input: IME composition shows inline (no extra popups over the field); text fields accept full-width digits where a number is expected and convert them.
13. Missing key fallback: selected language, English, key id; logged to the Output channel "Localization".

## 14. Performance rules that shape the design

1. Lists and trees over 100 rows are virtualized. Heavy previews decode lazily with an LRU cache and render at display size.
2. Inactive canvas cards and background tabs render once to a bitmap and stop updating.
3. An editor opens in its empty or loading state within one frame, then fills (section 9).
4. Typing, dragging and scrolling never wait on a job.
5. Idle CPU is 0%: no running animation unless the user started one.

## 15. Parity coverage

- UTMT-CTRL-01 to UTMT-CTRL-06: section 11 (shared controls).
- UTMT-APP-13, UTMT-APP-15: section 11 (text input and message dialogs).
- UTMT-APP-08: sections 9 and 10 (progress and notifications replace the modal loader).
- UTMT-APP-09: sections 1 and 2 (one themeable default theme with contrast rules; replaced, no light and dark toggle).
- UTMT-SET-11: section 2 (replaced by the single theme; see settings.md).
