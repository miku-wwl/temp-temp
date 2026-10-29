# Echoes of the Valley · Phase 5.2-B5（绘制环境）审阅包

**这是作者自评材料，不是验收。** `GATE_B5_RESULT=PARTIAL_OR_FAIL_UNTIL_OWNER_APPROVAL`、`GATE_B5_OWNER_APPROVAL=PENDING`、`HUMAN_VISUAL_APPROVAL=PENDING`、`HUMAN_GAMEPLAY_QA_STATUS=PENDING`、`WORLD_EXPANSION_ALLOWED=NO`。

- 所有截图和视频都来自真实 Windows Player，输入是**自动脚本（AUTOMATED_INPUT）**，没有人类试玩；视频没有声音。
- 对照图里的“B4”是同一个 B5 构建用 `-echoes-b5-before` 切回的 B4 画面：同一构建、同一位置、同一时刻。
- S1–S3 的图由取证脚本直接设定阶段（文件名写明 set-by-script），不是玩出来的；S0 是正常游戏状态。
- 标签：`SOURCE_ART`＝主人提供的 6 张原图（未改动，不是游戏画面）；`PRODUCTION_ART`＝脚本处理后游戏实际使用的图；`EDITOR_PREVIEW`＝编辑器渲染，仅供诊断；`REAL_PLAYER_SCREENSHOT` / `REAL_PLAYER_VIDEO`＝实机。

阅读顺序：
1. `comparison/index.html`：B4 vs B5 同构建同位置对照（广场、工坊正面/侧面、垂柳、泉水、花园、鸡舍、溪边、风车路口）；泉水、花园、广场、工坊、溪边、风车的 S0→S3；S1 泉水小路（B4 的低模花树曾挡住主角）；S3 的 B4 vs B5；遮挡对照（附实测“主角可见比例”）和最差遮挡；地表贴图 4×4 平铺接缝（原图 vs 修复）以及实机最远缩放下的地面；泉水两张原画与四个阶段；苗圃原画与抠出的植物；树原画与清理后的树；42/48/54 俯角；主角四向行走；对话、背包、暂停界面；小地图条。
2. `motion/actual-player-b5-motion-AUTOMATED_INPUT.mp4`：连续实机录像（自动输入，1080p；**录像帧率约 12.7 帧/秒，是逐帧读回并编码 JPEG 的采集速度，不是游戏帧率**；游戏在未录像的同路线运行中保持 60 FPS 上限）：广场待机与缩放、八方向、转圈、与 Mira 对话、工坊门口与工坊背后、垂柳下、采花、鸡舍旁的大树下、走进花园菜畦之间再出来、西路到泉水并调查、沿溪边树下、回广场。`motion/minimap-strip.png` 是录像帧里裁出的小地图。
3. `final-report.md`：Git 安全、B4 基线审计（五项差距的位置）、素材 A/B/C、实现、遮挡实测、停止规则逐条结果、测试、构建、性能（最终数据每种画面各一次；B4 在溪边卡住退出失败，对比为 PARTIAL）、**WHAT STILL LOOKS BAD**、未验证事项、未实施的视觉观察、状态字段。
4. `SOURCE_ART-owner-pack-not-game-footage/`、`PRODUCTION_ART/`、`asset-audit/`、`EDITOR_PREVIEW-not-player-evidence/`、`provenance/`（素材清单、AI 披露、授权边界、主人素材包说明）、`tests/`（完整 XML）、`logs/`、`source/`、`MANIFEST-SHA256.txt`、`LABELS.md`、`OMITTED-FILES-SHA256.txt`（未放进包里的截图及其哈希，原件留在本地）。

实机构建：`artifacts/build/phase52-b5-final/EchoesOfTheValley.exe`（构建号见报告；加 `-echoes-b5-before` 可在同一构建里看 B4）；B4 原构建已重建在 `artifacts/build/phase52-b4/`。

请重点判断：泉水 S0（脚本排干的枯池）到 S3 的变化是否可信、与地面是否融为一体；树木是否比 B2 低模好、树冠开孔（抖动镂空）在走动时是否舒服；草地和土壤的颜色与密度；花园菜畦（有碰撞、枯萎与盛开随剧情）是否好看；整体是否比 B4 更协调；以及仍然留着的风车、广场石块、鸡舍、灯柱等低模物件能否接受。


Finalization corrections: the final build source is e4e4e0fa2e6dd9cece55af9f3cf2d1206cad8e75. Fresh full Unity suites record all input hashes. Engineering and owner approval are separate: workshop body visibility is 0% (silhouette only), so stop rule 3 is FAIL. The B4 comparison route exited 1; performance parity is UNKNOWN. Separate 720p full-game smoke was NOT RUN. MP4 full decode PASS; actual GUI playback NOT VERIFIED because the owner stopped Computer Use with Escape. No B6 work was started. Read logs/finalization-audit.json and delivery-identifiers.json for exact identifiers.
