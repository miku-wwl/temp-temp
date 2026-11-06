# P08 本地验证记录

- 日期：2026-11-07（Pacific/Auckland）
- OS / Python：Windows / Python 3.13.9
- 测试：10 项运行，9 项通过，1 项因 Windows 不允许创建符号链接而跳过。
- 真实 CLI：样例交接包输出 `PASS`；验证了 1 个告警、仓库内 runbook 和证据文件 SHA-256 `bc947f29b0c73112ab591e2d6191430e7f61134cf7159fe1838ac88c1757b445`。输出见 `validation-output.log`。
- 负向用例拒绝证据篡改、缺失 runbook、路径穿越、绝对路径、无时区时间、重复告警和错误摘要格式。
- 证据文件的原始字节受 `.gitattributes` 保护，避免换行转换造成摘要漂移。
- 仅验证仓库内合成样例，没有访问外部告警或值班服务。
- PR Actions 尚待运行。
