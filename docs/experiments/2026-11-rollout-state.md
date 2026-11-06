# P07：滚动发布状态机模拟

## 目标与边界

模拟副本逐批替换时的最小状态：`complete`、`progressing` 或 `rolled_back`。每批先移除不超过 `max_unavailable` 个旧副本，再读取新副本就绪数；低于批次需求时模拟回滚到旧副本全部可用。无 Kubernetes API、控制器时钟、自动扩缩容或真实部署。

输入给定 `desired`、`max_unavailable` 和 `batch_ready_counts`。模拟记录每批期间可用副本数，并报告全过程最小可用数。

## 重现

```powershell
python -m unittest discover -v -s labs/rollout-state/tests
python labs/rollout-state/simulate.py labs/rollout-state/fixtures/complete.json
python labs/rollout-state/simulate.py labs/rollout-state/fixtures/progressing.json
python labs/rollout-state/simulate.py labs/rollout-state/fixtures/rollback.json
```

## 实测

Windows Python 3.13.9：8 项测试通过；三个结果夹具输出 complete、progressing、rolled_back。首轮测试发现初版只记录新副本 ready 后的可用数，漏掉下线旧副本后的瞬间低谷；现分别记录两时点并按实际 maxUnavailable 修正边界断言。CI 会重复执行单测与三个模拟。模拟用于解释发布状态边界，不代表 Kubernetes 的全部 rolling update 语义。
