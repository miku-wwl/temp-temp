# Claude Code · Echoes of the Valley B5 美术深化与实机验收（可直接复制）

你是 Echoes of the Valley 项目在 B5 阶段的主程 + Technical Artist。请独立实现、修复并验证，绝不能把素材预览、Unity Editor 截图或测试绿灯代替真实 Windows Player 视觉验收。

## 0. 前置上下文与不可破坏约束

- Unity `6000.3.27f1`、URP `17.3.0`、Windows x64，Radeon 780M / 1080p / cap 60 FPS；比赛截止 `2026-11-08 20:59 NZDT`。
- 以 `miku-wwl/echoes-of-the-valley` **main** 作为唯一开发分支。先安全检查 `git status`、`git branch -vv`、`git log`、`git remote -v` 和当前 B4 代码结构，确认 B4 的最新结果已在 main。未经用户同意不要 stash/drop、reset、force push、删除带独有提交的分支。**禁止新建 feature 分支**。若 main 未包含 B4 或工作区有他人改动，暂停并报告。
- B4 保留：女主四向精灵/轻动画、炼金工坊、垂柳、高角度镜头、真实小地图、现有世界范围及玩法。B5 **不得退回 B2 低角度模式**，**不得扩建场景或添加 Phase C/D**。
- 玩法完整：一个山谷，炼金、喂鸡、风车三条任务，Silence→Awakening→Resonance→Rest 与 S0→S3，存档/继续、交互/碰撞/导航、原测试不能减少或弱化。
- 固定 yaw45°，候选 pitch42/48/54、默认 pitch48°；以实际操作可读性为验收重点。
- 这次补充的六张图是**候选源素材**，不是已经生产化的 Unity Assets，不可在报告中标注 REAL_PLAYER_SCREENSHOT / PRODUCTION_ART_UNITY_IN_USE，除非确已导入并运行验证。

## 1. 先做基线审计

1. 阅读 `docs/10-PHASE52-B4.md`、B4 报告、现有 B4 美术生成器与 `Phase52B4Pilot.cs`、`StaticProp25D`、`StageVariant25D`、`MinimapController.cs`、B4 Edit/PlayMode tests。
2. 列出 B4 实机静帧可见的五项质量差距及确切位置：旧低多边形树与风车周边旧物、大片单调草地、无精致农田、泉水像孤立贴画/被旧溪流破坏、树冠遮挡角色。以真实 Windows Player 截图确认，不把作者报告当结果。
3. 确定原世界地形材质和交互 anchor：`game/Assets/Echoes/Art/Terrain/{Grass,Meadow,Path,Stone}.terrainlayer`；记录具体改造文件及其对任务、NavMesh、旧存档的风险。

## 2. 导入我提供的 6 张美术素材（必须使用、也允许有理由拒收）

本 Prompt 同目录随附：

```
spring_S0_dormant.png
spring_S3_restored.png
meadow_grass_albedo.png
tilled_soil_albedo.png
farm_raised_beds.png
broadleaf_tree.png
```

读取 `README_FIRST_zh.md`；将源图放入可追踪的 B5 Source 目录，例如 `game/Assets/Echoes/Art/Phase52B5/Source/`。允许用仓库已有的 Python/Pillow 等美术**离线构建脚本**修复色边、透明区域、锚点、地面贴图接缝，以及图集打包，必须保留源图并记录生成步骤；不要把一张概念图贴成整个场景。

### P0-A 泉水：质量优先

- 两张泉水图要遵循**同一个世界坐标、相同落脚点和可互动区域**。配准两图，避免切阶段时石拱/水池尺寸跳动；如果原始图几何不一致，修剪/重绘局部或分层替换，不得硬交差。
- S0 要符合**游戏实际剧情**的衰败/沉寂表现；注意提供的 `spring_S0_dormant.png` 仍有清晰的流水，**不能因为文件名写 S0 就认定已经干涸**。如果 S0 设定为干涸，必须制作局部去水流/低水位的衍生图并与 S3 对齐。S3 恢复为流动清泉、温暖日式幻想炼金氛围。S1/S2 随原始真实剧情渐变；禁止跳过真实剧情、篡改存档阶段或引入截图专用假切换作为运行逻辑。
- 新春泉要有可信的接地：地面接触阴影、遮挡/排序、石块与周围草石的融合、足够可辨认的水池边缘；如用 shader 水光/轻粒子，不可把动画开销强加给核显。不得引入现成 IP 素材。
- 修旧的 B2 溪流/蓝色方块水晶在泉水附近的风格冲突；如果旧资产仍由 gameplay 引用，保留逻辑并仅替换 visual，明确记录旧组件为何留存。

### P0-B 地表与农田

- 将 `meadow_grass_albedo.png` 和 `tilled_soil_albedo.png` 作为原始纹理，**先做 4×4 tile 无缝检查**。若边缘不连续，先制作无缝衍生图，生成器必须可复现；不能把 `Wrap Mode=Repeat` 当成已修接缝。
- 使用 TerrainLayer 或合适的局部网格/贴花叠加；与旧 Path/Stone 材质、广场鹅卵石之间必须有自然过渡。场景所见纹理密度不能过细造成爬纹、过密或闪烁；需要合理 mipmap、anisotropic filtering、贴图压缩和颜色校正。
- 农田加入 `farm_raised_beds.png` 作为装饰层，与 **现有** 实际种植地块/作物状态/碰撞/采集交互严格对齐。玩家不能看到一块已经长满蔬菜的装饰画，却无法交互、或明明已收割仍持续显示满田。必要时将绘图拆为静态木床和随状态变化的植物层。若无法使之与玩法一致，宁可只使用木框/土壤层，不允许视觉欺骗。

### P0-C 树木遮挡与风格

- `broadleaf_tree.png` 替换主要视野中突兀的低多边形锥树/树冠视觉，但尽量保留旧 collider/导航占地；同时检查 B4 柳树和 S1 之后出现的旧樱花树。
- 角色进入屋顶/树冠背后时必须可定位：使用排序、部位分层、局部淡出、柔和描边等，避免硬剪裁、屏幕上消失、整棵树瞬间变透明。
- 人物美术保持 B4 原版，不重画角色。确认四向 walking 的步频与移动速度一致，50%–100% 缩放下没有贴纸滑动、头身比例突变、强烈闪帧。

### P1 全局视觉统合

- 降低 B2 平面感和单调青绿色，提高局部暖光、草石色差、植被大小分级、视觉焦点；调色参考 B4 主人目标图的**氛围**，不复制任何游戏 IP 或直接把概念图当实际场景。
- 限定在既有 plaza/workshop/willow/spring/garden 等可见范围，不扩图、不大量重构任务。
- UI 小地图继续反映真实世界：实时道路/地标/玩家位置朝向/任务目标，不能改为静态装饰图；检查 UI 与提示信息的重叠。

## 3. Stop rules——出现即 B5 不能 PASS

1. 真实操控不如 B4 可读（主人试玩结果优先）。
2. 小地图是假地图、位置/目标不同步。
3. 角色被树冠或建筑严重遮挡、或透视/接地明显不可信。
4. 人物动画滑步或方向不连贯。
5. B5 植被/泉水/农田与女主及 B4 房屋风格冲突加重。
6. 炼金、喂鸡、风车、存档/继续、任务阶段任一真实回归失败。
7. 在相同 780M、1080p、60FPS cap、同构建同路线条件下，性能明显恶化，或产生严重显存/资源加载问题。

## 4. 测试和必须提交的证据

- 原有 EditMode≥B4 基线 244、PlayMode≥B4 基线 100（不是任意削弱原测试后取得的数），额外覆盖 B5 TerrainLayer、StageVariant、农田视觉状态和实际采集、角色与树冠遮挡、泉水交互、存档恢复、shader/import 设置；报告实际 test totals 与失败数。
- 真实 Windows x64 build 记录构建日志、错误/警告，实际启动全流程 smoke。不能只用编辑器模拟。
- 同 build / 同分辨率 / 尽可能同坐标对照 B4 与 B5：plaza、workshop-front、willow、spring S0、spring S3（注明 S1–S3 如脚本设定）。必须附最差遮挡例子和 4×4 地表 tile 接缝图。
- 连续≥30秒、最好 60–90秒 Windows Player 移动视频：展示转向、绕房屋、穿树冠、进出农田、泉水互动及小地图；自动输入则明显标记 AUTOMATED_INPUT，而非真人试玩。
- Radeon780M 1080p、cap60 同路线 B4 与 B5：平均FPS、p95/p99 frametime、显存/加载情况；性能若数据环境改变须注明不可比，不能冒称无退化。
- `README`、文件来源/AI disclosure、SHA256 manifest、changed-files、commit ID、build ID、完整测试 XML、截图视频及独立 review package；标记 REAL_PLAYER / EDITOR_PREVIEW / CONCEPT_ART、不要混用。

## 5. 工作策略与交付

1. 先做最小可行集成的 2–3 张 Windows 实机截图，集中解决**泉水的接地、草地接缝、树木遮挡**，对照 B4 基线确认方向。若有困难，承认 PARTIAL 并给出技术原因，不以更换文字标签掩盖。
2. 再整合农田、土壤和阶段演化；每轮测试质量和玩法，保持可回退小 commit。
3. 所有实现都在 **main**，常规小步提交，最后确认工作树洁净、`origin/main` 同步及 CI 状态；不要创建任何开发分支、不要 force push。
4. 最终向项目主人提供 **PASS / PARTIAL / FAIL 的逐条、可被独立复核报告**，列出仍难看的地方、最糟糕截图、无法验证事项和推荐 B6 方向。不能直接代替项目主人批准 Gate B5。

请直接开始实施；遇到权限/外部资产缺失等实质阻塞时停止并报告。不要把导入成功、构建成功或测试全绿自动等同于视觉合格。
