# P02 本地验证证据

日期：2026-11-07（Pacific/Auckland）

仓库：D:\workshop\nov\temp-temp
分支：p02/reproducible-archive

## 环境与范围

- Windows 本地，Python 3.13.9；实现仅用标准库。
- Pandoc 3.8、Microsoft Edge Headless 156.0.0.0、Poppler 26.07.0。
- 未启动 Docker、Podman、WSL、Kubernetes 或云资源；无付费 API。
- 参考仓没有写入或执行其工作流。

## 回归测试

命令：

~~~powershell
python -B -m unittest discover -v -s labs/reproducible-archive/tests
~~~

结果：12 项测试中 10 项通过，chmod 与符号链接两项 Windows 平台集成断言跳过，进程结论 OK。完整逐项输出见 unit-tests.log。

覆盖：epoch 必填与环境变量读取、epoch 边界、固定 mtime、gzip 头时间、tar 成员顺序和归一化 owner/mode、内容变化、朴素元数据漂移、输出路径与覆盖保护、POSIX 执行位映射、文件与目录符号链接拒绝。

Windows 的 chmod 不暴露 POSIX 执行位，且符号链接创建受权限策略影响，所以这两项宿主集成断言在本机跳过。Linux Actions 应运行全部 12 项测试，包括实际执行位与符号链接拒绝检查。

## 对照实验

命令：

~~~powershell
python -B labs/reproducible-archive/reproduce.py
~~~

完整命令行、临时目录和原始摘要见 reproduction.log。一次本地结果：

| 构建 | SHA-256 | 预期 |
|---|---|---|
| 朴素 #1 | df716e330924b63f793628119036af9e3de03d15ae620295ebc36988f62d146a | 与朴素 #2 不同 |
| 朴素 #2 | 0fb1c2f101f1812e693209eb852efecf7d614eaac2de9f144cd58157ea8cb626 | 与朴素 #1 不同 |
| 归一化 #1 | 0e15bb81a0631b3f9343fc59a58636d251e47968515a68696714a87c309f2661 | 与归一化 #2 相同 |
| 归一化 #2 | 0e15bb81a0631b3f9343fc59a58636d251e47968515a68696714a87c309f2661 | 与归一化 #1 相同 |
| 改动内容 | 56a495c05cbf0af417d1bd9fabf0cfaa4a5651ae13e02c89a253bf4b35c771ce | 与原内容不同 |

朴素摘要是某一次实测值，不是跨运行的固定预期值；tar/gzip 默认时间和源文件 mtime 会使它漂移。稳定摘要仅说明当前样例、规则和 Python/zlib 工具链中的重复结果，不代表跨平台或容器镜像可复现。

## CI 工作流静态检查

使用 PyYAML BaseLoader 解析工作流并断言两个 job、push/PR 触发、contents: read 和 SHA 固定的第三方 action，结果 PASS。静态检查原始输出未单独存档。GitHub Actions 尚待 PR 运行，不能将 Linux 集成测试记为已通过。

## 教材 PDF

输出：[第 2 章 PDF](../../book/dist/02-reproducible-archive.pdf)。

- pdfinfo：4 页，A4（594.96 × 841.92 pt），347457 bytes，PDF 1.4，Tagged=yes，未加密。
- Edge headless 返回成功并生成 PDF；标准输出含一条 Chromium renderer fallback 诊断。Poppler 随后正常读取文件并渲染全部四页。
- 页面渲染：120 dpi，文件 pdf-page-1.png 至 pdf-page-4.png 位于本目录。
- 视觉检查：中文字体和符号、SVG 图、表格、代码块、页边距和分页均清楚；没有发现裁切、重叠、缺字或越界。
- 可复现 PDF 构建命令见 book/README.md；默认不覆盖已存在 PDF。

## 未完成的外部验证

分支还没有推送；Linux GitHub Actions、PR required checks 和合并后的 branch protection 更新尚未执行。下一次恢复从 docs/WORKLOG.md 的 P02 恢复点继续。P02 仅是 tar.gz 封装实验，不提供 OCI 镜像或 Docker build 的可复现性结论。
