G3MStudio closed beta
=====================

G3MStudio is an editor for GameMaker data files (data.win and its relatives).
You can browse and change sprites, sounds, objects, rooms, code and more.

READ THIS FIRST
  This is an early beta. It can damage files.
  Work on a copy of your game and keep a backup of data.win.

SHARING
  Do not share these zips without permission. Permission is given easily:
  just ask.
  You are free to share what you make in G3MStudio and to show how G3MStudio
  looks and works.
  Do not publicly share bugs or crashes. Without the context that this is a
  closed beta, people may take this for the full release and think the
  product is broken. Report problems in the reports channel instead.

FIRST START
  Windows   SmartScreen may block the unsigned program. Click "More info",
            then "Run anyway".
  macOS     Right click G3MStudio.app and choose Open, then Open again.
            If macOS still refuses, run this in a terminal:
              xattr -dr com.apple.quarantine G3MStudio.app
  Linux     Make the file runnable:  chmod +x G3MStudio
            It needs libfontconfig and an X11 or Wayland session.

OPENING A FILE
  - File > Open
  - Drag a file onto the window
  - Right click a file in your file manager and choose "Open with"
  Supported files: .win  .ios  .droid  .unx  .symbian
  (these are the data files of the GameMaker targets)

WHERE YOUR DATA LIVES
  Settings, recent files, window layouts and logs are in the app data
  folder. Help > Open Data Folder shows it.

REPORTING A PROBLEM
  Use #🆘・g3ms-reports channel for that. Please include the version information:
  copy it from the Help menu.
  Say what you did, what you expected and what happened. If you can, add
  the log file from the data folder.

CRASH REPORTS
  After a crash the app can send an anonymous report. It contains no file
  paths, no user names and no game text. Choose whether to allow it on the
  first start page. You can switch it off any time in Settings.

KNOWN LIMITS
  - Beta software: some features are unfinished and some data may not
    round trip exactly.
  - The program is not signed or notarized, hence the first start steps.
  - Project > Open Project converts a UndertaleModTool 0.9 project once (or you
    drop its folder on the window). It needs the original data file. Project
    options (patches, external files, copies, scripts) are applied but not kept,
    and fonts and backgrounds of a UTMT project are converted without a test of
    their own yet.
  - Running a game on Linux and macOS (Wine, Proton, umu, PortProton or your
    own command, Settings > Run) is tested only with simulated setups, not
    yet on real machines. Tell us how it goes.

Thank you for testing.
