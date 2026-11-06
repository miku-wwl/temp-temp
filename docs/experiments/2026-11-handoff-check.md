# P08：值班交接包完整性校验

## 目标与边界

检查交接材料是否包含事件编号、服务、负责人、带时区的 ISO-8601 时间、唯一告警名、可访问的仓库内 runbook，以及 SHA-256 与实际字节相符的证据文件。拒绝绝对路径、路径穿越、符号链接和重复告警名。

它检查交接包的完整性和文件一致性，不证明告警真实、事故已解决或内容可信；没有连接 PagerDuty、Slack 或外部监控。

## 重现

```powershell
python -m unittest discover -v -s labs/handoff-check/tests
python labs/handoff-check/validate.py --root labs/handoff-check/sample --bundle handoff.json
```

## 实测

样例关联 `High5xxRate` 告警、runbook 和本地证据文件。测试覆盖缺字段、缺 runbook、内容变更、路径穿越、绝对路径、时区缺失、重复告警及坏摘要。完整验证输出见 `evidence/p08/`。
