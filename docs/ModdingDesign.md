# Roco Mod 设计实现说明

Roco Mod Studio 不把所有修改都强行塞进同一个转换器，而是按资产类型选择正确策略：

## 1. 直接 Cooked 覆盖

适用于 Texture、Material、MaterialInstance、Particle/Niagara、UI、音频和脚本等可直接替换的完整资产。

流程：

1. CUE4Parse CLI 解包 Patch/Base PAK。
2. 资源扫描器根据路径和文件名识别资产类别。
3. 用户可用过滤条件将指定资产复制到 `pack-root`，保留 `NRC/Content/...` 原路径。
4. repak 以 `../../../` 挂载点打包，游戏原路径加载修改后的资产。

这种方式最稳定，适合“贴图外观、技能特效、UI、音频、配置表”类 Mod。

## 2. 宠物 BP 外科替换

适用于目标宠物职业/玩法必须保留，但需要替换模型、动画配置、碰撞体、头顶 UI 和世界语音的场景。

- 以目标主 BP 为基底。
- 只移植 `CollisionCylinder`、`CharacterMesh0`、`RocoAnim_GEN_VARIABLE`、`HeadWidget_GEN_VARIABLE`。
- 保留目标生成类、CDO 身份、导航、移动、交互、音频组件和玩法导出。
- 语音 Bank 使用 V3 方法：替换 Bank ID 与 Event ID，不重建 Wwise HIRC 图。
- RideAll 使用完整供体 ABP + 匹配 SkeletalMesh 成对别名，避免骨架失配。

## 3. Blender 自定义骨骼模型

这是无法通过替换单个 cooked export 完成的流程，必须经过 Unreal Cook：

1. FModel 浏览 SkeletalMesh、Skeleton、AnimSequence、Material。
2. FModel 导出 glTF。
3. Roco Blender Bridge 自动导入 glTF，建立 `.blend`，保留骨架、蒙皮、材质和动画。
4. 用户在 Blender 修改模型，保存 `.blend`。
5. 桥接脚本导出 FBX/GLB。
6. Unreal Editor Python 将模型导入指定 `/Game/...` 路径，并绑定目标 Skeleton。
7. RunUAT BuildCookRun 为目标平台 Cook。
8. 工具将 `Cooked/<Platform>/<Project>/Content` 复制到 `pack-root/<Project>/Content`。
9. 宠物 BP 替换负责把目标角色逻辑指向新模型；或直接覆盖原目标资产路径。

关键约束：目标 Skeleton 必须正确。仅让模型外观相似但没有一致骨骼层级，会导致参考姿势、动画错位或骑乘/飞行动画失效。

## 4. 特效类 Mod

特效不通过改蓝图图节点实现。更可靠的方式是：

- 修改粒子材质/DDS 贴图。
- 替换 Niagara/ParticleSystem 相关 cooked 资源。
- 调整贴图参数、颜色曲线、材质实例参数。
- 保留原对象路径，直接覆盖并打包。

这样可以实现火焰颜色、技能拖尾、发光强度、UI 图标和角色染色等视觉变化，不改玩法逻辑。

## 5. 自动识别策略

扫描器统计 UAsset、Texture、SkeletalMesh、AnimSequence、Blueprint、Effect、VoiceBank，并从任意 `Pets` 目录提取候选宠物名。

过滤规则支持名称片段、路径片段和 `*` / `?` 通配符。宠物替换页可自动定位：

- `BP/Pets/<Pet>`
- `AnimSequence/Pets/<Pet>`
- `Pet_Vo_<Pet>*.bnk`

## 6. PAK 输出结构

```text
pack-root/
  NRC/
    Content/
      ArtRes/...
      NewRoco/...
```

或者自定义 Cooked 内容：

```text
pack-root/
  <ProjectName>/
    Content/
      ...
```

最后统一使用 V11 PAK、`../../../` 挂载点打包。

## 7. 必须验证的项目

- 模型：轮廓、蒙皮权重、骨骼、材质槽、LOD。
- 动画：待机、跑动、起飞、悬停、滑翔、落地、上下骑乘。
- 碰撞：胶囊大小、地面高度、交互距离。
- 特效：材质、贴图、粒子层级、性能。
- 语音：世界语音、详情页语音、战斗事件。
- 兼容：禁用覆盖相同路径的旧 PAK，并在干净客户端测试。
