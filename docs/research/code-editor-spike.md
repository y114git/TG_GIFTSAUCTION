# Code editor spike (M0)

Date: 2026-10-27. Status: measured on Windows x64 only.

Question: does AvaloniaEdit with G3MLib tokens open and scroll the largest code entry of the test games at 60 fps (frame budget 16.7 ms)?

## Versions

| Package | Version | Compatibility |
| --- | --- | --- |
| Avalonia.AvaloniaEdit | 12.0.0 (MIT) | NuGet dependency Avalonia >= 12.0.0; the app uses 12.1.4 |
| G3MLib | 1.1.0 | `GmlTokenizer.Tokenize(text, gms2OrLater)` returns kinds and source spans, never fails |

## Method

`G3MStudio.Tests/EditorBenchmarkTests.cs`, headless session with Skia and real drawing (`UseHeadlessDrawing = false`), window 1100 x 700, read-only `TextEditor` with line numbers and `GmlColorizer` (all tokens computed once, binary search per visible line). Scroll: 240 steps of 3 lines through the editor's scroll viewer, each followed by a forced render (`CaptureRenderedFrame`), after 10 warm-up frames. Every step must change the picture (checked by hashing the frames), otherwise the test fails. A frame therefore includes layout, text shaping, coloring and CPU rasterisation of the whole window; a GPU window has less raster work. The corpus case runs when `G3MSTUDIO_CORPUS_DIR` names a folder with `data.win` files and picks the code entry with the most instructions across them; otherwise only the generated case runs.

## Results (Windows x64, debug-free Release build)

| Text | Size | Tokens | Tokenize | Open (tokens, document, first frame) | Scroll frame mean / p95 / max | Same without highlighting |
| --- | --- | --- | --- | --- | --- | --- |
| Largest corpus entry: FRICKBEARS3 `gml_GlobalScript_VoiceLineSubtitles`, 42,025 instructions, decompiled | 350,573 chars, 4,451 lines | 44,043 | 15.5 ms | 27 ms | 6.2 / 7.4 / 9.0 ms | open 42 ms; frame 7.5 / 12.2 / 17.2 ms |
| Generated GML | 1,000,066 chars, 14,387 lines | 233,100 | 43 ms | 63 ms | 6.4 / 7.8 / 9.7 ms | open 62 ms; frame 6.9 / 11.1 / 14.2 ms |

Verdict: both cases stay under the 16.7 ms budget (60 fps) at p95 and max on this machine. Highlighting costs nothing measurable in the frame. Open time excludes reading the data file and decompiling the entry.

## Not measured

Linux, macOS, Wayland; a GPU-backed window; typing latency and re-tokenizing after an edit (the colorizer has none yet); word wrap; folding; the 100/125/150/200% scale cases; a 5 MB text.
