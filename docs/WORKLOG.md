# 恢复检查点

最后更新：2026-11-07（Pacific/Auckland）

## 工作边界

- 独立仓库：D:\workshop\nov\temp-temp。只在该仓库中创建实验、教材、PDF 和证据。
- 参考仓库：D:\workshop\nov\devops-delivery，只读。本任务只读取 P10 章节中“同一提交两次构建 digest 不同，固定 SOURCE_DATE_EPOCH”这一条目；参考仓库原本已有用户修改，且期间修改文件清单继续扩大。没有在参考仓库中写文件、运行构建或启动其环境。
- 不启动 Docker、Podman、WSL、Kubernetes、云资源或付费 API。P02 使用本地 Python 标准库和公开仓库的 GitHub-hosted runner。
- P01 PDF 与实验输入保持不变；P02 页面图像与日志放在 evidence/p02/。

## 仓库与分支保护

- 起点 main：047925a0d7f0725469a9dddee8058bf870874cf9；从此提交创建 p02/reproducible-archive。
- main 的远端保护规则最近读取确认：required status production-config-guard，strict=true；必须经 PR，审批数 0；enforce_admins=true。
- P01 的两个提交在最终 PR 保护规则生效前直接推送。保护规则生效之后，本任务在 P02 分支工作，后续 main 变更必须走 PR。
- P01 两次远端 Actions 已成功：run 42283738283 与 42284531560；证据见 evidence/p01/local-validation.md。

## P01 已完成

- P01：生产配置 faults.errorRate / faults.latencyMs 合并门禁；20 项本地测试通过，GitHub Actions 成功，首章 PDF 4 页并完成逐页检查。
- 提交：873f54be5b503242354e8ec97447c80398cfef2b 与 047925a0d7f0725469a9dddee8058bf870874cf9。
- 实验与验证证据：docs/experiments/2026-11-prod-config-guard.md、evidence/p01/local-validation.md。

## P02 进行中：确定性 tar.gz

- 目标：用标准库生成确定性归档，固定路径顺序、tar 时间/uid/gid/owner/mode 与 gzip 时间/名称；拒绝 symlink/特殊文件、源内输出与默认覆盖。
- 代码：labs/reproducible-archive/；CI 在 .github/workflows/lab-validation.yml 新增独立 job。
- 本机 Python 3.13.9：12 项测试中 10 项通过，Windows chmod 与符号链接集成断言按平台跳过；实际对照重放中，朴素归档摘要不同，固定 epoch 后摘要相同，改变内容后摘要变化。
- 原始日志：evidence/p02/unit-tests.log、evidence/p02/reproduction.log。
- 教材：book/chapters/02-reproducible-archive.md，SVG 源图位于 book/assets/reproducible-archive.svg；PDF 已生成（4 页 A4、347457 bytes）；四页已渲染并逐页检查，图像位于 evidence/p02/pdf-page-1.png 至 pdf-page-4.png。
- 分支当前尚未推送；GitHub Actions 尚未运行。下一步：复核 staged diff；提交并推送该分支；创建 PR，等待两个验证 job 成功；将 reproducible-archive 加入 main required checks；复核规则后合并 PR。

## 恢复方式

运行 git status --short --branch 与 git log -3 --oneline，阅读本文件，再查看 labs/reproducible-archive/、book/chapters/02-reproducible-archive.md 和 evidence/p02/。不要重置或清理工作区；先确认文件存在性后续写，避免重复覆盖已有证据。
