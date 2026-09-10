# Changelog

## 0.4.1

- Fixed the `FileDialog.Filter` `ArgumentException` that crashed the application when opening file/save pickers.
- Normalized all picker filters into the format required by Windows WPF.
- Added guarded file/folder/save dialogs with visible error reporting.
- Added global UI/Task/AppDomain exception logging to `%LOCALAPPDATA%\RocoModStudio\Logs`.
- Moved tool detection off the UI thread to prevent startup and settings freezes.
- Added cancellable long-running operations.
- Added one-click diagnostics for tools, PAK directory, AES format, output permissions, disk space, and CLI responsiveness.
- Fixed background NRC logging so it no longer updates WPF controls from a worker thread.
## 0.4.0

- Integrated the compiled FModel 2b09c12f GUI build.
- Integrated the nrc/CUE4Parse 2b09c12f CLI for Roco Patch mounting and unpacking.
- Added one-click PAK directory + AES -> unpack -> scan -> Mod workspace workflow.
- Added automatic cooked asset classification and pet-directory detection.
- Added direct cooked override PAK generation.
- Added Blender model preparation/export bridge.
- Added Unreal 4.26 Python import, RunUAT cook, and pack-root write-back workflow.
- Added asset browser with search and type filters.
- Added GitHub-ready source layout, CI, release scripts, and tool preparation scripts.

## 0.3.0

- Added UAsset editor, texture studio, NRC texture conversion, pet BP swap, and repak integration.

