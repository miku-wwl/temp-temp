# P07 本地验证记录

- 日期：2026-11-07（Pacific/Auckland）
- OS / Python：Windows / Python 3.13.9
- 测试：8 项全部通过；完整输出见 `unit-tests.log`。
- 场景：4 副本按 1 个不可用完成更新，最小可用数 3；按 2 个不可用并只提供一批时报 `progressing`；第二批 readiness 不足时报 `rolled_back`，最终恢复 4 个可用旧副本。输出见 `scenarios.log`。
- 排错：第一轮测试暴露批次期间可用数漏记；现在同时记录旧副本下线后的值与新副本就绪后的值。回滚用例的 maxUnavailable=2，正确下界为 2，断言已按策略修正。
- 全部为状态机模拟，没有连接集群或执行部署。
- PR Actions 尚待运行。
