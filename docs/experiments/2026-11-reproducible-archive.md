# P02 实验记录：确定性 tar.gz 归档

| 项目 | 记录 |
|---|---|
| 日期 | 2026-11-07（Pacific/Auckland） |
| 参考问题 | 参考项目 P10 记录同一提交两次镜像构建 digest 不同，并提出固定 SOURCE_DATE_EPOCH |
| 实验位置 | labs/reproducible-archive/ |
| 成本 / 资源 | 本地 Python 标准库；GitHub Actions 标准 ubuntu-24.04 runner；没有容器、云资源或付费 API |
| 状态 | 本地与 push/PR GitHub Actions 验证通过；PR #1 已合并，P02 VALIDATED |

## 假设

若 tar 成员顺序、tar 头部元数据、gzip mtime 和 gzip 文件名均被固定，那么相同文件字节在同一 Python/zlib 工具链下可产生相同 tar.gz 字节。若输入内容变化，SHA-256 应变化。

## 方法

1. 朴素基线使用 tarfile 默认递归打包；只把临时副本中一个文件的 mtime 改变，再比较两份 SHA-256。
2. 确定性实现只读取普通文件，按 POSIX 相对路径排序，拒绝符号链接与特殊文件。
3. tar 的 mtime 设置为 SOURCE_DATE_EPOCH，uid/gid 归零，属主名称置空，权限规范为 0644/0755；gzip mtime 固定且文件名为空。
4. 将输出写到目标目录的临时文件，完整关闭后再原子发布；默认不覆盖已有输出。
5. 对照脚本先改变临时源文件 mtime，再比较重复确定性构建；最后改变临时文件内容并验证摘要变化。
6. 运行单元测试，在 GitHub Actions 中以 Linux runner 重复验证；PDF 单独渲染并检查页面。

## 本地结果

- Python 3.13.9；测试结果见 evidence/p02/unit-tests.log。12 项测试中 10 项通过，chmod 与符号链接集成断言因 Windows 宿主能力跳过；POSIX 执行位映射逻辑通过。
- 完整实测命令和摘要见 evidence/p02/reproduction.log。朴素样例的两次摘要不同；固定 epoch 的两次归档摘要均为 0e15bb81a0631b3f9343fc59a58636d251e47968515a68696714a87c309f2661；改动内容后为 56a495c05cbf0af417d1bd9fabf0cfaa4a5651ae13e02c89a253bf4b35c771ce。
- 两个确定性包在源文件 mtime 不同的情况下字节完全相同。重新运行实验时，朴素归档的实际摘要会随运行环境和当前时间变化。
- PDF：4 页 A4、347457 bytes、Tagged=yes、未加密；PDF 元数据见 evidence/p02/pdfinfo.txt。四页在 120 dpi 渲染并已逐页检查，页面图像与记录见 evidence/p02/local-validation.md。

## 调试过程

1. 第一次 unittest discovery 找不到 pack 模块。原因是测试目录被加入 sys.path，而实验源码在上一级；在测试入口显式加入实验目录后，发现测试。
2. 首次 Windows 执行位集成断言失败。观察到 Windows chmod 不暴露 POSIX 执行位；同时 Windows 符号链接创建受系统权限策略限制。增加平台无关的 0644/0755 规则断言，在 Windows 跳过这两项宿主集成测试；Linux Actions 会运行权限和符号链接拒绝检查。
3. 对源文件检查增加权限位与身份快照，避免测试中途权限变化被静默接收。归档器仍不提供目录锁；并发写入时应使用只读快照。

## 范围与局限

这只验证 tar.gz 封装，不是源码编译实验，也不等价于 Docker/OCI 镜像可复现性。Python/zlib 版本、操作系统、压缩实现、输入路径规则以及源文件本身都可能影响跨工具链结果。本实现也没有建造不可变快照，不能抵御有意的并发路径替换。

参考资料：[SOURCE_DATE_EPOCH 文档](https://reproducible-builds.org/docs/source-date-epoch/)、[SOURCE_DATE_EPOCH 规范](https://reproducible-builds.org/specs/source-date-epoch/)、[Python gzip](https://docs.python.org/3.13/library/gzip.html)、[Python tarfile](https://docs.python.org/3.13/library/tarfile.html)。

## 远端验证与恢复点

- P02 commit：18650c9129352088dc4fa1358d57f3641e4d7202。
- 代码 Push run [42288716912](https://github.com/miku-wwl/temp-temp/actions/runs/42288716912) 与 PR run [42288843939](https://github.com/miku-wwl/temp-temp/actions/runs/42288843939) 的两个 job 均成功。文档证据提交对应的 Push run [42289268526](https://github.com/miku-wwl/temp-temp/actions/runs/42289268526) 与 PR run [42289274237](https://github.com/miku-wwl/temp-temp/actions/runs/42289274237) 也都成功。Linux job 运行 12 项测试全部通过，并生成样例归档 SHA-256 81e474718db3baf6bdeb7439b53bee8ce9bfeee8a309dca803e393df568b1edd。
- 当前 main 保护要求以上两个状态检查、PR 与 0 个审批，并对管理员生效；保护规则已通过 GitHub API 复核。
- PR [#1](https://github.com/miku-wwl/temp-temp/pull/1) 已合并，merge commit 为 3104a82ae1d863b738f3eabd21a7dee1cc46d57d；合并后的 main push run [42289359295](https://github.com/miku-wwl/temp-temp/actions/runs/42289359295) 两个 job 均成功。main 保护规则包含两个必需状态检查、PR 与管理员约束。下一阶段为 P03 镜像供应链材料和消费方校验。
