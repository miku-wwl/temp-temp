# 恢复检查点

最后更新：2026-11-07（Pacific/Auckland）

## 工作边界

- 只写入 D:\workshop\nov\temp-temp。
- D:\workshop\nov\devops-delivery 是只读参考仓；检查时它在 p12/manual 分支，已有用户未提交的教材变更和 evidence/p12/，不能覆盖或运行会生成文件的构建。
- 本实验不启动 Docker、Podman、WSL、Kubernetes、云资源或付费服务。
- Python/YAML 测试、PDF 中间文件、图像和证据均放在独立仓内；book/build/ 已加入 .gitignore。

## 当前检查点

- P01 代码、夹具、GitHub Actions 工作流、实验记录、中文版第 1 章、流程图、4 页 PDF 和页图已写入。
- 本地验证：20 项 unittest 通过；生产正例退出 0；非零延迟退出 1；重复键退出 2；PDF 元数据和 4 页图像已经检查。
- 首次测试中的开发/生产路径假设、PyYAML 对 workflow 键 on 的解析、XeLaTeX 无进度和 Edge 异步打印竞态均已修正，实情记录在 evidence/p01/local-validation.md。
- 尚需：检查 Git 状态/diff 和空白；确认 origin/main 最新仍是初始提交；提交并推送；通过 GitHub 读取远端 Actions 运行结果。若远端失败则修复并重新提交。
- P01 完成后，回到 docs/ROADMAP.md 选择 P02 镜像供应链实验。
- 若中断，先运行 git status --short --branch、git log -1 --oneline，阅读本文件和 evidence/p01/local-validation.md。不要重置、清理或改动其他工作区。

## P01 验收条件

1. 生产配置缺省或显式数值 0 通过；非零值、类型错配、重复键、格式错误和缺失输入有明确阻断及非零状态码。
2. GitHub Actions 对所有分支 push、pull request 及手动触发执行，不只覆盖 main。
3. 教材明确区分预部署策略门禁和部署后 Canary 的作用与边界。
4. PDF 实际生成并完成页图检查；证据只记录实际观察结果。
5. git diff --check 通过；变更只在本仓新增工作目录和根 README 中。
6. GitHub Actions 远端运行通过，记录 run URL 和状态。Branch protection 由仓库所有者设置，本任务未配置。
