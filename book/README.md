# DevOps 软件交付与 SRE 实验教材

章节以独立实验为中心：先交代故障机制和设计约束，再给出可复现代码、实验步骤、实际输出和局限。PDF 与源 Markdown 同步保存。

## 章节

- [第 1 章：把生产故障配置挡在合并之前](chapters/01-prod-config-safety.md)
- [P01 实验代码](../labs/prod-config-guard/)
- [P01 实验记录](../docs/experiments/2026-11-prod-config-guard.md)
- [进度与恢复检查点](../docs/WORKLOG.md)

## 构建第 1 章 PDF

依赖：Pandoc、Microsoft Edge 与 Microsoft YaHei 字体；策略实验需要 requirements.txt 中的 PyYAML。

在仓库根目录运行以下命令，构建脚本会生成独立 HTML，再使用 Edge headless 打印 PDF：

~~~powershell
powershell -ExecutionPolicy Bypass -File book/tools/build_pdf.ps1
~~~

构建脚本会先运行 Pandoc。配图可通过 python book/tools/render_diagram.py 重绘。HTML、浏览器隔离 profile 和打印中间产物放在忽略的 book/build/；PDF 文件保存在 book/dist/。
