# 0002 Solution layout

Status: proposed.

`G3MStudio.Core` (no UI dependency), `G3MStudio.App` (Avalonia, all views and editors), `G3MStudio.Tests`.

Why: Core logic (workspace, undo, save pipeline, commands, settings, git) is testable without a UI. A project per editor would add build and reference overhead with no second consumer. Editors register through `IEditorProvider`, so new resource types do not touch the shell.

Revisit when: App build time hurts, or an editor is needed outside the app.
