using RocoModStudio.Models;

namespace RocoModStudio.Services;

public sealed class ToolLocator
{
    private readonly string _toolsRoot;

    public ToolLocator()
    {
        _toolsRoot = Environment.GetEnvironmentVariable("ROCO_TOOLS_ROOT")
                     ?? Path.Combine(AppContext.BaseDirectory, "Tools");
    }

    public string ToolsRoot => _toolsRoot;

    public IReadOnlyList<ToolStatus> Detect(StudioSettings settings)
    {
        var results = new List<ToolStatus>
        {
            new()
            {
                Name = "UAsset 引擎",
                Purpose = "检查、导出和重写蓝图/资源结构",
                Available = true,
                Detail = "内置 UAssetAPI 1.1"
            }
        };

        var fmodel = FindFModel(settings);
        results.Add(new ToolStatus { Name = "FModel 浏览器", Purpose = "图形浏览、预览和导出 Roco cooked 资源", Available = File.Exists(fmodel), Detail = fmodel ?? "未找到最新 FModel.exe" });
        var cue4 = FindCue4ParseCli(settings);
        results.Add(new ToolStatus { Name = "CUE4Parse 解包核心", Purpose = "自动列出、解包、提取对象和 Roco 配置", Available = File.Exists(cue4), Detail = cue4 ?? "未找到 cue4parse-cli.exe" });
        var blender = FindBlender(settings);
        results.Add(new ToolStatus { Name = "Blender", Purpose = "编辑骨骼、网格、材质和导出模型", Available = File.Exists(blender), Detail = blender ?? "未找到 blender.exe" });
        var unreal = FindUnrealEditor(settings);
        results.Add(new ToolStatus { Name = "Unreal Editor", Purpose = "把 Blender 模型重新导入并 Cook 为游戏资产", Available = File.Exists(unreal), Detail = unreal ?? "未找到 UnrealEditor-Cmd.exe" });        var repak = FindRepak(settings);
        results.Add(new ToolStatus
        {
            Name = "PAK 打包器",
            Purpose = "解包、列表和打包 UE4.26 .pak",
            Available = File.Exists(repak),
            Detail = repak ?? "未找到 repak.exe"
        });

        var node = FindNode(settings);
        results.Add(new ToolStatus
        {
            Name = "工作流运行时",
            Purpose = "执行宠物蓝图安全替换工作流",
            Available = File.Exists(node),
            Detail = node ?? "未找到 node.exe"
        });

        var petScript = FindPetSwapScript(settings);
        results.Add(new ToolStatus
        {
            Name = "宠物 BP 替换",
            Purpose = "移植模型、动画、碰撞体和世界语音",
            Available = File.Exists(petScript),
            Detail = petScript ?? "未找到 build_pet_bp_swap.mjs"
        });

        var cli = FindAssetCli();
        results.Add(new ToolStatus
        {
            Name = "资源转换 CLI",
            Purpose = "为替换工作流提供 UAsset JSON 往返",
            Available = File.Exists(cli),
            Detail = cli ?? "未找到 RocoAssetCli.exe"
        });

        var ddsScript = FindDdsScript(settings);
        var ddsPython = FindDdsPython(settings, ddsScript);
        results.Add(new ToolStatus
        {
            Name = "贴图工作台",
            Purpose = "注入、导出、转换 DDS/TGA/PNG",
            Available = File.Exists(ddsScript) && (File.Exists(ddsPython) || IsOnPath(ddsPython)),
            Detail = File.Exists(ddsScript) ? $"脚本: {ddsScript}" : "未找到 UE4-DDS-Tools"
        });

        return results;
    }

    public string? FindRepak(StudioSettings settings)
    {
        return FirstExisting(
            settings.RepakPath,
            Path.Combine(_toolsRoot, "repak", "repak.exe"),
            Path.Combine(_toolsRoot, "repak", "bin", "repak.exe"),
            FindRecursive(_toolsRoot, "repak.exe"),
            FindOnPath("repak.exe"));
    }

    public string? FindNode(StudioSettings settings)
    {
        return FirstExisting(
            settings.NodePath,
            Path.Combine(_toolsRoot, "Node", "node.exe"),
            Path.Combine(_toolsRoot, "node", "node.exe"),
            FindRecursive(_toolsRoot, "node.exe"),
            FindOnPath("node.exe"));
    }

    public string? FindPetSwapScript(StudioSettings settings)
    {
        return FirstExisting(
            settings.PetSwapScriptPath,
            Path.Combine(_toolsRoot, "PetBpSwap", "scripts", "build_pet_bp_swap.mjs"),
            FindRecursive(_toolsRoot, "build_pet_bp_swap.mjs"));
    }

    public string? FindPetRideAllScript(StudioSettings settings)
    {
        return FirstExisting(
            settings.PetRideAllScriptPath,
            Path.Combine(_toolsRoot, "PetBpSwap", "scripts", "build_rideall_override.mjs"),
            FindRecursive(_toolsRoot, "build_rideall_override.mjs"));
    }

    public string? FindAssetCli()
    {
        return FirstExisting(
            Environment.ProcessPath,
            Path.Combine(_toolsRoot, "RocoAssetCli", "RocoAssetCli.exe"),
            FindRecursive(_toolsRoot, "RocoAssetCli.exe"));
    }

    public string? FindDdsScript(StudioSettings settings)
    {
        return FirstExisting(
            settings.Ue4DdsScriptPath,
            Path.Combine(_toolsRoot, "UE4-DDS-Tools", "main.py"),
            Path.Combine(_toolsRoot, "UE4-DDS-Tools", "src", "main.py"),
            FindRecursive(_toolsRoot, "main.py", path => path.Contains("UE4-DDS", StringComparison.OrdinalIgnoreCase)));
    }

    public string? FindDdsPython(StudioSettings settings, string? scriptPath = null)
    {
        var candidates = new List<string?>
        {
            settings.Ue4DdsPythonPath,
            Path.Combine(_toolsRoot, "UE4-DDS-Tools", "python.exe"),
            Path.Combine(_toolsRoot, "UE4-DDS-Tools", "runtime", "python.exe"),
            Path.Combine(_toolsRoot, "UE4-DDS-Tools", "python", "python.exe")
        };

        if (!string.IsNullOrWhiteSpace(scriptPath))
        {
            var parent = Path.GetDirectoryName(scriptPath)!;
            candidates.Add(Path.Combine(parent, "python.exe"));
            candidates.Add(Path.Combine(parent, ".venv", "Scripts", "python.exe"));
            candidates.Add(Path.Combine(parent, "..", "python.exe"));
        }

        candidates.Add(FindRecursive(_toolsRoot, "python.exe", path => path.Contains("UE4-DDS", StringComparison.OrdinalIgnoreCase)));
        candidates.Add(FindOnPath("python.exe"));
        return FirstExisting(candidates.ToArray());
    }


    public string? FindFModel(StudioSettings settings)
    {
        return FirstExisting(settings.FModelPath, Path.Combine(_toolsRoot, "FModel", "FModel.exe"), Path.Combine(_toolsRoot, "FModel.exe"), FindRecursive(_toolsRoot, "FModel.exe"));
    }

    public string? FindCue4ParseCli(StudioSettings settings)
    {
        return FirstExisting(settings.Cue4ParseCliPath, Path.Combine(_toolsRoot, "CUE4ParseCli", "cue4parse-cli.exe"), Path.Combine(_toolsRoot, "cue4parse-cli", "cue4parse-cli.exe"), FindRecursive(_toolsRoot, "cue4parse-cli.exe"));
    }

    public string? FindBlender(StudioSettings settings)
    {
        var installed = FindNewestExecutable(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Blender Foundation"), "blender.exe");
        return FirstExisting(settings.BlenderPath, Path.Combine(_toolsRoot, "Blender", "blender.exe"), installed, FindOnPath("blender.exe"));
    }

    public string? FindUnrealEditor(StudioSettings settings)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return FirstExisting(
            settings.UnrealEditorCmdPath,
            Path.Combine(programFiles, "Epic Games", "UE_4.26", "Engine", "Binaries", "Win64", "UnrealEditor-Cmd.exe"),
            Path.Combine(programFiles, "Epic Games", "UE_4.27", "Engine", "Binaries", "Win64", "UnrealEditor-Cmd.exe"),
            FindRecursive(Path.Combine(programFiles, "Epic Games"), "UnrealEditor-Cmd.exe"));
    }

    public string? FindRunUat(StudioSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.UnrealRunUatPath) && File.Exists(settings.UnrealRunUatPath)) return settings.UnrealRunUatPath;
        var unreal = FindUnrealEditor(settings);
        if (string.IsNullOrWhiteSpace(unreal)) return null;
        var candidate = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(unreal)!, "..", "..", "Build", "BatchFiles", "RunUAT.bat"));
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? FindNewestExecutable(string root, string fileName)
    {
        try
        {
            return !Directory.Exists(root) ? null : Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).OrderByDescending(path => path).FirstOrDefault();
        }
        catch { return null; }
    }    private static string? FindRecursive(string root, string fileName, Func<string, bool>? predicate = null)
    {
        try
        {
            if (!Directory.Exists(root)) return null;
            return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories)
                .FirstOrDefault(path => predicate is null || predicate(path));
        }
        catch
        {
            return null;
        }
    }

    private static string? FindOnPath(string fileName)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var path in paths)
        {
            try
            {
                var candidate = Path.Combine(path, fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch
            {
                // Ignore malformed PATH entries.
            }
        }
        return null;
    }

    private static bool IsOnPath(string? executable)
    {
        if (string.IsNullOrWhiteSpace(executable)) return false;
        return File.Exists(executable) || FindOnPath(Path.GetFileName(executable)) is not null;
    }

    private static string? FirstExisting(params string?[] candidates)
    {
        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }
}



