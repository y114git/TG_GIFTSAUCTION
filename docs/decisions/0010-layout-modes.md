# 0010 Layout modes: docks, tabs and canvas

Status: accepted direction (owner, 2026-10-25); details set by the M0 prototypes. Answers questions.md Q15.

## Decision

One layout engine with three kinds of editor area that can be mixed freely:

1. Tab groups with splits (VS Code style).
2. Canvas workspaces (GameMaker style): a pannable, zoomable surface where editors are floating cards, related editors linked (object to its events to their code).
3. Floating OS windows for any panel or editor (multi-monitor).

Every panel and editor can move between docks, tab groups, a canvas and its own window by drag or command. Layouts save as named presets.

Built-in presets, chosen in onboarding and switchable any time:

- Studio (default): explorer left, inspector right, tabs in the centre, output and problems at the bottom, a canvas available as an extra tab for linked editing.
- Code: VS Code-like, minimal chrome, activity bar, everything in tabs.
- Canvas: GameMaker-like, workspaces with floating editors.

## Rules

- The default must be clear to a beginner: few panels open, empty states that say what to do, the command palette always one shortcut away.
- No mode hides a feature; only placement differs.
- Layout state persists per workspace and restores exactly.
- Performance: inactive canvas cards render as cached bitmaps; a workspace with 50 open editors stays at 60 fps panning.

## Prototype questions for M0

Docking library fit (Dock.Avalonia) for moving content between tabs, canvas and windows; canvas rendering cost; how onboarding presents the presets.
