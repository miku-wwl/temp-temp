# Claude Code Opus 5.5 Max — Echoes 5.2-B4「高质量静态场景 + 轻量人物动画」落地 Prompt

你负责《Echoes of the Valley》Unity 2.5D 游戏美术升级。**请直接执行，不只写规划。** 本轮主人已经明确修正方向：他提供的两张游戏截图展示了想要的 2.5D——**更高的固定俯视角度，精细茂密、丰富而静态的植物与建筑环境**；人物可以保留精美立绘，只要适度动作，不需要花草树木房屋动起来。整体应融合这些截图的世界密度/表现方式与温暖、青春、明快的日式炼金奇幻 RPG 气质（质量参考《莱莎》系列，但禁止复制其受版权保护的形象、纹理、地图、UI 和其他具体元素）。

## 项目与文件

- Repo `D:\workshop\oct\echoes-of-the-valley`，Unity 6000.3.27f1，URP 17.3.0，Windows x64 Mono；低配性能基准 Radeon 780M。
- B3 历史基线：`art/phase52-b3-2d5d`，commit `c829e65aa55bb68faac9aa7f6b720550b4b69ebf`；EditMode 229/229，PlayMode 96/96，实际 player smoke PASS；**这些不是 B4 结果**。
- 主人会提供 `Echoes-B4-Original-Art-Assets-20261028.zip`。请先在仓库外解压和核验 `SHA256SUMS.txt`，然后读取 `README.zh-CN.md` 和 `docs/ASSET-QA.md`。
- ZIP 已有 22 张本轮生成的新原创图像：S/N/E/W 主角自然站姿 4 张；四方向 run-keypose 源稿 4 张；静态斜视角工坊/垂柳/泉水 3 张；植物纹理 5 张；墙瓦木石材质 4 张；小地图装饰框 1 张；2.5D 场景视觉目标概念 1 张。另有用户先前提供的 3 张角色/NPC 参考图。
- 本轮只允许 Phase 5.2-B4 已有世界中的**广场、炼金工坊、垂柳/泉水**垂直切片；不要 Phase C/D 扩图。Jam deadline 2026-11-08 20:59 NZDT。

## 0 — 安全与基线 Gate

1. 读 `AGENTS.md`、当前架构、B3 report；检查 `git status`, `branch`, `HEAD`, main/B3 提交祖先关系，记录真实状态。仓库可能已合入 main，**不能假定历史分支仍是最新**。任何未提交工作都属于主人，不能 `git reset --hard` 或覆盖；若遇冲突及时停下报告。
2. 建独立 B4 pilot 场景/开关、可回退 commit；保护旧 RC1/RC2、B2/B3、存档、全部任务/炼金/Resonance/结局流程及 E 交互。**别改别的项目，不要自动推送、删分支或发布 itch。**
3. 用 asset audit 把图分为三类：A 可经抠图清理后进入游戏，B 仅作为美术指导，C 未达到标准需退回。尤其 `b4_isometric_style_target.png` 是目标画面概念，**不是可直接贴进整个场景的背景，也不代表已实现**。

## 1 — 统一美术语言，按实机观感决定做法

视觉锚点 A（用户两张 2.5D 实机截图）：固定高角度三分之四构图、清晰路径、细节丰富的植被/岩石/建筑、不依赖树木和房子运动，屏幕内的场景具有可信体积、前中后景层次，角色比例较小但可辨认。**不要照搬截图中的 UI 堆栈、夸张 BOSS/金色宫殿/水晶造型、地图布置和角色。**

视觉锚点 B（温暖日式炼金奇幻）：草绿与黄绿花簇、奶白砂石、琥珀色木梁、砖红屋瓦、少量金铜细节与冷青炼金晶体点光；画面柔和鲜亮、手绘感、细腻而不过度写实；主角依旧是原创马尾棕发、青色金边外套、红围巾、米白裙摆、腰间炼金包的女孩。

1. 优先让**静态美术本身**好看，千万别把“2.5D”理解为“物体是一个薄卡片就够”。可以选择固定角度多层插画/2.5D billboard + 真实地面/碰撞；也可以保留 3D 体积贴高级手绘材质，**允许混合**。取决于实际玩家镜头里哪种画质好、排序可信和工作量可控。**树木、花草、房屋不要求任何动画**。
2. 使用 `environment/isometric-static-props/`：工坊、垂柳、遗迹泉水是静态斜俯视插画**候选**；其透视须和实际相机精确匹配，拆分前景/背景和遮挡层、对齐地面世界坐标、材质剔除、player 与其交互和遮挡均要真实。如果整体大图占地形过多、透视错、人物穿屋顶，请不强行使用：转为材质/建模参考，用现有 3D 交互根节点展示同等精度的视觉层。
3. 适量利用 `environment/foliage/` 和 `environment/materials/`；原图有可能带背景柔光、不能保证无缝贴图。alpha 清理、纹理接缝处理、足底排序、分层视差后才启用。不要让半透明大卡片遮住人物、形成明显灰绿矩形，或者把原本地形碰撞拆坏。
4. 优先场景对比：工坊正门与侧面、广场主路径、垂柳阴影与泉水，共 3 处固定机位 before/after。树冠与植被应有细密叶形、小花与自然变化；房屋要看起来是可爱的真实村屋而非单盒子；若有余力再做石墙、花箱、小招牌和细节。**禁止大范围扩建新房屋、增加无关剧情和大型装饰系统。**
5. 画面层次和色调经过现有 S0-S3 恢复状态的真实测试，保持不同阶段可辨认；保留既有 canopy fade/叶片遮挡治理。角色与环境在色温和阴影上统一，不要人物照片感/场景低模的割裂。

## 2 — 相机：从偏平视切到 2.5D 高斜俯视

- B3 `FixedFollowCamera25D` 默认 pitch≈30°, yaw≈25°。本轮请尝试 pitch **42° / 48° / 54°**；默认起点 48°。先看代码确认角度定义，避免把镜头意外往下压低。
- 以用户两张参考图的高斜俯视空间感为布局目标，保留固定 yaw、WASD、滚轮缩放；校准 distance/FOV/look-ahead，使人物既能看清衣装又不会遮住前方小路。
- 防相机穿屋、树枝前景遮挡，车间/泉水交互和剧情特殊镜头保留；测试 720p/1080p、缩放极限、角色转向和路线边缘。
- 记录最终 pitch、距地高度、FOV 和对比截图，不宣称“画面达标”直到真实 player 由人审阅。

## 3 — 人物：保留画稿、轻量动态即可

- 用户喜欢当前人物立绘的细节，不要废弃原创造型或换 3D。把 `characters/idle/` 的四方向自然双臂下垂站姿作为新的候选，保持 S/N/E/W 视觉身高、足底 pivot、衣装与包的一致性。
- `characters/run-keyposes/` 每张画了约 4 个姿态，**不是已经切好的 sprite sheet**；需要抽取/分离姿态，修脸、遮挡、alpha 与红色/灰色光晕。只用合格帧，必要时使用现有 B3 的距移动速度匹配/IK 流程做有限插值；**不要整张图平移假装走路**。
- 动画目标是合理**微动**：站立呼吸、马尾/围巾/裙摆轻轻摆、WASD 移动时真实迈步和手臂幅度、停步过渡顺畅。无需为每个环境对象制作风动，也不必搞 3D 骨架换装。
- 保持原 PlayerController 的 collider、交互根、坐标、存档、速度和游戏逻辑不变；遇到极端方向立绘质量不一致先标明并回退，不要为了自评 PASS 牺牲真实画质。

## 4 — 左上任务/右上小地图等 UI

- 右上实现**真实小地图**，不是仅显示 `ui/minimap_frame.png`。允许用现有地形生成静态北向地图底图，通过 world X/Z bounds 把玩家箭头、转向、工坊、泉水、风车、任务目标准确映射上去；也可选低频正交 RenderTexture，但不能影响 Radeon 780M。
- 小地图可使用装饰框，建议 200–240px @1080p；状态框 Coins/Clover 下移或侧移，不能互相覆盖；左上任务牌保留但别扩大。720p/1080p、对话/暂停/背包/Continue 不越界且地图状态正确。
- 追求参考图“右上可以看清地图”这一功能和美观，**不要复制 MMO 复杂 HUD/顶部几十个队友血条**。

## 5 — 质量与预算

1. 只在已有场景选择 3 个代表性静态地点做可比较的风格整合；减少透明大面积 overdraw，尽可能维持 B3 在 Radeon 780M 上的 1080p 60FPS capped unrecorded route 水平（这是**目标，不是自动承诺**）。
2. B3 全量 EditMode/PlayMode 测试回归（此前 229/229、96/96），新增镜头、minimap、透明排序、角色姿态帧与玩法回归测试；原有测试不得删除/弱化。重新构建真实 Windows exe 到 `artifacts/build/phase52-b4/`。
3. 截取实际运行的 Windows player：固定相机 42/48/54 对比、人物四向 idle/行走、同一位置三个场景前后、地图位置更新、遇到树冠和工坊墙时的遮挡。录制**至少 30 秒自动连续移动**，注明是自动输入；真正手动玩家 QA 仍 PENDING。
4. 出 `artifacts/visual-qa/phase-5.2/b4/final-report.md` + 比较页 + 图片 + 录像 + 测试 XML + hash + 原图用量/修改记录 + review ZIP。报告必须列出用了哪些新素材、哪些因为边缘或透视问题被拒绝/只作为参考。不要把概念图冒充实机。
5. 仅在确认源码、测试、build 与可回退提交均安全的情况下本地 commit；用户曾倾向合并 main，但**先检查 branch 的实际状态，并在画面 Gate B 人工确认之后再做正常 merge**；禁止自行 push 或提交 itch。
6. **Stop rule**：相机更难操作、小地图是假图、树木/建筑遮挡严重、角色像贴纸滑动、世界和人物违和、旧玩法回归失败、性能明显退化，任一存在则 `PARTIAL`/`FAIL`，不得宣布 PASS，不得进 Phase C/D。

## 必须交付的最终汇报格式

```text
PHASE52_B4_STATUS=
SOURCE_BRANCH=
SOURCE_COMMIT=
ART_DIRECTION=HIGH_ANGLE_STATIC_25D + WARM_ALCHEMY_ANIME
ASSET_ZIP_VERIFICATION=
NEW_ASSETS_USED=
REJECTED_ASSETS_AND_REASONS=
HERO_IDLE_STATUS=
HERO_LIGHT_ANIMATION_STATUS=
CAMERA_PITCH_FINAL=
MINIMAP_REAL_WORLD_STATUS=
STATIC_ENVIRONMENT_VISUAL_STATUS=
WORLD_CHARACTER_STYLE_COHERENCE=
EDITMODE_STATUS=
PLAYMODE_STATUS=
WINDOWS_BUILD_STATUS=
ACTUAL_PLAYER_SCREENSHOTS=
CONTINUOUS_REAL_PLAYER_MOVEMENT_VIDEO=
Radeon_780M_1080P_PERFORMANCE=
GATE_B_RESULT=PARTIAL_OR_FAIL_UNTIL_OWNER_APPROVAL
GATE_B_OWNER_APPROVAL=PENDING
HUMAN_VISUAL_APPROVAL=PENDING
HUMAN_GAMEPLAY_QA_STATUS=PENDING
WORLD_EXPANSION_ALLOWED=NO
REMOTE_PUSH_STATUS=NOT_ATTEMPTED
ITCH_SUBMISSION_STATUS=NOT_SUBMITTED
REVIEW_ZIP=
NEXT_ACTION=Owner plays actual B4 Windows build, assesses 2.5D visual fidelity
```

**核心验收：真实游戏画面要明显接近目标的 2.5D 视觉密度与暖色系；花草树木房屋可以完全静止，人物少量自然运动。实际视觉表现比纯代码完成率更重要。**
