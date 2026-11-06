# 恢复检查点

最后更新：2026-11-07（Pacific/Auckland）

## 工作边界

- 只写入 D:\workshop\nov\temp-temp。
- D:\workshop\nov\devops-delivery 是只读参考仓；任务开始时本地副本是 p12/manual，已有用户改动。工作期间该仓的未提交文件清单继续扩大；本任务没有在其中写入或运行构建。
- 实验不启动 Docker、Podman、WSL、Kubernetes、云资源或付费 API，除非后续任务另有明确、低成本的本地隔离方案。
- PDF 中间文件、页图和证据都在独立仓内；book/build/ 已加入 .gitignore。

## P01 已完成

- 本地生产配置策略、测试、20 个测试用例、GitHub Actions 和中文版首章/PDF 已提交并推送。
- 代码提交：873f54be5b503242354e8ec97447c80398cfef2b。
- GitHub Actions run #1 成功：https://github.com/miku-wwl/temp-temp/actions/runs/42283738283。
- main strict required status check 已设为 production-config-guard。没有强制 PR 审批或禁止管理员直接推送；此限制已记录。
- 证据：evidence/p01/local-validation.md；实验：docs/experiments/2026-11-prod-config-guard.md。
- 参考仓仍只读；没有使用其中的共享运行环境。

## 下一任务 P02：确定性制品

参考项目 P10 记录了同一提交重复构建得到不同镜像摘要的问题，并提出固定构建时间输入。先用 Python 标准库构建确定性 tar.gz：稳定路径顺序、文件元数据、uid/gid 和 mtime；用 SOURCE_DATE_EPOCH 固定 gzip/tar 时间；重复构建应有相同 SHA-256，修改内容应产生不同摘要。这个本地归档实验不等价于 Docker 镜像可复现构建，教材会明确边界。

预计目录：labs/reproducible-archive/、docs/experiments/、book/chapters/、evidence/p02/。先查看 P01 完成后 git status 和 GitHub main 状态，再创建这些新路径。保持参考仓只读，禁止覆写已存在路径。

恢复方式：先运行 git status --short --branch、git log -3 --oneline，阅读本文件，再按 P02 目标继续；若已开始 P02，查 labs/reproducible-archive/ 与 docs/experiments/ 的现状，不重置、不清理。
