# P05：SLI、SLO 与错误预算

## 目标与口径

从一份 HTTP 状态码计数 JSON 计算 availability SLI、SLO 是否满足、错误预算消耗百分比与剩余百分比。2xx/3xx 是成功，5xx 是失败，1xx/4xx 排除在 availability SLI 分母之外。默认目标 99.9%；调用者可用 `--target` 指定其他 (0, 1) 内的比例。

错误预算为 `eligible_requests * (1 - target)`；消耗比例为 `5xx / allowed_errors`。超过 100% 时剩余预算为负数，以显示超支幅度。展示数字舍入到 10 位小数，比较仍使用原始值。此简化演示不含时间窗口、流量加权、告警 burn-rate 或服务端点归因。

## 重现

```powershell
python -m unittest discover -v -s labs/slo-budget/tests
python labs/slo-budget/calculate.py labs/slo-budget/fixtures/within-budget.json
python labs/slo-budget/calculate.py labs/slo-budget/fixtures/exhausted.json
python labs/slo-budget/calculate.py labs/slo-budget/fixtures/at-target.json
```

## 实测

本地单测和三份输入夹具的输出保存在 `evidence/p05/`。夹具覆盖预算未耗尽、耗尽和恰好达到目标；单测检查 4xx 排除、3xx 成功、负值/布尔/非有限目标和无有效请求等边界。发现恰好达标时浮点舍入会留下极小的非零剩余值，已加断言并修正显示舍入。GitHub Actions 结果以 PR run 为准。

已合入 PR #5（merge commit `fa9f2099efa631e8a328b9dd9d7919adf5967745`）；PR Actions run `42295160863` 的五个实验 job 均通过。
