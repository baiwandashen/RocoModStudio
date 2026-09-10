# External tools

This directory is populated locally and is intentionally excluded from Git.
Release builds copy these folders into `Tools/` next to `RocoModStudio.exe`.

Required layout:

```text
tools/
  FModel/FModel.exe
  CUE4ParseCli/cue4parse-cli.exe (+ dependencies)
  Node/node.exe
  repak/repak.exe
  UE4-DDS-Tools/src/main.py
  PetBpSwap/scripts/*.mjs
```

Run `scripts/prepare-tools.ps1` to download the public tools. The custom FModel and
Roco/custom-encryption CUE4Parse CLI builds can be passed as local archives or URLs
to that script, or attached to a GitHub Release.
