# P01 验证证据

日期：2026-11-07（Pacific/Auckland）
独立仓：D:\workshop\nov\temp-temp

## 资源与工具

- Windows 本地运行；Python 3.13.9、PyYAML 6.0.3、Pandoc 3.8、Microsoft Edge HeadlessChrome/156.0.0.0。
- 没有启动 Docker、Podman、WSL、Kubernetes、云资源或付费 API。
- GitHub 仓库为公开仓库，只使用标准 ubuntu-24.04 runner。GitHub 文档说明公开仓库使用标准 GitHub-hosted runners 免费且不限量：[GitHub-hosted runners reference](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)。

## 本地策略与 CLI

命令：

~~~powershell
python -B -m unittest discover -v -s labs/prod-config-guard/tests
~~~

结果：**20 tests，全部通过**。覆盖零值和缺省、错误率与延迟非零、布尔/字符串/NaN/超大整数、YAML 类型错误、重复键、merge 值、merge 冲突、坏 YAML、缺失文件、CLI 退出码以及工作流分支范围和生产路径。

CLI 正反例：

~~~text
production: exit_code=0
PASS: production fault injection is disabled or absent.
bad-latency: exit_code=1
BLOCK: faults.latencyMs must be numeric 0 or be omitted (got 800)
duplicate-key: exit_code=2
ERROR: cannot safely read labs\prod-config-guard\fixtures\prod-duplicate-key.yaml: while constructing a mapping
~~~

复现命令：

~~~powershell
python labs/prod-config-guard/guard.py labs/prod-config-guard/envs/prod/values.yaml
python labs/prod-config-guard/guard.py labs/prod-config-guard/fixtures/prod-bad-latency.yaml
python labs/prod-config-guard/guard.py labs/prod-config-guard/fixtures/prod-duplicate-key.yaml
~~~

## GitHub Actions 实际运行

- 提交：873f54be5b503242354e8ec97447c80398cfef2b。
- Run #1：[Independent lab validation](https://github.com/miku-wwl/temp-temp/actions/runs/42283738283)，事件 push，结论 success。
- Job production-config-guard：success；checkout、Python 3.13 设置、PyYAML 安装、20 项回归测试与生产 values CLI 检查均成功。
- 当前 branch protection 已读取确认：strict required status checks production-config-guard 与 reproducible-archive；PR 审批数为 0；enforce_admins=true。P01 提交发生在最终 PR 规则生效前。

- Run #2：[Independent lab validation](https://github.com/miku-wwl/temp-temp/actions/runs/42284531560)，提交 047925a0d7f0725469a9dddee8058bf870874cf9，结论 success。

## 后续保护规则校正

- P01 提交之后将 main 保护强化为必须通过 PR，要求 production-config-guard 成功，审批数为 0，并对管理员生效。P02 工作改为分支 PR；每次完成后复核远端规则。

## 教材 PDF

- 输出：[第 1 章 PDF](../../book/dist/01-prod-config-safety.pdf)。
- PDF 信息：4 页、A4、416345 bytes、PDF 1.4、Tagged=yes、未加密。
- 页面检查：Poppler pdftoppm 渲染四页，逐页检查中文字体、配图、代码块、表格、边距和分页；没有发现裁切或缺字。
- 页面图像：pdf-page-1.png 至 pdf-page-4.png，都保存在本目录。
- 构建命令：powershell -ExecutionPolicy Bypass -File book/tools/build_pdf.ps1。

## 调试记录

1. 首轮 16 项测试有 1 项失败：测试误以为通用策略函数能从 YAML 内容判断 dev/prod。修正后让开发夹具明确保留非零值，并证明该值若被当作 prod 输入会被拒绝；工作流只将生产路径传给门禁。
2. 首轮工作流静态检查使用 PyYAML SafeLoader；YAML 1.1 会将键 on 解释为布尔值，导致检查脚本错误地显示无触发器。测试改用 BaseLoader 按字面读取工作流并断言 push、pull_request 无分支过滤。最终 20 项测试通过。
3. 首次 XeLaTeX PDF 构建启动后没有写入日志或输出；经检查仍在运行的 xelatex 进程约一分钟无进展，随后中断。随后尝试 Pandoc HTML + Edge；第一次遇到 Edge 异步打印，检查过早发生。构建脚本现在等待临时 PDF 文件大小稳定后复制为正式文件。第二次构建成功。
4. Edge 在写出 PDF 时打印一个 Chromium renderer fallback 诊断，但进程返回成功，PDF 可由 pdfinfo 读取，四页渲染检查通过。

## 当前结论

P01 的本地策略、正反例、两次远端 Actions、required status check 和 PDF 均已验证。当前 main 还要求经 PR 并通过 required checks，审批数为 0，且该规则对管理员生效。P01 提交发生在最终保护规则生效前。
