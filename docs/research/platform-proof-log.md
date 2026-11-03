# Platform proof log

G3MStudio is fully supported on every supported OS and architecture (requirements N-PLT-2, questions.md Q26). A feature may be blocked on one platform only after extensive CI tests prove it impossible there, with a visible warning in the UI, and only after a partial implementation was attempted. Every block needs an entry here. No entry, no block.

Rules:

1. Evidence is CI evidence: workflow run references for all six targets (win, linux, osx; x64, arm64), not a manual test on one machine.
2. At least two different approaches are tried (another API, another library, a user-driven fallback) before "impossible".
3. A partial alternative is built and tested first. The entry says what it does and what it lacks.
4. The UI shows a warning where the feature lives. The entry records the warning text.
5. Entries are reviewed at every release. When the platform or a library changes, the feature is retried and the entry is closed.

## Entry template

Copy this block for each feature.

```
### <Feature name> on <platform and architecture>

| Field | Value |
| --- | --- |
| Feature | What the user cannot do, and the requirement or editor spec it belongs to |
| Platform | OS and architecture (for example linux-arm64) |
| What was tried | Each approach, library and version, with the reason it failed |
| CI run references | Workflow run ids or links for every attempt, on all six targets |
| Result | Impossible, with the failing evidence in one or two lines |
| Partial alternative | What ships instead, and what it lacks |
| UI warning | Exact text and where it appears |
| Opened | Date |
| Last retried | Date and G3MStudio version |
| Status | open, retried, closed |
```

## Entries

None yet. G3MStudio has no code, so no feature is blocked.
