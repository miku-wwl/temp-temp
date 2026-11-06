# DevOps 软件交付与 SRE 实验教材

章节以独立实验为中心：先交代故障机制和设计约束，再给出可复现代码、实验步骤、实际输出和局限。PDF 与源 Markdown 同步保存。

## 章节

- [第 1 章：把生产故障配置挡在合并之前](chapters/01-prod-config-safety.md)
- [第 2 章：用 SOURCE_DATE_EPOCH 构建可重复归档](chapters/02-reproducible-archive.md)
- [P01 实验代码](../labs/prod-config-guard/)
- [P02 实验代码](../labs/reproducible-archive/)
- [P02 实验记录](../docs/experiments/2026-11-reproducible-archive.md)
- [进度与恢复检查点](../docs/WORKLOG.md)

## 构建 PDF

依赖：Pandoc、Microsoft Edge 与 Microsoft YaHei 字体。构建脚本默认拒绝覆盖已有 PDF；有意重建时追加 -Force。

重建第 1 章：

~~~powershell
powershell -ExecutionPolicy Bypass -File book/tools/build_pdf.ps1 -ChapterPath book/chapters/01-prod-config-safety.md -OutputName 01-prod-config-safety.pdf -Force
~~~

首次构建第 2 章：

~~~powershell
powershell -ExecutionPolicy Bypass -File book/tools/build_pdf.ps1 -ChapterPath book/chapters/02-reproducible-archive.md -OutputName 02-reproducible-archive.pdf
~~~

章节 HTML、Edge 隔离 profile 和打印中间文件放在忽略的 book/build/。P02 配图是可编辑的 SVG 源文件，可用文本编辑器更新。
