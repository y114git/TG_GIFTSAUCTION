# G3MStudio knowledge base

Design documents for G3MStudio, a GameMaker data file editor.

| Doc | Contents |
| --- | --- |
| [vision.md](vision.md) | Goals, foundations, out of scope |
| [requirements.md](requirements.md) | Functional (F-*) and non-functional (N-*) requirements with priorities |
| [architecture.md](architecture.md) | Solution shape, main parts, threading, files on disk, performance budget, G3MLib work |
| [design.md](design.md) | Visual and interaction rules (2 px borders, tokens, commands) |
| [design/](design/shell.md) | Shell, onboarding, UX standards and per-editor specs |
| [roadmap.md](roadmap.md) | Milestones M0 to M5 with acceptance checks |
| [risks.md](risks.md) | Ranked risks and next steps |
| [decisions/](decisions/README.md) | Architecture decision records |
| [l10n/glossary.md](l10n/glossary.md) | Terminology for ru, es, ja, ko, zh_cn, zh_tw with sources |

Research:

| Doc | Contents |
| --- | --- |
| [research/utmt-parity.md](research/utmt-parity.md) | UndertaleModTool feature inventory, parity checklist, script list |
| [research/g3mlib.md](research/g3mlib.md) | What G3MLib and G3MTool provide, gaps for an editor |
| [research/code-editor-spike.md](research/code-editor-spike.md) | M0 code editor spike: AvaloniaEdit with G3MLib tokens, open time and scroll frame cost |
| [research/gamemaker-format.md](research/gamemaker-format.md) | Format investigation log: headless builds, round-trip results, open questions |
| [research/gamemaker-format-coverage.md](research/gamemaker-format-coverage.md) | Chunk and field coverage of G3MLib against UndertaleModLib |
| [research/gamemaker-fixtures.md](research/gamemaker-fixtures.md) | Feature fixtures built with Igor and how G3MLib handles each |
| [research/vscode-zed.md](research/vscode-zed.md) | VS Code and Zed patterns to adopt and avoid |
| [research/modding-suite.md](research/modding-suite.md) | G3MTool, G3M-MCE and G3M features for modders, mod format, URI schemes, compatibility constraints |
| [research/legal-re-policy.md](research/legal-re-policy.md) | What reverse engineering is allowed and how it is logged |
| [research/corpus.md](research/corpus.md) | Regression game files |
| [research/g3m-design.md](research/g3m-design.md) | G3M visual language and the proposed token set |
| [research/platforms-and-languages.md](research/platforms-and-languages.md) | Platform matrix, languages, update mechanisms |
| [research/platform-proof-log.md](research/platform-proof-log.md) | Evidence log for any feature blocked on one platform (N-PLT-2) |
| [research/utmt-script-compat.md](research/utmt-script-compat.md) | Compile and run results of the UndertaleModTool scripts against G3MLib |
| [research/utmt-scripts-analysis.md](research/utmt-scripts-analysis.md) | Analysis of the 110 UndertaleModTool scripts and the fate of each |
| [research/library-gui-split.md](research/library-gui-split.md) | What belongs in G3MLib and what in the app, per batch feature |
| [research/room-render-spike.md](research/room-render-spike.md) | M0 room rendering spike |
| [research/editors-parity-2026-11.md](research/editors-parity-2026-11.md) | Editor parity against GameMaker, state per editor |
| [research/image-editor-parity-2026-11.md](research/image-editor-parity-2026-11.md) | Image editor parity against GameMaker |
| [research/code-intelligence-parity-2026-11.md](research/code-intelligence-parity-2026-11.md) | Code editor features against GameMaker and UndertaleModTool |
| [research/bypass-audit-2026-11.md](research/bypass-audit-2026-11.md) | Where Studio refuses an action and whether the user can go on |
| [research/copy-real-files-2026-11.md](research/copy-real-files-2026-11.md) | Copy to Other Data File checked against real games |
| [research/performance-2026-11.md](research/performance-2026-11.md) | Speed and memory measurements on large games |
| [research/real-window-review.md](research/real-window-review.md) | Notes from reviewing the real window |
