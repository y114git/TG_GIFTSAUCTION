# Room render spike (M0)

Date: 2026-10-27. Status: measured on Windows x64 only.

Question (roadmap M0, OI-003): do one GMS2 and one GMS1 room, with tiles and instances, render and pan at 60 fps (frame budget 16.7 ms) in a custom Avalonia control?

## Design

- `G3MStudio/Rooms/RoomScene.cs`: turns a G3MLib `GameMakerRoom` into a flat draw list of items (texture page bitmap, source rectangle, destination rectangle), back to front. GMS2: layers by depth (higher depth first), tile layers from `TileData` with the tileset's tile size, output border and column count, instance layers. GMS1: `Tiles` and `GameObjects` merged and ordered by depth. Texture pages are converted once to bitmaps (G3MLib `GMImage` to raw BGRA).
- `G3MStudio/Rooms/RoomView.cs`: a `Control` that fills black, applies a translate for the pan offset, skips items outside the viewport and draws the rest with `DrawImage` (nearest-neighbor). Pointer drag pans; the offset is also settable for tests.
- Not drawn yet: room and layer backgrounds, views, asset and effect layers, tile mirror, flip and rotate bits, instance rotation, color and visibility flag (so Undertale's invisible collision objects show), sprite animation.

## Method

`G3MStudio.Tests/RoomBenchmarkTests.cs`, headless session with Skia and real drawing, window 1100 x 700. 240 pan steps (3 px right and 2 px down per step as a triangle sweep, so small rooms stay in view) after 10 warm-up frames, each followed by a forced render (`CaptureRenderedFrame`). The test fails when fewer than 80 percent of the frames differ (hash check). A frame includes layout, culling, drawing and CPU rasterisation of the window; a GPU window has less raster work. The corpus cases run when `G3MSTUDIO_CORPUS_DIR` names a folder with `data.win` files and pick the room with the most tiles plus instances of each kind (Layers present means GMS2); they skip otherwise. The synthetic case always runs. The pan starts at the room's top-left corner and sweeps a fixed path, so it is not the densest view of the room.

## Results (Windows x64, Release build)

| Room | Items | Build (data file read, page decode and convert, draw list) | First frame | Items drawn in last frame | Pan frame mean / p95 / max |
| --- | ---: | ---: | ---: | ---: | --- |
| Synthetic, 300 x 200 tiles of 16 px (4800 x 3200) | 60,000 | 214 ms | 3.5 ms | 3,080 | 12.7 / 16.1 / 23.2 ms |
| GMS2: Pizza Tower `exit_1` (19000 x 10540) | 186,144 | 1,698 ms | 2.7 ms | 809 | 10.4 / 14.2 / 18.1 ms |
| GMS1: Undertale `room_water_undynebridge` (1940 x 1240) | 2,157 (2 skipped) | 1,671 ms | 2.7 ms | 1,138 | 7.2 / 8.8 / 16.8 ms |

Verdict: mean and p95 stay under 16.7 ms for both real rooms; the worst frame of the large GMS2 room is 18.1 ms, 1.4 ms over. The synthetic case draws 3,000 items per frame and sits at the budget edge on CPU raster. Culling by a linear scan over all items is not a cost yet (186,144 items). A GPU window and a spatial index (grid by tile) are the first levers if a real window misses the budget. Build time includes the data file read; read, page decode and draw list were not timed apart.

Pixel check: saved frames of both real rooms were opened and look right (Pizza Tower tiles and objects in place; Undertale tiles plus collision instances in the correct grid).

## Not measured

Linux, macOS; a GPU-backed window; densest viewport of a room; zoom (only pan); sprite animation; rotated and tinted items; effect layers; memory use of the converted pages (Pizza Tower pages are held as BGRA bitmaps).
