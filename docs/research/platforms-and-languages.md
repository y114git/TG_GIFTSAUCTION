# Platforms, languages and updates baseline for G3MStudio

Sources: G3M 3.4.3, G3MTool 1.0.0, G3MLib 1.0.1, G3M-MCE 1.0.0. Paths are relative to `G3MSpace/`.

## 1. Languages

### G3M

Seven UI languages, all at lang file version `3.4.3` with the same 1,619 keys:

| Code | `language_name` | `language` | Font file | Qt translation |
| --- | --- | --- | --- | --- |
| `en` | English | english | `NotoSans-Bold.ttf` | `qtbase_en` |
| `ru` | Русский | russian | `NotoSans-Bold.ttf` | `qtbase_ru` |
| `es` | Español | spanish | `NotoSans-Bold.ttf` | `qtbase_es` |
| `ja` | 日本語 | japanese | `NotoSansJP-Bold.ttf` | `qtbase_ja` |
| `ko` | 한국어 | korean | `NotoSansKR-Bold.ttf` | `qtbase_ko` |
| `zh_cn` | 简体中文 | chinese_simplified | `NotoSansSC-Bold.ttf` | `qtbase_zh_cn` |
| `zh_tw` | 繁體中文 | chinese_traditional | `NotoSans-Bold.ttf` | `qtbase_zh_tw` |

`zh_tw` points at the Latin `NotoSans-Bold.ttf`, which has no CJK glyphs, so Traditional Chinese text falls back to an OS font. No Noto Sans TC file ships.

Files and format:

- Bundled files: `G3M/src/assets/lang/lang_<code>.json` plus the four font files in the same folder.
- File format: nested JSON objects. A top-level `metadata` object holds `language`, `language_name`, `font`, `qt_translation`, `version`. Top-level groups: `onboarding`, `blocklist`, `common`, `actions`, `launch_modes`, `ui`, `files`, `chapters`, `tabs`, `checkboxes`, `tags`, `buttons`, `tooltips`, `dropdowns`, `dialogs`, `errors`, `startup`, `status`, `defaults` and more. Placeholders use Python `str.format` syntax (`{error}`, `{count}`); escapes `\n`, `\t`, `\"` are processed.
- Plugins ship their own `lang/lang_<code>.json` (for example `G3M/catalog/plugins/custom_saves_folders/lang/`).

Lookup (`G3M/src/services/localization_service.py`):

- `tr(key, **kwargs)` calls `LocalizationManager.get_text()`. Keys are dotted paths (`dialogs.theme_save_title`).
- At each path segment, `k` is tried first, then `_k`. An underscore-prefixed key is a user override that bundled updates never touch.
- Order: plugin strings for keys starting with `plugins.<id>.` (current language, then that plugin's `en`), then the current language file, then the English file preloaded as fallback, then the literal `[key]`.
- Startup: saved `language` setting if available; else `detect_system_language()` (locale `ll_CC` normalized to `ll_cc`, tried as full code, then base code), else `en`.
- External packs: bundled files are copied to the user lang dir (`get_user_lang_dir()`) and merged on each start. Bundled values overwrite matching keys, keys missing from the bundle are removed, `_`-prefixed keys and `metadata` stay. Any `lang_<code>.json` dropped there becomes a new language; it can name its own font file next to it.
- The language's font is registered with `QFontDatabase` and becomes the UI font. A Qt base translator (`qtbase_<code>`) is installed for Qt's own dialogs.

### G3M-MCE

`G3M-MCE/src/i18n.js` with i18next 26 and react-i18next 17. The same seven codes (`en`, `ru`, `zh_cn`, `zh_tw`, `es`, `ja`, `ko`) as flat-nested JSON files in `G3M-MCE/src/locales/<code>.json` (142 keys in English), bundled statically. Selected language persists in `localStorage` key `language`; unknown or missing value falls back to `en`; `fallbackLng: 'en'`; `escapeValue: false`; the selector sets `data-lang` on `body`. Only `NotoSans-Bold.ttf` is loaded, so CJK uses system fonts. Interpolation uses `{{name}}` (i18next default), not G3M's `{name}`.

### G3MTool

No localization. G3MTool GUI strings are hardcoded English in `G3MTool/G3MToolGUI/MainWindow.axaml` and `.axaml.cs`; the CLI prints English. G3MLib messages are English.

### Terminology in G3M language files

Quoted strings from `G3M/src/assets/lang/lang_<code>.json`. The key used is in the first column.

| Concept (key) | en | ru | es | ja | ko | zh_cn | zh_tw |
| --- | --- | --- | --- | --- | --- | --- | --- |
| mod (`shortcut.dialog_mod`) | Mod | Мод | Modificación | モジュール | 모드 | 模组 | 模組 |
| data file (`dialogs.data_file`) | DATA File | DATA Файл | DATA Archivo | DATAファイル | DATA 파일 | DATA 文件 | DATA 檔案 |
| patch (`modding_tools.tab_patch`) | Patch | Патч | Parche | パッチ | 패치 | 补丁 | 補丁 |
| game (`ui.settings_tab_game`) | Game | Игра | Juego | ゲーム | 게임 | 游戏 | 遊戲 |
| settings (`ui.settings_title`) | Settings | Настройки | Configuración | 設定 | 설정 | 设置 | 設置 |
| save (`ui.manual_install_save`) | Save | Сохранить | Guardar | 保存 | 저장 | 保存 | 儲存 |
| open (`community_feed.open`) | Open | Открыть | Abrir | 開く | 열기 | 打开 | 開啟 |
| export (`actions.export`) | Export | Экспорт | Exportar | エクスポート | 내보내기 | 导出 | 匯出 |
| import (`buttons.import`) | Import | Импорт | Importar | インポート | 불러오기 | 导入 | 匯入 |
| resource (`diagnostics.tab_actual_resources`) | Actual Resources | Фактические ресурсы | Recursos reales | 実際のリソース | 실제 리소스 | 实际资源 | 實際資源 |
| search (`ui.clear_search_tooltip`) | Clear search: '{text}' | Сбросить поиск: '{text}' | Limpiar búsqueda: '{text}' | 検索をクリア: '{text}' | 검색 초기화: '{text}' | 清除搜索：'{text}' | 清除搜尋：'{text}' |
| update (`ui.update_button`) | Update | Обновить | Actualizar | 更新 | 업데이트 | 更新 | 更新 |
| script (closest: `checkboxes.merge_code`) | Code merge | Слияние кода | Fusión de código | コードのマージ | 코드 병합 | 代码合并 | 程式碼合併 |
| sprite | not present | | | | | | |
| room | not present | | | | | | |

Notes for G3MStudio glossaries:

- "sprite", "room" and "script" have no strings in G3M or G3M-MCE; G3MStudio defines them first. MCE mentions "CSX scripts" only inside help text.
- Japanese uses モジュール ("module") for mod, while Korean and Chinese use the common "mod" loanwords. Decide whether G3MStudio keeps モジュール for consistency.
- Traditional Chinese uses 設置 for Settings (G3M) and 修補程式 for patch in G3M-MCE (`ui.mod_editor_type_patch`), but 補丁 in G3M. The two apps already disagree.
- "DATA" stays uppercase and untranslated in every language.

## 2. Platforms and architectures

### Matrix

| OS | Arch | G3M | G3MTool GUI | G3MTool CLI | G3MLib | G3M-MCE |
| --- | --- | --- | --- | --- | --- | --- |
| Windows | x64 | Yes: portable zip and Inno Setup installer | Yes | Yes | Yes (tested) | Browser |
| Windows | arm64 | Yes: portable zip and installer | Yes | Yes | Yes (tested) | Browser |
| Linux | x64 | Yes: zip with one binary | Yes | Yes | Yes (tested) | Browser |
| Linux | arm64 | Yes | Yes | Yes | Yes (tested) | Browser |
| macOS | x64 | Yes: `G3M.app` zip | Yes: `G3MToolGUI.app` zip | Yes | Yes (tested) | Browser |
| macOS | arm64 | Yes | Yes | Yes | Yes (tested) | Browser |

No 32-bit x86 builds exist for any app.

### G3M

- Runtime: Python `>=3.14` (`G3M/pyproject.toml`, CI `PYTHON_VERSION: "3.14"`), PyQt6, packaged by PyInstaller (`G3M/builds/G3MExecutable.spec`, `console=False`, UPX on).
- CI: `G3M/.github/workflows/build-g3m.yml` (manual dispatch) and `test-g3m.yml`. Runners: `ubuntu-24.04`, `ubuntu-24.04-arm`, `macos-15-intel`, `macos-15`, `windows-latest`, `windows-11-arm`.
- Each build compiles the G3MTool CLI from G3MTool `main` for the matching RID (`linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`, `win-x64`, `win-arm64`) into `src/assets/bin/g3mtool_<os>`. Windows also bundles `UnRAR.exe`.
- Release assets (all `.zip`, version suffix): `G3M-Linux-x86_64-<v>`, `G3M-Linux-arm64-<v>`, `G3M-macOS-x64-<v>`, `G3M-macOS-arm64-<v>`, `G3M-Windows-x86_64-<v>`, `G3M-Windows-x86_64-Setup-<v>`, `G3M-Windows-arm64-<v>`, `G3M-Windows-arm64-Setup-<v>`. macOS uses `x64` in the file name while CI artifacts and update keys use `x86_64`.
- Windows installer: `G3M/builds/G3MWindowsInstaller.iss`, `MinVersion=0,10.0.17763` (Windows 10 1809), per-architecture `ArchitecturesAllowed` (`x64compatible` or `arm64`), installs to `{autopf}\G3M`.
- macOS: ad-hoc signed (`codesign --force --deep --sign -`), zipped with `ditto`, bundle id `com.y114.g3m`, URL schemes `g3m` and `deltahub`. Not notarized. No minimum macOS is declared in the spec.
- Linux: one executable in a zip. No AppImage, Flatpak or package. PortProton and Wine paths are supported for launching Windows games.
- README states Windows, Linux and macOS on x86_64 and ARM64.

### G3MTool

- .NET 10 (`net10.0`, C# 14), Avalonia 12.1.4 with Fluent theme, Magick.NET-Q8-AnyCPU 14.17.2, Roslyn scripting 5.9.0.
- RIDs (`G3MTool/README.md`, `G3MTool/.github/workflows/build.yml`): `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`. Runners: `windows-latest`, `windows-11-arm`, `ubuntu-latest`, `ubuntu-24.04-arm`, `macos-26-intel`, `macos-26`.
- Publish: self-contained single file, compressed, native libraries self-extracted, no PDBs (`G3MToolGUI.csproj`). An `xdelta` binary per RID is embedded as a resource.
- Assets: `G3MTool-CLI-<Suffix>-<v>.zip` and `G3MTool-GUI-<Suffix>-<v>.zip`, with Suffix `Windows-x64`, `Windows-arm64`, `Linux-x64`, `Linux-arm64`, `macOS-x64`, `macOS-arm64`. Release tag defaults to the UTC date `YYYY.MM.DD`. Archives must contain `LICENSE`, `THIRD_PARTY_NOTICES.md`, `SECURITY.md` and four license texts.
- macOS GUI: CI builds `G3MToolGUI.app` with a generated `Info.plist` and `G3MTool_icon.icns`. The workflow has no `codesign` step.

### G3MLib

`net10.0`, NuGet package `G3MLib` 1.0.1, AnyCPU managed library. Depends on Magick.NET-Q8-AnyCPU (native ImageMagick per platform), SharpZipLib, Roslyn scripting, Fody. Tested on the same six runner pairs as G3MTool (`G3MLib/.github/workflows/test.yml`).

### G3M-MCE

Static React 19 site built with Vite 8 on Node 22, deployed to GitHub Pages by `G3M-MCE/.github/workflows/deploy.yml`. End-to-end tests run in Chromium only. Any OS with a current browser.

### Minimum OS for a .NET 10 app (G3MStudio)

From Microsoft Learn (install pages for .NET 10): Windows 10 (1607 LTSC, 1809, 21H2) and Windows 11 on x64 and Arm64; macOS 14 Sonoma and later; Linux per distribution list. G3M's Windows minimum (10.0.17763) fits inside that range. G3M's macOS builds on `macos-15` runners; G3MTool builds on `macos-26`, so a macOS 14 floor for G3MStudio is not tested by any current CI runner.

## 3. Update mechanisms

### G3M application updates

Code: `G3M/src/services/updatecheck_service.py` (`UpdateChecker`), wiki `G3MWiki/G3M/features/update-system.md`.

Check:

1. At startup G3M calls the cloud function `{CLOUD_FUNCTIONS_BASE_URL}/getGlobalSettings` (5 s timeout, `bootstrap/bootstrap_coordinator.py:59`) and stores the JSON as `app_state.global_settings`. Offline start skips it.
2. `get_update_info()` reads `launcher_files`, or `launcher_beta_files` when **Beta Updates** (`beta_updates_enabled`) is on. Fields: `version`, `urls`, `message`, `message_ru`, `message_en`.
3. Remote `version` is compared with `APP_VERSION` via `version_sort_key`. Not newer: status "up to date".
4. Platform key `<windows|linux|macos>-<x86_64|arm64>` picks the URL. Missing x86_64 key falls back to the legacy key without architecture. No URL for the platform: a warning; another architecture is never substituted.
5. macOS running from App Translocation (`AppTranslocation` in the executable path) blocks the update with an error.
6. The user confirms in a dialog that shows the release message.

Download: streamed with `requests` (60 s timeout, 8 KiB chunks, progress from `content-length`) into a temp dir. No checksum or signature check exists for the application package.

Install per platform:

| Platform | Steps | Rollback |
| --- | --- | --- |
| Windows | Extract the archive, take the first `.exe` found, write a `.cmd` helper to `%TEMP%`, start it elevated (`ShellExecuteW` with `runas`). The helper waits for the G3M PID to exit, runs the installer with `/wait`, deletes the update dir and itself. G3M quits. | None from G3M. Inno Setup handles its own failure. The feed URL must point at the Setup zip; a portable zip would only launch its `G3M.exe`. |
| Linux | Extract, take the first executable file (else the largest extensionless or `.AppImage` file), copy to a persistent temp dir, chmod 700. A bash helper waits for the PID, renames the current binary to `<path>.old`, moves the new one in, chmod, starts it. | If the move fails or the new binary fails to launch, the helper restores `.old`. Backup and temp files are deleted 10 s after a good start. Log: `/tmp/g3m_update.log`. |
| macOS | Extract with `ditto`, find the `.app`, fix the Python symlink, copy the bundle to a temp dir. Same bash helper on the enclosing `.app`, also removes `com.apple.quarantine`, starts it with `open`. | Same `.old` rename and restore. |

Linux and macOS require write access to the folder that holds the binary or bundle; otherwise a permission error is shown and nothing changes. User data is stored separately and is not touched.

### G3M mod updates

GameBanana-backed mods can be checked and updated (Settings, Library, Update Mods: scope selected game, active profile or all profiles; optional automatic updates; optional direct replacement without a saved fallback version). Separate from application updates.

### G3MTool

No update mechanism in the GUI or CLI; the GUI shows only its version (`G3MTool (GUI) - <version>`). G3MTool reaches G3M users only as the CLI bundled in each G3M release.

### G3M-MCE

Updates by redeploy to GitHub Pages on every push to `main`; the browser loads the new build.

### Implications for G3MStudio

- Reuse the G3M platform key scheme (`<os>-<x86_64|arm64>`) and asset naming, and fix the macOS `x64` versus `x86_64` mismatch.
- A self-contained .NET single-file app can follow the Linux and macOS rename-and-restore helper pattern on all three OSes. Add a SHA-256 check of the downloaded package, which G3M lacks.
