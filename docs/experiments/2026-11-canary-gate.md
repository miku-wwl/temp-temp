# P04：Canary 指标门禁

## 目标

对隔离的 JSON 快照执行一次本地 rollout 决策。指标包含 baseline/canary 的请求数、错误数和 p95 延迟。策略先检查已观察到的错误率/延迟越界（立即 rollback），再检查样本量（不足则 hold），最后在全部通过时 promote。没有部署服务或真实流量。

默认门槛：错误率不超过 1%，且不比 baseline 高 0.5 个百分点；p95 不超过 500 ms，且不高于 baseline 的 1.5 倍；baseline 和 canary 均至少 100 个请求。门槛可经 CLI 参数调整。

## 重现

```powershell
python -m unittest discover -v -s labs/canary-gate/tests
python labs/canary-gate/gate.py labs/canary-gate/fixtures/promote.json
python labs/canary-gate/gate.py labs/canary-gate/fixtures/hold.json
python labs/canary-gate/gate.py labs/canary-gate/fixtures/rollback.json
```

退出码 0 表示输入有效且完成策略决策；`decision` 字段表示是否继续发布。无效指标或阈值返回退出码 2。规则是教学示例，不代表适用于任意服务的通用 SLO。

## 实测

Windows Python 3.13.9：10 项测试全部通过。三个样例输出分别为 `promote`、`hold` 和 `rollback`；rollback 样例同时超过错误率和 p95 门槛。首轮边界复核发现 JSON 解析器接受 `NaN`，会让比较结果失真；策略现拒绝非有限 p95 和非有限阈值，并有两项回归测试。完整命令输出见 `evidence/p04/`；GitHub Actions 结果待 PR。

已合入 PR #4（merge commit `a492c9929bff979e231049928a6024c23abe91dd`）；两个 PR Actions run 均包含全部通过的四个实验 job。
