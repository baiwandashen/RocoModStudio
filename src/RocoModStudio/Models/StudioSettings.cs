namespace RocoModStudio.Models;

public sealed class StudioSettings
{
    public string? RepakPath { get; set; }
    public string? Ue4DdsPythonPath { get; set; }
    public string? Ue4DdsScriptPath { get; set; }
    public string? NodePath { get; set; }
    public string? PetSwapScriptPath { get; set; }
    public string? PetRideAllScriptPath { get; set; }
    public string? FModelPath { get; set; }
    public string? Cue4ParseCliPath { get; set; }
    public string? OodleDllPath { get; set; }
    public string? BlenderPath { get; set; }
    public string? UnrealEditorCmdPath { get; set; }
    public string? UnrealRunUatPath { get; set; }
    public string? UnrealProjectPath { get; set; }
    public string? LastProjectRoot { get; set; }
    public string? LastPakPath { get; set; }
    public string? LastAssetPath { get; set; }
    public string? LastGamePaksPath { get; set; }
    public string? LastUnpackedPath { get; set; }
    public string? LastBlenderModelPath { get; set; }
    public string EngineVersion { get; set; } = "VER_UE4_26";
}

public sealed class ToolStatus
{
    public required string Name { get; init; }
    public required string Purpose { get; init; }
    public required bool Available { get; init; }
    public required string Detail { get; init; }
    public string Badge => Available ? "可用" : "需配置";
}

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class UnpackedAssetSummary
{
    public required string Root { get; init; }
    public int TotalFiles { get; init; }
    public int UAssetCount { get; init; }
    public int TextureCount { get; init; }
    public int StaticMeshCount { get; init; }
    public int SkeletalMeshCount { get; init; }
    public int AnimationCount { get; init; }
    public int BlueprintCount { get; init; }
    public int EffectCount { get; init; }
    public int VoiceBankCount { get; init; }
    public IReadOnlyList<string> PetNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Samples { get; init; } = Array.Empty<string>();
}

public sealed record ModProject(string Name, string Root, DateTimeOffset CreatedAt)
{
    public string WorkingDirectory => Path.Combine(Root, "working");
    public string PackRoot => Path.Combine(Root, "pack-root");
    public string OutputDirectory => Path.Combine(Root, "dist");
}

