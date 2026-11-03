# 0005 Updater

Status: revised (owner, 2026-10-26, Q27, Q28, Q33). Velopack is not chosen.

G3MStudio ships only as six portable zips with the files in the zip root (Q27). There is no installer, no Update.exe stub, no AppImage swap and no .app installer flow, which is what Velopack is built around, so it adds nothing here. The updater is G3MStudio's own.

Flow:

1. Read the releases of the owner's GitHub repository. The highest of the latest release, the optional pre-release and the running version wins.
2. If it is higher, download the zip named `G3MStudio-<OS>-<ARCH>-<VERSION>.zip` for the detected OS and architecture, over HTTPS from the repository's releases only.
3. Compare the file with the SHA-256 digest GitHub publishes for that asset. On a mismatch delete it and refuse the update. There is no code signing and no signature scheme; the trust root is the owner's GitHub account.
4. "Restart to Update" saves everything. A small updater program, copied to a temporary folder so it can replace itself, waits for the app to exit, extracts all files of the zip into the folder of the previous executable, replaces them (keeping the old files so a failed replace can be undone) and starts the new executable.
5. If the program folder is not writable, nothing is replaced; the message names the downloaded zip so the user can extract it by hand.

All user data lives in the per-user app data folder, never in the program folder, so replacing the program folder loses nothing.

The spike (roadmap M0) must confirm on all six RIDs: the digest check, an interrupted download, an interrupted extraction, replacing the files of a running program folder, execute permissions after extraction on Linux and macOS, and a non-writable folder.

History: the first draft proposed Velopack 1.2.161 (MIT) with a G3M-style updater as fallback.
