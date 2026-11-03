# Reviewing the real window without computer use

Status: working method, 2026-11-02. Computer use cannot list G3MStudio (Q65), but the built app can still be driven and photographed from PowerShell, with no focus and no keystrokes.

1. Start the Release build with a data file: `G3MStudio.exe "<path>\data.win"`.
2. Make the script DPI aware (`SetProcessDPIAware`) before reading the window rectangle, or the capture is cut off at 125% scaling.
3. Click with `PostMessage` to the main window handle: `WM_MOUSEMOVE` (0x200), then `WM_LBUTTONDOWN` (0x201) and `WM_LBUTTONUP` (0x202), the position in client pixels (window pixels minus the client offset from `ClientToScreen`). Avalonia reacts without the window being in front.
4. Take the picture with `PrintWindow(hwnd, dc, 2)` into a `Bitmap` and read the PNG.

What does not work: keystrokes (`SendKeys`, `keybd_event`) need the window in front, and Windows refuses the foreground to a background tool, so shortcuts and typing cannot be tested this way. Menus and flyouts are separate windows and are missing from the picture (the menu title shows as pressed). The explorer restores its last layout, so row positions differ between runs: take a picture before clicking.

Found with it: shader tabs wrapped to two rows and the editor touched the property filter (fixed). Everything else photographed (sprite, tile set, shader, object tabs, the Game group with Tags, Feature Flags and Language) matched the headless renders.
