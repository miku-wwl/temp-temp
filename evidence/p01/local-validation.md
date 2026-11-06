# P01 本地验证证据

日期：2026-11-07（Pacific/Auckland）
工作目录：D:\workshop\nov\temp-temp

## 资源与工具

- Windows 本地运行；Python 3.13.9、PyYAML 6.0.3、Pandoc 3.8、Microsoft Edge HeadlessChrome/156.0.0.0。
- 没有启动 Docker、Podman、WSL、Kubernetes、云资源或付费 API。
- GitHub workflow 只做了本地静态检查，尚未推送触发远端 Actions。

## 实际验证

### 策略与 CLI

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

### 教材 PDF

- 输出：[第 1 章 PDF](../../book/dist/01-prod-config-safety.pdf)
- PDF 信息：4 页、A4、416345 bytes、PDF 1.4、Tagged=yes、未加密。
- 页面检查：Poppler <code>pdftoppm</code> 渲染四页，逐页检查中文字体、配图、代码块、表格、边距和分页；没有发现裁切或缺字。
- 页面图像：<code>pdf-page-1.png</code> 至 <code>pdf-page-4.png</code>，都保存在本目录。
- 构建命令：<code>powershell -ExecutionPolicy Bypass -File book/tools/build_pdf.ps1</code>；实际记录见本文件后的调试过程。

## 调试记录

1. 首轮 16 项测试有 1 项失败：测试误以为通用策略函数能从 YAML 内容判断 dev/prod。修正后让开发夹具明确保留非零值，并证明该值若被当作 prod 输入会被拒绝；工作流只将生产路径传给门禁。
2. 首轮工作流静态检查使用 PyYAML SafeLoader；YAML 1.1 会将键 <code>on</code> 解释为布尔值，导致检查脚本错误地显示无触发器。测试改用 BaseLoader 按字面读取工作流并断言 push、pull_request 无分支过滤。最终 20 项测试通过。
3. 首次 XeLaTeX PDF 构建启动后没有写入日志或输出；经检查仍在运行的 xelatex 进程约一分钟无进展，随后中断。随后尝试 Pandoc HTML + Edge；第一次遇到 Edge 异步打印，检查过早发生。构建脚本现在等待临时 PDF 文件大小稳定后复制为正式文件。第二次构建成功。
4. Edge 在写出 PDF 时打印一个 Chromium renderer fallback 诊断，但进程返回成功，PDF 可由 pdfinfo 读取，四页渲染检查通过。

## 当前结论

P01 的本地策略、正反例、CI 配置范围静态检查和 PDF 均已验证。远端 GitHub Actions 结果和仓库 branch protection 尚未验证或配置；因此项目路线图仍保持 <code>IN_PROGRESS</code>，不会把远端状态写成通过。
