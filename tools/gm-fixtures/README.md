# GameMaker fixtures

`gmfixtures.py` builds one GameMaker project per feature variant with Igor (no IDE), then checks each data file with G3MLib through the G3MTool CLI: read and write with an empty script, compare bytes, and list which chunks changed compared with the base build.

Requirements: Python 3.12 or later (standard library only), an installed GameMaker with a runtime and a signed-in user folder, a Release build of `G3MTool/G3MToolCLI`.

```
python gmfixtures.py --base <base project folder> --work <scratch folder>
  --runtime <GameMaker cache>/runtimes/runtime-<version>
  --user <GameMaker roaming folder>/<user folder>
  --prefabs <GameMaker ProgramData folder>/Prefabs
  --g3mtool <G3MTool.exe> --out <results folder> [--only <variant> ...]
```

The base project must contain one asset of every type; the variants change `Object1`, `Room1`, `Sound1` and the texture groups. Add a variant by writing a function that edits the copied project and appending it to `VARIANTS`. Built data files stay in the scratch folder and are never committed.

`--command` selects the Igor windows worker command (default `Compile`; LTS 2022 needs `PackageZip`).
`--lts2022` runs the LTS 2022 set (the three `lts22_` 2.3-format layer variants, without the 2026-only variants); `--only` runs any variant by name.

Results of each run are summarized in `docs/research/gamemaker-fixtures.md`.
LTS 2022: create the base project with `lts2022/make_lts2022_base.py` (from a copy of the runtime's BaseProject) and run with `--command PackageZip`; that Igor has no Compile command and exits 1 after writing the data file.
