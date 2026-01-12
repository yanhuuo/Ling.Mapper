# Ling.Mapper 文档中心

<div align="center">

**快速、类型安全的对象映射库**

[![NuGet](https://img.shields.io/nuget/v/Ling.Mapper.svg)](https://www.nuget.org/packages/Ling.Mapper/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](../LICENSE)

</div>

---

## ?? 文档导航

### ?? 入门指南

| 文档 | 描述 | 适合人群 |
|------|------|----------|
| [项目主页](../README.md) | 介绍、安装、快速开始 | ?? **首次使用必读** |
| [功能概览](Feature-Summary.md) | 核心功能和特性列表 | 了解项目能力 |

### ?? 核心功能

| 文档 | 描述 | 关键词 |
|------|------|--------|
| [Adapt 方法使用](Adapt-Usage.md) | Adapt 方法的用法和配置选项 | 映射、回调、选项 |
| [枚举类型支持](EnumConversion_Support.md) | enum ? int/string | 枚举转换 |

### ??? 进阶主题

| 文档 | 描述 | 技术级别 |
|------|------|----------|
| [代码质量改进](Code-Quality-Improvements.md) | 代码质量和性能优化 | ?? 中级 |
| [异常处理快速指南](Exception-Handling-Quick-Guide.md) | 异常处理策略 | ?? 初级 |
| [异常处理核心](Exception-Handling-Core-Understanding.md) | 异常处理的核心概念 | ??? 中级 |
| [异常处理改进](Exception-Handling-Improvements.md) | 异常处理的改进历史 | ?? 中级 |
| [异常策略变更](Exception-Handling-Strategy-Change.md) | 异常处理策略的变更 | ?? 中级 |

### ?? 版本历史

| 版本 | 主要变更 |
|------|----------|
| v1.0.5 | ?? 可空类型支持 |
| v1.0.4 | ??? 警告修复 |
| v1.0.3 | ? 运行时规则 |

---

## ?? 快速查找

### 按场景查找

<table>
<tr>
<td width="50%">

**我想要...**

- ?? [快速上手](../README.md#-快速开始)
- ?? [安装配置](../README.md#-安装)
- ?? [基本映射](../README.md#-示例)
- ?? [集合映射](../README.md#集合映射)
- ?? [DI 集成](../README.md#di-集成)

</td>
<td width="50%">

**我遇到...**

- ? [int? → int 转换](#)
- ? [enum → string 转换](EnumConversion_Support.md)
- ? [忽略大小写](Adapt-Usage.md)
- ? [处理异常](Exception-Handling-Quick-Guide.md)

</td>
</tr>
</table>

### 按类型查找

| 需求类型 | 推荐文档 |
|---------|---------|
| **类型转换** | [枚举类型](EnumConversion_Support.md) |
| **运行时配置** | [Adapt 使用](Adapt-Usage.md) |
| **错误诊断** | [异常处理](Exception-Handling-Quick-Guide.md) |
| **代码质量** | [质量改进](Code-Quality-Improvements.md) |

---

## ?? 推荐学习路径

### ?? 初学者（30 分钟）

```
1. 项目主页 (10 min)
   ↓
2. 功能概览 (10 min)
   ↓
3. Adapt 使用 (10 min)
   ↓
? 开始使用
```

**阅读顺序**：
1. [项目主页](../README.md) - 了解项目、安装配置
2. [功能概览](Feature-Summary.md) - 核心功能列表
3. [Adapt 使用](Adapt-Usage.md) - 第一个映射示例

### ?? 进阶用户（1 小时）

```
1. Adapt 方法使用 (20 min)
   ↓
2. 枚举类型支持 (20 min)
   ↓
3. 异常处理 (20 min)
   ↓
? 掌握核心功能
```

**阅读顺序**：
1. [Adapt 使用](Adapt-Usage.md) - 运行时灵活配置
2. [枚举类型支持](EnumConversion_Support.md) - 枚举转换技巧
3. [异常处理快速指南](Exception-Handling-Quick-Guide.md) - 错误处理

---

## ?? 常见问题快速索引

| 问题 | 答案位置 | 相关文档 |
|------|---------|---------|
| 如何安装？ | [README.md](../README.md#-安装) | - |
| 基本映射怎么写？ | [README.md](../README.md#-快速开始) | [Adapt 使用](Adapt-Usage.md) |
| 如何忽略大小写？ | [Adapt 使用](Adapt-Usage.md) | - |
| 如何处理 null？ | [Adapt 使用](Adapt-Usage.md) | - |
| enum → int？ | [枚举类型](EnumConversion_Support.md) | - |
| 性能优化？ | [README.md](../README.md#-性能) | [代码质量](Code-Quality-Improvements.md) |
| DI 集成？ | [README.md](../README.md#di-集成) | - |

---

## ?? 实际应用场景

### 场景 1: API 响应映射

```csharp
[HttpGet]
public async Task<IActionResult> GetUsers()
{
    var users = await _repository.GetAllAsync();
    return Ok(users.AdaptList<UserDto>());
}
```

**相关文档**: [Adapt 使用](Adapt-Usage.md)

### 场景 2: 数据库映射

```csharp
var dbRow = await _connection.QueryFirstAsync<DbRow>(sql);
var user = dbRow.Adapt<User>(AdaptOptions.IgnoreUnderscoreOption);
```

**相关文档**: [Adapt 使用](Adapt-Usage.md), [枚举类型](EnumConversion_Support.md)

### 场景 3: 第三方 API 集成

```csharp
var apiData = await _client.GetAsync<ApiResponse>();
var model = apiData.Adapt<DomainModel>(AdaptOptions.IgnoreCaseOption);
```

**相关文档**: [Adapt 使用](Adapt-Usage.md)

---

## ?? 文档完整性

| 功能领域 | 文档 | 示例 |
|---------|------|------|
| 基础映射 | ? | ? |
| Profile 配置 | ? | ? |
| Adapt 选项 | ? | ? |
| 枚举类型 | ? | ? |
| 集合映射 | ? | ? |
| DI 集成 | ? | ? |
| 异常处理 | ? | ? |

---

## ?? 贡献指南

发现文档问题或有改进建议？

<table>
<tr>
<td width="33%">

**报告问题**
1. [提交 Issue](https://github.com/yanhuuo/Ling.Mapper/issues)
2. 标记 `documentation`
3. 详细描述问题

</td>
<td width="33%">

**改进建议**
1. Fork 项目
2. 修改文档
3. 提交 PR

</td>
<td width="33%">

**补充示例**
1. 在测试项目中添加
2. 更新相关文档
3. 提交 PR

</td>
</tr>
</table>

---

## ?? 外部资源

<div align="center">

| 资源 | 链接 | 说明 |
|------|------|------|
| ?? **NuGet** | [下载](https://www.nuget.org/packages/Ling.Mapper/) | 最新版本 |
| ?? **GitHub** | [仓库](https://github.com/yanhuuo/Ling.Mapper) | 源代码 |
| ?? **Issues** | [问题跟踪](https://github.com/yanhuuo/Ling.Mapper/issues) | Bug 报告 |
| ?? **Discussions** | [讨论区](https://github.com/yanhuuo/Ling.Mapper/discussions) | 问答交流 |

</div>

---

## ?? 文档维护信息

<table>
<tr>
<td width="50%">

**文档状态**
- ? 活跃维护中
- ?? 最后更新：2024
- ?? 当前版本：v1.0.5
- ?? 语言：简体中文

</td>
<td width="50%">

**贡献者**
- ????? [yanhuuo](https://github.com/yanhuuo)
- ?? 反馈邮箱：GitHub Issues
- ? 欢迎 Star 和贡献

</td>
</tr>
</table>

---

<div align="center">

### ?? 让对象映射变得简单高效！

**有问题？**

[查看 FAQ](../README.md) ? [提交 Issue](https://github.com/yanhuuo/Ling.Mapper/issues) ? [参与讨论](https://github.com/yanhuuo/Ling.Mapper/discussions)

---

Made with ?? by [yanhuuo](https://github.com/yanhuuo)

[![GitHub stars](https://img.shields.io/github/stars/yanhuuo/Ling.Mapper?style=social)](https://github.com/yanhuuo/Ling.Mapper)

</div>
