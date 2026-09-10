using System.Text.Json;
using RocoModStudio.Models;

namespace RocoModStudio.Services;

public sealed class ProjectService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public ModProject Create(string root, string name)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("请选择项目目录。", nameof(root));
        if (string.IsNullOrWhiteSpace(name)) name = "RocoMod";

        var safeName = string.Concat(name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch)).Trim();
        var projectRoot = Path.Combine(root, safeName);
        Directory.CreateDirectory(projectRoot);
        Directory.CreateDirectory(Path.Combine(projectRoot, "working"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "pack-root"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "dist"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "reports"));

        var project = new ModProject(safeName, projectRoot, DateTimeOffset.Now);
        Save(project);
        return project;
    }

    public void Save(ModProject project)
    {
        var path = Path.Combine(project.Root, "roco-mod-project.json");
        File.WriteAllText(path, JsonSerializer.Serialize(project, JsonOptions));
    }

    public ModProject? Load(string root)
    {
        try
        {
            var path = Path.Combine(root, "roco-mod-project.json");
            return File.Exists(path)
                ? JsonSerializer.Deserialize<ModProject>(File.ReadAllText(path))
                : null;
        }
        catch
        {
            return null;
        }
    }
}
