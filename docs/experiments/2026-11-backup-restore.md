# P06：目录备份、完整性校验与恢复

## 目标与边界

在隔离目录间复制常规文件，生成包含路径、大小和 SHA-256 的清单；验证副本；将有效备份恢复到全新的目录并再次校验。备份和恢复均拒绝覆盖已存在目标。实现不保留所有权、ACL、时间戳或空目录，不是企业备份产品，也不防止攻击者同时改写数据和清单。

## 重现

```powershell
python -m unittest discover -v -s labs/backup-restore/tests
python labs/backup-restore/backup.py create --source labs/backup-restore/sample/source --destination evidence/p06/backup
python labs/backup-restore/backup.py verify --backup evidence/p06/backup
python labs/backup-restore/backup.py restore --backup evidence/p06/backup --destination evidence/p06/restored
python labs/backup-restore/backup.py verify --backup evidence/p06/restored
```

## 实测

Windows Python 3.13.9：10 项测试通过，2 项符号链接用例因宿主策略跳过。实际创建、验证、恢复和再次验证均成功，2 个文件的 inventory SHA-256 为 `b902fc9d26839a39cc7b1ffeaaaaaa9f0d93eb35e2bb006cc96eab68dc157482`。单测验证内容损坏、缺文件和既有目标拒绝。实际运行输出见 `evidence/p06/`；所有目录均是本实验夹具和证据，未触及用户数据。

已合入 PR #6（merge commit `81f136133ebc00cf2a748c91843c5007f13c6b52`）；两个 Actions run 的六个 job 全部通过。
