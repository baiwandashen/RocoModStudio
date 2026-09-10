using RocoModStudio.Models;

namespace RocoModStudio.Services;

public sealed class AssetScanService
{
    public UnpackedAssetSummary Scan(string root)
    {
        if (!Directory.Exists(root)) throw new DirectoryNotFoundException(root);
        var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var pets = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in Directory.EnumerateDirectories(root, "Pets", SearchOption.AllDirectories))
        {
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(child);
                if (name.Length >= 3) pets.Add(name);
            }
        }

        return new UnpackedAssetSummary
        {
            Root = root,
            TotalFiles = files.Count,
            UAssetCount = files.Count(path => path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase)),
            TextureCount = files.Count(path => path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) && IsLikelyTexture(path)),
            StaticMeshCount = files.Count(path => path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(path).StartsWith("SM_", StringComparison.OrdinalIgnoreCase)),
            SkeletalMeshCount = files.Count(path => path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(path).StartsWith("SKM", StringComparison.OrdinalIgnoreCase)),
            AnimationCount = files.Count(path => path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) && (path.Contains("AnimSequence", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).StartsWith("AM_", StringComparison.OrdinalIgnoreCase))),
            BlueprintCount = files.Count(path => path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(path).StartsWith("BP_", StringComparison.OrdinalIgnoreCase)),
            EffectCount = files.Count(path => path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) && (path.Contains("Effects", StringComparison.OrdinalIgnoreCase) || path.Contains("Particle", StringComparison.OrdinalIgnoreCase) || path.Contains("Niagara", StringComparison.OrdinalIgnoreCase))),
            VoiceBankCount = files.Count(path => path.EndsWith(".bnk", StringComparison.OrdinalIgnoreCase)),
            PetNames = pets.ToList(),
            Samples = files.Where(path => path.EndsWith(".uasset", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".bnk", StringComparison.OrdinalIgnoreCase)).Take(40).ToList()
        };
    }

    public int CopyAssets(string sourceRoot, string destinationRoot, string filter)
    {
        if (!Directory.Exists(sourceRoot)) throw new DirectoryNotFoundException(sourceRoot);
        Directory.CreateDirectory(destinationRoot);
        var pattern = string.IsNullOrWhiteSpace(filter) ? "*" : filter.Trim();
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".uasset", ".uexp", ".ubulk", ".bnk", ".json", ".ini", ".locres" };
        var copied = 0;

        foreach (var source in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(source);
            var matchesName = MatchesSimplePattern(fileName, pattern) || MatchesSimplePattern(source, pattern);
            var parentMatches = source.Split(Path.DirectorySeparatorChar).Any(segment => segment.Contains(pattern, StringComparison.OrdinalIgnoreCase));
            if (!matchesName && !parentMatches) continue;
            if (!extensions.Contains(Path.GetExtension(source))) continue;

            var relative = Path.GetRelativePath(sourceRoot, source);
            var destination = Path.Combine(destinationRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
            copied++;
        }
        return copied;
    }

    private static bool IsLikelyTexture(string path) =>
        path.Contains("Texture", StringComparison.OrdinalIgnoreCase) ||
        Path.GetFileName(path).StartsWith("T_", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("Textures", StringComparison.OrdinalIgnoreCase);

    private static bool MatchesSimplePattern(string value, string pattern)
    {
        if (pattern == "*") return true;
        if (pattern.Contains('*') || pattern.Contains('?'))
        {
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(value), regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return value.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }
}

