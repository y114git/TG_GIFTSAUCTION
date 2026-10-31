# Open items

Every open, unresolved, assumed or unverified item found in the knowledge base on 2026-10-25, plus areas a finished product needs that the docs do not yet decide. Each item has options and a recommendation so the owner can decide it. Nothing here changes a decision already recorded; items that existing owner decisions fully settle are listed at the end and are not on the owner's list.

Scanned: every file under `docs/` (requirements, architecture, design specs, decisions, roadmap, risks, questions, status, research, glossary, G3MLib plan), `the project notes file` and `tools/gm-fixtures/README.md`. The docs were being edited while this list was built (the CLI spec was removed during the scan), so re-check "Where" references before acting on them.

Risk levels: blocker for M0 (the item must be decided before or during the M0 foundations and spikes), blocker for beta (must be decided before 1.0.0-beta ships), later (a default works until the owner decides).

## Summary

55 items: 18 blocker for M0, 29 blocker for beta, 8 later. OI-047 to OI-055 were added on 2026-10-26 (OI-053 to OI-055 are the script feature items, see the section "Script features"); OI-008, OI-010, OI-012, OI-013, OI-015, OI-025 and OI-035 are answered by Q27 to Q36 and OI-009 is partly answered (each carries a Status line).

| ID | Area | Item | Risk |
| --- | --- | --- | --- |
| OI-001 | Decisions and process | Accept the proposed ADRs 0002, 0006 and 0007 | blocker for M0 |
| OI-002 | Decisions and process | Acceptance checks that need machines the owner does not have | blocker for M0 |
| OI-003 | Decisions and process | Pass or fail thresholds and fallbacks for the library spikes | blocker for M0 |
| OI-004 | Decisions and process | Where the updater spike gets its feed while the repository is private | blocker for M0 |
| OI-005 | Decisions and process | Owner review of the 44 design specs marked proposed | blocker for M0 |
| OI-006 | Decisions and process | Q22: close with unsaved edits, keep or ask | later |
| OI-007 | Decisions and process | Behaviour defaults the specs chose without review | later |
| OI-008 | Distribution and updates | Installation formats per OS | blocker for M0 |
| OI-009 | Distribution and updates | Single file or program folder, runtime compiler and fonts | blocker for M0 |
| OI-010 | Distribution and updates | Update integrity without code signing | blocker for M0 |
| OI-011 | Distribution and updates | Minimum system requirements | blocker for beta |
| OI-012 | Distribution and updates | Per-user or system-wide install, administrator rights | blocker for beta |
| OI-013 | Distribution and updates | Update channels in a product whose first version is a beta | blocker for beta |
| OI-014 | Distribution and updates | Settings and state migration, downgrade behaviour | blocker for beta |
| OI-015 | Distribution and updates | Where a portable build keeps its data | later |
| OI-016 | Security | Script trust chain: bundled scripts, patches that carry scripts | blocker for beta |
| OI-017 | Security | Plugin re-trust on change and plugin API versioning | blocker for beta |
| OI-018 | Security | Mod archive safety (zip tricks, executables) | blocker for beta |
| OI-019 | Security | Game launch: online games, anti-cheat, runner commands | blocker for beta |
| OI-020 | Security | Corrupt or hostile data files | blocker for beta |
| OI-021 | Legal | Dependency licence policy and third-party notices | blocker for beta |
| OI-022 | Legal | GameMaker-derived material that would ship in the app | blocker for beta |
| OI-023 | Legal | Editing commercial games and distributing patches: guidance in the UI | blocker for beta |
| OI-024 | Legal | Use of the GameMaker and UndertaleModTool names | blocker for beta |
| OI-025 | Privacy | Telemetry, crash reporting and the feedback path | blocker for beta |
| OI-026 | Privacy | Local data inventory, cleanup and path redaction | blocker for beta |
| OI-027 | Accessibility and display | Exact 2 px borders at fractional display scaling | blocker for M0 |
| OI-028 | Accessibility and display | Accessibility target and who verifies it | blocker for beta |
| OI-029 | Accessibility and display | Right-to-left and complex scripts in game text | later |
| OI-030 | Localization | Plural syntax versus the G3M-compatible string file | blocker for M0 |
| OI-031 | Localization | Native review of unsourced glossary terms | blocker for beta |
| OI-032 | Performance and data | Performance budgets per machine class | blocker for beta |
| OI-033 | Performance and data | Memory ceiling and very large data files | blocker for beta |
| OI-034 | Performance and data | Undo history and the unsaved-edit journal across restarts | blocker for beta |
| OI-035 | Performance and data | Backup and local history disk use and location | blocker for beta |
| OI-036 | Performance and data | Saving into protected, locked or Steam-managed folders | blocker for beta |
| OI-037 | Performance and data | Git line endings, attributes and large files in project folders | blocker for beta |
| OI-038 | Format and library | G3MLib 1.1.0 items still open and the export speed target | blocker for M0 |
| OI-039 | Format and library | Q25 re-review of library features that are higher level than the rule | blocker for M0 |
| OI-040 | Format and library | UTMT scripts that do not compile on the G3MLib host | blocker for M0 |
| OI-041 | Format and library | Support policy for GameMaker versions beyond the tested set | blocker for beta |
| OI-042 | Format and library | Format variants with no sample file | blocker for beta |
| OI-043 | Test and documentation | Regression data in CI without commercial games or an installed GameMaker | blocker for M0 |
| OI-044 | Test and documentation | Test strategy per layer | blocker for M0 |
| OI-045 | Test and documentation | Documentation, wiki and in-app help plan | blocker for beta |
| OI-046 | Test and documentation | GUI launch arguments and the bridge to G3MTool after Q24 | later |
| OI-047 | Workspace and sync | Enable project: silent in the background, or ask for a location the first time | blocker for beta |
| OI-048 | Workspace and sync | Default snapshot triggers in project mode | blocker for beta |
| OI-049 | Workspace and sync | Sharing a project on its own and meeting a different base data file | later |
| OI-050 | Workspace and sync | Several data files of one game (Deltarune chapters) | later |
| OI-051 | Distribution and updates | macOS zip layout: plain program folder or an app bundle | blocker for M0 |
| OI-052 | Format and library | Scope of plan item I: strings shortening, variables and functions import, `.yy` interop | blocker for M0 |
| OI-056 | Format and library | Code compiled by G3MLib from a function script does not decompile (control flow error) | blocker for M0 |
| OI-057 | Format and library | Gaps left in the first `.yy` converters (strings shortening, asset layers, compressed tiles, instance creation code, transcoding) | later |
| OI-058 | Format and library | Library list for 1.1.0 after the split study (Q45) | blocker for M0 |

## Decisions and process

### OI-001 Accept the proposed ADRs 0002, 0006 and 0007
- Area: Decisions and process. Risk: blocker for M0.
- Where: decisions/README.md table; decisions/0002-solution-layout.md, 0006-localization-format.md, 0007-git-unit.md (all "Status: proposed"). ADR-0005 is also proposed but is waiting on a spike (see OI-003).
- Question: Does the owner accept the two-project layout (Core, App, Tests), the G3M JSON string format, and "Git tracks the project folder, not the data file"?
- Why it matters: M0 builds the solution skeleton and the localization pipeline on 0002 and 0006. 0007 must agree with accepted ADR-0008, which already makes the project folder the exported unit.
- Options:
  1. Accept all three as written. Fastest; 0006 still has the plural question (OI-030).
  2. Accept 0002 and 0007, hold 0006 until OI-030 is answered.
  3. Reopen 0002 for one project per editor group. Slower builds and more references, no second consumer today.
- Recommendation: option 2. Update ADR-0007 to cite ADR-0008 as its basis.

### OI-002 Acceptance checks that need machines the owner does not have
- Area: Decisions and process. Risk: blocker for M0.
- Where: roadmap.md M0 (docking spike on Linux X11, Wayland and macOS; updater spike "Windows and one other OS"); requirements.md N-PLT-1 ("tested on each"); questions.md Q8 (Windows x64 and Linux x64 by hand, everything else through CI); risks.md R5, R13.
- Question: How is "works on macOS, Wayland and arm64" proven when nobody tests those by hand?
- Why it matters: The M0 acceptance table cannot be signed off as written. Docking, floating windows and file dialogs behave differently per window system.
- Options:
  1. CI runs a scripted UI session per OS (virtual display for Linux, a hosted macOS runner) and stores screenshots and a log; acceptance means "starts, docks, floats, restores, no exception".
  2. Recruit one volunteer tester per missing platform for each milestone.
  3. Lower the acceptance wording to "builds and starts" and treat interactive behaviour as unverified, with the Q26 warning on any feature that fails.
- Recommendation: option 1, reword N-PLT-1 and the M0 table to match Q8, and record in each release note which platforms had CI-only coverage.

### OI-003 Pass or fail thresholds and fallbacks for the library spikes
- Area: Decisions and process. Risk: blocker for M0.
- Where: architecture.md (Dock.Avalonia, AvaloniaEdit, "TreeDataGrid or virtualized TreeView (spike)"); status.md "Not verified"; roadmap.md M0 spikes; risks.md R2, R5, R6; decisions/0005-updater.md.
- Question: What result makes a library fail its spike, and what replaces it? Only versions and licences were checked so far.
- Why it matters: Without thresholds a spike can drift. The shell, code editor, room renderer and updater all depend on these choices.
- Options:
  1. Write a pass and fail line per spike now (for example docking: persist and restore on three window systems with the 2 px style, else fork or own docking; room render: 60 fps pan with the largest GMS2 room, else Skia direct; updater: ADR-0005 checklist, else G3M-style updater with SHA-256).
  2. Decide case by case when each spike ends.
  3. Pick fallbacks now and skip the spikes for the ones with low risk (TreeView).
- Recommendation: option 1, with a one-line fallback per spike added to the roadmap table.

### OI-004 Where the updater spike gets its feed while the repository is private
- Area: Decisions and process. Risk: blocker for M0.
- Where: questions.md Q1 (private until done) and Q9 (GitHub Releases); roadmap.md M0 updater spike and M5; risks.md R6, R17.
- Question: Release downloads from a private repository need a token. Where does the feed live for the spike and for beta testers?
- Why it matters: The spike must show update, interrupted download and rollback against a real feed. An anonymous client cannot read a private repository's releases.
- Options:
  1. A local static server with a signed test feed for the spike; public GitHub Releases only at beta.
  2. The owner-designated public CI repository as a test feed with throwaway artifacts only (the owner has allowed its use; nothing from the private source may land there, R17).
  3. Private releases with a read-only token embedded in test builds (a leaked token exposes the source).
- Recommendation: option 1 for the spike, option 2 only if a real GitHub round trip is needed.

### OI-005 Owner review of the 44 design specs marked proposed
- Area: Decisions and process. Risk: blocker for M0.
- Where: docs/design/*.md and docs/design/editors/*.md, each "Status: proposed" (status.md says "done and reviewed", but that is a review of coverage, not owner acceptance); status.md "implementation runs ... from here".
- Question: When does the owner accept the specs, and does implementation of a milestone wait for acceptance of the specs it uses?
- Why it matters: Specs carry hundreds of concrete behaviours (shortcuts, messages, defaults). Building from unaccepted specs risks rework.
- Options:
  1. Accept per milestone: shell, ux-standards, settings, onboarding before M0 and M1; editor specs before M2 and M3.
  2. Accept all at once before M0.
  3. Treat specs as accepted unless the owner objects during milestone demos.
- Recommendation: option 1. Mark each spec "accepted" in its Status line when done.

### OI-006 Q22: close with unsaved edits, keep or ask
- Area: Decisions and process. Risk: later.
- Where: questions.md (the only row in the open table); design/settings.md 4.1 `files.closeUnsaved`; requirements.md F-WS-4.
- Question: Does closing a workspace with unsaved edits keep them silently (hot exit) or ask as UTMT does?
- Why it matters: It is the default for every user, and UTMT migrants expect a prompt.
- Options:
  1. Keep (journal restores edits), setting to ask. Current plan.
  2. Ask by default, setting to keep.
  3. Keep, and ask only on the first close of a session to teach the behaviour.
- Recommendation: option 1. Add a one-time toast on the first silent close that says where the edits are kept.

### OI-007 Behaviour defaults the specs chose without review
- Area: Decisions and process. Risk: later.
- Where: design/settings.md `explorer.reorderMode` (off, swap, move), `run.tempLocation` (gameFolder or appData), `window.openMode` (ask), `ui.density` (standard or compact), `files.autosave` (off); design/shell.md 18 "Design decisions taken".
- Question: Are these defaults the owner's choice?
- Why it matters: Each is a visible first-run behaviour. `run.tempLocation=gameFolder` writes a file into the game folder (see OI-036).
- Options:
  1. Accept all defaults and revisit after beta feedback.
  2. Review the list once with the owner during OI-005.
  3. Move the two with side effects (`tempLocation`, `autosave`) to ask-on-first-use.
- Recommendation: option 2, folded into the OI-005 review.

## Distribution and updates

### OI-008 Installation formats per OS
- Area: Distribution and updates. Risk: blocker for M0.
- Status: answered by Q27: six portable zips, no installers and no native packages for beta. The options below are historical.
- Where: roadmap.md M0 ("single-file publish") and M5 ("packaging per OS (unsigned...)"); decisions/0005-updater.md; design/platforms-formats.md 4.1 step 8 (portable zip). No document names MSI, MSIX, deb, rpm, Flatpak, dmg or pkg.
- Question: What does a user download on each OS?
- Why it matters: The updater, file associations, the URL scheme and the CI artifact list all depend on the format. M0 requires "each artifact starts".
- Options:
  1. Windows: Velopack setup plus portable zip. Linux: AppImage plus tar.gz. macOS: signed-ad-hoc `.app` in a zip or dmg. Matches ADR-0005.
  2. Add native packages (MSI or MSIX, deb, rpm). More reach, a second update path per package manager, and the in-app updater must switch off for them.
  3. Flatpak or Snap for Linux. Sandbox rules conflict with launching games, running `git` and writing next to data files.
- Recommendation: option 1 for beta. Revisit native packages and Flatpak after beta with a written list of the sandbox permissions needed.

### OI-009 Single file or program folder, runtime compiler and fonts
- Area: Distribution and updates. Risk: blocker for M0.
- Status: partly answered by Q27: single file if that proves best, otherwise a plain program folder with everything in the zip root; the size and start time measurement in the M0 CI still decides which, and runtime compiler references and fonts stay open.
- Where: roadmap.md M0; requirements.md N-SIZE-1; risks.md R13 ("single-file extraction"); decisions/0009-plugins.md (Roslyn compiles plugins on load); design/ux-standards.md 13.4 (Noto Sans, JP, KR, SC, TC fonts); architecture.md (Magick.NET).
- Question: Is the build one self-extracting file, or a program folder in the zip root, and what does it carry (Roslyn reference assemblies, four CJK fonts, native image and Skia libraries)?
- Why it matters: Roslyn compiles at run time and needs reference assemblies, which trimming removes. Single-file builds extract native libraries on Linux and macOS. Size and start time depend on this.
- Options:
  1. Single file, no trimming, reference assemblies embedded. Simple to ship, largest file, extraction on first run.
  2. Program folder in the zip root, trimmed where safe, reference assemblies and fonts as plain files. Faster start, easier updates by file.
  3. Single file for the app, reference assemblies and fonts downloaded on first use. Smaller download, needs network once.
- Recommendation: option 2. Measure the size of option 1 in the M0 CI first and record both numbers.

### OI-010 Update integrity without code signing
- Area: Distribution and updates. Risk: blocker for M0.
- Status: answered by Q28 and Q33: no code signing and no signature scheme; HTTPS plus GitHub's SHA-256 asset digest, trust root is the owner's GitHub account.
- Where: questions.md Q7 (unsigned beta); requirements.md F-UPD-2 ("SHA-256 at minimum, signature preferred"); design/platforms-formats.md 4.1 step 2 ("publisher signature too where the system supports it"); decisions/0005-updater.md ("code-signing hooks").
- Question: With no certificate, what proves an update is the author's? A checksum published in the same release is only as safe as the release page.
- Why it matters: The updater runs code with the user's rights. A compromised release page would reach every user.
- Options:
  1. Detached ed25519 signature over the feed, public key pinned in the app, private key held offline by the owner.
  2. SHA-256 from the feed only (same trust root as the download).
  3. Buy Authenticode and Apple certificates now (conflicts with Q7).
- Recommendation: option 1. It costs no money and does not change Q7. Document SmartScreen and Gatekeeper steps as already decided.

### OI-011 Minimum system requirements
- Area: Distribution and updates. Risk: blocker for beta.
- Where: design/platforms-formats.md 3 (Windows 10 22H2 and 11; macOS 13 and later; Linux glibc); research/platforms-and-languages.md 2 (.NET 10 needs macOS 14; no CI runner covers a macOS 14 floor); risks.md R13.
- Question: What are the stated minimums for OS, CPU features, RAM, disk and graphics?
- Why it matters: The two docs disagree on macOS (13 vs 14). Users and bug triage need one list. Avalonia needs a working renderer; very old GPUs or VMs may fall back to software.
- Options:
  1. macOS 14, Windows 10 22H2, Linux glibc as the CI image allows; 4 GB RAM; software rendering allowed but unsupported.
  2. Keep macOS 13 and test it by hand (not possible with .NET 10).
  3. Publish no minimums, only "what CI tests".
- Recommendation: option 1. Fix platforms-formats.md and put the table on the download page.

### OI-012 Per-user or system-wide install, administrator rights
- Area: Distribution and updates. Risk: blocker for beta.
- Status: answered by Q27 and Q28: portable zips, no install step, user data in the per-user app data folder; the program folder may be unwritable (F-UPD-5).
- Where: decisions/0005-updater.md ("installs per user on Windows"); design/onboarding.md ("written per user (no administrator)"); the scheme and associations are registered per user. No spec covers shared machines, school PCs or Linux system paths.
- Question: Is per-user the only install mode?
- Why it matters: Updates without elevation are only possible per user. A lab or school PC may need an all-users install.
- Options:
  1. Per-user only. Matches the updater and file associations.
  2. Per-user default, optional all-users installer without auto-update.
  3. All-users default.
- Recommendation: option 1 for beta; state it in the docs.

### OI-013 Update channels in a product whose first version is a beta
- Area: Distribution and updates. Risk: blocker for beta.
- Status: answered by Q28: the highest of release, optional pre-release channel and running version wins; `updates.channel` is `stable` or `prerelease`.
- Where: requirements.md F-UPD-3 (stable, beta); design/settings.md `updates.channel`; design/platforms-formats.md 4.1 step 7; questions.md Q0 (1.0.0-beta).
- Question: What is "stable" before any stable release? How are channels and the previous-version copy stored on the feed?
- Why it matters: Both channels would carry beta builds. The channel switch message ("beta is older than installed") assumes a stable line.
- Options:
  1. One channel (beta) until 1.0.0; add stable at 1.0.0.
  2. Two channels from day one: "stable" = last tagged beta, "next" = nightly builds.
  3. Hide the setting until a second channel exists.
- Recommendation: option 3 with one channel; keep the setting in the schema. Keep the last two versions on disk for rollback.

### OI-014 Settings and state migration, downgrade behaviour
- Area: Distribution and updates. Risk: blocker for beta.
- Where: architecture.md "Settings" (schema with defaults); design/platforms-formats.md 4.1 step 6 (revert to previous version); design/settings.md (unknown keys kept); decisions/0008 (project format versions). No spec names a settings, layout, workspace-state or recovery-file version.
- Question: What happens to settings, layouts, recovery journals and project files when the user reverts to an older build?
- Why it matters: The documented rollback can leave a newer settings or journal file that the old version misreads or drops.
- Options:
  1. A version number in every state file; older builds open newer files read-only or ignore them; migrations make a backup copy first.
  2. Per-version state folders (copy on update). Simple, uses disk.
  3. No guarantee for downgrade; only rollback to the same major.
- Recommendation: option 1, with the backup copy before every migration.

### OI-015 Where a portable build keeps its data
- Area: Distribution and updates. Risk: later.
- Status: answered by Q28: user data always lives in the per-user app data folder, never beside the program.
- Where: design/platforms-formats.md 4.1 step 8 (portable zip cannot self-update); architecture.md "Files on disk" (app data only).
- Question: Does the portable zip use the user's app data folder or a folder beside the program?
- Why it matters: Users run portable copies from USB sticks and expect no traces on the host.
- Options: 1. Always app data. 2. A marker file beside the program switches to a local `data` folder. 3. Portable always local.
- Recommendation: option 2.

## Security

### OI-016 Script trust chain: bundled scripts and patches that carry scripts
- Area: Security. Risk: blocker for beta.
- Where: design/scripting.md section 8 (trust prompt by SHA-256; "Bundled scripts are trusted by the signature of the release"); design/platforms-formats.md 5.4 (hash in the release manifest); design/editors/patches.md (a patch input may be a `.csx`); questions.md Q7 (no signing).
- Question: Releases are unsigned, so what do bundled scripts trust? Does applying a downloaded patch that is or contains a `.csx` show the same trust prompt?
- Why it matters: A patch from the internet that runs C# is code execution. The docs say a script can use the full user rights.
- Options:
  1. Trust bundled scripts through the OI-010 feed signature and manifest hash; any `.csx` from a patch, mod or link always asks, with source view.
  2. Run patch scripts only after a separate "this patch runs code" confirmation naming the file.
  3. Block `.csx` from patches by default and require a setting.
- Recommendation: option 1, plus the option 2 wording on the prompt.

### OI-017 Plugin re-trust on change and plugin API versioning
- Area: Security. Risk: blocker for beta.
- Where: decisions/0009-plugins.md (one-time trust "on first install or update", `IStudioContext` is "the whole public API", Roslyn cache by content hash); design/editors/plugins-panel.md (permissions are information only); requirements.md F-PLG-5.
- Question: Is trust tied to the plugin's content hash, so an edited or updated plugin asks again? Does the API carry a version and a stability promise?
- Why it matters: A plugin can change after approval (GitHub URL installs, hot reload). API changes between beta builds break plugins silently.
- Options:
  1. Trust by SHA-256 of all files; any change asks again (developer folders can be marked "mine" to skip). `plugin.json` has `apiVersion` as a range; the API is declared unstable until 1.0.0.
  2. Trust by plugin id and author only. Fewer prompts, weaker.
  3. Semantic versioning with a compatibility shim layer from the first beta.
- Recommendation: option 1.

### OI-018 Mod archive safety
- Area: Security. Risk: blocker for beta.
- Where: design/editors/mod-packaging.md 2 and 7 (rules cover unknown fields, duplicate keys, nesting above 64 levels, NFC; no rule for archive structure); design/editors/patches.md; requirements.md F-SUITE-2.
- Question: Which checks apply to zip mods and patches on import and launch?
- Why it matters: Mods come from the internet. Zip path traversal, symlinks, huge expansion ratios and executables inside a mod are known attacks. Launch with mods copies files into the game folder.
- Options:
  1. Reject entries with `..`, absolute paths or links; cap total size, entry count and compression ratio; list executables and scripts in the import dialog; never run anything from a mod automatically.
  2. Only the path and size checks; no executable listing.
  3. Rely on the OS and the library's zip handling.
- Recommendation: option 1, with limits as settings and defaults chosen from the largest real mod in the corpus.

### OI-019 Game launch: online games, anti-cheat and runner commands
- Area: Security. Risk: blocker for beta.
- Where: design/run-game.md 3 (Steam app id set to 0 in the temporary copy, temporary file beside the data file, `run.wineCommand`, first-run warning only about the temporary copy); design/editors/launch-profiles.md. No document mentions online games or anti-cheat.
- Question: Does G3MStudio warn that running a modified game can violate an online game's terms or trigger anti-cheat, and who is responsible for the Wine command string?
- Why it matters: Players can be banned. `run.wineCommand` and "Custom" runner paths execute user-supplied commands, including from imported launch profiles.
- Options:
  1. A fixed notice in the first-run warning and in the docs; imported profiles never run until the user views the command.
  2. Detect Steam API or multiplayer libraries next to the data file and warn only then.
  3. No warning; document in the wiki.
- Recommendation: option 1. Option 2 is guesswork and gives false comfort.

### OI-020 Corrupt or hostile data files
- Area: Security. Risk: blocker for beta.
- Where: risks.md R4 (fuzz kill-during-save only), R10; research/gamemaker-format-coverage.md; nothing covers a crafted input file (huge chunk sizes, cyclic references, decompression of textures and audio).
- Question: What must G3MStudio and G3MLib do when a data file is malformed on purpose?
- Why it matters: Users open files from other modders. A crash or an out-of-memory kill loses unsaved work in other windows (one process, F-WS-9).
- Options:
  1. Fuzz the reader in CI with seeds from the fixtures; allocation caps checked against the file size; errors name the chunk and offset.
  2. Same as 1 but only before beta.
  3. Treat as out of scope; any valid-looking file is trusted.
- Recommendation: option 1; the journal (F-WS-4) is the safety net for the crashes fuzzing finds.

## Legal

### OI-021 Dependency licence policy and third-party notices
- Area: Legal. Risk: blocker for beta.
- Where: architecture.md "Licensing" (GPL-3.0-only; candidate libraries); risks.md R14 (check in CI, THIRD_PARTY_NOTICES); questions.md Q12 (Lucide ISC); design/shell.md Help > Licences; roadmap M0 lists no licence check.
- Question: Which licences are allowed, how is the notice file produced, and where does the user see it? Bundled items include Avalonia, Dock.Avalonia, AvaloniaEdit, Roslyn, Magick.NET, Sentry (Apache-2.0), Noto fonts (OFL), Lucide, the 110 UTMT scripts (GPL-3.0) and the 36 G3MTool scripts.
- Why it matters: A missing notice or an incompatible licence is a release blocker and a legal exposure for the owner.
- Options:
  1. Allow list (MIT, BSD, Apache-2.0, ISC, OFL, GPL-3.0-compatible) checked in CI; notices generated into `THIRD_PARTY_NOTICES` and shown in Help > Licences and About.
  2. Manual notice file updated before each release.
  3. Allow list but check only at release time.
- Recommendation: option 1, added to the M0 CI.

### OI-022 GameMaker-derived material that would ship in the app
- Area: Legal. Risk: blocker for beta.
- Where: questions.md Q1 (remove research/gamemaker/gamemaker-hotkeys.md and gamemaker-terminology.md before public); design/editors/keyboard-shortcuts.md 5 (a "GameMaker" keymap preset); l10n/glossary.md (terms from GameMaker packs "reference only"); design/editors/tags-feature-flags.md (known flags kept in the app's metadata).
- Question: Which GameMaker-derived data may ship inside the program (the GameMaker keymap, terms, feature-flag descriptions, icons, screenshots in docs)?
- Why it matters: Q1 covers removing research files from the repository, not what the product embeds. A keymap preset and term lists are copied from files under the GameMaker licence.
- Options:
  1. Ship nothing copied; write the GameMaker preset from the public documentation of shortcuts and re-derive terms by hand.
  2. Ship the preset as bindings only (facts, not expression) and keep the research files out.
  3. Ship the extracted files with a notice.
- Recommendation: option 2, after the owner confirms with the licence text; keep all screenshots out of the public repository.

### OI-023 Editing commercial games and distributing patches: guidance in the UI
- Area: Legal. Risk: blocker for beta.
- Where: design/editors/mod-packaging.md and patches.md (export modified data file, selected resources, modpacks); requirements.md F-SUITE-4. No document gives copyright guidance.
- Question: Does the UI tell users that redistributing a modified data file or exported game assets is usually not allowed, and that patches (differences) are the accepted form?
- Why it matters: The tool makes exporting a full game file one click. The owner's exposure grows if it looks like it encourages piracy.
- Options:
  1. One short notice in the Export modified data file dialog and the mod packaging page, with "Don't show again", plus a docs page.
  2. A first-run line only.
  3. No in-app text; wiki only.
- Recommendation: option 1. Have the owner approve the wording; this is not legal advice.

### OI-024 Use of the GameMaker and UndertaleModTool names
- Area: Legal. Risk: blocker for beta.
- Where: vision.md ("replaces UndertaleModTool"); design/ux-standards.md and glossary (GameMaker terms and presets); onboarding detects Undertale, Deltarune and Pizza Tower folders.
- Question: What disclaimer and naming rules apply (no logos, "not affiliated", "GameMaker" only to describe compatibility)?
- Why it matters: Trademark misuse is a takedown risk for the public repository and the download page.
- Options:
  1. A short "not affiliated" statement in About, README and the wiki; names used only descriptively; no third-party logos or game art.
  2. Same plus a rename of the "GameMaker" keymap preset to "Familiar IDE".
  3. No statement.
- Recommendation: option 1.

## Privacy

### OI-025 Telemetry, crash reporting and the feedback path
- Area: Privacy. Risk: blocker for beta.
- Status: answered by Q29 and Q32: Sentry crash reports, opt-out with scrubbing, notice and switch in onboarding, setting later (F-PRIV-1).
- Where: design/onboarding.md principle 4 ("No account, no telemetry prompt... Nothing is sent anywhere"); design/settings.md ("no telemetry setting"); design/shell.md 14.5 (crash.txt stays local); design/run-game.md 7.2 (crash reports of the launched game). Not an owner decision in questions.md.
- Question: Does the owner confirm zero telemetry and no crash upload, and how do users send a crash to the author?
- Why it matters: A beta without any crash signal is hard to fix. Opt-out analytics would break the privacy promise made in onboarding.
- Options:
  1. No telemetry. Crash log stays local; "Copy Details" and a public issue template are the path. The update check reads a static feed (the host sees the IP, which the privacy text must say).
  2. Opt-in crash report with a preview of exactly what is sent.
  3. Opt-in anonymous usage counts.
- Recommendation: option 1 for beta, with the IP note in the privacy text; revisit option 2 after beta.

### OI-026 Local data inventory, cleanup and path redaction
- Area: Privacy. Risk: blocker for beta.
- Where: architecture.md "Files on disk" (recent list, per-file state, caches, recovery copies, backups); design/shell.md 14.2 and 14.5 (journal, crash.txt, logs); design/settings.md (`run.crashRetentionDays` 30; no retention for app logs or caches).
- Question: Which data stays on disk, for how long, how does the user remove it, and do "Copy Details" and logs strip user names and home paths?
- Why it matters: Recovery copies and caches hold game content. Users paste logs into public issues with their account name in every path.
- Options:
  1. A Privacy page in Settings: size per category, "Clear" buttons, retention days; "Copy Details" replaces the home folder with `~` and offers a preview.
  2. Documentation only.
  3. Cleanup on uninstall only.
- Recommendation: option 1.

## Accessibility and display

### OI-027 Exact 2 px borders at fractional display scaling
- Area: Accessibility and display. Risk: blocker for M0.
- Where: design.md rule 2 ("exactly 2 px", a test fails on any other `BorderThickness`); roadmap.md M0 theme tokens acceptance; design/ux-standards.md (`ui.zoom` 200%). No document mentions system scaling such as 125%, 150% or 175%.
- Question: Is 2 px a device pixel or a device-independent unit, and what happens at fractional scales where 2 units are 2.5 or 3.5 device pixels?
- Why it matters: The borders are the identity of the design. Fractional scales blur or uneven-width them; the M0 test only checks the number.
- Options:
  1. 2 device-independent units with layout rounding on, accepting that the rendered width is a whole device pixel count per scale (3 px at 150%).
  2. Snap to whole device pixels with a minimum of 2 (2 px at 100%, 3 px at 150%) and test screenshots at 100, 125, 150, 200%.
  3. Fixed 2 device pixels regardless of scale (thin on 4K screens).
- Recommendation: option 2; add the screenshot test to the M0 theme work.

### OI-028 Accessibility target and who verifies it
- Area: Accessibility and display. Risk: blocker for beta.
- Where: design/ux-standards.md 12 (checklist; Narrator and VoiceOver "before the beta", Orca "best effort"; custom canvases expose an automation tree); requirements.md N-UI-3; questions.md Q23 (no light theme, fully customizable).
- Question: What conformance level is promised, and what is the plan for pixel editors (room, sprite, path, curve) and for Linux where Avalonia's AT-SPI support is the weakest?
- Why it matters: The checklist is a test script, but nobody is named to run it, and canvas editors cannot be fully screen-reader operable.
- Options:
  1. WCAG 2.2 AA for shell, forms, lists and trees; for canvases, a keyboard and list-based alternative for every action; test with NVDA, Narrator and VoiceOver; Orca best effort.
  2. Keep the checklist as is and publish known gaps.
  3. Lower to "keyboard accessible, contrast compliant".
- Recommendation: option 1. Ship a high-contrast token set and a light variant as ordinary customizations (users decide), without a default light theme.

### OI-029 Right-to-left and complex scripts in game text
- Area: Accessibility and display. Risk: later.
- Where: design/ux-standards.md 13.4 and 13.10 (RTL not in the beta UI set; fonts only for CJK).
- Question: Do the string, code and language editors render Arabic, Hebrew, Thai or Devanagari text found in game data correctly?
- Why it matters: Modders edit translations of games in these languages. The UI language set does not limit the data.
- Options: 1. System font fallback plus bidi-correct text boxes in data editors. 2. Bundle Noto Sans Arabic and Hebrew. 3. Declare unsupported.
- Recommendation: option 1; add a test string per script to the localization tests.

## Localization

### OI-030 Plural syntax versus the G3M-compatible string file
- Area: Localization. Risk: blocker for M0.
- Where: decisions/0006-localization-format.md (G3M's JSON format, placeholders checked); design/ux-standards.md 13.7 (plural forms for every language, ru has four); l10n/glossary.md (G3M uses "mod(s)" hacks and a "Label: {count}" pattern).
- Question: How are plurals written in the file when G3M's own format apparently has no plural system?
- Why it matters: M0 builds the pipeline and the key checker. Choosing late forces a rewrite of every counted string.
- Options:
  1. Extend G3M's JSON with CLDR plural categories per key (one, few, many, other). G3M files remain readable.
  2. ICU MessageFormat strings. Powerful, new tooling for translators, not G3M-compatible.
  3. Avoid plurals with "Label: {count}" everywhere (no code, awkward text).
- Recommendation: option 1; keep G3M import and export for shared terms.

### OI-031 Native review of unsourced glossary terms
- Area: Localization. Risk: blocker for beta.
- Where: l10n/glossary.md ("(new)" cells, "Concepts without a source"; zh_tw converted from zh_cn); risks.md R8; status.md "Not verified"; questions.md Q5 (in-house translations).
- Question: Does beta ship with terms no native speaker has reviewed (git actions, local history, texture page, bytecode, particle system and others)?
- Why it matters: F-L10N-2 demands six complete languages. Wrong terms cost trust with the target audience.
- Options:
  1. Ship, mark the languages "beta translation" with a feedback link, review after.
  2. Recruit reviewers from the G3M community per language before beta.
  3. Source the git terms from VS Code's git extension pack now (the glossary notes it was not downloaded) and review the rest in a pass with dictionaries.
- Recommendation: option 3 first, then option 2 for ja, ko and zh; option 1 as the fallback.

## Performance and data

### OI-032 Performance budgets per machine class
- Area: Performance and data. Risk: blocker for beta.
- Where: architecture.md "Performance budget" (one "mid-range Windows x64 laptop", measured with Pizza Tower's 92 MB file); requirements.md N-PERF-1; roadmap M5.
- Question: What are the targets on low-end hardware, on Linux and macOS, on arm64, and with the biggest corpus files (Deltarune chapter 5 is above 250 MB)?
- Why it matters: "Mid-range laptop" is not measurable. The 200 MB memory line has no basis yet.
- Options:
  1. Three classes (low: 4 GB RAM, HDD; reference: the owner's laptop; high) with budgets only on the reference class and a "must not crash or freeze" rule on the low class.
  2. Budgets per class for every row.
  3. One class, as now, with the machine named.
- Recommendation: option 1; name the reference machine in the docs and re-measure every milestone in CI where noise allows.

### OI-033 Memory ceiling and very large data files
- Area: Performance and data. Risk: blocker for beta.
- Where: risks.md R9; architecture.md (200 MB above the model); research/g3mlib.md (whole model in memory); design/editors/strings.md (1 MiB warning only).
- Question: How much memory does the model need per MB of data file, and what happens when a file will not fit?
- Why it matters: A 250 MB file may need several times that in the model plus textures. A 32-bit-limited file size also bounds the format.
- Options:
  1. Measure the ratio on the corpus in M0, show an estimate before open, warn when it exceeds free memory, lazy decode of textures and code.
  2. Add lazy loading inside G3MLib (outside the 1.1.0 scope).
  3. State a supported maximum and refuse larger files.
- Recommendation: option 1, with the measured ratio written into architecture.md.

### OI-034 Undo history and the unsaved-edit journal across restarts
- Area: Performance and data. Risk: blocker for beta.
- Where: requirements.md F-WS-4, F-RES-12; design/shell.md 14.2 (journal recovery copy every 2 s idle), 14.4 (undo stack); design/editors/code.md (200-step editor history); risks.md R9 (undo stores deltas).
- Question: After a crash or a normal close with unsaved edits, is the undo stack restored, or only the resulting state?
- Why it matters: The journal format is chosen in M1. If it stores commands, undo can survive; if it stores model snapshots, it cannot.
- Options:
  1. Journal stores the command log; undo and redo survive restarts up to a cap.
  2. Journal stores state; recovery appears as one "Recovered edits" step.
  3. Persist undo only for saved workspaces (local history snapshots).
- Recommendation: option 1 if the M1 prototype shows commands serialize cheaply, otherwise option 2; decide before M1 starts.

### OI-035 Backup and local history disk use and location
- Area: Performance and data. Risk: blocker for beta.
- Status: answered by Q35 and ADR-0010: backups are 1 to 3 rotating copies in the user data folder; snapshot limits default to none.
- Where: design/settings.md `files.backupCount` 3 and `files.history.maxSnapshots` 20; architecture.md "Files on disk" (backups next to the file as `data.win.bak`); requirements.md F-WS-6.
- Question: Where are backups and snapshots stored, as full copies or deltas, and what is the disk cap?
- Why it matters: Three backups of a 250 MB file plus twenty full snapshots is over 5 GB. Extra files in a Steam folder can be removed or reported by "verify files".
- Options:
  1. Backups in app data, snapshots as deltas against the original, a total size cap setting.
  2. Backups beside the file as documented; snapshots as deltas.
  3. Full copies with count limits only.
- Recommendation: option 1, with the cap default chosen after measuring Pizza Tower.

### OI-036 Saving into protected, locked or Steam-managed folders
- Area: Performance and data. Risk: blocker for beta.
- Where: requirements.md F-WS-5 (write next to the target, atomic replace); design/shell.md 14.2 ("This file is in a read-only folder", Save As); design/run-game.md 3 (temporary file beside the data file, macOS `.app` copy); design/onboarding.md (no administrator).
- Question: What happens when the game lives in Program Files, a read-only volume or a bundle, or the game still has the file open?
- Why it matters: Windows file locks and UAC make "write temp, then replace" fail in common installs; Steam may restore files on update.
- Options:
  1. Never elevate; offer Save As, a copy of the whole game folder, or a patch; explain locks with the process name when known.
  2. Offer a one-time elevated helper to replace the file.
  3. Always edit a working copy and export patches only.
- Recommendation: option 1, with the "copy game folder here" action.

### OI-037 Git line endings, attributes and large files in project folders
- Area: Performance and data. Risk: blocker for beta.
- Where: decisions/0004-git-cli.md, 0007-git-unit.md, 0008-project-format.md (byte-identical rebuild); design/projects.md 3 (`.gitignore` written, nothing else); design/editors/git-panel.md. No document mentions `.gitattributes`, `core.autocrlf` or Git LFS.
- Question: How does a project folder survive Git line-ending conversion and 30,000-file repositories?
- Why it matters: `core.autocrlf` on Windows rewrites text files; a rebuild would no longer be byte-identical (ADR-0008 acceptance). Texture PNGs and audio make repositories heavy.
- Options:
  1. Write `.gitattributes` with `-text` for generated files and `binary` for assets, check on build, suggest LFS for audio and large images.
  2. Normalize line endings when reading the project on build.
  3. Leave to the user.
- Recommendation: option 1; add a test that clones with `autocrlf=true` and rebuilds.

## Format and library

### OI-038 G3MLib 1.1.0 items still open and the export speed target
- Area: Format and library. Risk: blocker for M0.
- Where: g3mlib-1.1.0-plan.md items A, H, I, J (E rename service and B thread model in flight); status.md "Next actions" 0 (Pizza Tower full export 74.1 s against a 60 s target; item A reopened); research/gamemaker-format.md "Left open" (a UTMT export was never opened in UTMT 0.9; no end-to-end explorer open API timing).
- Question: Is 60 s the right export target after more resource types were added, and when is 1.1.0 final? Manual checks (runner loads repacked pages, UTMT 0.9 opens an export) are still to be done.
- Why it matters: G3MStudio must not start before the library is final (ADR-0003); late changes mean 1.1.x and editor workarounds (R16).
- Options:
  1. Hold 1.1.0 until A meets 60 s.
  2. Set the metric to incremental re-export (unchanged-aware) and accept about 75 s for the first export.
  3. Move A to 1.1.x.
- Recommendation: option 2, and run the two manual checks before release.

### OI-039 Q25 re-review of library features that are higher level than the rule
- Area: Format and library. Risk: blocker for M0.
- Where: questions.md Q25 ("items already built on the branches that are higher level... are re-reviewed"); status.md L2 state (project folder service, `OpenAsync` project index, ScriptRunner, rename service, integrity service); decisions/0008-project-format.md.
- Question: Which parts of the project service and similar features stay in G3MLib as single operations, and which move to G3MStudio?
- Why it matters: G3MTool 1.3.1 also uses the library; moving code after release is a breaking change.
- Options:
  1. Keep serializers, single-resource export and import, build and diff primitives in G3MLib; move orchestration (full versus delta mode choice, base identity checks, unchanged-aware rebuild scheduling) into G3MStudio.
  2. Keep everything in G3MLib because G3MTool needs it too and mark the higher-level calls as convenience layers.
  3. Split into a second package (G3MLib.Workflows).
- Recommendation: option 1 reviewed by the owner against each public type before the 1.1.0 release. The owner has already said per-resource serializers stay in G3MLib as building blocks, so only the orchestration remains to decide.

### OI-040 UTMT scripts that do not compile on the G3MLib host
- Area: Format and library. Risk: blocker for M0.
- Where: research/utmt-script-compat.md (12 of 110 do not compile: 3 need WPF or WinForms, 2 need Newtonsoft.Json, 6 differ by model API, 1 is broken in UTMT too; "candidates for alias members if script compatibility matters"; 24 untested on real games); requirements.md F-PAR-2; roadmap.md M3 acceptance.
- Question: Which of the 12 get fixed in 1.1.0, which are replaced by built-in commands, and which are declared not applicable?
- Why it matters: Parity (F-PAR-1) allows "not applicable" only with a reason the owner accepts. Alias members must be in 1.1.0 to avoid a later release.
- Options:
  1. Add Newtonsoft.Json to the script host, alias the six model differences in 1.1.0, replace the three WPF scripts with native editors and mark DeltaHATE and DeltaruneClearShaderData not applicable.
  2. Fix only the model differences; drop scripts needing extra packages.
  3. Document all 12 as not applicable.
- Recommendation: option 1. Run the 24 untested scripts on corpus games before accepting.

### OI-041 Support policy for GameMaker versions beyond the tested set
- Area: Format and library. Risk: blocker for beta.
- Where: questions.md Q17 (full support for LTS 2026 and 2022; GMS 1.4 and GMS2 2.x through game files; others only for research); design/platforms-formats.md 7 (up to 2026.1; "newer than" banner); g3mlib-1.1.0-plan.md (new runtimes via passthrough and 1.1.x); risks.md R10.
- Question: What does "supported" mean for monthly and non-LTS runtimes (2023.x to 2025.x) that real games use, and how fast does G3MStudio follow new GameMaker releases?
- Why it matters: Many games ship on non-LTS builds. Users need to know if a bug report from such a game is in scope.
- Options:
  1. Tested set (LTS 2026, LTS 2022, GMS 1.4, corpus games) plus best effort for everything the format map covers; each beta release adds one fixture from the newest monthly runtime.
  2. Tested set only; other versions open with a warning.
  3. Add all monthly runtimes to the fixture matrix.
- Recommendation: option 1, written into the docs as the support policy.

### OI-042 Format variants with no sample file
- Area: Format and library. Risk: blocker for beta.
- Where: research/gamemaker-format-coverage.md gap 7 ("needs a GMS 2.0 to 2.2 sample"); research/gamemaker-fixtures.md (fixtures need an installed GameMaker); research/corpus.md (11 local Windows files); design/platforms-formats.md (Android and iOS files "not started" by Run).
- Question: Where do samples for GMS2 2.0 to 2.2, GMS 1.4 with extensions (build 1773 and above), 1.4.9999, YYC, Android, iOS, macOS and Linux builds come from?
- Why it matters: Round-trip and version-detection claims for these are untested. Build fixtures needs the old IDEs.
- Options:
  1. Install the older IDEs for research (Q17 allows it) and build tiny fixtures; store only our own tiny files.
  2. Ask the community for sample files under a permissive agreement.
  3. Mark those variants "read and write untested" in the compatibility table.
- Recommendation: option 1 for GMS2 2.x and 1.4, option 3 for console and mobile targets.

## Test and documentation

### OI-043 Regression data in CI without commercial games or an installed GameMaker
- Area: Test and documentation. Risk: blocker for M0.
- Where: questions.md Q11 (corpus = commercial games and a local folder), Q8; roadmap.md M2 and M4 acceptance (corpus byte-identical, run the game); tools/gm-fixtures/README.md (needs GameMaker, a runtime and a signed-in user folder); risks.md R7, R17.
- Question: What data does CI use to check round trips and script compatibility, given that games cannot be committed or uploaded, and fixtures cannot be rebuilt on a hosted runner?
- Why it matters: Several roadmap acceptance lines cannot run in CI as written.
- Options:
  1. Commit the small fixture data files built from our own empty projects (the 29 KB set) and run corpus checks only on the owner's machine, with results recorded in status.md.
  2. A private runner at the owner's PC for the corpus job.
  3. Encrypted corpus in private storage, fetched by private CI (copies commercial files, avoid).
- Recommendation: option 1 for CI plus option 2 for the corpus at milestone ends.

### OI-044 Test strategy per layer
- Area: Test and documentation. Risk: blocker for M0.
- Where: requirements.md N-QA-1 (logic tests, G3MLib round trip, headless UI smoke tests); roadmap.md acceptance lines; risks.md R3, R4, R7.
- Question: What is tested at each layer, with what tools, and what is the bar?
- Why it matters: Avalonia headless tests cannot drive native menus, file pickers or real docking. Performance numbers on hosted runners are noisy.
- Options:
  1. A table per layer: Core unit tests with a coverage floor; G3MLib fixture round trips; headless view tests; screenshot tests on three OS; fuzz and kill-during-save tests nightly; performance tests as trend only; plugin and localization checks in the build.
  2. Unit and headless tests only; the rest by hand.
  3. Same as 1 but staged by milestone.
- Recommendation: option 3, with the table written into architecture.md during M0.

### OI-045 Documentation, wiki and in-app help plan
- Area: Test and documentation. Risk: blocker for beta.
- Where: roadmap.md M5 ("G3MWiki pages for G3MStudio"); the project notes file (G3MWiki has a stub); requirements.md N-DOC-1 (internal knowledge base only); design/onboarding.md (tour). The Help menu has Licences and Release Notes but no spec says what F1 opens.
- Question: What user documentation ships, in which languages, and where does it live (web, offline, in-app)? Who documents the plugin API, the script API and the release notes?
- Why it matters: Without a plan the beta ships with only the tour. Plugin authors need API documentation before the plugin system is useful.
- Options:
  1. G3MWiki as the single source; F1 opens the matching page online; a bundled offline copy of the quick start.
  2. Bundled offline help only.
  3. In-app help pages generated from the specs.
- Recommendation: option 1 in English, with the six languages following the string translations; API reference generated from code comments for plugins and scripts.

### OI-046 GUI launch arguments and the bridge to G3MTool after Q24
- Area: Test and documentation. Risk: later.
- Where: questions.md Q24 and obsolete Q20 and Q21; design/shell.md 13.1 (`G3MStudio <file>`, `--new-instance`, `--profile`); design/settings.md 10 and the parity IDs UTMT-CLI-01 to 08 and UTMT-FILE-12, mapped to G3MTool per Q24.
- Question: Which launch arguments does the GUI keep (open file, new instance, profile, special launch), and does the app offer a way to run a repeated task through G3MTool?
- Why it matters: The deleted CLI spec had a "Copy command line" button. The GUI still needs a documented argument set for file associations and shell verbs.
- Options:
  1. Keep file, `--new-instance`, `--profile` and the URL scheme; drop the rest. Add "Copy G3MTool Command" where G3MTool has the equivalent.
  2. File and URL scheme only.
  3. Keep UTMT's argument set for drop-in compatibility.
- Recommendation: option 1.

## Workspace and sync (added 2026-10-26)

### OI-047 Enable project: silent or with a location prompt
- Area: Workspace and sync. Risk: blocker for beta.
- Where: decisions/0010-workspace-and-sync.md "Still to decide" 1.
- Question: Does Enable project run silently in the background into the user data folder, or always ask for a location the first time?
- Why it matters: A project of a large game has tens of thousands of files (Pizza Tower: 65,000) and can use several GB. A silent export is quick to start but surprises users on small drives.
- Options:
  1. Silent into the user data folder, with the size shown in Settings.
  2. Always ask for a location the first time, then remember it (`projects.location`).
  3. Silent, but ask when the free space is below a threshold.
- Recommendation: option 3.

### OI-048 Default snapshot triggers
- Area: Workspace and sync. Risk: blocker for beta.
- Where: decisions/0010-workspace-and-sync.md "Still to decide" 2; design/settings.md `files.history.*`.
- Question: Are snapshots taken on every sync, on a timer, or only by hand by default?
- Why it matters: Limits default to none, so the trigger decides how fast disk use grows. Content addressing keeps unchanged resources free.
- Options:
  1. On every sync.
  2. On a timer (for example every 30 minutes while edits exist).
  3. Manual only.
- Recommendation: option 1, since sync is the natural save point and stores only changed resources.

### OI-049 Sharing a project on its own
- Area: Workspace and sync. Risk: later.
- Where: decisions/0010-workspace-and-sync.md "Still to decide" 3.
- Question: May a project be shared alone with other users (Git remote), and what does the app show when a shared project meets a different base data file?
- Why it matters: A shared project assumes the same base. Another user's data file may differ by version or by patches.
- Options:
  1. Allow it; on a different base, offer the three-way merge of ADR-0010 with the shared project as theirs.
  2. Allow it, open read-only until the user rebases (Project > Rebase on Data File).
  3. Not in beta.
- Recommendation: option 2 for beta; revisit option 1 after beta.

### OI-050 Several data files of one game
- Area: Workspace and sync. Risk: later.
- Where: decisions/0010-workspace-and-sync.md "Still to decide" 4.
- Question: How do several data files of one game (Deltarune chapters) relate?
- Why it matters: Chapters share many resources. Linking them adds merge rules; separating them wastes disk.
- Options:
  1. Separate projects (proposed in ADR-0010).
  2. One project with a folder per data file.
- Recommendation: option 1.

## Distribution and updates (added 2026-10-26)

### OI-051 macOS zip layout
- Area: Distribution and updates. Risk: blocker for M0.
- Where: questions.md Q27 (files in the zip root); design/platforms-formats.md 3 and 5 (macOS menu bar, `Scripts/` folder); decisions/0005-updater.md (extract into the folder of the previous executable).
- Question: On macOS, is the zip root a plain program folder with the executable, or an app bundle (`G3MStudio.app`)?
- Why it matters: A bare executable has no Dock icon identity, native menu bar integration or file association (document types come from the bundle's Info.plist). A bundle changes where "the folder of the previous executable" is (`Contents/MacOS`) and where `Scripts/` lives.
- Options:
  1. A `G3MStudio.app` bundle at the zip root; the updater replaces the bundle contents.
  2. A plain folder of files at the zip root; associations and menu bar limits are tested in CI first.
- Recommendation: option 1, as an app bundle is the only way to register file types and the URL scheme on macOS; the updater then targets the executable's bundle.

### OI-052 Scope of plan item I: strings shortening, variables and functions import, `.yy` interop
- Area: Format and library. Risk: blocker for M0.
- Where: g3mlib-1.1.0-plan.md item I; decisions/0008-project-format.md decision 6; requirements.md F-WS-11. No document defines "strings shortening" or "variables and functions import", and G3MLib has no `.yyp` or `.yy` reader or writer today (checked 2026-10-26).
- Question: What exactly do "strings shortening" and "variables and functions import" do, and which resource types and GameMaker versions must the first `.yy` converters cover?
- Why it matters: The acceptance line ("a GameMaker sprite and object imported from a `.yyp` build and run") needs a mapping from `.yy` fields to G3MLib models. `.yy` layouts differ between LTS 2022 and LTS 2026, and an object needs its events compiled from `.gml` files. Without the two definitions a worker would guess behaviour that ships in the public API.
- Options:
  1. Define the two operations (suggest: shortening = drop unreferenced and duplicate strings from STRG; import = add VARI and FUNC entries a compiled script needs), then build converters in this order: sprite, sound, script, object, room.
  2. Ship only `.yy` converters for sprite, sound and script in 1.1.0 and move the rest to 1.1.x.
  3. Move all of item I to G3MStudio as import workflow code on top of existing per-resource serializers.
- Recommendation: option 2 now (the sprite path reuses `TextureGroupPacker` and `TexturePageRepacker`), and the owner confirms the two definitions before anything beyond that starts.

### OI-056 Code compiled by G3MLib from a function script does not decompile
- Area: Format and library. Risk: blocker for M0.
- Where: `l2/interop` in G3MLib, `G3MLib.Tests/YyScriptTests.cs`. Found 2026-10-26 while writing the script converter.
- Question: Is the compiled bytecode of `function f() { g = 1; }` wrong, or is the decompiler missing a case for it?
- Why it matters: Import a script with `CodeImportGroup`, write the data file, read it back, then decompile either `gml_GlobalScript_f` or the child entry `gml_Script_f`: `DecompileContext` throws "Expected loop to be around unresolved branch" (control flow stage, `Branches.ResolveRemainingExternalJumps`). Scripts that GameMaker built (fixtures `data-2022.win`, `data-2026.win`, `Script1`) decompile fine. Writing a `.yy` project back from a data file that holds imported function scripts therefore fails at the `.gml` step. Object event code (no functions) was not affected in the tests, but the object write test only used GameMaker-built code.
- Options:
  1. Compare the emitted bytecode with GameMaker's for the same source (Igor build of one script) and fix the compiler.
  2. Fix the decompiler case.
  3. Keep the limit and document that only GameMaker-built code round trips to `.gml`.
- Recommendation: option 1 first, because a data file that G3MLib compiled should read like one GameMaker built.

### OI-057 Gaps left in the first `.yy` converters
- Area: Format and library. Risk: later.
- Where: `G3MLib/DataFile/Interop/Yy*.cs` on `l2/interop`; plan item I.
- Question: Which of these gaps must be closed for 1.1.0?
- Why it matters: Each gap is a silent or loud limit of the public converters.
  1. Strings shortening is not implemented (no converter queue item; it needs a reference scan over every chunk).
  2. Rooms: asset, path and effect layers are skipped (names reported), compressed tile data throws, instance creation code throws, layer and instance ids are newly assigned.
  3. Sprites: only the first image layer composite is read; ellipse and diamond collision kinds become rectangles; masks are rebuilt from alpha, not copied.
  4. Sounds: no transcoding (compressed sound with a wav source throws), only the default audio group, streamed sounds leave the external file to the caller.
  5. Scripts: only the 2.3+ global script entry or the old `gml_Script_` entry; `isCompatibility` is ignored.
  6. Objects: DnD events and object variables are dropped.
  7. Folders, `.yyp` reading (resource list, folders, options) and tilesets, fonts, paths, shaders, timelines are not covered.
- Options: close the gaps in the listed order after the owner confirms OI-052; or leave 2 to 7 for G3MStudio import code.
- Recommendation: close 2 (compressed tiles, creation code) and 7 (`.yyp` reader) before 1.1.0, leave the rest.
- Update 2026-10-26: asset (sprite graphics only), path and effect layers are now read and written (item 2). Sequence and particle system graphics are still reported as skipped. Layer ids now start at 0 per room like GameMaker. Rectangle sprite kinds now store no mask. See OI-059 and OI-060.

### OI-059 Sprite differences against GameMaker's own build
- Area: Format and library. Risk: later.
- Where: `YySprite.Read`; docs/research/gamemaker-fixtures.md "Project import".
- Question: Do we copy GameMaker's rules for collision masks and one large frame?
- Why it matters: On Cover Assault 160 sprites imported; 42 precise or ellipse masks differ from GameMaker's by a few edge pixels (the mask has fewer set bytes than an alpha above 0 gives; alpha above 127 is too strict). `Splash_Image_Character` (3840x2160) differs in visible pixels after decoding, cause not found. Frames otherwise differ only in the colour of fully transparent pixels.
- Options: (1) find GameMaker's rule by building sprites with known alpha ramps through Igor; (2) accept the difference, because masks stay valid; (3) copy masks from the built file when present.
- Recommendation: option 1 as a small fixture task, then fix; the large frame needs a single-sprite repro first.
- Update 2026-10-26 (run 11, black box on GameMaker's own `Cover Assault.win` build, nothing decompiled): the differing masks are the ellipse kind (`collisionKind` 2, 37 sprites in this project), not precise masks. GameMaker builds those from the bounding box and ignores alpha, so no alpha threshold can match them (the 64x64 `spr_enemy_bullet` is a hard 0/255 circle and 90 full-alpha pixels are clear in GameMaker's mask). For that sprite an ellipse test matches the built mask exactly: set where ((x - (W-2)/2) / ((W-1)/2))^2 + ((y - (H-2)/2) / ((H-1)/2))^2 <= 1, with last row and last column clear. The other 36 ellipse masks (widths 449 to 532) follow the same shape on the right edge, but the left edge of each row is rounded down to a multiple of 8 in some sprites and not in others, so the rule is not derived. Not fixed. Next step: build ellipse sprites of chosen sizes (width 63 to 80, odd and even, multiples of 8 and not) through Igor and compare, then add the ellipse (and diamond, kind 3) rule to `YySprite.Mask`. Assumed until then: alpha above tolerance for every kind, as now; masks stay valid but differ by edge pixels from GameMaker's.

### OI-060 GML compiler gaps found by the Cover Assault import
- Area: Format and library. Risk: later.
- Where: `CodeImportGroup` compile of `obj_floor` Create (`Invalid expression on accessor`) and `obj_wave_manager` Create (`Failed to find a valid expression around line 120`, `expected '}' or 'end'`).
- Question: Which GML constructs does the compiler not parse, and who fixes them?
- Why it matters: Two of the project's object events do not import, so the whole code import batch fails and no code of the batch is applied.
- Options: (1) find the constructs and extend the parser; (2) import objects one by one and report failures per event; (3) both.
- Recommendation: option 3. Also open: `rm_level_city` imports 23 room instances where GameMaker's build has 22 (not investigated).
- Update 2026-10-26: parser gaps fixed on `l2/split` (25a4132): `return` before `}`, `f()[i]`, and template strings on LTS 2026 data. All Cover Assault sources compile. Per event import results (option 2) stay for L-08. Room instance count still open.

### OI-061 G3MLib 1.1.0 library items: behaviour changes and deferred parts
- Area: Format and library. Risk: later.
- Where: G3MLib branch `l2/split`, commits for L-02 to L-12 (docs/status.md lists each); docs/research/library-gui-split.md section 7.1.
- Question: Does the owner accept these differences from the sketches and from the old behaviour?
  1. Sprite import (L-05) now packs through `TextureGroupPacker` (border 2, border pixels generated, power of two pages). New sprites are created in file name order, where the old private packer created them in its packing order, and page layouts differ. G3M and G3MTool results are valid but not byte identical to before.
  2. `RepackOptions` (L-04) has only `MaxPageSize`, `Border` and `Groups`. Power of two, max item size, keep alone predicate and group key from the sketch are not built.
  3. A dry run or stage import (L-08) checks each operation against the original code (chained operations on one entry do not see each other), and entries or assets created by queueing by name are not rolled back. A failure while linking after a clean dry run cannot be undone.
  4. `TryWriteExternalSoundAudio` (import of sounds into group files) still parses and rewrites the whole group file per sound; only export uses `AudioGroupFile` (L-07).
  5. Batch export and import (L-02) report per item results for the per type methods; `ExportAssetOrder`, `ExportTypes`, `ExportSelectiveExceptCode` and `ImportAssetOrder` have no batch form.
  6. The bytecode 15 case of `RecomputeVariableCounters` (L-11) has no sample file.
- Why it matters: Items 1 and 4 change results or speed that G3M users can see; the others limit what G3MStudio can promise in its UI.
- Options: (a) accept all and build the missing parts when the feature that needs them starts; (b) build items 2 and 4 before the 1.1.0 release; (c) keep the old packer for sprite import (reverts part of L-05).
- Recommendation: option (a) for 2, 3, 5 and 6; option (b) for 4 only if import speed on audio heavy games matters; accept item 1 and note it in the 1.1.0 changelog.

## Script features (added 2026-10-26)

### OI-053 Code patch verbs in the plugin API (answered by Q47)
- Area: Format and library. Risk: later.
- Where: design/script-features.md section 5 (plugin candidates P-1 to P-8); research/utmt-scripts-analysis.md finding 7; decisions/0009-plugins.md.
- Question: Does the plugin API expose code patch verbs (replace, append, prepend, find and replace, regex find and replace) with a per-patch result, strict "no match is an error" and an "already applied" marker, or do plugins call G3MLib's `CodeImportGroup` directly?
- Why it matters: 19 of the 24 old Undertale and Deltarune scripts are one patch each. A shared verb set makes them short plugins, makes failures visible, and lets running a patch twice be safe. Calling G3MLib directly keeps the API small but repeats the same checks in every plugin.
- Options:
  1. Add a small `ICodePatch` helper to the plugin API (verbs, result, strict default, already-applied marker, backup and revert of a patch set). New code in G3MStudio, not in G3MLib.
  2. Plugins call G3MLib directly; the helper is a sample plugin library.
  3. No game patch plugins in the first release.
- Recommendation: option 1, built when the first plugin candidate (P-1 Debug Hooks) is started; nothing is needed for 1.0.0-beta.

### OI-054 Which library gaps of the script features go into G3MLib 1.1.0 (answered by Q45 (research task))
- Area: Format and library. Risk: blocker for M0.
- Where: design/script-features.md section 7 (twelve gaps); g3mlib-1.1.0-plan.md; Q25 (G3MLib stays low level).
- Question: Which of the listed single operations does the owner want in 1.1.0 (the library is final before G3MStudio starts, ADR-0003), and which are built inside G3MStudio or wait for 1.1.x?
- Why it matters: Most features degrade without their gap but still work: code search can use the decompiler directly, resize and GIF can use Magick.NET in G3MStudio, the data file locator is a small function. The ones that cannot be done well outside the library are the reference visitor over every model type (SF-05, SF-07), audio group file renumbering (SF-10), and bytecode level code copy (SF-09).
- Options:
  1. Add only the three that need model access to 1.1.0 (reference visitor, audio group helper with id renumbering, code copy with dependency listing) and build the rest in G3MStudio.
  2. Add all twelve to 1.1.0.
  3. Add none; all features use the 1.1.0 API as it is and the three weak features ship simpler (report only, no automatic fix).
- Recommendation: option 1. Decide before the 1.1.0 release candidate; the reference visitor may already be covered by the reverse reference index of `l2/batch4`, which the implementer should check first.

### OI-055 Which script features are in 1.0.0-beta (answered by Q44)
- Area: Decisions and process. Risk: blocker for beta.
- Where: design/script-features.md section 4 (SF-01 to SF-13); roadmap.md M3 and M4; requirements.md F-PAR-3 (P1).
- Question: Does 1.0.0-beta contain all thirteen features, or only the ones with a direct UTMT parity (SF-01, SF-02, SF-03, SF-05, SF-06, SF-08, SF-11, SF-13) and the rest (SF-04 rename by pattern, SF-07 remove unreferenced, SF-09 copy, SF-10 audio tools, SF-12 compare folders) later?
- Why it matters: SF-04 and SF-12 have no script behind them; SF-07, SF-09 and SF-10 touch audio group files and cross-file references, the riskiest operations.
- Options:
  1. All thirteen in beta.
  2. The eight with parity in beta, the five others in 1.0.0.
  3. Only SF-01, SF-02, SF-03, SF-06 in beta.
- Recommendation: option 2. F-PAR-3 asks for an outcome for every script, which the analysis already gives; the features can ship in stages.

### OI-058 Library list for 1.1.0 after the split study (Q45)
- Area: Format and library. Risk: blocker for M0.
- Where: research/library-gui-split.md section 7 (L-01 to L-17); OI-054; g3mlib-1.1.0-plan.md.
- Question: Which of the library items go into 1.1.0, and which follow in 1.1.x?
- Why it matters: Q3 and Q4 say breaking changes ship as 1.1.0, and G3MStudio takes the library from NuGet only. Changes that alter an API already on the branches (token, progress and per item results on export and import, batch rename, repacker plan and apply, compile group result and dry run, reference index update cost, ordered chunk write, name and string index) cannot be added later without a breaking change. Pure additions can follow in 1.1.x.
- Options:
  1. 1.1.0 gets section 7.1 (L-01 to L-05, L-07 to L-12: shape changes and the primitives the beta features need). Section 7.2 (clone and code copy L-13, donor read options, converter, structured check) follows in 1.1.x before SF-09, SF-10 and SF-13 start.
  2. 1.1.0 gets 7.1 and 7.2. Longest library phase; G3MStudio starts later.
  3. 1.1.0 gets only the three items of OI-054 recommendation (reference visitor, audio group helper, code copy). Every shape change in 7.1 then ships as a breaking 2.0.
- Recommendation: option 1. L-08 (compile group: token, progress, per operation result, dry run, stage) is the largest item of 7.1 and also carries SF-02, SF-03, SF-06 and the plugin patch results, so start with it. Effort: L-01, L-11, L-12 are S; L-02, L-03, L-05, L-07, L-09, L-10 are M; L-04 and L-08 are M to L.

## Settled by existing owner decisions (not on the owner's list)

| Topic | Settled by | Remaining work |
| --- | --- | --- |
| Repository private until done; remove GameMaker-extracted research files before going public | Q1 | Carry the removal into the public-release checklist (the product-embedded part is OI-022) |
| G3MLib consumed from NuGet only; breaking changes ship as 1.1.0 | Q3, Q4, ADR-0003 | none |
| Release label 1.0.0-beta | Q0 | none |
| Parity priorities | Q2 | none |
| Translation method (in-house, glossary-based), language set | Q5, ADR-0006 text | Plural syntax is OI-030; review is OI-031 |
| Unsigned beta with documented SmartScreen and Gatekeeper steps | Q7 | Fix "signed and notarized app" in design/platforms-formats.md 3; update integrity is OI-010 |
| System `git` required | Q6 | Change ADR-0004 status from proposed to accepted |
| Test machines and CI-only coverage elsewhere; use of the designated public repository for CI | Q8, risks.md R17 mitigation | Update R17 and N-PLT-1 wording; the evidence question is OI-002 |
| Update hosting on GitHub Releases | Q9 | Feed while private is OI-004 |
| G3M relation, complete modding suite, `g3mstudio://`, export to G3M | Q10, requirements F-SUITE-1 to 5 | none |
| Corpus content | Q11 | CI use is OI-043 |
| Lucide icons | Q12 | Notice handling is OI-021 |
| File associations like UTMT | Q13 | none |
| One customizable default theme, 90% G3M and 10% GameMaker, VS Code and Zed, no light theme, no dark/light split | Q14, Q23 | Remove "Themes system" from roadmap "After beta"; vision.md and design.md wording was updated in the working tree during this scan |
| Layout: docks, tabs, canvas, floating windows, presets | Q15, ADR-0010 | Details from the M0 prototypes (OI-003) |
| Reverse engineering allowed where licence and law permit | Q16, research/legal-re-policy.md | Close risks.md R15; roadmap L1 line "only after the owner clears the EULA" can say "cleared by Q16" |
| GameMaker versions in scope (LTS 2026, LTS 2022, GMS 1.4 and 2.x by game files) | Q17 | Support policy beyond that is OI-041 |
| G3MTool 1.3.1 and 1.0.1 and G3M check after 1.1.0 | Q18 | none |
| Version detection follows UndertaleModLib | Q19 | none |
| Plugins: folder, plugin.json, C# compiled on load, full access asked once, local, zip or URL | Q20 (plugins row), ADR-0009 | Re-trust and API version are OI-017 |
| Project format: GameMaker `.yyp`-like layout, UTMT import | Q21 (project row), ADR-0008 | Git side effects are OI-037 |
| No G3MStudio CLI; G3MTool covers it; the two obsolete CLI rows (Q20, Q21 with duplicate numbers) | Q24 | Remove leftovers: settings.md section 10 CLI note, UTMT-CLI parity rows, F-SUITE/F-PAR wording; launch arguments are OI-046 |
| G3MLib stays low level | Q25 | The re-review itself is OI-039 |
| Full support on every platform; block a feature only after CI proves it impossible, with a warning | Q26 | none |
| Portable zips only, six of them, self-contained, no installers; GitHub releases updater with SHA-256 digest check, no signing | Q27, Q28, Q33, ADR-0005 | The M0 updater spike; OI-051 |
| Sentry crash reports, opt-out with scrubbing | Q29, Q32, F-PRIV-1 | Scrubbing test; R19 |
| Workspace model: Direct mode default, opt-in project, journal, 1 to 3 backups, sync, close prompt, snapshots | Q31, Q34, Q35, Q36, ADR-0010 | OI-047 to OI-050 |
| G3MTool: cancel support welcome, otherwise no new features, minimal new code | Q30 | roadmap L3 |
| Multiple windows in one process; quit behaviour per OS | F-WS-9, design/platforms-formats.md 6, shell.md 18 item 5 | none |
| GMAssetTrack never written by GameMaker; gap 3 closed | research/gamemaker-format.md question 6 (research result, not a risk) | none |
| Stale statements to correct, no decision needed | Q23, Q24, Q7, Q8, Q16 | status.md says "7 decisions" (there are 10); risks.md R15 and R17 wording |

## Risk register rows (risks.md), all marked open

| Risk | Handled by |
| --- | --- |
| R1 scope | Roadmap milestones; no decision. Cut rules for P1 items need a written reason (requirements.md) |
| R2 room editor | M0 room render spike, thresholds in OI-003 |
| R3 thread safety | G3MLib item B (in flight); tests in OI-044 |
| R4 data loss | Save pipeline in M1; OI-034, OI-035, OI-036, OI-020 |
| R5 Dock.Avalonia | M0 spike, OI-002, OI-003 |
| R6 updater | M0 spike, OI-004, OI-008, OI-010 |
| R7 UTMT script compatibility | OI-040, OI-043 |
| R8 translation quality | OI-030, OI-031 |
| R9 memory | OI-033, OI-034 |
| R10 new GameMaker versions | OI-041 |
| R11 decompile round trip | G3MLib round-trip tests; OI-043 |
| R12 Git on binaries | Settled by ADR-0008 and Q21; remaining edge in OI-037 |
| R13 platform differences | OI-002, OI-011, OI-009 |
| R14 licences | OI-021, OI-022 |
| R15 reverse engineering | Settled by Q16 (close the row) |
| R16 late library needs | OI-038, OI-039 |
| R17 public CI repository | Settled by Q8 and the owner's recorded grant; keep the mitigation steps |

OI-061 answer (owner, 2026-10-26): item 1 accepted if it proves worth it (fast, light, reliable; old g3mpatches, csx scripts and data file opening must keep working, so compare against the old packer with benchmarks and regression tests). Item 4 built before 1.1.0 (one group file write per import batch, fast, nothing broken). Item 2 built now (power of two, max item size, keep alone predicate, group key). Items 3, 5, 6 accepted as limits.
OI-061 build note (2026-10-26): items 2 and 4 are built on G3MLib branch `l2/oi061` (1730e5f one group file write per sound import batch, 37876dc RepackOptions PowerOfTwo, MaxItemSize, KeepAlone, GroupKey). Item 1 was benchmarked and fixed (5c9e8c4): sprite import is now about 39% faster than the old packer. Numbers and open points are in status.md.

## M0 assumptions (2026-10-26, code run)

Decisions taken while building M0 because the docs left them open. Each is assumed, not decided; change it by editing the named place.

| ID | Assumed | Where |
| --- | --- | --- |
| OI-062 | UI strings are read by string key (`{loc:Tr key}` in XAML, `Loc.Current["key"]` in code), not by an accessor generated from the English file (ADR-0006). A test fails on a key that is unused or missing, which covers the typo risk. Generate the accessor if the key count makes the scan slow. | `G3MStudio.Core/Localization`, `LocalizationTests` |
| OI-063 | The 20 M0 commands are a subset of design/shell.md section 2. Shortcuts are written once with Ctrl and become Cmd on macOS. Ctrl+Q exits, Ctrl+Y redoes, F1 opens documentation, Ctrl+, opens settings. The palette lists only enabled commands and filters by substring. Commands that need a workspace use the context key `workspaceOpen`; no feature sets it yet. | `AppCommands.cs`, `PaletteWindow` |
| OI-064 | Default UI font is Arial Bold (owner, 2026-10-27): family `Arial, Liberation Sans, sans-serif` and weight Bold in `Theme/Tokens.axaml` (`Font.Body`, `Font.Weight`), applied by the Window style. No fonts are bundled; a missing Arial falls back to the next name and then the OS default. Assumed for M1: the settings keys `appearance.fontFamily` and `appearance.fontWeight` override the two tokens at run time (no settings store exists yet). CJK text relies on the OS fallback (F-L10N-5 still to check per platform); the code font stack still names Noto Sans Mono first and falls back to system monospace. | `Theme/Tokens.axaml`, `Theme/Styles.axaml`, `ThemeTests` |
| OI-065 | Russian is a stub: only the shell strings, written from the glossary. All seven languages must be complete for beta (F-L10N-2). The key test already requires every shipped language to have exactly the English keys, so a new language file must be complete when added. | `Languages/lang_ru.json` |
| OI-066 | Border test counts a side as valid when it is 0 or 2 px; zero means no border is drawn. Fluent keys for list, progress, slider and scroll bar track stay at 0. Separators are drawn by height, not border thickness, and are not covered yet. | `ThemeTests`, `FluentOverrides.axaml` |
| OI-067 | CI cannot restore G3MLib 1.1.0 while it only exists in the local feed folder (the folder is not in Git, and a missing feed folder is a restore error). Until the owner publishes the package, a CI run needs the nupkg copied into `localfeed/` first. At publish, revert the commit `chore: use local package feed`. | `nuget.config`, `.github/workflows/build.yml` |
| OI-068 | Zip names use `Windows`, `Linux`, `macOS` for the OS part (`G3MStudio-Windows-x64-0.1.0.zip`), like G3MTool. macOS has no icon and is not signed; the zip holds `LICENSE` next to the app. Single-file publish is used on every platform; the 64 MB size comes mostly from G3MLib's image library. | `.github/workflows/build.yml`, `G3MStudio.csproj` |
| OI-069 | Updater: staged files live in `.g3mstudio-update/new` inside the program folder (same volume, so each move is a rename); backups go to `.g3mstudio-update/old` and are deleted on success. The script moves files one by one from an explicit list and rolls back on the first failure, so the swap is per file, not one directory rename. Files that exist only in the old version stay. The script writes `ok` or `failed` to `.g3mstudio-update/result.txt` for the next start. Not done yet: the free space check, the 24 hour check timer, revert to the previous version after two failed starts (the backups are gone after success, which follows Q43 and not the older design text), reading `result.txt` at start, retry when antivirus briefly locks a file. | `G3MStudio.Core/Updates` |
| OI-070 | Updater: the zip entry mode (execute bit) is applied with `File.SetUnixFileMode` after extraction on Linux and macOS; symbolic links inside a zip would extract as plain files (the app bundle has none). The script waits with `tasklist` on Windows and `kill -0` elsewhere, for at most 120 s. | `UpdateInstaller.cs` |
| OI-071 | Docking: Dock.Avalonia 12.1.0.12 is compatible with Avalonia 12.1.4 (NuGet dependency Avalonia >= 12.1.4), so no custom dock is built. `Factory.SplitToDock` copies the panel instead of moving it, so `StudioDock.Split` removes it from its dock first. `Factory.Find` only sees layouts shown in a control, so `StudioDock` walks the tree itself. Layout JSON holds ids and placement but no content; a panel id missing from the app after an update must be skipped on restore (not built). | `G3MStudio/Docking/StudioDock.cs` |
| OI-072 | Code editor: AvaloniaEdit 12.0.0 is compatible with Avalonia 12.1.4, so it is used as designed. `TextEditor.ScrollToVerticalOffset` did not move the view in the headless session; the benchmark sets the inner scroll viewer offset and `ScrollTo(line, column)` works. The colorizer tokenizes the whole text once and has no incremental pass after edits (add a debounced background re-tokenize with the editing work). Syntax brush colors are placeholders in `Tokens.axaml`. The corpus benchmark is controlled by the environment variable `G3MSTUDIO_CORPUS_DIR`. | `G3MStudio/Editor`, `EditorBenchmarkTests.cs` |
| OI-073 | Assumed (M1): the journal, the backups and the recent list are keyed by a hash of the data file path, not by the identity (size and MD5), because a save changes both. The identity is read at every open and is the base the journal header checks. Matching a project to a data file by identity is left for Project mode. | `UserData.KeyFor`, `Journal`, `SavePipeline` |
| OI-074 | Assumed (M1): Q52 asks for undo that survives restarts. The journal is the persisted command log, replayed through the same history at open, so the undo and redo stack returns for edits since the last save only. Commands made before the last save are not rebuilt, because their inverse (a code snapshot) lives in memory; an undo that crosses the save point cannot be replayed and the replay stops there with `RecoveryProblem` set. Full persistence needs invertible command records, planned with the M2 editors. | `EditHistory`, `Workspace.StartJournal` |
| OI-075 | Assumed (M1): every journal line is flushed to disk when written (stricter than the 1 second of F-WS-4; edits are rare, so the cost is small). A journal that starts from another file state (saved, patched, replaced) is renamed `.stale` and ignored; one `.stale` file is kept per data file. | `Journal` |
| OI-076 | Assumed (M1): the save validation compares the chunk names and the entry count of every list type after reading the file back; it does not compare hashes of chunk bytes (G3MLib has no per chunk hash API; the read itself rejects unread or malformed data). A locked target is retried 3 times, 500 ms apart, then reported with the holder; the holder name comes from the Windows Restart Manager and is not available on Linux and macOS. Unix rename ignores open handles, so a locked file is not detected there. The backup is a copy (the user data folder may be on another volume), so a save of a large file costs one extra file copy. | `SavePipeline`, `FileHolders` |
| OI-077 | Assumed (M1): the Explorer lists the 26 resource and data types that are plain lists in G3MLib in two groups (resources open, data collapsed). Not built yet: the Game group (General Info, Options, Global Init, Game End), Quick Access, Tags, Included Files, null entries toggle, filter tokens other than `type:text`, sort, virtual folders, badges, context menus, multi-select, row zoom, view modes. Rows show the name only; the index tooltip is missing. | `ExplorerModel`, `ResourceTypes` |
| OI-078 | Assumed (M1): the start page is one wrapping list of cards (no grid and list switch), pin and remove are text buttons, the opened time is an absolute local date and time, the recent list keeps 50 unpinned entries, and Open Recent is not in the File menu yet (the cards and `recent.json` cover it). The primary button class is not used on the start page because its fill equals the border color and the scale test cannot tell them apart. | `StartPage`, `RecentFiles` |
| OI-079 | Assumed (M1): an open error shows in the status line (no notification system yet); the open progress is an overlay over the work area with Cancel, not a bar inside the Explorer; the window opens one file at a time and closes the old workspace when the new one has loaded; closing the window with unsaved edits has no prompt yet and leaves the edits in the journal (the Do it later outcome). | `MainWindow` |
| OI-080 | Assumed (M1): rename and code edit exist as commands with undo, redo, journal and tests, but no screen creates them yet (no F2 rename, no Apply in a code editor) and the history list (Edit > Undo History) is not built. A code edit undo restores the entry, its child entries and the code, script and function lists; strings and variables added by the new code stay in their tables, so a file saved after an undo can hold unused entries. | `EditCommands.cs` |
| OI-081 | Assumed (M1 part 2): the tab key is `<type key>:<name>`, so two entries with one name in a type share a tab. Preview tabs are italic through an attached property read by a style on the dock tab header. Pinned tabs keep their normal width (icon only is not built). Split uses a new document group beside the old one for Right and Bottom; Left and Top are not wired to a command. `file.close` has no shortcut because the registry has no chords (spec: Ctrl+K F). | `StudioDock`, `EditorTabs` |
| OI-082 | Assumed (M1 part 2): the view state (dock layout JSON, tabs, Explorer selection) is saved to `view/<path key>.json` on every change and is keyed by data file path, not by size and MD5, so it survives saves. Closing the window closes the workspace and releases the journal. | `ViewStateStore`, `MainWindow` |
| OI-083 | Assumed (M1 part 2): `files.closeUnsaved` defaults to asking (Q35, ADR-0010 item 6) although settings.md lists `keep` as the default; the setting itself is not built. Open another file with unsaved edits does not ask (the edits stay in the journal). Notifications: info and warning toasts expire after 8 s, errors stay; the status bar button only clears them (no history list yet). | `CloseGuard`, `NotificationCenter` |
| OI-084 | Assumed (M1 part 2): the code editor decompiles once on open and holds typed text locally until Apply (no live compile). A code tab closed with the X button of the tab strip, not the command, drops typed text without applying it. The history list shows time, text and the opened state but not the editor group name (every command carries one in `Editor`; both current commands are listed in English and Russian through `history.*`). Rename acts on resources the Explorer selects and on the active tab; the dialog has no live validation beyond an empty or unchanged name. | `CodeEditorTab`, `HistoryView`, `RenameWindow` |
| OI-085 | Assumed (M1 part 2, Project mode): Studio still works around the old G3MLib export (progress only as files written, no cancel, no incremental mode): autosave exports into a staging folder (a delta for code edits, a full export for anything else, so a rename costs a full export of the model, 26 s on Pizza Tower, and needs temporary disk space for one more project) and `ProjectFolder.Merge` copies only the resources whose hash changed and patches the `.g3mproj` JSON. New resources are appended to the resource list, so its order can differ from a fresh export until the next full refresh. G3MLib 1.1.0 (local branch `l2/release-final`, commit 544d370, package rebuilt in `localfeed`) now has the calls to replace this; Studio now uses them (M1 part 3, piece 1): the first export shows done/total resources and has a Cancel button (the folder and record are removed); autosave calls `Update` with `Only`, a full refresh calls `Export` into the live folder; Sync records the new base file with an `Update` that has an empty `Only`; the staging merge and the JSON patching of the `.g3mproj` file are gone. `G3MProjectService.Export(data, dir, baseFile, options, IProgress<BatchProgress> progress = null, CancellationToken cancellationToken = default)`: `BatchProgress(Done, Total, Item)` is the count of resources written, the total (complete within milliseconds of the start, once every type has begun, so it can grow at first) and the name of the resource just written; strings, variables, functions and the options files are not counted. Cancel throws `OperationCanceledException` and leaves the project folder byte-identical to before (everything is written into `.staging-*` first and removed; a folder the call created is removed); the token is honoured until the resources are written, the final move into place and the `.g3mproj` write are not interrupted. A resource that fails is left out and reported in `Warnings`; the rest of its type is kept (before, the whole type was dropped). `G3MProjectService.Update(data, dir, baseFile, options, progress, token)` with `options.Only` (type to resource names, as the old Delta mode) writes only those resources into the existing project folder, with the same files and the same `.g3mproj` as a full export of the same data (test compares every byte). A name in `Only` also removes the existing folder of that name, including `name__idxNNNN`, when the data no longer has it, so list the old name of a renamed resource. Strings, variables and functions are whole-list files and are rewritten when their type is in `Only`. Hashes of untouched folders are kept, so a hand edit is still seen by `Build`. The project keeps its mode, `Mode` is ignored, a missing project file throws `ProjectException`, `Result.Resources` is the number written. G3MLib does not decide which resources a rename touches: Studio passes the renamed resource plus the referencing ones (objects, rooms, code, strings; the library test derives the list by diffing two full exports). The package keeps version 1.1.0, so delete `~/.nuget/packages/g3mlib/1.1.0` before restoring Studio. | `ProjectLink`, `ProjectFolder` |
| OI-086 | Assumed (M1 part 2): the list of projects lives in `projects.json` in the user data folder, not in the project folder (a project folder moved by hand is not found again; adopting one is not built). A data file is matched to a project by path, then by size and MD5 of the last sync or of the enable time; a copy at another path with the synced hash is adopted silently and the stored path updated (ADR-0010 asks). A data file that differs from the last sync is only flagged (state "Data file changed outside"); the three-way merge, overwrite and keep both choices are built in M1 part 3 (OI-093). An archived project is ignored when a file is opened; Enable project creates a new one. | `ProjectRegistry`, `ProjectService` |
| OI-087 | Assumed (M1 part 2): edits made while an export or a save runs are queued in memory and run in order afterwards; they reach the journal only then, so a crash in that window loses them. Undo and redo are off while the workspace is busy. A queued edit that fails is reported in a notification. Sync refuses to start while an export runs. | `Workspace.Edit`, `ProjectLink` |
| OI-088 | Assumed (M1 part 2): defaults are autosave After a delay of 2 s and autosync On save (ADR-0010 item 5); the autosync delay when chosen is 30 s. Save in Project mode with autosync Off writes the project only and the state item shows how many changes are not synced. Focus loss uses the window Deactivated event. The two settings are per project (in the registry), not yet in a Settings page. | `ProjectSettings`, `ProjectLink` |
| OI-089 | Assumed (M1 part 2): snapshots are taken on every sync and not at enable; the first one copies the whole project (no hard links, so the disk cost of a first snapshot of a large game equals the project size). Limits (count, age, size) default to none and are not implemented. Compare and restore per resource exist in `SnapshotStore` without a UI. Snapshots live in the user data folder (Q52). | `SnapshotStore` |
| OI-090 | Assumed (M1 part 2): Change data file location needs the original data file to exist for Recreate (G3MLib `Build` needs a base file); recovering a lost data file from the project needs a base file and is built in M1 part 3 (OI-094). Move deletes the original after a size comparison only. The journal of the old path is left in the user data folder. A target that exists is refused. | `ProjectLink.RelocateAsync` |
| OI-091 | Assumed (M1 part 2): the dialogs are minimal (location question, project settings, location change, disable); no project list with sizes (F-WS-16), no Git handling, no `.gitignore` for the project, no external change watcher on the project folder. The one-time offer is a toast with an Enable project action; the state item and the Project menu are always there. | `ProjectDialogs`, `MainWindow.Project` |
| OI-092 | Assumed (M1 part 3): a rename lists its project resources from a `ReferenceIndex` built for each rename (about 160 ms on the library benchmark game, not measured on Pizza Tower; it runs on the UI thread under the write lock). Touched: owners of the renamed resource, of every changed name and of its uses, under the new and the old name, plus the whole strings file. An owner without a project type (anything but resources, code, functions, variables, strings, general info, options) makes the next autosave a full `Export`. Library warnings from an autosave are kept in `ProjectLink.Warnings` and shown only after the first export. | `RenameResourceCommand`, `ProjectLink` |
| OI-093 | Assumed (M1 part 3, external change merge): base is a copy of the `.g3mproj` file taken at enable and at every sync (`base.g3mproj` in the user data folder, not the snapshot); a project without it treats every difference as a conflict. Theirs is the open model (a changed file opens without journal edits), exported to a scratch project to hash it. Strings, variables and functions follow theirs without asking. Bring changes in = `Update` the theirs resources into the project, `Build` over the data file on disk (the library imports every resource because the recorded base differs), write the result through the save pipeline (validation, backups) and open it again; resources only the project dropped go through the project file `Deleted` list, which is cleared afterwards. Replace data file = the same without the theirs step; a resource only the data file has stays. Keep both archives the project and opens the file in Direct mode. Sync, autosync and Save refuse while the data file counts as changed. Conflict choice is per resource with the project version as default. The mode is asked on open and from the state item; there is no watcher yet (OI-091). | `ProjectLink.Merge`, `MainWindow.Project` |
| OI-094 | Assumed (M1 part 3, lost data file): a missing or unreadable data file with a known project offers Rebuild. The base for the library `Build` is the newest rotating backup, else a file the user picks; the damaged file is kept as `.corrupt`. The rebuilt file is read back before it replaces the old one. A project that never synced has no backup and needs a picked file. Rebuild is slow on large games (full import, 18 to 24 s measured in G3MLib alone). | `ProjectService.RebuildAsync` |
| OI-095 | Assumed (M1 part 3, settings): the Settings page is an overlay on the work area, opened by Ctrl+Comma, and lists installed fonts only; loading a font file of the user's own (Q56) is not built. Interface scale is the same value as zoom (50 to 200 percent). Defaults for new projects also change the open project, but only when the defaults themselves change. |
| OI-096 | Assumed (M1 part 3, Git): the panel offers initialize, stage all, unstage all, commit, status and the last 50 commits, through the `git` program. Push, pull, fetch, branches, a diff view and per-file stage are not built (`GitService.DiffAsync` exists). A Git LFS hint appears for audio or images of 10 MB or more. `.gitignore` and `.gitattributes` are written only when missing. |
| OI-097 | Assumed (tooling): warnings are errors in every build (analyzers included), except package advisory warnings NU1900 to NU1904. CI annotations after this change: only the notice that `ubuntu-latest` moves to Ubuntu 26 in 2026-10; the workflow keeps `ubuntu-latest` until the owner chooses to pin. |
| OI-098 | Assumed (M1 part 3, shortcuts and panels): shortcuts are stored per command id in `settings.json` (`shortcuts`; an empty value means none) and the editor warns about shared gestures but allows them; a layout saved before the Git, Problems or Output panels existed does not get them until Reset Layout (no release has shipped, so only developer data is affected); settings search is not built, only the shortcut filter; on macOS Ctrl and Cmd cannot be told apart in a captured shortcut. Problems lists compile errors of code entries only; Output shows the notification history. |
| OI-099 | Assumed (M1 part 3 end): own fonts are copied into `fonts/` of the user data and registered with Avalonia at start (OI-095 closed); a removed font leaves the lists at once and memory at the next start. The Git panel now also has branch list and switch, new branch, fetch, pull (fast-forward only, a pull that needs a merge is reported and left to the user), push (publishes a new branch to the first remote), add remote, per-file stage by double click and a text diff of the selected change (OI-096 closed except: discard changes, clone, merge and conflict resolution, resource-aware diff, which stay for M4). Git asks nothing in a terminal (`GIT_TERMINAL_PROMPT=0`); sign-in works through the credential helper of the machine. Settings search matches row labels and section headings (OI-098 closed). |
| OI-100 | Assumed (M2 start): one reflection based property editor opens every resource (and later the general info and options) until a specialized editor exists. It lists public properties of the G3MLib model, shows references to other entries by name only, never descends into bytecode, and picks or adds a string table entry for string properties (a name stays read-only, rename is F2). Lists show at most 50 items and three levels. A round trip of 29,266 properties of a real game (numbers, flags, enums, text) leaves the data file byte identical; string table properties are left out of that check because choosing an entry by content can pick an equal duplicate with another index. |
| OI-101 | Assumed (M2 start): the Game group lists General Info and Game Options under fixed English names (they are the tab keys); Save As (Direct mode) writes the file through the save pipeline and opens it, deleting the old file's journal once the new file is open, and refuses with a hint when a project exists (Project > Data File Location moves those); Save a Copy writes only; Find opens the search box of the code tab in front, or the Explorer filter when the focus is in the Explorer; Documentation opens `https://github.com/y114git/G3MWiki` until the owner names the real address. |
| OI-102 | Assumed (M2): Find in code decompiles every top-level code entry in parallel with one shared decompile context (3,342 entries of the smallest corpus game in 0.6 s; building a context per entry took 113 s). Matching is by line on the decompiled text; the window applies the text typed in code editors first, as Save does. Search results are not cached between searches. The panel labels of Search and Git follow the language through bindings; Output and Problems have no labels of their own. |
| OI-103 | Assumed (M2): Find Usages (Shift+F12) builds the library's reverse reference index per search and lists the entries that hold a reference to the selected resource, opening them on a double click; code usages show the instruction offset, not a line. Time budgets of the tests (100 ms) fail only on a developer machine; on a shared CI runner they are written to the output because those runners are several times slower and noisy. A panel missing from a saved layout is added to the bottom dock when a command shows it, which closes the layout part of OI-098. |
| OI-104 | Assumed (M2): sprite tabs show the frames cut out of their texture pages and room tabs draw the room (tiles and instances with sprites, pan by dragging) above the properties; pages are decoded once per workspace on a pool thread under the model's read lock and shared by both. Not drawn yet: room backgrounds, views, asset and effect layers, tile flip and rotation, instance rotation and colour. References in the property editor (parent object, sprite, code) are links that open the entry. Editing images, frame lists and room contents is not built; those fields are shown read-only or as raw numbers until their editors exist. |
| OI-105 | Assumed (M2): export and import of resources use the library's batch services (one result per item, progress, cancel that removes what a run added) through the progress overlay. Export Selected writes the selected entry (all of an extension or the general info) in the library's folder layout; Export Everything writes every type. Import takes a folder of that layout, imports the folders it finds in the library's import order, and is a bulk change: it needs a workspace without unsaved changes, cannot be undone, drops the undo stack, switches crash recovery off until the next save, makes a project refresh every resource, and after a cancel the model may hold part of it, so Revert to Saved is the way back. A single named entry cannot be imported on its own yet; the whole folder is imported. |
| OI-106 | Assumed (M3): the Mods menu runs the library's patch service on files, with the progress overlay and Cancel. Create Patch uses the open, saved data file as the modified one and asks for the original and the patch file; Apply Patch uses the open file (or asks for one) and always writes a new file; Validate Patch checks a patch file, against the open file when there is one. Merging patches, batch jobs and mod packaging are not in the window yet. |
| OI-107 | Assumed (M3): Replace Sprite Frame (Edit menu) replaces the frame clicked in the sprite tab with a picture file through the library's texture replace (the picture is fitted to the frame's box on the page). It is one undoable step; the picture is kept in the journal. Adding or removing frames, editing masks and repacking pages are not done yet. |
| OI-108 | Assumed (M3): Run Game (F5) writes the model, saved or not, to `g3mstudio_run.tmp` beside the data file, starts the game executable named in the general info with `-game` on it, and shows its output lines in the Output panel; the temporary file is removed when it exits. Stop Game is Shift+F5. Launch profiles, other runners, mods, restoring files, the game log tab and crash reports from the design are not built yet. Windows executables only are tested; elsewhere the same name without .exe is tried. |
| OI-109 | Assumed (security): replacement pictures are read only as PNG, BMP, GIF, JPEG, TGA or WebP, detected from the bytes, up to 8192 pixels a side, so vector and script types the image library can read are refused. |
| OI-110 | Assumed (M4): Help > Check for Updates reads the releases of `y114git/G3MStudio` (the real repository name is not decided), offers a newer zip for this system, and on yes downloads it, stages it, asks about unsaved changes, then starts the update script and closes. Tests use seams; the network, download and script handover are not exercised against a real release. |
| OI-111 | Done in draft (2026-10-27): all seven languages (en, ru, es, ja, ko, zh_cn, zh_tw) have every UI key and a View > Language command each. The five new files are first drafts written against the glossary and have not been read by native speakers; native review is open (supersedes the stub part of OI-065). The glossary test now ignores `{placeholders}`, lets a longer term (save as) cover a shorter one (save), and compares Japanese and Korean verbs and Spanish infinitives by stem; Spanish abierto is listed as an exception. CJK glyphs come from the system fonts or user-added font files, since no font is bundled (Q56). |
| OI-112 | Assumed (M3): Merge Patches (Mods menu) uses the library merge with code and property merging on and asks for the patches (two or more) and the output; the original is the open file or one the user picks. Run options (runner, extra arguments, game log) are one set in Settings, not named profiles. Duplicate and Remove Sprite Frame are undoable list edits (a copy shares the picture of its source; the last frame stays); collision masks are not touched. |
| OI-113 | Assumed (M3): object tabs list events with links to their code; sound tabs have Play through the system player (audio written to a private temp file); Compare Data Files writes the library diff report; Export as Mod ZIP writes `mod.g3mpatch` plus a generated `mod_config.json` (config 2.0.0, id from the name, game from the general info name, no authors) that the user edits afterwards. The full packaging editor (metadata tabs, placeholders, validation, versions, modpacks) is not built; G3M-MCE covers it. The update repository `y114git/G3MStudio` was confirmed by the owner. |
| OI-114 | Assumed (M3): room editing covers object instances only: click selects, drag or arrow keys (Shift = 8 px) move, Delete removes, and an object picker with Add Instance adds one at the top-left of the view. Each is one undoable edit that changes the room's master instance list and its layer list together (an instance only in a layer would read back as nonexistent). Zoom, grid and snap, marquee and multi-select, copy and paste, flip, layer management, tile painting, backgrounds, views, UI layers and the instance creation order are not built. |
| OI-115 | Assumed (M3): the room view now has zoom (Ctrl+wheel, Ctrl+plus, Ctrl+minus, Ctrl+0 fit, Ctrl+Shift+0 actual), grid (G) and snap (Shift+G) using the room's own grid size (16 px when 0), marquee and multi-select (Shift adds, Ctrl toggles, Ctrl+A all), Space or middle-drag pan, copy and paste (Ctrl+C, Ctrl+V at the pointer, one undo step), and flip (X, Y; the position moves so the picture stays in place). Several changes in one gesture are one `GroupCommand`. Not yet: rulers and guides, Alt-drag painting, pasting to a chosen layer, particle and sequence elements, saving the room as PNG, per-tab restore of scroll and zoom. |
| OI-116 | Assumed (M3): the room tab has a layers panel (left, 240 px): visibility check box, name and type per row, lowest depth first; Add (an Instances layer in front of all), Delete (an instance layer takes its instances out of the room's master list too), Up and Down (the two depths swap), Rename (names must be unique). Each is one undoable edit. Not yet: other layer types to add, folders and groups, lock, drag reorder, depth lock, layer properties in the Inspector. |
| OI-117 | Assumed (M3): tile painting works on tile layers with a tile set. The room toolbar has Select, Paint, Erase and Fill; choosing a tile layer in the layers panel shows its tile set as a palette (click a cell, index 0 is empty). A stroke is one undoable edit; Fill replaces the connected cells with the same tile index (flags ignored). Tile flip and rotate flags, rectangle and eyedropper tools, stamp brushes, auto-tiling, tile layer creation, resizing, CSV import and export, and "fit tile layer to room" are not built. |
| OI-118 | Assumed (M3): the room tab has an instance inspector (right side) for the selected instances: X, Y, scale X and Y, rotation, frame, image speed and object. A value typed there is applied to every selected instance as one undoable edit. Colour, creation code and pre-create code are still edited through the property grid and the Code tab. |
| OI-119 | Assumed (M3): the layers panel adds Instances, Assets, Background and Tiles layers (tiles ask for a tile set and size the grid to the room) and edits depth (-16000 to 16000), X and Y offset and horizontal and vertical speed of the selected layer. New layers are empty; their contents are added with the tools. Path and effect layers, folders, lock and the depth lock toggle are not built. |
| OI-120 | Assumed (M3): Room > Save Room as PNG writes the room in front at 100% from its draw list (visible layers, no grid or selection), up to 16384 pixels a side. A Room menu now exists for the room commands; the others from the design (play room, element list, guides) are not built. |
| OI-121 | Assumed (M3): asset layers now draw their sprite elements and show sequences, particle systems and text items as labelled boxes; all can be selected and moved, deleted, and sprite elements can be added (first assets layer, name graphic_N). Their other properties are still edited in the property grid. Legacy tiles are drawn on GMS1 rooms only. |
| OI-122 | Assumed (M3): Alt+drag over an instance paints copies of it along the pointer path (snapped to the grid when snapping is on), as one undo step; the copies keep the source's object and scale. |
| OI-123 | Assumed (M3): the room tab has an element list under the canvas: the instances and asset elements of the layer chosen in the layers panel (all layers when none is chosen), one row each with object or kind, name and place. Rows and the canvas selection follow each other. Per-element export checkboxes, search and the instance creation order are not built. |
| OI-124 | Assumed (M3): the room canvas has rulers on the top and left edges (ticks at 1, 2 or 5 times a power of ten in room pixels). Dragging from the top ruler into the canvas makes a horizontal guide, from the left ruler a vertical one; right-click removes a guide within 4 screen pixels; moved objects snap to a guide within 8 screen pixels. Guides are not saved with the room (the data file has no place for them). |
| OI-125 | Assumed (M3): tile painting also has Rectangle (drag a box of cells) and Pick (copy a tile and its flags from the map into the palette), and the palette has Mirror, Flip and Rotate 90 flags stored in bits 28 to 30 of the cell value. Flags draw as mirror and flip about the tile centre, then a quarter turn; the order GameMaker applies them in has not been checked against GameMaker itself. |
| OI-126 | Assumed (M3): tile sets, fonts and embedded images show their picture at 1x to 8x above the property grid; paths are drawn as a line (curved when smooth, closed when closed) with their points; shaders list their sources; timelines list their moments with links to the code. These are viewers: points, moments and sources are edited through the property grid and the code tabs. |
| OI-127 | Assumed (M4): run profiles are named copies of the three run options (runner, arguments, game log) saved in Settings; picking one copies its values into the current options, which Run Game then uses. Profiles for mods, working folder and environment variables, per-workspace remembering and the toolbar switcher from the design are not built. |
| OI-128 | Assumed (M4): Tools > Repack Texture Pages plans the library repack of every texture group with default options, shows the page count before and after, and applies it as a bulk change (needs no unsaved edits, cannot be undone, Revert to Saved returns). Per-group choice, page size, padding, algorithm and the per-page fill report from the design are not offered yet. |
| OI-129 | Assumed (M3): Recalculate Collision Mask (Edit menu, sprite tab in front) rebuilds every mask the sprite has from the alpha of its frames (solid when alpha is above 0, leftmost pixel in the top bit), keeping the number of masks; one undoable step. On the test sprite the rebuilt mask has exactly the pixels of the original. The tolerance, bounding box modes, import and export of mask PNGs and manual mask bounds are not built. |
| OI-130 | Fixed (M4): the test harness did not await async test bodies, which hid failures. Bulk changes (import, repack) now run on a pool thread and raise their events on the UI thread (`Workspace.ApplyBulkAsync`), duplicate compile problems at one spot are shown once, and progress reports that arrive after their operation ended no longer show the overlay again |
| OI-131 | Assumed (M3): Room > Instance Creation Order lists the room's instances in master list order and moves one up, down, to the top or to the end as an undoable edit; it does not list UI layer instances yet |
| OI-132 | Assumed (M3): the layer lock is a view setting kept per room tab, not stored in the file; locked layers are drawn but not picked. Views (V) are outlined in orange for the enabled views |
| OI-133 | Assumed (M3): dragging an object from the Explorer onto the room adds an instance, a sprite adds a sprite element on the first assets layer, both at the grid-snapped drop point; other resource types are ignored for now |
| OI-134 | Assumed (M3): the path canvas adds a point with a click on empty space (at the end), moves a point by dragging and deletes the selected point with Delete; inserting into a segment and the point table are not built yet |
| OI-135 | Assumed (M4): plugins are folders under the user data folder `plugins` with plugin.json and C# sources in src; they are compiled with Roslyn at start, cached by content hash, loaded in a collectible load context and offer IStudioContext with AddCommand, Notify, Log and the open workspace. Plugin commands appear in the palette and a Plugins menu; on/off is stored in plugins.json. Install from ZIP or GitHub, trust prompt, panels, editors, .csx and hot reload are not built yet |
| OI-136 | Assumed (M3): Edit > Import Mask and Export Mask work on the mask of the clicked frame (the last mask when masks are shared); the PNG is white where solid, an import needs the mask size and counts a pixel as solid when it is opaque and lighter than mid grey, and it is one undo step |
| OI-137 | Assumed (M3): Room > Export Tiles as CSV and Import Tiles From CSV work on the selected tile layer; the CSV has one line per cell row and the raw tile values (flags included), an import must match the layer size and applies only the changed cells as one undo step |
| OI-138 | Assumed (M4): Plugins window installs from a ZIP (plugin.json in its root) or a folder after a trust prompt that shows name, version, authors, source and the permissions the plugin says it needs; an installed plugin with the same id is replaced, and a ZIP entry that leaves the plugin folder is refused. GitHub URL install and update checks are not built |
| OI-139 | Assumed (M3): a room tab remembers its zoom and top-left position per data file; it is written with the tab layout when tabs change and when the window closes, not on every pan |
| OI-140 | Assumed (M3): Tools > Strings opens a window with the whole string table, a text or regex filter and an edit box; a change edits the entry for every use as one undo step. The use counts, the single-use choice and the kind column are not built yet |
| OI-141 | Assumed (M4): saving any file in a plugin folder reloads that plugin after half a second of quiet (hot reload); a plugin that fails to compile shows its error in the Plugins window and is retried on the next save. Turned-off plugins are not reloaded |
| OI-142 | Assumed (M3): an animation curve tab shows its channels as lines through their points, one colour per channel, above the property grid; points are not edited on the canvas and Bezier handles are not drawn yet |
| OI-143 | Assumed (M3): a click within 8 pixels of a path segment inserts the new point into that segment instead of at the end (the closing segment of a closed path appends); not covered by a pointer test, only by the point command tests |
| OI-144 | Assumed (M3): a path tab has a point list with X and Y boxes beside the canvas; the list and the canvas share the selected point and a typed value is one undo step. The speed column and Move All are not built yet |
| OI-145 | Assumed (M3): the layer toolbar has Fit to room for a tile layer; it resizes the cell grid to cover the room with its tile size, keeps the tiles that still fit and undoes as one step. A new tile layer is already created at room size |
| OI-146 | Assumed (M3): shader sources are editable text boxes with an Apply button per language; each apply is one undo step through the property command. There is no syntax check or compile of the shader |
| OI-147 | Assumed (M3): a font tab lists its glyphs (code point, character, sheet position, size, shift, offset) under the sheet picture, with a filter that takes a character, U+code or number; glyphs are read-only for now |
| OI-148 | Assumed (M3): a particle system tab lists its emitters (name, on or off, mode and count, shape and region) above the property grid; emitters are read-only for now |
| OI-149 | Assumed (M3): a sequence tab shows a summary line (size, length, speed, moment and message counts) and its tracks as an indented tree above the property grid; tracks and keyframes are read-only for now |
| OI-150 | Decision (M3): G3MLib stores no data for path layers and no UI layers in the data file (path layers are an empty layer type, UI layers only exist in project files), so the room editor lists them but has nothing to edit. Background and effect layers get a property list under the layer fields, edited through the room property paths |
| OI-151 | Assumed (M4): Install From GitHub takes https://github.com/owner/repository, downloads the default branch ZIP through the GitHub API (50 MB limit, github.com only), and runs the same trust prompt as ZIP and folder installs; a ZIP whose files sit in one top folder installs from that folder. Update checks for installed plugins are not built; a plugin is updated by installing it again |
| OI-152 | Assumed (M3): a path tab has a Speed % box for the selected point (100 is the normal path speed) and Move all, which shifts every point by an X and Y distance; each is one undo step |
| OI-153 | Assumed (M3): the Strings window shows how many places use the selected string, counted from the reference index (rebuilt per selection, about 160 ms on a large game); the change-only-this-use choice and the kind column are not built |
| OI-154 | Assumed (M3): a timeline tab has a step box for each moment (one undo step per change) under the list of code links; adding and removing moments and editing their code is not built. The corpus holds no timeline with moments, so the step path is tested on a timeline built in code |
| OI-155 | Assumed (M3): a timeline tab can add an empty moment (one step after the last) and remove a moment with its actions, each as one undo step; the actions of a moment are still edited in the code tabs |
| OI-156 | Assumed (M3): each emitter of a particle system and each top-level track of a sequence opens, when clicked, into a property list of its own fields; edits go through the property command (one undo step). Nested tracks and keyframe lists are shown as item counts only; adding or removing emitters and tracks is not built |
| OI-157 | Assumed (M3): picking a glyph in a font tab opens a property list of its fields (position, size, shift, offset, character); each change is one undo step. Adding and removing glyphs and regenerating the sheet are not built |
| OI-158 | Assumed (M4): a plugin installed from GitHub remembers its address; Update from GitHub downloads and installs it again after the same trust prompt. There is no automatic check for a newer version, and plugins installed from a ZIP or folder have no update source |
| OI-159 | Measured (M5): the room view builds a 186,144-item room in 1.2 s and pans at a mean of 11.6 ms per frame (p95 13.9 ms); a synthetic 60,000-tile room builds in 0.2 s and pans at 13.9 ms. No further optimization was needed for the beta targets |
| OI-160 | Assumed (M4): a plugin panel is a command (AddPanel in the plugin context) that opens a window with an Avalonia control the plugin builds; it is not docked into the layout yet, and plugin editors for resource types are not built |
| OI-161 | Assumed (M3): each string in the Strings window is labelled name (equal to the name of a resource) or text; the library gives no finer kind (code, constant, unused), so those are not shown |
| OI-162 | Assumed (M3): a font tab can add a glyph for a character (empty, inserted at its sorted place; set its sheet position afterwards) and remove one; a particle system tab can add an emitter with default settings (also added to the emitter table under a new name) and remove one from the system (it stays in the table because other systems may share it). Each is one undo step; an added emitter was verified to survive a write and read of the data file |
| OI-163 | Assumed (M3): Room > Play This Room (Ctrl+F5) runs the game like Run Game, but with the open room moved to the front of the room order for the length of the save, so the game starts there. Warned in a notification: earlier rooms never ran, so globals, saved state and persistent objects they set up are missing and the game can fail; a room that is not in the room order cannot be started this way. The room order in the open file is restored right after the temporary copy is written and is never part of the undo history. Not run against a real game in the tests |
| OI-164 | Decision (M3): change-only-this-use already exists: editing a string property in a resource tab picks or adds a table entry for that one use, and the Strings window edits the entry itself for all uses. The window now says so next to the use count instead of adding a prompt |
| OI-165 | Assumed (M4): Tools > Run Script runs a .csx file on the open data file as one bulk change after a warning (a script can change anything and read and write any file; it cannot be undone; Revert to Saved is the way back). It needs a file without unsaved edits. Only a small part of UndertaleModTool's script functions exist: Data, FilePath, ScriptPath, ScriptMessage, ScriptWarning, ScriptError, ScriptQuestion (answered yes); a script that needs anything else stops with a compile error naming it. Scripts bundled in plugins and registered as commands are not built |
| OI-166 | Assumed (M3): a sequence tab can remove a top-level track with its keyframes (one undo step). Adding a track is not offered because a track needs keyframes of a kind that depends on its type; nested tracks are not removable |
| OI-167 | Assumed (M4): a plugin can add an editor view for a resource type (AddEditor with the Explorer type key). It is shown above the property list in that resource's tab, and only for types that have no built-in view; the first active plugin that returns a view wins, and an editor that throws is logged and skipped. A plugin cannot replace the property list or add a whole new tab kind |
| OI-168 | Assumed (M3): each timeline moment lists its actions (the code entry each runs) with a Remove button, and Add action takes the name of an existing code entry; a new action carries the fixed values GameMaker writes for a code action. Creating a new code entry from the timeline is not offered. An added action was verified to survive a write and read of the data file |
| OI-169 | Assumed (M4): Check for update on a plugin installed from GitHub compares the commit it was installed at (read from the folder name inside the GitHub ZIP) with the latest commit of the default branch and says whether a newer version exists; the check runs only when the button is pressed, never on its own, and tells the user to use Update from GitHub. The GitHub request itself is not tested (no network in tests) |
| OI-170 | Assumed (M3): a sequence tab can add an empty group track (the only track kind without keyframes) next to removing a track, each as one undo step; an added group track was verified to survive a write and read of the data file. Other track kinds (instance, graphic, audio, real, colour, text, particle) are not offered because they need keyframes of a type that depends on the model |
| OI-171 | Assumed (M5): the Inspector panel shows the type and name of the Explorer selection and how many places use it, with a Find Usages button; it was an empty panel before. Editing still happens in the resource tabs |
| OI-172 | Decision (M5): menu bar regrouped after a placement review: no menu with a single item (Git and Go removed; Git panel and the other panels are under View > Panels, Go to Resource is in Edit), a Sprite menu next to Room whose commands are greyed out unless a sprite or room tab is in front, Settings under File, languages in a View > Language submenu, separators between groups. Panel titles are now translated (they were English in every language) |
| OI-173 | Assumed (M5): the GML colorizer colors functions, variables, constants and assets (built-in names from G3MLib plus the resource names of the open file, read when the tab opens) besides keywords, strings, numbers, comments, operators and preprocessor lines; the code font comes from the theme. Settings > Colors changes the accent and the 10 code colors live (#RRGGBB, Enter or leaving the box applies, Reset per color); other theme tokens, themes as files and density are not offered yet |
| OI-174 | Assumed (M5): the Settings page has a category list on the left (one category shows at a time; search looks in all) and the Inspector shows the resource of the tab in front |
| OI-175 | Decision (M5): the room toolbar lost its object and sprite lists and the two Add buttons (the lists held thousands of entries without a search and duplicated drag from the Explorer). The Place tool puts the object or sprite selected in the Explorer where the canvas is clicked (grid-snapped), and drag from the Explorer still works. The old TileTool enum is now RoomTool |
| OI-176 | Decision (M5): like GameMaker, the layers, the selected layer's fields, the instance fields and the tile palette of a room are shown in the Inspector panel while a room tab is in front, so the room canvas gets the whole tab; the Inspector scrolls and shows the resource summary under them |
| OI-177 | Assumed (M5): Settings > Code editor sets the text size (8 to 32), word wrap and line numbers of every code view, live. Problems and Output show a short hint when empty; the Start page is centered with a primary Open button. Still not built from design/settings.md: theme files and Edit Theme, density, reduce motion, format culture, per-workspace scope, import from UndertaleModTool |
| OI-178 | Assumed (M5): Settings > Colors can export the colors to a theme file (JSON, {"colors": {...}}) and import one; an import keeps the valid #RRGGBB values of known keys, replaces the current colors and reports a file that is not a theme. A theme only carries these colors, not density, fonts or layout. Density, reduce motion, format culture, per-workspace scope and the UndertaleModTool settings import remain unbuilt |
| OI-180 | Decision (M5): after opening GameMaker live and its room Inspector (Room Settings, Viewports and Cameras, Room Physics as collapsible sections), the property list of every resource now has collapsible headings: groups start open, lists start closed (they can hold thousands of rows), a click on a heading with its arrow flips it, and a filter shows every match regardless. Live GameMaker comparison so far: Start page and the room Inspector layout only |
| OI-181 | Decision (M5): two duplicates removed after a render review: the sequence tab no longer repeats size, length, speed and counts above the tracks (the property list has them), and the timeline tab no longer lists its code actions twice (the action names in the editable moment view are now the links that open the code) |
| OI-182 | Decision (M5): comparing with the live GameMaker room editor showed that grid, snap, zoom and fit were only keyboard shortcuts here. The room toolbar now has Grid, Snap, Views, zoom out and in, Fit and 100% buttons that share their state with the keys. The tile-layer button was renamed Resize tile grid so it is not confused with the view Fit. GameMaker also has Creation Code and Instance Creation Order buttons in its room settings, which are only menu items here |
| OI-183 | Decision (M5): four settings from design/settings.md are deliberately not built. Reduce motion: nothing in the app animates, so the checkbox would change nothing; it is added with the first animation. Format culture: numbers are shown and parsed in the invariant format everywhere, a display culture would make shown values differ from the ones typed back. UndertaleModTool import: the settings.json format is not available to test against and most of its settings (null entries, asset order swapping, GMS 1.4 folder) have no equivalent here. Per-workspace scope: needs a settings layer per data file with its own UI, deferred until a setting exists that differs per project. |
| OI-184 | Decision (M5): compared with the live GameMaker sprite and script editors. Taken over: current line highlight, spaced property names, brace folding. Sprites now have a Play/Pause button that cycles the frames at the sprite speed. Object physics fields and the sprite texture and collision mask fields sit in collapsible sections that start closed. Code tabs show INS or OVR. Sounds with 16-bit WAV audio draw a waveform. Frames show the origin as a crosshair and a Lock origin toggle makes the Origin X and Y boxes read-only; the library-internal OriginXWrapper and OriginYWrapper rows are hidden. Code views fold multi-line { } blocks. Room Creation Code opens from the Room menu and a button beside Instance Creation Order. |
| OI-185 | Decision (M5): compared with the live GameMaker font and shader editors. Texture items (font sheets, tile sets) open at the largest whole zoom that fits instead of 1x. Shader sources are tabs (one per language) in the code view with line numbers and the GML colors for comments, numbers and shared keywords, and the property list no longer repeats them. The font glyph list, path points, particle emitters, sequence tracks and timeline moments are no longer repeated in the property list, and empty lists get no row. The glyph box also adds a range (A-Z or U+0020-U+007E) as one undo step. Not built: regenerate texture and the SDF options as a form; the glyph list and the property list cover the data. Paths could not be compared (Cover Assault has none). |
| OI-186 | Correction (M5): later work made parts of older entries out of date. The Git panel has branches, new branch, fetch, pull, push, remotes, ahead and behind counts and a diff view (OI-096); the Strings window has the kind column and the use count, and a single use is changed through that property in its own tab (OI-140, OI-153); the path editor has the point table, the speed column and Move All (OI-134, OI-144); timeline moments can be added, removed and edited (OI-154); emitters, tracks and glyphs can be added and removed (OI-156, OI-157). The Room menu has Play This Room, Instance Creation Order, Room Creation Code, tile CSV export and import and Save Room as PNG (OI-120). |
| OI-187 | Decision (M5): compared with the live GameMaker particle system and sequence editors. Emitters are listed by name with their enabled state and settings as here. Not built, each a feature of its own: the live particle preview (needs a particle simulation), the sequence canvas and the keyframe timeline with its playhead (the tracks list and the property list edit the same data). The animation curve view was compared too and now has a grid and value labels on its axes. The Settings page and the Git panel were not compared live: GameMaker's Preferences window is unreliable to drive, and its Source Control tab needs a repository. |
| OI-188 | Decision (M5): compared with the live GameMaker Preferences window (a tree of pages, Restore defaults on each, Import Preferences, Apply, OK, Cancel). Settings now has Restore defaults for the selected section (appearance, colors, code editor, files, projects, run, shortcuts), next to Close; the language and the saved run profiles are kept. Not taken over: Apply, OK and Cancel (settings save at once, so there is nothing to confirm), and Import Preferences (see OI-183). G3MStudio's Settings has a search box, which GameMaker's Preferences lacks. |
| OI-189 | Decision (M5): a check of the settings mapping table in design/settings.md against what the app has shows that of the UndertaleModTool settings only "warn about saving before closing" (AskOnClose) has an equivalent today; the others map to settings that do not exist here (GameMaker paths, asset order swapping, null entries, global room grid override, recompile on save). The settings import stays unbuilt until those settings exist, since it would change one value. A scan for unused public members found none that is not used from XAML, a plugin or a script. |
| OI-190 | Decision (M5): per-data-file settings (F-UI-6, OI-127). Each data file remembers the run profile picked while it was open (`fileRunProfiles` in settings.json, full path to profile name) and opening the file picks it again, which sets the runner, arguments and debug output; a file with nothing remembered keeps the current options. This is the only per-file setting: the other settings do not differ per game. The Settings page says so under the profile list. |
| OI-191 | Decision (M5): sequence tabs show a timeline above the track list (a keyframe bar can be dragged to another whole frame, one undoable edit of its key; lengths, adding, removing and values stay in the property list): a frame ruler, one row per track (nested tracks indented), the moments and the broadcast messages, each keyframe a bar from its key to its key plus its length. The sequence canvas and the playhead are not built. |
| OI-192 | Decision (M5): particle system tabs show a live preview above the emitters: an approximate simulation stepping 60 times a second (stream and burst emitters with delay and interval, region shape and distribution, speed, direction, gravity, per-step increases, size, lifetime, start, mid and end colors; at most 4000 particles, every value from the file clamped to a finite range), drawn as disks, with a Restart button; it starts again when no particle is left for 45 steps. Not simulated: the emitter sprite and texture, orientation, wiggle, spawn on death and spawn on update, so it will not match the game exactly. |
| OI-193 | Decision (M5): OI-183 said Reduce motion would come with the first animation. The particle preview is that animation, so Settings > Appearance now has "Reduce motion": with it on, the preview does not run and shows the system as it looks after 40 steps (Restart rebuilds that picture). Sprite playback starts only when the user presses Play, so it needs no change. The setting does not follow the operating system setting; that is not read. Format culture, the settings import and per-workspace scope other than the run profile stay unbuilt as in OI-183 and OI-189, 190. |
| OI-194 | Decision (M5): sequence tabs have a canvas and a playhead above the timeline. The canvas draws the sprite of each graphic track whose keyframe covers the frame, moved by the position tracks of the track and of its groups, scaled and turned by its scale and rotation tracks (linear between keyframes when the track says so, stepped otherwise), fitted to the canvas frame of the sequence or, when it has none, to the extent of its sprites over the whole sequence. A slider, a Play button (at the playback speed) or a click on the ruler of the timeline moves the playhead, which the timeline draws. Not drawn: blend colors, masks, origin tracks, and instance, text and particle tracks; the fit is computed again after an edit. A nested sequence track plays its sequence from the frame of its keyframe. |
