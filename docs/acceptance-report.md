# P03–P08 连续工程实验验收报告

日期：2026-11-07（Pacific/Auckland）
仓库：`miku-wwl/temp-temp`
结论：**6/6 phases 完成**。每阶段均有独立代码、可运行验证、记录和通过保护规则合入 `main` 的 PR。

## Phase 结果

| Phase | 交付与本地验证 | PR / 合并提交 | 可核查证据 |
|---|---|---|---|
| P03 | Syft SPDX 2.3、DSSE/Ed25519 消费方验证；11 项运行，10 通过、1 项 Windows 符号链接跳过；实际扫描 2 个 package，验签 PASS | #3；`9d6bcf9` → `b28108f` | `labs/supply-chain-consumer/`、`evidence/p03/` |
| P04 | Canary 门禁；10 项通过；输出 promote / hold / rollback | #4；`0fdacb9` → `a492c99` | `labs/canary-gate/`、`evidence/p04/` |
| P05 | Availability SLI、SLO 和错误预算；8 项通过；夹具验证剩余预算 50%、超支 50%、达标 0% | #5；`015f30c` → `fa9f209` | `labs/slo-budget/`、`evidence/p05/` |
| P06 | 文件清单、备份/恢复；10 项运行，8 通过、2 项 Windows 符号链接跳过；2 个文件恢复后摘要一致 | #6；`afe73b5`、`493add1` → `81f1361` | `labs/backup-restore/`、`evidence/p06/` |
| P07 | maxUnavailable 滚动发布模拟；8 项通过；complete / progressing / rolled_back | #7；`8da1912` → `0715223` | `labs/rollout-state/`、`evidence/p07/` |
| P08 | 值班交接包校验；10 项运行，9 通过、1 项 Windows 符号链接跳过；真实 CLI 输出 PASS | #8；`a1aac2c` → `d9ddd41` | `labs/handoff-check/`、`evidence/p08/` |

P03–P08 本机总计运行 **57 项**：53 通过、4 项因 Windows 主机策略跳过符号链接用例。GitHub Actions 各阶段 Linux job 全部成功，并执行了适用的 POSIX/符号链接用例。

## 远端验证

- P03 PR Actions：run `42294129417`，3 个 job 全绿。
- P04：runs `42294566158`、`42294608733`，4 个 job 全绿。
- P05：run `42295160863`，5 个 job 全绿。
- P06：runs `42296001380`、`42296041323`，6 个 job 全绿。
- P07：runs `42296511990`、`42296549557`，7 个 job 全绿。
- P08：runs `42297007440`、`42297043623`，8 个 job 全绿。

所有阶段均通过 PR #3–#8 合入 main；没有使用管理员绕过。main 的 branch protection 继续强制原有 `production-config-guard` 和 `reproducible-archive` 两个状态检查；每个 PR 还等待本阶段新增 job 全部完成并成功后才合入。

## 自动排错

- P03：本机拒绝包含清理/管道的复合命令；改成独立、非清理命令后 Syft 可用。仓库没有预期的 `ci.yml`，定位后使用真实工作流 `.github/workflows/lab-validation.yml`。
- P04：JSON `NaN` 会绕过 p95 数值比较；加入有限值校验及回归测试。
- P05：恰好达标时浮点尾差显示为极小非零剩余；添加精确断言并规范显示舍入。
- P06：拒绝悬空符号链接作为输出目标；用 `.gitattributes` 固定哈希保护文件的原始字节，避免换行转换。
- P07：首轮测试发现漏记“旧副本下线、新副本就绪前”的最低可用数；增加中间状态并修正 maxUnavailable=2 的期望值。
- GitHub 合并偶有策略状态刷新延迟；等待检查完成并用普通或 GitHub 支持的自动合并流程重试，没有启用管理员绕过。

## 未覆盖与限制

- P03 扫描的是本地源码目录，不是 OCI 镜像；未构建 SLSA provenance、OIDC/keyless 身份或透明日志。教学签名私钥留在本机临时目录，仓库只保留公钥、SBOM 和签名 envelope。
- P04、P05、P07 使用合成数据和纯本地模拟；没有 Canary 真实流量、生产服务、集群或监控接入。
- P06 只校验相对路径、大小和内容摘要；不保留 ACL、所有权、时间戳或空目录，也不防止攻击者同时改写文件与清单。
- P08 只验证仓库内的合成交接包；未连接 PagerDuty、Slack 或外部告警服务。
- 没有启用付费资源；没有启动 Docker、Podman、Kubernetes 或云环境。
- GitHub Actions 对固定版本的 checkout action 给出 Node.js 20 弃用提示；它是非阻塞注释，所有 job 均通过，本次未改动 action 版本。
