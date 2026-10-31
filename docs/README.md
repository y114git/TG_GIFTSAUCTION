# G3MStudio knowledge base

Read [status.md](status.md) first. It says where work stands and what to do next.

| Doc | Contents |
| --- | --- |
| [vision.md](vision.md) | Goals, foundations, out of scope |
| [requirements.md](requirements.md) | Functional (F-*) and non-functional (N-*) requirements with priorities |
| [architecture.md](architecture.md) | Solution shape, main parts, threading, files on disk, performance budget, G3MLib work |
| [design.md](design.md) | Visual and interaction rules (2 px borders, tokens, commands) |
| [g3mlib-1.1.0-plan.md](g3mlib-1.1.0-plan.md) | Final G3MLib 1.1.0 work list with acceptance checks |
| [design/](design/shell.md) | Shell, onboarding, UX standards and per-editor specs |
| [roadmap.md](roadmap.md) | Milestones M0 to M5 with acceptance checks |
| [risks.md](risks.md) | Ranked risks and next steps |
| [questions.md](questions.md) | Open questions with working defaults |
| [decisions/](decisions/README.md) | Architecture decision records |
| [status.md](status.md) | Current state, verified work, next actions |

Research:

| Doc | Contents |
| --- | --- |
| [research/utmt-parity.md](research/utmt-parity.md) | UndertaleModTool feature inventory, parity checklist, script list |
| [research/g3mlib.md](research/g3mlib.md) | What G3MLib and G3MTool provide, gaps for an editor |
| [research/code-editor-spike.md](research/code-editor-spike.md) | M0 code editor spike: AvaloniaEdit with G3MLib tokens, open time and scroll frame cost |
| [research/gamemaker/README.md](research/gamemaker/README.md) | GameMaker LTS 2026 hands-on findings with screenshots |
| [research/gamemaker/editors.md](research/gamemaker/editors.md) | GameMaker editors in detail, from the manual |
| [research/gamemaker/coverage.md](research/gamemaker/coverage.md) | Checklist of every GameMaker window and editor with its capture status |
| [research/gamemaker-format.md](research/gamemaker-format.md) | Format investigation log: headless builds, round-trip results, open questions |
| [research/gamemaker-format-coverage.md](research/gamemaker-format-coverage.md) | Chunk and field coverage of G3MLib against UndertaleModLib |
| [research/gamemaker-fixtures.md](research/gamemaker-fixtures.md) | Feature fixtures built with Igor and how G3MLib handles each |
| [l10n/glossary.md](l10n/glossary.md) | Terminology for ru, es, ja, ko, zh_cn, zh_tw with sources |
| [research/vscode-zed.md](research/vscode-zed.md) | VS Code and Zed patterns to adopt and avoid |
| [research/modding-suite.md](research/modding-suite.md) | G3MTool, G3M-MCE and G3M features for modders, mod format, URI schemes, compatibility constraints |
| [research/legal-re-policy.md](research/legal-re-policy.md) | What reverse engineering is allowed and how it is logged |
| [research/corpus.md](research/corpus.md) | Local regression game files |
| [research/g3m-design.md](research/g3m-design.md) | G3M visual language and the proposed token set |
| [research/platforms-and-languages.md](research/platforms-and-languages.md) | Platform matrix, languages, update mechanisms |
| [research/platform-proof-log.md](research/platform-proof-log.md) | Evidence log for any feature blocked on one platform (N-PLT-2) |

Excluded from Git: all screenshots (binary). Before the repo goes public, also remove the material extracted from GameMaker (gamemaker-hotkeys.md, gamemaker-terminology.md).
