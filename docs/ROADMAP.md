# DevOps 实验与教材路线图

## 当前工作区

- 独立仓库：miku-wwl/temp-temp
- 基线：main；实验放入 labs/，教材放入 book/，证据放入 evidence/
- 当前状态：P01、P02 已验证并合入 main；P03 正在独立分支实施

## 阶段顺序

| 阶段 | 目标 | 验收证据 | 状态 |
|---|---|---|---|
| P01 | 生产 Helm values 策略门禁 | 20 项本地测试、远端 Actions success、main branch protection、实验记录、教材 PDF | VALIDATED |
| P02 | 用 SOURCE_DATE_EPOCH 生成可重复归档，比较两次 SHA-256，并证明内容变化会改变摘要 | 独立 Python 实验、Windows 本地结果、Linux Actions、教材第 2 章与页面渲染 | VALIDATED (MERGED) |
| P03 | 本地 SPDX SBOM、DSSE/Ed25519 签名与消费方策略校验 | Syft 实际扫描、负向测试、Linux Actions | IN PROGRESS |
| P04 | Canary 指标门禁：样本量、错误率、延迟与回滚边界 | 本地指标夹具和 promote/hold/rollback 测试 | PLANNED |
| P05 | SLI/SLO 与错误预算消耗计算 | 请求日志夹具、计算器与边界用例 | PLANNED |
| P06 | 备份清单、恢复校验与损坏检测 | 临时目录备份/恢复和篡改拒绝记录 | PLANNED |
| P07 | 滚动发布状态机模拟 | 就绪/不可用约束、失败与回滚测试 | PLANNED |
| P08 | 值班交接包完整性检查 | 本地告警/运行手册/证据索引校验 | PLANNED |

## 选择后续任务的规则

1. 先选能填补真实工程缺口、能在本机零付费验证的任务。
2. 每个实验先写资源边界、版本、命令、预期、实测、失败与清理。
3. 先本地隔离验证，再决定是否接入云或外部服务；不得声称未运行的外部验证已通过。
4. 每个阶段结束后更新本文件和 WORKLOG，再开始 P 阶段。所有 main 更新通过 PR 和必需状态检查。
