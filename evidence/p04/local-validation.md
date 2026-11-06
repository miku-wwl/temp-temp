# P04 本地验证记录

- 日期：2026-11-07（Pacific/Auckland）
- OS / Python：Windows / Python 3.13.9
- 测试：10 项全部通过；完整输出见 `unit-tests.log`。
- CLI 夹具：`promote.json` -> `promote`；`hold.json` -> `hold`；`rollback.json` -> `rollback`。完整输出见 `fixtures-results.log`。
- 默认错误率界限为 `min(1%, baseline + 0.5 个百分点)`；p95 界限为 `min(500 ms, baseline * 1.5)`；两侧请求数至少 100 才能 promote。
- 排错：复核 Python JSON 非标准 `NaN` 行为后发现它可绕过 `>` 越界比较；拒绝 NaN/Infinity 并新增 p95 和阈值回归测试。
- 仅使用合成输入；没有部署 canary、接收真实流量或触发实际回滚。
- PR Actions 尚待运行。
