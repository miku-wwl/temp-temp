# 恢复检查点

最后更新：2026-11-07（Pacific/Auckland）

## 工作边界

- 独立仓库：D:\workshop\nov\temp-temp。所有实验代码、教材、PDF 和证据均在本仓库中。
- 参考仓库：D:\workshop\nov\devops-delivery，只读。本任务仅读取 P10 章节中的构建可复现性条目；该仓在任务开始前已有用户修改，未写入或运行其工作流。
- 没有启动 Docker、Podman、WSL、Kubernetes、云资源或付费 API。P01/P02 只使用本地资源和公开仓库的标准 GitHub Actions runner。
- P01 PDF 和实验输入保持不变。P02 PDF 页面图像与原始日志保存在 evidence/p02/。

## 分支保护

- main 需要 production-config-guard 与 reproducible-archive 两个 status checks，strict=true。
- 所有 main 更新必须经 PR；审批数为 0；enforce_admins=true。
- 最终规则最近通过 GitHub API 读取确认。P01 的两个直接提交发生在最终 PR 规则生效之前；之后的 P02 通过受保护 PR 合并。

## P01 已完成

- 生产配置门禁校验 faults.errorRate 和 faults.latencyMs。
- 20 项本地测试、两次 GitHub Actions 与首章 4 页 PDF 均成功。
- 提交：873f54be5b503242354e8ec97447c80398cfef2b、047925a0d7f0725469a9dddee8058bf870874cf9。
- 实验与证据：docs/experiments/2026-11-prod-config-guard.md、evidence/p01/local-validation.md。

## P02 已完成：确定性 tar.gz

- PR #1 已合并，合并提交 3104a82ae1d863b738f3eabd21a7dee1cc46d57d；实现提交 18650c9129352088dc4fa1358d57f3641e4d7202。
- Push 与 PR 验证 run 42288716912、42288843939 均通过；补充记录提交 run 42289268526、42289274237 也通过。PR 合并后的 main push run 42289359295 两个 job 同样成功。production-config-guard 和 reproducible-archive 全绿。
- Linux Actions 在 ubuntu-24.04 / Python 3.13.18 运行 12 项测试全部通过，包括 POSIX 执行位与符号链接拒绝断言；样例归档 SHA-256 为 81e474718db3baf6bdeb7439b53bee8ce9bfeee8a309dca803e393df568b1edd。
- Windows 本地 Python 3.13.9：12 项中 10 项通过；chmod 和符号链接集成断言因宿主能力跳过。权限映射单元测试通过。
- 本地重放：固定 SOURCE_DATE_EPOCH 的两份 tar.gz SHA-256 相同，为 0e15bb81a0631b3f9343fc59a58636d251e47968515a68696714a87c309f2661；修改内容后变为 56a495c05cbf0af417d1bd9fabf0cfaa4a5651ae13e02c89a253bf4b35c771ce。完整实际输出见 evidence/p02/reproduction.log。
- 中文第二章 PDF：4 页 A4、347457 bytes；四页渲染通过视觉检查。代码、PDF、教材、实验记录与截图均在仓库内。
- 详见 docs/experiments/2026-11-reproducible-archive.md 与 evidence/p02/local-validation.md。

## 下一阶段 P03

继续沿用独立分支与受保护 PR。研究镜像供应链工件和消费方校验，先选零付费、可本地验证的路径；任何 OCI 镜像重建结论都必须基于真实构建器、基础镜像与依赖输入，不得从 tar.gz 实验外推。

## 恢复方式

先运行 git status --short --branch、git log -3 --oneline 和 gh pr view 1。确认 main 与 origin/main 同步后，阅读本文件和 docs/ROADMAP.md，再创建 P03 分支。不要重置或清理工作区；写入新文件前先确认目标不存在。
