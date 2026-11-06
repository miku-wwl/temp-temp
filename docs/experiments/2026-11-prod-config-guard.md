# P01 实验记录：生产配置故障注入门禁

| 项目 | 记录 |
|---|---|
| 日期 | 2026-11-07（Pacific/Auckland） |
| 参考缺口 | 参考项目 P10 练习提出：生产 values 中的 faults.errorRate 与 faults.latencyMs 必须为 0 或缺省，并应成为合并门禁 |
| 实验位置 | labs/prod-config-guard/ |
| 成本 / 资源 | 本地 Python 测试、公开仓库的标准 GitHub Actions runner；没有云资源、容器、集群、付费 API 或用户凭据 |
| 实验状态 | **VALIDATED**：本地测试、远端 Actions、main required status check 与 PDF 均已验证 |

成本依据：仓库为公开仓库，工作流只使用标准 ubuntu-24.04 runner。GitHub 文档说明公开仓库使用标准 GitHub-hosted runners 免费且不限量：[GitHub-hosted runners reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)。

## 问题与假设

只修改 GitOps 配置的变更可能绕开应用镜像的合成测试。假设在任何分支的 PR/推送流水线中解析实际生产 values，并把已知故障注入字段设为允许值，可以在配置进入集群前阻断这类变更。

## 实验设计

1. 使用 SafeLoader 解析 YAML；重复键、坏格式和缺失文件失败关闭。
2. 校验根文档与 faults 的结构；两个目标字段缺省或数值为 0 时通过。
3. 对非零数值、布尔值、字符串、NaN/无穷值和结构错误返回阻断。
4. 单元测试覆盖生产安全样例、危险反例、解析反例、merge 值和 CLI 退出码。
5. GitHub Actions 对所有分支 push、pull request 与 workflow_dispatch 执行同一验证。
6. 构建章节 PDF 并检查每页图像。

## 实测结果

- 单元测试：20 项全部通过；详见 [验证证据](../../evidence/p01/local-validation.md)。
- 生产配置 CLI：退出码 0，输出 PASS。
- 非零延迟 CLI：退出码 1，输出 BLOCK。
- YAML 重复键 CLI：退出码 2，安全解析错误。
- workflow 静态检查：push、pull_request、workflow_dispatch 均存在，push/PR 没有分支过滤；检查命令仅传入 labs/prod-config-guard/envs/prod/values.yaml。
- 远端 GitHub Actions：[run #1](https://github.com/miku-wwl/temp-temp/actions/runs/42283738283)，提交 873f54be5b503242354e8ec97447c80398cfef2b，结论 success。20 项回归测试与生产 values 检查都成功。
- 当前 main protection 已复核：strict required status checks production-config-guard 与 reproducible-archive；所有人（含管理员）必须经 PR 并通过检查；审批数为 0。P01 提交发生在最终 PR 规则生效之前。
- PDF：4 页 A4，已生成并完成逐页视觉检查。

- 后续记录提交 047925a0d7f0725469a9dddee8058bf870874cf9 的 Actions [run #2](https://github.com/miku-wwl/temp-temp/actions/runs/42284531560) 也成功；它验证了当时 main 上的检查点。

## 调试与修正

首轮测试错误地期待通用策略函数根据文件内容区分开发与生产值。实际边界应由 CI 指定文件路径，因此测试改为验证开发夹具保留非零值，而生产检查只传入 prod 路径。

首次使用 SafeLoader 做工作流检查时，YAML 1.1 将键 on 解析为布尔值。测试改用 BaseLoader 读取 GitHub Actions 语法的字面键，并明确断言没有分支过滤。

MiKTeX XeLaTeX 在首次编译中无日志、无进度，已中止。Pandoc HTML + Edge headless 构建第一次因 Edge 异步写入而过早检查；脚本增加了等待 PDF 大小稳定的步骤，第二次成功。完整记录见本地验证证据。

## 局限与后续

当前规则只检查源 values 中两个字段，没有运行 Helm 模板渲染。源 values 尚未经过 Helm 模板渲染，因此真实仓库仍应对最终渲染结果检查同一不变量。main 当前已强制所有变更经 PR 且通过 production-config-guard 与 reproducible-archive 两项必需状态检查（审批数 0，管理员也受约束）。
