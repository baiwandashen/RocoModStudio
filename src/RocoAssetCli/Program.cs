using Newtonsoft.Json;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: RocoAssetCli tojson <source> <destination> <engine> [mappings]");
    return 2;
}

try
{
    switch (args[0].ToLowerInvariant())
    {
        case "tojson":
            if (args.Length < 4) throw new ArgumentException("tojson requires source, destination and engine version.");
            ToJson(args[1], args[2], args[3], args.Length > 4 ? args[4] : null);
            return 0;
        case "nrc-texture":
            if (args.Length < 3) throw new ArgumentException("nrc-texture requires input directory and output directory.");
            ConvertTexturesToNrc(args[1], args[2], args.Length > 3 ? args[3] : "VER_UE4_26");
            return 0;
        case "fromjson":
            if (args.Length < 3) throw new ArgumentException("fromjson requires source JSON and destination asset.");
            FromJson(args[1], args[2], args.Length > 3 ? args[3] : null);
            return 0;
        default:
            Console.Error.WriteLine($"Unknown command: {args[0]}");
            return 2;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static void ToJson(string source, string destination, string engineVersion, string? mappingsPath)
{
    var asset = new UAsset(source, ParseEngineVersion(engineVersion), LoadMappings(mappingsPath));
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
    File.WriteAllText(destination, asset.SerializeJson(Formatting.Indented));
    Console.WriteLine($"Exported {source} -> {destination}");
}

static void FromJson(string source, string destination, string? mappingsPath)
{
    using var stream = File.OpenRead(source);
    var asset = UAsset.DeserializeJson(stream) ?? throw new InvalidDataException("Unable to deserialize asset JSON.");
    asset.Mappings = LoadMappings(mappingsPath);
    asset.FilePath = source;
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);
    asset.Write(destination);
    Console.WriteLine($"Imported {source} -> {destination}");
}

static void ConvertTexturesToNrc(string inputDirectory, string outputDirectory, string engineVersion)
{
    if (!Directory.Exists(inputDirectory)) throw new DirectoryNotFoundException(inputDirectory);
    Directory.CreateDirectory(outputDirectory);
    foreach (var input in Directory.GetFiles(inputDirectory, "*.uasset", SearchOption.TopDirectoryOnly))
    {
        var asset = new UAsset(input, ParseEngineVersion(engineVersion), null, CustomSerializationFlags.SkipParsingExports);
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
        Console.WriteLine($"Converted {input} -> {output}");
    }
}

static Usmap? LoadMappings(string? path){
    return string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? null : new Usmap(path);
}

static EngineVersion ParseEngineVersion(string value)
{
    if (value.Contains('.'))
    {
        var normalized = "VER_UE" + value.Replace('.', '_').Replace("VER_", string.Empty);
        if (Enum.TryParse<EngineVersion>(normalized, true, out var parsed)) return parsed;
    }
    if (Enum.TryParse<EngineVersion>(value, true, out var direct)) return direct;
    return EngineVersion.VER_UE4_26;
}

