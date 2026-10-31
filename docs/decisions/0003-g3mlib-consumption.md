# 0003 Consuming G3MLib

Status: accepted (owner, 2026-10-25). Replaces the earlier proposal of a sibling project reference.

G3MStudio references G3MLib only as a NuGet package. No project reference, no copied source.

All library work G3MStudio needs is done first, in G3MLib, and released as G3MLib 1.1.0 after the owner approves the release. 1.1.0 is meant to be final for G3MStudio 1.0.0-beta: the work list comes from the full UTMT parity matrix and every planned editor, and it ships with updated benchmarks and README results.

Before the library work starts, GameMaker's formats, tools and runtime behaviour are investigated in depth (roadmap phase L), so G3MLib covers current GameMaker versions completely.

Consequence: a library gap found later means a G3MLib 1.1.x release, not a workaround in G3MStudio.
