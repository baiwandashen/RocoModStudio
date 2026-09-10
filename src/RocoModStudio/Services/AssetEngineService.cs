using Newtonsoft.Json;
using UAssetAPI;
using UAssetAPI.ExportTypes;
using UAssetAPI.UnrealTypes;
using UAssetAPI.Unversioned;

namespace RocoModStudio.Services;

public sealed record AssetInspection(
    string Path,
    int Exports,
    int Imports,
    int Names,
    IReadOnlyList<string> Classes);

public sealed record NrcConversionResult(
    int Total,
    int Converted,
    int Skipped,
    IReadOnlyList<string> Messages);

public sealed class AssetEngineService
{
    public AssetInspection Inspect(string assetPath, string engineVersion, string? mappingsPath = null)
    {
        var asset = Load(assetPath, engineVersion, mappingsPath);
        var classes = asset.Exports
            .Select(export => export.GetExportClassType()?.Value?.Value ?? "(unknown)")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value)
            .ToList();
        return new AssetInspection(assetPath, asset.Exports.Count, asset.Imports.Count, asset.GetNameMapIndexList().Count, classes);
    }

    public void ExportJson(string assetPath, string jsonPath, string engineVersion, string? mappingsPath = null)
    {
        var asset = Load(assetPath, engineVersion, mappingsPath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(jsonPath))!);
        File.WriteAllText(jsonPath, asset.SerializeJson(Formatting.Indented));
    }

    public void ImportJson(string jsonPath, string outputAssetPath, string? mappingsPath = null)
    {
        using var stream = File.OpenRead(jsonPath);
        var asset = UAsset.DeserializeJson(stream) ?? throw new InvalidDataException("UAssetAPI 无法读取该 JSON。");
        asset.Mappings = LoadMappings(mappingsPath);
        asset.FilePath = jsonPath;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputAssetPath))!);
        asset.Write(outputAssetPath);
    }

    public NrcConversionResult ConvertToNrc(
        string inputDirectory,
        string outputDirectory,
        string assetType,
        string engineVersion = "VER_UE4_26",
        Action<string>? log = null)
    {
        if (!Directory.Exists(inputDirectory)) throw new DirectoryNotFoundException(inputDirectory);
        Directory.CreateDirectory(outputDirectory);

        var files = Directory.GetFiles(inputDirectory, "*.uasset", SearchOption.TopDirectoryOnly);
        var messages = new List<string>();
        var converted = 0;
        var skipped = 0;

        foreach (var inputPath in files.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var fileName = Path.GetFileName(inputPath);
            try
            {
                var asset = Load(inputPath, engineVersion, null, CustomSerializationFlags.SkipParsingExports);
                var exports = asset.Exports;
                var changed = false;

                foreach (var export in exports)
                {
                    var className = export.GetExportClassType()?.Value?.Value ?? string.Empty;
                    if (!MatchesType(className, assetType)) continue;

                    if (export is not RawExport rawExport)
                    {
                        messages.Add($"{fileName}: {className} 不是可安全处理的 RawExport，已跳过");
                        continue;
                    }

                    if (assetType is "Texture" or "All" && className.Contains("Texture", StringComparison.OrdinalIgnoreCase))
                    {
                        var original = rawExport.Data ?? Array.Empty<byte>();
                        var transformed = new byte[original.Length + 16];
                        Buffer.BlockCopy(original, 0, transformed, 16, original.Length);
                        rawExport.Data = transformed;
                        changed = true;
                        messages.Add($"{fileName}: Texture 已插入 NRC 16 字节头");
                    }
                    else
                    {
                        messages.Add($"{fileName}: {className} 的上游转换规则仍为实验占位，未修改");
                    }
                }

                if (!changed)
                {
                    skipped++;
                    continue;
                }

                var outputPath = Path.Combine(outputDirectory, fileName);
                asset.Write(outputPath);
                CopySidecar(inputPath, outputDirectory, ".ubulk");
                converted++;
                log?.Invoke($"已转换 {fileName}");
            }
            catch (Exception ex)
            {
                skipped++;
                messages.Add($"{fileName}: {ex.Message}");
                log?.Invoke($"[错误] {fileName}: {ex.Message}");
            }
        }

        return new NrcConversionResult(files.Length, converted, skipped, messages);
    }

    private static bool MatchesType(string className, string assetType)
    {
        return assetType switch
        {
            "All" => true,
            "Texture" => className.Contains("Texture", StringComparison.OrdinalIgnoreCase),
            "StaticMesh" => className.Equals("StaticMesh", StringComparison.OrdinalIgnoreCase),
            "SkeletalMesh" => className.Equals("SkeletalMesh", StringComparison.OrdinalIgnoreCase),
            "AnimSequence" => className.Equals("AnimSequence", StringComparison.OrdinalIgnoreCase),
            "Material" => className.Contains("Material", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static void CopySidecar(string inputPath, string outputDirectory, string extension)
    {
        var sidecar = Path.ChangeExtension(inputPath, extension);
        if (!File.Exists(sidecar)) return;
        File.Copy(sidecar, Path.Combine(outputDirectory, Path.GetFileName(sidecar)), overwrite: true);
    }

    private static UAsset Load(
        string assetPath,
        string engineVersion,
        string? mappingsPath,
        CustomSerializationFlags flags = CustomSerializationFlags.None)
    {
        var engine = ParseEngineVersion(engineVersion);
        return new UAsset(assetPath, engine, LoadMappings(mappingsPath), flags);
    }

    private static Usmap? LoadMappings(string? mappingsPath)
    {
        return string.IsNullOrWhiteSpace(mappingsPath) || !File.Exists(mappingsPath)
            ? null
            : new Usmap(mappingsPath);
    }

    public static EngineVersion ParseEngineVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return EngineVersion.VER_UE4_26;
        if (value.Contains('.'))
        {
            var normalized = "VER_UE" + value.Replace('.', '_').Replace("VER_", string.Empty);
            if (Enum.TryParse<EngineVersion>(normalized, true, out var parsed)) return parsed;
        }
        if (Enum.TryParse<EngineVersion>(value, true, out var direct)) return direct;
        return EngineVersion.VER_UE4_26;
    }
}

