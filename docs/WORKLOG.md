# 恢复检查点

最后更新：2026-11-07（Pacific/Auckland）

## 工作边界

- 独立仓库：D:\workshop\nov\temp-temp。所有实验代码、教材、PDF 和证据均在本仓库中。
- 参考仓库：D:\workshop\nov\devops-delivery，只读。本任务仅读取 P10 章节中的构建可复现性条目；该仓在任务开始前已有用户修改，未写入或运行其工作流。
- 没有启动 Docker、Podman、WSL、Kubernetes、云资源或付费 API。P01/P02 只使用本地资源和公开仓库的标准 GitHub Actions runner。
- P01 PDF 和实验输入保持不变。P02 PDF 页面图像与原始日志保存在 evidence/p02/。

## 分支保护

- main 需要 production-config-guard 与 reproducible-archive 两个 status checks，strict=true。
- 所有 main 更新必须经 PR；审批数为 0；enforce_admins=true。
- 最终规则最近通过 GitHub API 读取确认。P01 的两个直接提交发生在最终 PR 规则生效之前；之后的 P02 通过受保护 PR 合并。

## P01 已完成

- 生产配置门禁校验 faults.errorRate 和 faults.latencyMs。
- 20 项本地测试、两次 GitHub Actions 与首章 4 页 PDF 均成功。
- 提交：873f54be5b503242354e8ec97447c80398cfef2b、047925a0d7f0725469a9dddee8058bf870874cf9。
- 实验与证据：docs/experiments/2026-11-prod-config-guard.md、evidence/p01/local-validation.md。

## P02 已完成：确定性 tar.gz

- PR #1 已合并，合并提交 3104a82ae1d863b738f3eabd21a7dee1cc46d57d；实现提交 18650c9129352088dc4fa1358d57f3641e4d7202。
- Push 与 PR 验证 run 42288716912、42288843939 均通过；补充记录提交 run 42289268526、42289274237 也通过。PR 合并后的 main push run 42289359295 两个 job 同样成功。production-config-guard 和 reproducible-archive 全绿。
- Linux Actions 在 ubuntu-24.04 / Python 3.13.18 运行 12 项测试全部通过，包括 POSIX 执行位与符号链接拒绝断言；样例归档 SHA-256 为 81e474718db3baf6bdeb7439b53bee8ce9bfeee8a309dca803e393df568b1edd。
- Windows 本地 Python 3.13.9：12 项中 10 项通过；chmod 和符号链接集成断言因宿主能力跳过。权限映射单元测试通过。
- 本地重放：固定 SOURCE_DATE_EPOCH 的两份 tar.gz SHA-256 相同，为 0e15bb81a0631b3f9343fc59a58636d251e47968515a68696714a87c309f2661；修改内容后变为 56a495c05cbf0af417d1bd9fabf0cfaa4a5651ae13e02c89a253bf4b35c771ce。完整实际输出见 evidence/p02/reproduction.log。
- 中文第二章 PDF：4 页 A4、347457 bytes；四页渲染通过视觉检查。代码、PDF、教材、实验记录与截图均在仓库内。
- 详见 docs/experiments/2026-11-reproducible-archive.md 与 evidence/p02/local-validation.md。

## 下一阶段 P03

继续沿用独立分支与受保护 PR。研究镜像供应链工件和消费方校验，先选零付费、可本地验证的路径；任何 OCI 镜像重建结论都必须基于真实构建器、基础镜像与依赖输入，不得从 tar.gz 实验外推。

## P03 执行记录：本地 SBOM 消费方验签

- 已完成并合入 PR #3；实现提交 `9d6bcf90665cddde38770c741110cb09242f7fcc`，合并提交 `b28108f21ab9460d00897e7b57992268a150ec51`；PR Actions run `42294129417` 三个 job 全绿。
- 实验范围收敛为源码目录，而非 OCI 镜像重建：Syft v1.55.0 实际扫描 `requirements.txt`，生成 SPDX 2.3；Python 消费方按 DSSE v1 PAE 用 Ed25519 验签，并检查源码树 SHA-256、subject 名、SPDX 与 `requests==2.31.0` 策略。
- Syft Windows 发布包 SHA-256 经 GitHub release metadata 与 checksums 文件核对：`ada4c9b93af575f479ce80fce6629897272a2d41f300a14bc49bace020767ca2`。工具实际报告 v1.55.0。
- 真实端到端结果：2 个 SBOM package；源码树 SHA-256 `01150085a4d3469a1905909c0b4aa1850359dfb79c9530e83a1b6eb00d74738b`；可信 key id `4ee1d7ca0889c0e8d574372d50663338cc6ebda6de4dc5dbf7ac042f89781b0f`；生成后消费方实际输出 `PASS`。私钥留在系统临时目录，未加入仓库；仅提交公钥、SBOM 和签名 envelope 作为可核查证据。
- 本地测试：11 项，10 通过；符号链接拒绝用例因 Windows 权限跳过。测试覆盖篡改 payload、错误信任密钥、源码漂移、错误 subject、缺包、脱离签名的 SBOM 与错误 payload type。Linux Actions job 已配置，等待 PR 结果。
- 工具恢复记录：第一次复合命令被执行策略拒绝；移除清理/管道步骤后，Syft 能正常运行。检查工作流时尝试读取不存在的 `ci.yml`，根据仓库文件清单改用实际的 `.github/workflows/lab-validation.yml`。
- 限制：未构建/扫描 OCI 镜像；这是本地自签名教学样例，不是 SLSA 构建证明、透明日志或生产信任根。
- 实验记录与执行证据：`docs/experiments/2026-11-supply-chain-consumer.md`、`evidence/p03/`。

## P03–P08 轻量连续执行范围

- P03：源码 SBOM/签名消费方校验。
- P04：Canary 指标门禁。
- P05：SLI/SLO 与错误预算计算。
- P06：备份和恢复完整性校验。
- P07：滚动发布状态机模拟。
- P08：值班交接包完整性检查。
- 每阶段保留最小代码、真实测试/验证和短记录；不为阶段单独生成 PDF。

## P04 执行记录：Canary 指标门禁

- 最小目标：对一份 baseline/canary 指标 JSON 计算 `promote`、`hold` 或 `rollback`；低样本量进入 hold，观察到错误率或 p95 越界则 rollback。
- 实验代码：`labs/canary-gate/`；本地测试和 fixture 结果保存在 `evidence/p04/`。
- 本地 Windows 验证：10 项测试通过；三个夹具分别输出 promote、hold、rollback。首轮边界复核发现 JSON `NaN` 可绕过延迟比较，已加入非有限数拒绝和回归测试。
- 已完成并合入 PR #4；实现提交 `0fdacb9d262a58dae13daa8d0a226cbe47a06425`，合并提交 `a492c9929bff979e231049928a6024c23abe91dd`；Actions runs `42294566158` 和 `42294608733` 全绿。首次普通 merge 被保护策略拒绝，改用 GitHub 建议的 auto-merge，未绕过保护。

## P05 执行记录：SLI 与错误预算

- 最小目标：从 HTTP 状态计数计算可用性、SLO 是否达标、错误预算消耗与剩余比例；按明确规则排除 4xx。
- 实验代码：`labs/slo-budget/`；输入夹具和本地输出位于 `evidence/p05/`。
- 已完成并合入 PR #5；实现提交 `015f30c222b9a696365211bf73a91aeed13cc7e5`，合并提交 `fa9f2099efa631e8a328b9dd9d7919adf5967745`；Actions run `42295160863` 五个 job 全绿。首次输出发现浮点尾差，精度断言修复后本地和 CI 通过。

## P06 执行记录：备份清单与恢复校验

- 最小目标：对隔离样例目录建立 SHA-256 文件清单，实际恢复到新目录并再次校验；篡改、缺文件与目标已存在都应安全拒绝。
- 实验代码：`labs/backup-restore/`；实际备份、恢复和验证记录位于 `evidence/p06/`。
- 本地 Windows 验证：10 项测试通过，2 项符号链接用例因宿主策略跳过；实际备份/校验/恢复 2 个文件成功，inventory SHA-256 `b902fc9d26839a39cc7b1ffeaaaaaa9f0d93eb35e2bb006cc96eab68dc157482`。
- 已完成并合入 PR #6；实现提交 `afe73b5e4b83e160d0209e0ce41ea4a1590d1b18`、证据字节保护提交 `493add1da3b3d94f17d10e5dd08d291254e0f16c`，合并提交 `81f136133ebc00cf2a748c91843c5007f13c6b52`；PR checks runs `42296001380` 与 `42296041323` 全绿。

## P07 执行记录：滚动发布状态模拟

- 最小目标：按 desired replicas、maxUnavailable 和每批就绪数模拟 complete、progressing 与 readiness 失败后的 rollback。
- 实验代码：`labs/rollout-state/`；测试和三种模拟输出位于 `evidence/p07/`。
- Windows 本地验证：8 项测试通过；模拟 `complete`、`progressing`、`rolled_back`。首轮测试发现遗漏了旧副本下线后的短暂可用数低谷，现记录下线前后状态；第二轮将 maxUnavailable=2 的预期最小可用数修正为 2。
- 当前分支：`p07-rollout-state`；本地验证完成，远端 CI 待确认。

## 恢复方式

恢复时先运行 `git status --short --branch`、`git log -3 --oneline`，查看 `docs/WORKLOG.md` 与 `docs/ROADMAP.md`，并检查当前阶段 PR/CI。P03–P06 已合入；当前恢复点为 `p07-rollout-state`。不要重置或清理工作区；写入新文件前先确认目标不存在。
