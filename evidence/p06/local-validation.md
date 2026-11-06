# P06 本地验证记录

- 日期：2026-11-07（Pacific/Auckland）
- OS / Python：Windows / Python 3.13.9
- 测试：10 项运行，8 项通过，2 项因 Windows 不允许创建符号链接而跳过；覆盖损坏、缺失、既有目标、错误路径和恢复。
- 真实命令：备份样例目录 -> 验证清单 -> 恢复到 `evidence/p06/restored/` -> 再验证恢复目录，全部成功；完整 stdout 见 `commands.log`。
- 文件数：2；inventory SHA-256：`b902fc9d26839a39cc7b1ffeaaaaaa9f0d93eb35e2bb006cc96eab68dc157482`。
- 清单内每个文件的大小与 SHA-256 也已对照 Git index blob 核验；`.gitattributes` 对样例、备份和恢复副本关闭换行转换，避免 Windows checkout 改变受清单保护的字节。
- 清单只覆盖相对路径、文件大小和文件内容 SHA-256；不保存 ACL/所有权/时间戳/空目录，也不防止攻击者同时修改数据与清单。
- 只在本仓库新建的样例与证据目录内运行，未读取或写入用户数据。
- PR Actions 尚待运行。
