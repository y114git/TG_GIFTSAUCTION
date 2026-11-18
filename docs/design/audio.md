# Audio playback

Studio plays sounds in the sound view and in the audio editor. This page covers the backends, where the native library comes from and how to replace it.

## Backends

Settings > Sound > Sound backend picks one. The choice is `AudioOptions.Backend`.

| Id | Name | What it does |
| --- | --- | --- |
| `openal` | OpenAL Soft (in the program) | Plays inside the process through `IAudioOutput` (`OpenAlOutput`). Default. Has the output device list, the start volume and the buffer size. |
| `system` | System player | Hands the extracted sound file to the player of the operating system. Nothing plays inside Studio, so seeking, loops and the cursor are the player's. |
| `off` | Off (no sound) | Nothing plays, and the system player is not used either. |

`AudioDevices.Backends` is the registry: each entry has an id, a label key, an open function that returns an `IAudioOutput` (or null when it cannot play), a device list and an availability check. Another backend is one more entry plus a label in the language files. An id in the settings that is not registered (a setting from a newer build) is read as `openal`.

## Fallback

If the OpenAL library cannot load, or no output device opens, `AudioDevices.Acquire` returns null and the player falls back to the system player. When the cause is the missing library, Studio posts one warning per window session that names the file to supply, and the Sound page shows the same line. Nothing throws: load errors (missing file, wrong architecture, missing symbols) are caught in `OpenAlOutput`. Setting the environment variable `G3MSTUDIO_NO_AUDIO` switches the OpenAL backend off without a message.

Tests never open a device: `AudioDevices.Open`, `List` and `LibraryPresent` are replaced, and `NullAudioOutput` stands in for a device.

## Where the library comes from

No native binary is tracked in git. `G3MStudio.csproj` references the NuGet package `Silk.NET.OpenAL.Soft.Native`, which carries OpenAL Soft for each platform. A release build runs `dotnet publish --runtime <rid>`, so each release zip holds only the library of its own runtime id. In the single-file build the library is bundled and extracted at start.

| System | File name |
| --- | --- |
| Windows | `soft_oal.dll` |
| Linux | `libopenal.so` |
| macOS | `libopenal.dylib` |

A source build with a plain `dotnet build` or `dotnet run` gets the library from the package on the platforms the package covers. A platform the package does not cover has no library, and Studio uses the system player until one is supplied.

## Supplying or replacing the library

Put a file with the name from the table in the folder that holds the program (next to the executable). A library found there is used instead of the bundled one. Any OpenAL Soft build that matches the architecture of the program works, for example one built from the OpenAL Soft sources or the package of the system.

## License

OpenAL Soft is LGPL-2.0-or-later. Studio links it dynamically as a separate native library and does not modify it, so the user can replace it with another build of the same library as described above. The source of OpenAL Soft is at https://github.com/kcat/openal-soft. The notice is in THIRD-PARTY-NOTICES.txt.
