# P05 本地验证记录

- 日期：2026-11-07（Pacific/Auckland）
- OS / Python：Windows / Python 3.13.9
- 测试：8 项全部通过；完整结果见 `unit-tests.log`。
- CLI 夹具：未耗尽 -> 50% budget remaining；耗尽 -> -50%；恰好达到 99.9% -> 0%。完整结果见 `fixtures-results.log`。
- SLI 分母只包含 2xx/3xx 与 5xx；夹具中的 4xx 被计入 `excluded_requests`，不计入可用性分母。
- 排错：第一版恰好达标时输出 `8.88e-14%` 剩余预算；补充精确零值断言后，将展示数字舍入到 10 位小数，测试和三种夹具重跑通过。
- 仅使用合成状态码计数；不代表某个真实服务的时间窗口或生产 SLO。
- PR Actions 尚待运行。
