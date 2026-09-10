# Roco Mod Studio

Roco Kingdom World 的统一 Mod 工作台。界面按实际制作顺序组织：

1. **游戏包解包 / FModel**：内置最新 FModel 2b09c12f 图形浏览器，以及可解 Roco Patch 的 nrc/CUE4Parse 2b09c12f 命令行核心。支持 AES、Oodle、usmap、列表、断点续传解包、对象提取、自动识别资源并生成直接覆盖 PAK。
2. **PAK 提取 / 打包**：调用 repak 查看、列出、哈希、解包与重新打包 UE4.26 PAK。
3. **蓝图与资源编辑**：内置 UAssetAPI，检查 export/import/name map，导出和写回 cooked `.uasset` JSON。
4. **贴图工作台**：调用 UE4-DDS-Tools 从 Texture2D/TextureCube 等资源导出贴图，或把 DDS/TGA/PNG/JPG/BMP/HDR 注入资产。
5. **NRC 魔改转换**：集成上游已明确的纹理 16 字节头规则；未实现类型会明确报告而不是伪造成功。
6. **宠物外观替换**：执行用户提供的 `unreal-pet-bp-swap` 工作流，移植 Mesh、AnimBP、PhysicsAsset、碰撞体、头顶 UI 与世界语音；支持 RideAll 实验流程与自动打包。
7. **Blender / Cook 模型**：把 FModel 导出的 glTF 建立成 Blender `.blend`，编辑骨骼、蒙皮、材质与动画；再交给 UE4.26 Python 导入及 RunUAT Cook，最后回写 `pack-root`。
8. **Mod 发布打包**：按项目目录和挂载点生成发布 PAK，并支持回读检查。

## 运行

直接运行 `RocoModStudio.exe`。程序已随包携带 FModel、CUE4Parse CLI、Node、repak 与 UE4-DDS-Tools。Blender 和 Unreal Engine 4.26 不随包分发，可在设置页手动配置；运行环境需要 .NET 10 Desktop Runtime。

## 自定义骨骼模型闭环

```text
PAK
  -> FModel 定位 SkeletalMesh / Skeleton / AnimSequence
  -> FModel 导出 glTF
  -> Blender 建工程并编辑骨骼/蒙皮/材质/动画
  -> 导出 FBX/GLB
  -> Unreal 4.26 Python 导入到目标 Content 路径
  -> RunUAT BuildCookRun
  -> 复制 Cooked 到 pack-root
  -> 宠物 BP 替换或直接覆盖
  -> repak 打包
```

## 重要边界

- cooked 蓝图不适合直接重写完整 Kismet 图；本工具重点是资源结构、默认属性、组件数据、路径、贴图、特效覆盖和模型 Cook。
- 自定义骨骼模型必须使用匹配目标角色的 Skeleton，否则会出现参考姿势、动画不匹配或骑行失效。
- Unreal Cook 需要正确安装 UE4.26、项目插件和平台 SDK；工具已经生成并调用桥接脚本，但不会伪造缺失的引擎环境。
- NRC 转换器上游除 Texture 之外仍有占位实现。Roco Mod Studio 不会对这类资产执行未经验证的二进制修改。
- 宠物替换与 RideAll 别名结果只有在干净客户端中完成运行测试后，才能视为功能正确。

## 工具来源

- FModel：https://github.com/4sval/FModel
- CUE4Parse nrc/Roco：https://github.com/LukeFZ/CUE4Parse
- UAssetAPI / UAssetGUI：https://github.com/atenfyr/UAssetGUI
- UE4-DDS-Tools：https://github.com/matyalatte/UE4-DDS-Tools
- RocoKingdomWorld-CookedAssetConverter：https://github.com/Bu-Hong/RocoKingdomWorld-CookedAssetConverter
- repak：https://github.com/trumank/repak
- Blender：https://github.com/blender/blender

完整第三方说明见 `Tools/THIRD-PARTY-NOTICES.txt`。

## 构建

```powershell
dotnet restore src\RocoModStudio\RocoModStudio.csproj
dotnet build src\RocoModStudio\RocoModStudio.csproj -c Release
dotnet publish src\RocoModStudio\RocoModStudio.csproj -c Release -o outputs\RocoModStudio
```

生成发布目录前，`work/portable-tools` 中应包含 FModel、CUE4ParseCli、Node、repak、UE4-DDS-Tools 和 `PetBpSwap`。
