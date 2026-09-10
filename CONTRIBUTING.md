# Contributing

1. Fork the repository and create a feature branch.
2. Keep asset-format changes isolated and document the exact UE/version assumptions.
3. Never commit game PAKs, extracted game assets, AES keys, or proprietary cooked data.
4. Run:

```powershell
dotnet restore src/RocoModStudio/RocoModStudio.csproj
dotnet build src/RocoModStudio/RocoModStudio.csproj -c Release
```

5. For a portable release, populate `tools/` using `scripts/prepare-tools.ps1`, then run `scripts/build-release.ps1`.
6. Describe runtime tests for any Blueprint, Skeleton, Animation, Material, or Wwise change.
