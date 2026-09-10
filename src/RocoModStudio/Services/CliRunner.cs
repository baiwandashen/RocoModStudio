using Newtonsoft.Json;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace RocoModStudio.Services;

public static class CliRunner
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0) return 2;
        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "tojson":
                    if (args.Length < 4) throw new ArgumentException("tojson requires source, destination and engine version.");
                    ToJson(args[1], args[2], args[3], args.Length > 4 ? args[4] : null, stdout);
                    return 0;
                case "fromjson":
                    if (args.Length < 3) throw new ArgumentException("fromjson requires source JSON and destination asset.");
                    FromJson(args[1], args[2], args.Length > 3 ? args[3] : null, stdout);
                    return 0;
                case "scan":
                    if (args.Length < 2) throw new ArgumentException("scan requires an unpacked directory.");
                    var scan = new AssetScanService().Scan(args[1]);
                    stdout.WriteLine($"files={scan.TotalFiles} uasset={scan.UAssetCount} textures={scan.TextureCount} skeletal={scan.SkeletalMeshCount} animations={scan.AnimationCount} effects={scan.EffectCount} blueprints={scan.BlueprintCount} banks={scan.VoiceBankCount}");
                    stdout.WriteLine("pets=" + string.Join(",", scan.PetNames));
                    return 0;
                case "tools":
                    var settings = new SettingsService().Load();
                    foreach (var status in new ToolLocator().Detect(settings)) stdout.WriteLine($"{status.Name}\t{status.Available}\t{status.Detail}");
                    return 0;
                case "nrc-texture":
                    if (args.Length < 3) throw new ArgumentException("nrc-texture requires input and output directories.");
                    ConvertTextures(args[1], args[2], args.Length > 3 ? args[3] : "VER_UE4_26", stdout);
                    return 0;
                default:
                    stderr.WriteLine($"Unknown command: {args[0]}");
                    return 2;
            }
        }
        catch (Exception ex)
        {
            stderr.WriteLine(ex);
            return 1;
        }
    }

    private static void ToJson(string source, string destination, string engine, string? mappings, TextWriter stdout)
    {
        var asset = new UAsset(source, AssetEngineService.ParseEngineVersion(engine), LoadMappings(mappings));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        File.WriteAllText(destination, asset.SerializeJson(Formatting.Indented));
        stdout.WriteLine($"Exported {source} -> {destination}");
    }

    private static void FromJson(string source, string destination, string? mappings, TextWriter stdout)
    {
        using var stream = File.OpenRead(source);
        var asset = UAsset.DeserializeJson(stream) ?? throw new InvalidDataException("Unable to deserialize asset JSON.");
        asset.Mappings = LoadMappings(mappings);
        asset.FilePath = source;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
        asset.Write(destination);
        stdout.WriteLine($"Imported {source} -> {destination}");
    }

    private static void ConvertTextures(string inputDirectory, string outputDirectory, string engine, TextWriter stdout)
    {
        Directory.CreateDirectory(outputDirectory);
        foreach (var input in Directory.GetFiles(inputDirectory, "*.uasset", SearchOption.TopDirectoryOnly))
        {
            var asset = new UAsset(input, AssetEngineService.ParseEngineVersion(engine), null, CustomSerializationFlags.SkipParsingExports);
            var changed = false;
            foreach (var export in asset.Exports)
            {
                var className = export.GetExportClassType()?.Value?.Value ?? string.Empty;
                if (!className.Contains("Texture", StringComparison.OrdinalIgnoreCase) || export is not RawExport rawExport) continue;
                var original = rawExport.Data ?? Array.Empty<byte>();
                var transformed = new byte[original.Length + 16];
                Buffer.BlockCopy(original, 0, transformed, 16, original.Length);
                rawExport.Data = transformed;
                changed = true;
            }
            if (!changed) continue;
            var output = Path.Combine(outputDirectory, Path.GetFileName(input));
            asset.Write(output);
            var ubulk = Path.ChangeExtension(input, ".ubulk");
            if (File.Exists(ubulk)) File.Copy(ubulk, Path.ChangeExtension(output, ".ubulk"), true);
            stdout.WriteLine($"Converted {input} -> {output}");
        }
    }

    private static Usmap? LoadMappings(string? path) =>
        string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? null : new Usmap(path);
}


