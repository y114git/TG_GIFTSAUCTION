# 0008 Project folder format

Status: revised (owner, 2026-10-25; scope re-review for Q25 the same day). Accepted earlier, revised the same day: the native layout follows GameMaker's `.yyp` project structure; UndertaleModTool 0.9 projects are supported by import and export instead of being the native layout. Implements requirements F-WS-7, F-WS-10, F-WS-11 and G3MLib work item 6.

## Context

G3MLib has two disk formats (research/g3mlib.md, section 3): `ProjectContext` (UTMT 0.9 mod projects, 13 types, GML kept as source, selective export) and the modding resource export (all 30 types, resource order, patch-shaped). The owner wants G3MStudio projects to feel like GameMaker projects in structure and configuration, while staying fast and reliable, and UTMT users must be able to move over.

## Decision

1. Native layout mirrors GameMaker's project layout (research/gamemaker/README.md, TEST project):
   - `<Name>.g3mproj` at the root, JSON, the counterpart of `.yyp`: project metadata, base data file identity (path, size, MD5), mode, resource list, virtual folders for the explorer, resource order per type, audio and texture groups, format version.
   - One folder per resource: `<type folder>/<ResourceName>/<ResourceName>.json` plus side files (frame PNGs and masks, `.gml` per code entry or event, `.vsh`/`.fsh`, audio as stored, font textures). Type folder names follow GameMaker (`sprites`, `objects`, `rooms`, `scripts`, `sounds`, `fonts`, `paths`, `timelines`, `shaders`, `sequences`, `animcurves`, `particles`, `tilesets`, `extensions`, `notes`, `options`, `datafiles`) plus `code` and `strings` for data-file-only content.
   - Field names follow GameMaker's `.yy` names where the concept is the same, so modders who know GameMaker recognize them. Plain JSON without trailing commas.
2. Two modes, recorded in the project file: delta (only changed or added resources, applied over the base data file; the default for mods) and full (every resource; the folder plus the base data file rebuild the game).
3. Deterministic output: sorted enumeration, fixed property order, UTF-8 without BOM, LF, final newline, invariant culture. Re-exporting an unchanged project changes no file.
4. Binary content stays in side files, never base64 in JSON.
5. UTMT 0.9 compatibility: open a UTMT project (converted on open, original untouched) and export a project in UTMT 0.9 layout for the 13 types UTMT knows. Both go through the existing `ProjectContext` code.
6. GameMaker project interop: import resources from a GameMaker `.yyp`/`.yy` project into a data file where G3MLib can compile them (sprites, sounds, scripts and code, objects, rooms; others as G3MLib support grows), and export resources as `.yy` folders.
7. One serializer per resource type, shared by the project format, the modding export and patch payloads.
8. `formatVersion` in the project file; older versions migrate on load; never write older versions.

## Re-review against Q25 (G3MLib stays low level)

Owner decision (questions.md Q25): G3MLib holds single operations; G3MStudio holds workflows. File-format serializers are single operations and stay in G3MLib. Orchestration moves to G3MStudio. Built work is kept until G3MStudio takes it over.

| Part of this ADR | Kind | Owner after review |
| --- | --- | --- |
| Decision 7: one serializer per resource type (serialize and deserialize one resource to its JSON and side files) | file-format serializer | G3MLib (primitive) |
| Decision 3, per resource: fixed property order, UTF-8 without BOM, LF, invariant culture | file-format serializer | G3MLib (primitive) |
| Decision 4: binary content in side files | file-format serializer | G3MLib (primitive) |
| Decision 6, per resource: `.yy` converters for one resource | file-format serializer | G3MLib (primitive) |
| Decision 5, per resource: UTMT 0.9 `ProjectContext` read and write of one resource | file-format serializer | G3MLib (primitive) |
| Decision 1: the folder layout, `.g3mproj` writing and reading, virtual folders, resource lists | orchestration | G3MStudio (workflow); folder service on `l2/project-format` stays until taken over |
| Decision 2: delta and full modes, base data file identity, Build | orchestration | G3MStudio (workflow); same rule |
| Decision 3, across files: sorted enumeration, no change on re-export | orchestration | G3MStudio (workflow) |
| Decision 5, whole project: open UTMT project, export in UTMT layout | orchestration | G3MStudio (workflow) |
| Decision 6, whole project: import and export of a GameMaker project | orchestration | G3MStudio (workflow) |
| Decision 8: `formatVersion` and migrations of one resource file | file-format serializer | G3MLib (primitive) |
| Decision 8: project-level version and migration registry | orchestration | G3MStudio (workflow) |

The acceptance checks stay. They are run by G3MStudio once it owns the folder service, using the G3MLib serializers.

## Acceptance

- Full export of each corpus game and fixture, then rebuild: byte-identical data file.
- Export twice: no file changes.
- UTMT 0.9 sample projects open and rebuild; a UTMT export opens in UTMT 0.9 (manual check).
- Opening a 20,000-resource project to a usable explorer under 2 s on the reference machine.

## Rejected

- UTMT 0.9 layout as the native format: does not match the GameMaker structure the owner wants and cannot hold all types cleanly.
- One JSON file per game: unusable diffs and merges.
