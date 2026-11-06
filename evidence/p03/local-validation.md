# P03 本地验证记录

- 日期：2026-11-07（Pacific/Auckland）
- OS：Windows；Python 3.13.9；cryptography 46.0.3
- Syft：1.55.0，Windows amd64；release zip SHA-256 `ada4c9b93af575f479ce80fce6629897272a2d41f300a14bc49bace020767ca2`，与官方 checksums 文件匹配。
- 命令：`python -m unittest discover -v -s labs/supply-chain-consumer/tests`
- 结果：11 项运行，10 通过，1 项因 Windows 符号链接权限跳过；完整输出见 `unit-tests.log`。
- 端到端：Syft 实际扫描 `labs/supply-chain-consumer/sample/`，SPDX 包数 2；Ed25519 签名后的 DSSE envelope 由消费方使用仓库内公钥成功验证，并匹配 detached SPDX 和策略 `requests==2.31.0`。命令输出见 `consumer-verify.log`，版本输出见 `syft-version.txt`。
- 源码树 SHA-256：`01150085a4d3469a1905909c0b4aa1850359dfb79c9530e83a1b6eb00d74738b`。
- 签名 key id：`4ee1d7ca0889c0e8d574372d50663338cc6ebda6de4dc5dbf7ac042f89781b0f`。
- 私钥保存在系统临时目录，未提交。`public-key.pem`、`sbom.spdx.json`、`attestation.dsse.json` 为验证证据，不代表生产身份或信任根。
- PR Actions 尚待运行；远端结果须在合并前确认。
