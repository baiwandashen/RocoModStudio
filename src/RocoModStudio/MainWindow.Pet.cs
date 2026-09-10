using System.Windows;

namespace RocoModStudio;

public partial class MainWindow
{
    private async void PetRun_Click(object sender, RoutedEventArgs e)
    {
        await RunOperationAsync("宠物 BP 外观替换", async () =>
        {
            var targetBp = RequireDirectory(PetTargetBpBox.Text, "目标 BP/Pets 目录");
            var donorBp = RequireDirectory(PetDonorBpBox.Text, "供体 BP/Pets 目录");
            var targetAnim = RequireDirectory(PetTargetAnimBox.Text, "目标 AnimSequence/Pets 目录");
            var donorAnim = RequireDirectory(PetDonorAnimBox.Text, "供体 AnimSequence/Pets 目录");
            if (string.IsNullOrWhiteSpace(PetOutputBox.Text)) throw new InvalidOperationException("请选择新的输出目录。");
            var output = Path.GetFullPath(PetOutputBox.Text.Trim());
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
            {
                throw new InvalidOperationException("输出目录必须为空或尚不存在，避免覆盖已有 Mod。");
            }
            Directory.CreateDirectory(output);

            var node = _toolLocator.FindNode(_settings) ?? throw new InvalidOperationException("未找到 node.exe。");
            var script = _toolLocator.FindPetSwapScript(_settings) ?? throw new InvalidOperationException("未找到 build_pet_bp_swap.mjs。");
            var assetCli = _toolLocator.FindAssetCli() ?? throw new InvalidOperationException("未找到 RocoAssetCli.exe。");
            var engine = PetEngineCombo.SelectedIndex == 1 ? "VER_UE4_27" : "VER_UE4_26";
            var arguments = new List<string>
            {
                script,
                "--target-bp-dir", targetBp,
                "--donor-bp-dir", donorBp,
                "--target-anim-dir", targetAnim,
                "--donor-anim-dir", donorAnim,
                "--uassetgui", assetCli,
                "--engine", engine,
                "--output", output
            };

            if (!string.IsNullOrWhiteSpace(PetTargetBankBox.Text)) arguments.AddRange(new[] { "--target-voice-bank", RequireFile(PetTargetBankBox.Text, "目标语音 Bank") });
            if (!string.IsNullOrWhiteSpace(PetDonorBankBox.Text)) arguments.AddRange(new[] { "--donor-voice-bank", RequireFile(PetDonorBankBox.Text, "供体语音 Bank") });
            if (PetSkipVoiceCheck.IsChecked == true) arguments.AddRange(new[] { "--skip-voice-bank", "true" });

            AppendLog("启动标准 BP/Anim/语音替换与结构验证...", System.Windows.Media.Brushes.LightGreen);
            var standard = await _processRunner.RunAsync(node, arguments, Path.GetDirectoryName(script), _operationCancellation?.Token ?? default);
            if (standard.ExitCode != 0) throw new InvalidOperationException(standard.StandardError.Trim());

            if (PetRideAllCheck.IsChecked == true)
            {
                var rideAllScript = _toolLocator.FindPetRideAllScript(_settings) ?? throw new InvalidOperationException("未找到 build_rideall_override.mjs。");
                var rideAllArguments = new List<string>
                {
                    rideAllScript,
                    "--target-anim-dir", targetAnim,
                    "--donor-anim-dir", donorAnim,
                    "--uassetgui", assetCli,
                    "--engine", engine,
                    "--output", output
                };
                AppendLog("执行 RideAll 完整包别名与结构验证...", System.Windows.Media.Brushes.Orange);
                var rideAll = await _processRunner.RunAsync(node, rideAllArguments, Path.GetDirectoryName(rideAllScript), _operationCancellation?.Token ?? default);
                if (rideAll.ExitCode != 0) throw new InvalidOperationException(rideAll.StandardError.Trim());
            }

            if (PetAutoPackCheck.IsChecked == true)
            {
                var packRoot = Path.Combine(output, "pack-root");
                if (!Directory.Exists(packRoot)) throw new DirectoryNotFoundException($"替换流程未生成预期的 pack-root：{packRoot}");
                var pakPath = Path.Combine(output, $"{new DirectoryInfo(output).Name}.pak");
                AppendLog("开始打包替换结果...");
                await RunRepakAsync("pack", packRoot, pakPath, "--mount-point", "../../../", "--version", "V11");
                AppendLog($"PAK 已生成：{pakPath}", System.Windows.Media.Brushes.LightGreen);
            }

            AppendLog("结构验证通过。仍需在禁用旧 PAK 的干净客户端中完成运行测试。", System.Windows.Media.Brushes.LightGreen);
        });
    }
}
