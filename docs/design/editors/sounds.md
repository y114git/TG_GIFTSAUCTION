# Sounds and audio groups

Status: proposed. Editors for SOND resources, embedded audio (AUDO), audio groups (AGRP) and the external files they use: `audiogroupN.dat` files and streamed external audio. Includes the Sound Mixer for batch volume. Shared rules: [../ux-standards.md](../ux-standards.md).

## 1. Purpose and entry points

Play, replace, re-encode and organize game audio, and see what a change costs in file size. Opens from Explorer > Sounds, Explorer > Data > Embedded Audio, Explorer > Data > Audio Groups, Go To `snd:`, the Sound Mixer, References results and the room or sequence that uses a sound.

## 2. Sound editor layout

```
+--------------------------------------------------------------------------------------+
| snd_bgm_intro   Index 41     Embedded, OGG, 44100 Hz stereo    [Replace...] [Export...]|
+---------------------------------------+----------------------------------------------+
| Properties                            |  waveform (L and R)                           |
|  Name [snd_bgm_intro]                 |  |||||||||||||||||||||||||||||||||||||||||||| |
|  File [intro.ogg] (external only)     |  0:00.0 ----o---------------------- 0:42.3    |
|  Type [.ogg]                          |  [ < ] [ > ] [ loop ] [ rewind ] Volume [==o=]|
|  Volume [0.8]  Pitch [1.0]            +----------------------------------------------+
|  Storage [Compressed, not streamed v] |  Sizes: original 1.2 MB   export 1.1 MB  91% |
|  Audio group [audiogroup_default v]   |  [Open in Sound Mixer]                        |
|  Preload [x]                          |                                               |
|  v Flags (flags box)                  |                                               |
|  v Effects (flags box)                |                                               |
+---------------------------------------+----------------------------------------------+
```

Left: properties (300 px). Right: waveform with a playhead (click to seek), transport, preview volume, and size figures. The waveform has an accessible alternative: a text summary "Duration 42.3 s, peak -3 dB, 2 channels" and a seek slider.

## 3. Fields

### 3.1 Sound properties (UTMT-SND-01)

| Field | Control | Rules |
| --- | --- | --- |
| Name | Name field | |
| Flags | Flags box: Embedded, Compressed, Streamed (not embedded), Decompress on load, Regular | The storage combo below is the easy way to set them; the box exposes the raw flags |
| Storage | Combo with four entries: Uncompressed, Not Streamed; Compressed, Not Streamed; Uncompress on Load, Not Streamed; Compressed, Streamed | Setting it changes the flags and, if needed, moves audio between the data file and an external file. A change that moves data shows "This moves the audio {out of / into} the data file. Continue?" |
| Type | Read-only text (".ogg", ".wav") | From the audio header |
| File | Text, for external sounds | The external file name; edits check that the file exists beside the data file: "'{file}' was not found next to the data file." |
| Effects | Flags box | Old audio-system effects, GMS1 files |
| Volume | Slider 0 to 1 with number (float, 2 decimals) | Asset-level gain |
| Pitch | Number, float > 0 | |
| Audio group | Combo of audio groups | Disabled for streamed sounds ("Streamed sounds cannot be in a group.") |
| Audio file reference | Reference field (Embedded Audio) with Clear | UTMT-SND-03 behaviour below |
| Preload | Checkbox | Old audio system |
| Sample rate, bit depth, channels, quality | Read-only facts from the audio header; shown as "Sample rate 44100 Hz, 16 bit, stereo" | Used as defaults for Re-encode |
| Export directory | Text (relative path) | Only for files that store it |

### 3.2 Audio file reference (UTMT-SND-03)

A Reference field with extras: it shows the embedded audio entry index and its group ("AUDO 41 in audiogroup_default"). "..." opens the entry. When the entry lives in an external audio group file, "..." opens it in a linked file tab (see 6), not a second program. "X" removes the reference after a confirm "This sound will have no audio. Remove?" (undoable). Dropping an Embedded Audio resource onto the field relinks it.

## 4. Playback (UTMT-SND-02)

Play and Stop for WAV and OGG, from the embedded audio and from external files including entries in external audio groups. Space plays or pauses, Home rewinds, L toggles loop. Output device and master volume come from Settings > Audio; the preview volume slider here is not saved. Playback starts only on user action and stops when the tab closes or another sound plays. Waveform renders from decoded samples on a background job; large files show a progress overlay.

Errors: "This audio cannot be played. G3MStudio could not decode the {format} data. The file is kept unchanged. Export it and check it in an audio program." Failure is also written to Problems.

## 5. Replace, export, re-encode

| Command | Behaviour |
| --- | --- |
| Replace Audio File... | WAV, OGG or MP3 (MP3 is converted to OGG). A dialog shows source facts, target storage and "Sizes: original 1.2 MB, new 3.4 MB, +2.2 MB". OGG sources have "Keep as is" (default) or "Re-encode with these settings". After Replace: undo available |
| Export Audio File... | Writes WAV or OGG by the data header (UTMT-SND-04) |
| Re-encode... | Sample rate (default from the source), channels (Mono, Stereo), compression quality (0 to 10, compressed only); shows estimated size; runs as a job with Cancel |
| Edit in External Program | Exports to a temp file, opens the program set in Settings > Paths, watches the file and offers "Reimport the edited file" |

Size figures (original size, export size, ratio) appear in the right pane with a copy button and byte counts in tooltips.

## 6. Audio groups

### 6.1 Audio group editor (UTMT-SND-05)

Explorer > Data > Audio Groups. Fields: Name (Name field) and Path (text, relative folder or file; GM 2024.14 and later; hidden otherwise). Member list of sounds with Add Member..., Remove from Group, and "Move to Group..." for multi-select. `audiogroup_default` has no path and cannot be deleted. Moving a sound between groups moves its audio between the main file and an `audiogroupN.dat` file; the dialog states the size change and which files will be written ("Writes data.win and audiogroup2.dat").

### 6.2 External audio group files (UTMT-SND-06, UTMT-FILE-11)

Opening `audiogroupN.dat` directly opens it as a data file (audio only; every other editor tab shows "This file has no {type}"). Opening an embedded audio entry that lives in an external group from the main data file opens the file as a linked file of the same workspace, in the Explorer under "Linked Files". It is read once, edits are saved with the main save (each changed linked file is written with its own backup), and the open tab focuses the entry. This replaces UTMT's second process and named pipe. Closing the main workspace closes linked files; a linked file cannot be saved alone while its main file is dirty.

### 6.3 Streamed external audio

Streamed sounds point at `.ogg` files next to the data file (UTMT-PROJ-13 for projects). The sound editor shows the file name, whether it exists, its size, and "Show in File Manager". Replace Audio File writes the new file with a unique name next to the data file and updates the reference; the list of external files appears in Explorer > Included Files. Missing files raise a Problems entry with "Locate File..." and "Remove Reference". Copying a project's streamed sounds to the save directory (UTMT-PROJ-13) is part of "Build Data File From Project" (projects.md).

## 7. Embedded audio editor (UTMT-SND-04)

For AUDO entries: size, format, Import... (replace from WAV or OGG by header), Export..., Play, Stop, "Used by" list (sounds referencing it). Unreferenced entries show a warning "No sound uses this audio."

## 8. Sound Mixer

Tools > Sound Mixer. Columns of sounds side by side, GameMaker style: each column has Play All, Loop, Rewind, column volume; a drop-down to load an audio group; a list of sounds with play, loop and volume per sound; a link button joins neighbouring columns; the + button adds a column. Opening from the Explorer with several sounds selected loads them into one column. Volume sliders edit the asset volume live and write on release as one undo step per column drag ("Set volume of {n} sounds"). Pitch is read-only. Keyboard: Tab through sliders, arrows step 0.05.

## 9. Actions and shortcuts

| Command | Id | Windows and Linux | macOS |
| --- | --- | --- | --- |
| Play or Pause | `sounds.play` | Space | Space |
| Rewind | `sounds.rewind` | Home | Home |
| Loop | `sounds.loop` | L | L |
| Replace Audio File... | `sounds.replaceFile` | None | None |
| Export Audio File... | `sounds.exportFile` | None | None |
| Re-encode... | `sounds.reencode` | None | None |
| Open in Sound Mixer | `sounds.openMixer` | None | None |
| Edit in External Program | `sounds.openExternal` | None | None |
| Remove Audio Reference | `sounds.removeAudio` | None | None |

## 10. Validation and messages

| Case | Message |
| --- | --- |
| Streamed sound in a group | "Streamed sounds cannot be in an audio group." |
| Unsupported audio | "'{file}' is not WAV, OGG or MP3." |
| Audio group file missing | "audiogroup{n}.dat was not found next to the data file. Sounds in this group cannot be played." with "Locate File..." |
| Volume out of range | "Volume must be between 0 and 1." |
| Delete a sound referenced by code or rooms | Reference scan dialog |
| Sample rate mismatch on replace | Info "The new file is {new} Hz; the old one was {old} Hz. Keep the new rate?" |

## 11. Undo

Property commits, replace, re-encode, group moves and mixer drags are single steps. Replace and re-encode keep the previous audio in the undo record so Undo restores the exact bytes (memory limit: records over 256 MB spill to the recovery folder).

## 12. Empty and loading states

- No sounds: "This game has no sounds."
- Sound without audio: the waveform shows "No audio" and Replace Audio File is highlighted.
- Decoding: the waveform draws progressively with "Decoding..." text; playback is available when the first block is ready.

## 13. Parity coverage

- UTMT-SND-01: section 3.1.
- UTMT-SND-02: section 4.
- UTMT-SND-03: section 3.2.
- UTMT-SND-04: section 7.
- UTMT-SND-05: section 6.1.
- UTMT-SND-06, UTMT-FILE-11: section 6.2.
- UTMT-FMT-07: sections 5 and 6.3 (audio formats).
- UTMT-PROJ-13: section 6.3.
