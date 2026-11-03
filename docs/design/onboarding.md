# Onboarding

Status: proposed. Covers F-ONB-1 (language, theme import from G3M, file associations, short tour), the layout preset choice from ADR-0010, and the first game. Rules that apply to every screen are in [ux-standards.md](ux-standards.md). The shell it leads into is in [shell.md](shell.md).

Parity and suite IDs covered here: UTMT-PLAT-01 (file associations), UTMT-SET-04, UTMT-RUN-06 (shell verbs), UTMT-APP-07 (welcome view), G3M-36 (game detection).

## 1. Principles

1. Six short steps, each answerable in under 20 seconds, none required. The whole flow takes about two minutes.
2. Every step has a default that is already good. "Skip Setup" on any screen applies all defaults at once.
3. Every choice is reversible in Settings, and the screen says where ("You can change this later in Settings > Language.").
4. No account, no marketing, no usage analytics. Only two things can leave the PC: the update check (GitHub sees the IP address) and scrubbed crash reports, which the welcome step explains and lets the user switch off (F-PRIV-1).
5. The flow is a normal window pane, not an operating system modal. Keyboard: Tab through controls, Enter for the primary button, Alt+Left for Back, Escape asks "Skip setup?" instead of quitting.
6. Each step shows "Step {n} of 6" and the title is the first focus target for screen readers.

## 2. When it runs

| Situation | Behaviour |
| --- | --- |
| First launch, started normally | Full-window flow before the start page |
| First launch, started by opening a file (double-click, `g3mstudio://`) | The file opens first. The flow appears as a "Getting Started" tab beside it, steps 1 to 4 only, starting with the welcome step's crash report notice, with the tour offered after |
| Settings file missing or `onboarding.completed` is false after an update that added steps | A "Finish setup" notification with a "Set Up" action; never a modal |
| Help > Getting Started | Re-runs the flow with the current values preselected |
| Help > Interface Tour | Runs only the tour (step 6) |
| Environment variable `G3MSTUDIO_NO_ONBOARDING=1` (CI and automated tests) | Skipped, defaults applied |

State is stored in the user settings folder as `onboarding.json`: `completed`, `version` (flow version), `skippedSteps`, `tourSeen`. Changing the flow version offers only the new steps.

## 3. Step 0: Welcome

Layout: 880 x 600 centred pane. Left: the G3M logo (diamond G, 96 px) over the background image. Right: text and buttons. A progress rail with six dots runs along the bottom; the current dot is `Select`, finished dots `Accent.Fill`.

Copy:

- Title: "Welcome to G3MStudio"
- Body: "G3MStudio edits GameMaker games: sprites, rooms, code, sounds and more. It also builds mods and patches. Setup takes about two minutes and every choice can be changed later."
- Primary button: "Get Started"
- Secondary button: "Skip Setup"
- Crash report notice with a switch: "G3MStudio sends anonymous crash reports so problems can be fixed. A report has no file contents, no user name and no IP address, and paths are shortened. You can change this in Settings > Advanced." Check box "Send crash reports" (checked) and a link "What is sent". Sentry is not started and nothing is sent until this notice has been shown (F-PRIV-1, Q29, Q32).
- Footer link: "Read the documentation" (opens the docs in the browser)

Actions: "Get Started" goes to step 1. "Skip Setup" applies the defaults in section 10, writes `completed: true`, opens the start page and shows a notification "Setup skipped. You can run it again from Help > Getting Started."

## 4. Step 1: Language

Title: "Choose your language". Body: "G3MStudio uses this language for menus, messages and help. Resource names and code stay as they are."

Controls:

- A list of seven languages, each written in its own language, 36 px rows, single select: English, Español, Русский, 日本語, 한국어, 简体中文, 繁體中文. The one matching the operating system language is preselected and tagged "Detected". If none match, English is selected.
- A preview line under the list in the chosen language: "Open a data file to begin." The whole window (title, buttons, rail) switches to the chosen language at once, using that language's font.
- A "Format dates and numbers like my system" checkbox (checked). Unchecked, the formats follow the chosen language (`ui.formatCulture`).

Buttons: "Back" is disabled on this step. "Next" and "Skip Step".

Rules: no restart is needed. Fonts for ja, ko, zh_cn and zh_tw load on selection. A missing translation shows English and logs to the Output channel "Localization". Settings path: Settings > Language.

## 5. Step 2: Theme

Title: "Pick your look". Body: "G3MStudio uses one default theme, 90% G3M's look. There is no light theme; every colour can be changed later. You can import your G3M colours or leave the default."

Controls:

- Three radio cards:
  1. "G3M default" (selected): dark neutral surfaces, green outline, the G3M background image behind the side panels. Small preview mock-up (Explorer, tab, code lines).
  2. "Import from G3M": "Use the colours, corner radius and background from your G3M settings." Below it, a status line: "G3M found: {path}" with the seven channel swatches, or "G3M was not found on this computer." with a link "Choose a G3M theme file...".
  3. "Choose a theme file...": opens a file picker for a G3M theme `.zip` (it holds `theme_config.json`).
- A checkbox "Show the background image" (checked). It applies to the side docks and the start page. Code, tables and hex views always use an opaque surface so text stays readable.
- A checkbox "Reduce motion" with a hint "Follows your system setting by default." (shows the system value; unchecking does not override the system unless changed).
- After an import, a result line: "Imported 7 colours and a corner radius of 7." If a colour pair fails the 4.5:1 text contrast rule: "2 colours are hard to read. Fix contrast adjusts them." with a "Fix Contrast" button (ux-standards section 2, rule 5). Importing never fails because of contrast.

Errors: "The theme file is not valid. It needs theme_config.json at its root." and "G3M settings could not be read. Choose a theme file instead." Both have "Copy Details".

Rules: the import is one way (G3M to G3MStudio, design.md rule 5). Border widths stay 2 px whatever the theme says. A theme radius of 0 sets all radii to 0. Settings path: Settings > Appearance.

## 6. Step 3: Layout preset

Title: "Choose a layout". Body: "All three layouts have the same features. They only place things differently. You can switch any time from Window > Layout."

Controls: three radio cards in a row, each with a wireframe thumbnail (static image drawn from vector shapes) and three lines of text.

| Card | Title | Text |
| --- | --- | --- |
| 1 (selected, "Recommended") | Studio | "Explorer on the left, Inspector on the right, tabs in the middle, Problems and Output below. A good start if you are new." |
| 2 | Code | "Minimal chrome like a code editor. One side bar with icons, everything opens in tabs. Best for scripts and code." |
| 3 | Canvas | "Editors float as cards on a large board and stay linked, like GameMaker. Best for working across objects, rooms and code." |

A line below: "Studio can also open a canvas as a tab later." Next applies the preset to the start page state; the layout appears when a workspace opens. Settings path: Window > Layout (no Settings page; a preset is a layout, not a setting).

## 7. Step 4: File associations

Title: "Open game files with G3MStudio". Body: "Choose which files open in G3MStudio when you double-click them. You can change this later in Settings > File Associations."

Controls: a checklist with a group header per kind, a select-all checkbox, and the current owner of each extension when it can be read.

| Group | Extensions | Default | Note |
| --- | --- | --- | --- |
| GameMaker data files | `.win`, `.unx`, `.ios`, `.droid`, `.3ds`, `.symbian` | Checked | The same set UndertaleModTool associates. "Currently opens in: UndertaleModTool" shown when detected |
| G3M files | `.g3mpatch`, `.g3mproj` | Checked | Patches and project files |
| Scripts | `.csx` | Unchecked | Many people edit scripts elsewhere |
| Links | `g3mstudio://` | Checked | Lets other apps open files, mods and actions in G3MStudio |
| Explorer menu (Windows) | "Launch game" and "Launch with options" on data files | Checked | UTMT-RUN-06 shell verbs |

Notes and platform behaviour:

- Windows: written per user (no administrator). Windows 11 may ask the user to confirm the default app; the result line says "Windows may ask you to confirm. Open Default Apps." with a button.
- Linux: a `.desktop` file and MIME types, then `xdg-mime default`.
- macOS: the app declares its document types; the system picks the default. The step shows "On macOS, select a file in Finder, choose Get Info, set Open with to G3MStudio and click Change All." and an "Open Finder" button.
- If UndertaleModTool is the current owner: "Taking over replaces UndertaleModTool for these files. You can restore it in Settings > File Associations."

Buttons: "Apply and Continue" (primary), "Skip Step". Result: "Registered 8 file types." or an error with the failing extension: "Could not register .win. Windows blocked the change. Open Default Apps to set it yourself." Failures never block the flow.

## 8. Step 5: Open your first game

Title: "Open a game". Body: "Pick a game to start with, or open any data file."

Controls:

- A "Found on this computer" list of detected games with icon, name, platform and path. Detection uses the same rules as G3M (Steam library folders and G3M's game list; data files `data.win`, `game.ios`, `game.win`, `game.unx`, `game.droid`). Selecting one enables "Open".
- Buttons: "Open Data File..." (file picker), "Open Project Folder...", and "Try a Sample" is not offered (no bundled game).
- When the list is empty: "No supported games were found. Open a data file to start."

Behaviour: "Open" loads the workspace behind the flow with the progress bar of the Explorer; the flow moves to the tour once the Explorer is usable (under 1.5 s on the reference machine). Reading is read-only until the first edit; nothing is written to the game folder. A line states this: "G3MStudio never changes the original file until you save, and it keeps a backup when you do."

Errors (see ux-standards section 8): "'{name}' is not a GameMaker data file. Choose a file named data.win, game.ios, game.unx or game.droid, or an audiogroup file." / "Could not open '{name}'. The file is in use by another program. Close the game and try again."

## 9. Step 6: Short tour

Title (first card): "Take a short tour" with "Start Tour" and "Skip Tour". The tour runs on the real workspace: six coach marks anchored to live regions. Each mark is a popover with a 2 px `Border` outline, an arrow to its target, "Step {n} of 6", a title, two sentences at most, and "Back", "Next", "Skip Tour" (Done on the last). Escape skips. The target region gets a 2 px `Select` ring. No scrim; the rest of the window stays usable but input to the target is blocked while the mark is shown.

| Stop | Target | Title | Text |
| --- | --- | --- | --- |
| 1 | Explorer | Find anything | "Every resource of the game is here, grouped by type. Press Ctrl+P to jump to any resource by name." |
| 2 | Editor tabs | Open many things | "Single click previews a resource in an italic tab. Edit it or double-click to keep it open." |
| 3 | Inspector | Edit without opening | "Select anything and change its properties here. Select several to change them together." |
| 4 | Command palette | Every command is searchable | "Press Ctrl+Shift+P to search every command and setting. Menus show the shortcuts." |
| 5 | Problems and Output | See what went wrong | "Warnings and errors from loading, saving and compiling collect here. Double-click one to jump to it." |
| 6 | Save and Launch Game | Safe saving, quick testing | "Save writes a new file and keeps a backup. Launch Game starts the game with your changes without touching the original." |

Shortcut text in the tour uses the platform key (Cmd on macOS). Final card: "You are ready." with "Open the Start Page", "Open Settings" and a checkbox "Check for updates when G3MStudio starts" (checked, stable channel). Re-run: Help > Interface Tour.

## 10. Defaults applied by "Skip Setup"

| Setting | Default |
| --- | --- |
| `ui.language` | OS language if supported, else English |
| Theme | G3M default with background image |
| Layout preset | Studio |
| File associations | None changed (skipping never touches the registry or other apps) |
| Updates | Check at startup, stable channel |
| Crash reports | On, after the notice of step 0 has been shown |
| Tour | Not shown; a "Take the tour" item stays in Help |

Skipping the file association step never registers anything. The default for "Skip Setup" differs from the defaults ticked in step 4 on purpose: a skipped step must not change the user's system.

## 11. Skip and re-run rules

1. Skip Step moves to the next step without changing that step's setting. Skip Setup applies section 10 and ends the flow.
2. Closing the window during the flow quits the app; the flow starts again next launch at the first unfinished step.
3. Re-running preselects current values. Back changes nothing until "Finish". Cancel on a re-run discards its changes.
4. Every step is also a Settings page with the same labels (Language, Appearance, Layout via Window, File Associations, Updates), so no choice exists only in onboarding.
5. A new flow version adds only the new steps and never changes earlier answers.

## 12. Accessibility

1. Tab order: step title, step content, "Back", "Skip Step", primary button.
2. The rail is decorative; "Step {n} of 6" is real text.
3. Radio cards are one radio group each, arrow keys move the choice, Space selects.
4. The tour's coach marks are announced as dialogs (`role=dialog`, labelled by their title) and focus moves to the mark's "Next" button; Escape returns focus to where it was.
5. The language list shows names in their own script; each row has a `lang` attribute equivalent for screen readers so pronunciation is right.
6. Nothing moves except the 160 ms card selection transition, which Reduce motion removes.

## 13. Parity coverage

- UTMT-PLAT-01, UTMT-SET-04: section 7 (file associations).
- UTMT-RUN-06: section 7 (shell verbs row).
- UTMT-APP-07: section 3 (welcome screen) and the start page in shell.md.
- G3M-36: section 8 (game detection).
