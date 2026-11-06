# P03：本地 SPDX SBOM 消费方验签

## 目标与边界

验证本地源码依赖清单能否被实际扫描、签名，并由独立消费方按策略拒绝错误工件。使用 Syft v1.55.0、SPDX 2.3、in-toto Statement v1、DSSE v1 和临时 Ed25519 密钥；不需要容器引擎、注册表、OIDC、透明日志或付费服务。

本实验不构建 OCI 镜像，也不证明构建平台身份或 SLSA 等级。演示私钥由本机生成，仅用于教学；真实生产信任必须由外部受控密钥分发与身份策略建立。

## 实验对象

`labs/supply-chain-consumer/sample/` 包含一个最小 Python 文件和 `requests==2.31.0`。`attest.py create` 调用 Syft 对目录生成 SPDX JSON，再将 SPDX predicate 放入 in-toto Statement，以 DSSE PAE 签名。源码摘要由排序后的相对路径、文件长度和文件内容 SHA-256 计算；时间戳不参与摘要，符号链接与特殊文件拒绝。

消费方 `verify` 从显式提供的公钥计算 key id，验证 DSSE 签名、payload type、Statement/predicate 类型、唯一 subject、subject 名与源码树摘要；可选地要求分离的 SBOM 与已签名 predicate 完全一致，并执行 `name==version` 包策略。

## 重现

在仓库根目录安装锁定依赖并运行测试：

```powershell
python -m pip install -r labs/supply-chain-consumer/requirements.txt
python -m unittest discover -v -s labs/supply-chain-consumer/tests
```

使用已下载并经 SHA-256 核验的 Syft v1.55.0，密钥应放在仓库之外：

```powershell
$keyDir = Join-Path $env:TEMP 'p03-demo-key'
$syft = 'C:\path\to\syft.exe'
New-Item -ItemType Directory -Path $keyDir
python labs/supply-chain-consumer/attest.py keygen --private-key "$keyDir/private.pem" --public-key "$keyDir/public.pem"
python labs/supply-chain-consumer/attest.py create --source labs/supply-chain-consumer/sample --subject-name supply-chain-lab --source-version 0.1.0 --syft $syft --private-key "$keyDir/private.pem" --sbom evidence/p03/sbom.spdx.json --envelope evidence/p03/attestation.dsse.json
python labs/supply-chain-consumer/attest.py verify --source labs/supply-chain-consumer/sample --subject-name supply-chain-lab --envelope evidence/p03/attestation.dsse.json --public-key "$keyDir/public.pem" --sbom evidence/p03/sbom.spdx.json --expected-package requests==2.31.0
```

示例中的临时目录需在重跑前换成新的目录；输出已存在时工具默认拒绝覆盖。

## 实测结果

- Syft 发布包 `syft_1.55.0_windows_amd64.zip` SHA-256：`ada4c9b93af575f479ce80fce6629897272a2d41f300a14bc49bace020767ca2`，与官方 checksums 文件一致。
- 实际扫描识别 2 个 package，包含 `requests` 2.31.0。
- 签名 envelope 验证通过，源码摘要为 `01150085a4d3469a1905909c0b4aa1850359dfb79c9530e83a1b6eb00d74738b`。
- Windows 本地单测：11 项运行，10 通过，1 项因 Windows 符号链接权限跳过。Linux Actions job 已配置，CI 结果以 PR run 为准。
- 负向测试拒绝 payload 篡改、错误公钥、修改后的源码、错误 subject、缺失策略包、被替换的 detached SBOM 和错误 payload type。

实际产物与公钥见 `evidence/p03/`。私钥不在仓库内。这个实验验证的是自签名 envelope 的一致性与本地消费策略；它没有提供独立身份认证或公开透明日志。
