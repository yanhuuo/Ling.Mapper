# 手动发布 runtime / generator 双包

仓库遵守禁止 CI 的要求：`.github/workflows/` 不保留自动工作流。原 `publish.yml` 已删除，不保留工作流内容副本；如需追溯可查看 Git 历史，不应恢复自动发布。任何 GitHub 标签均只标记源码，不触发本仓库自动打包/上传。

1. 核对两个 csproj 版本一致，审查修复和说明，完成目标框架编译、全量控制台与独立双包消费测试。`dotnet test` 不执行此仓库的控制台套件。
2. 提交已审查源码，记录最终 commit；标签精确指向同一 commit。打标签前再次确认 `.github/workflows/` 无自动触发文件，且用户已授权标签。
3. 使用最终 commit 的干净源码与官方 .NET SDK、NuGet 源构建；分别 `dotnet pack src/Ling.Mapper.csproj` 与 `dotnet pack generators/Ling.Mapper.Generator/Ling.Mapper.Generator.csproj`，传入 `-c Release -p:RepositoryCommit=<最终commit>`。不要覆盖旧版本产物。
4. 记录两包 SHA256、nuspec id/version/repository commit、runtime 四 TFM 内容和 analyzer 位置。通过只消费新包的项目验证，不用 ProjectReference 冒充包消费。
5. 经用户授权，手动上传两包；不提交 API key、不记录或输出凭据、不触发自动发布。上传成功仍需等待 NuGet 校验及索引。
6. 从官方 feed 重新下载两包，允许新加的 NuGet repository signature，但校验其余全部 payload 与上传包一致；用全新缓存、仅官方源恢复并运行消费者/回归，才确认公开发布完成。

Native AOT 执行、JIT 生成器消费、四TFM编译是不同证据；任何未执行或环境阻塞项必须单独报告。历史 Native AOT 成绩不能替代新版本实际执行。
