# SuperCalc · 为了计算，先体验

Windows 11 / Windows 10 2004+，x64。完整解压 ZIP 后运行 `SuperCalc.App.exe`，请保留同目录的资源文件。

## 选择下载

- `self-contained`：自包含包，携带 .NET 8 与 Windows App SDK，适合直接解压运行。
- `lightweight`：轻量包，不携带上述运行时。需要安装 x64 [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) 和与本应用 SDK 兼容的最新稳定版 [Windows App Runtime 1.8](https://learn.microsoft.com/windows/apps/windows-app-sdk/downloads)。运行时已经安装的电脑可使用此包。

文件名末尾是构建提交 SHA 的前 12 位；`BUILD.txt` 包含完整 SHA 和包类型。Actions 中两种包由并行任务分别上传，下载任意一个即可直接解压应用。Release 另外提供 `SHA256SUMS.txt`，包含两个 ZIP 的校验值。标签构建的文件名还包含四段版本号。

右上角“专注”可切换专注模式。账号、订阅、云、AI 和更新均为本地模拟，不收费、不上传数据。发布包无代码签名。

百分数为数学后缀（`200 + 10% = 200.1`），平方根使用近似值。完整功能和验证限制见仓库 README。
