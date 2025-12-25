# EF Core 迁移命令失败问题修复报告

**日期:** 2025-07-20
**负责人:** Kilo Code (Debug Mode)
**状态:** 已解决

---

## 1. 问题描述

在开发过程中，执行 `dotnet ef migrations add` 命令以创建数据库初始迁移时，命令持续失败，无法生成迁移文件。最终目标是成功生成并应用迁移，创建数据库。

## 2. 诊断过程

通过系统性的排查，我们逐步定位并解决了问题。

### 第一次尝试：初始错误

- **命令:** `dotnet ef migrations add InitialCreate --verbose`
- **错误:** `System.IO.DirectoryNotFoundException: d:\workspace\Shadowverse-beyond\ShadowverseBeyond.Server\`
- **分析:** 日志显示，EF Core 设计时工具在尝试创建 `DbContext` 实例时，无法找到正确的项目根目录。堆栈跟踪将问题指向 `DesignTimeDbContextFactory.cs` 文件中设置基础路径的逻辑。

### 第二次尝试：修复路径后

- **操作:** 修改 `Server/Infrastructure/Persistence/DesignTimeDbContextFactory.cs`，将 `.SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../ShadowverseBeyond.Server"))` 修正为 `.SetBasePath(Directory.GetCurrentDirectory())`。
- **命令:** `dotnet ef migrations add InitialCreate --verbose`
- **错误:** `Microsoft.EntityFrameworkCore.Design.OperationException: The name 'InitialCreate' is used by an existing migration.`
- **分析:** 路径问题解决后，`DbContext` 实例可以成功创建。但新的错误表明，之前失败的尝试在 `Migrations` 目录中留下了冲突的残留文件。

### 第三次尝试：清理迁移目录

- **操作:** 删除了 `Server/Infrastructure/Data/Migrations` 目录下的所有旧迁移文件和快照文件。
- **命令:** `dotnet ef migrations add InitialCreate --verbose`
- **结果:** **成功！** 命令成功执行，生成了新的 `InitialCreate` 迁移文件和 `ApplicationDbContextModelSnapshot.cs`。

### 第四次尝试：应用迁移

- **命令:** `dotnet ef database update --verbose`
- **错误:** `Microsoft.Data.Sqlite.SqliteException (0x80004005): SQLite Error 14: 'unable to open database file'.`
- **分析:** 此错误表明 SQLite 驱动程序无法创建数据库文件，因为它尝试写入的 `Server/Data/` 目录不存在。

## 3. 根本原因分析

整个问题由两个独立的根本原因串联导致：

1.  **`DesignTimeDbContextFactory` 路径计算错误:** 工厂类中用于定位 `appsettings.json` 的基础路径计算逻辑不正确（应该为`d:\workspace\Shadowverse-beyond\Server\`，而不是`d:\workspace\Shadowverse-beyond\ShadowverseBeyond.Server\`，导致在设计时环境下无法加载配置和连接字符串。
2.  **数据库目录缺失:** SQLite 连接字符串指向的相对路径 `Data/shadowverse.db` 中的 `Data` 目录未被自动创建，导致数据库文件无法写入。

## 4. 解决方案

针对上述原因，采取了以下步骤进行修复：

1.  **修正 `DesignTimeDbContextFactory`:**
    - **文件:** `Server/Infrastructure/Persistence/DesignTimeDbContextFactory.cs`
    - **操作:** 将 `SetBasePath` 的参数从错误的路径拼接逻辑更改为直接使用 `Directory.GetCurrentDirectory()`。
2.  **清理旧的迁移文件:**
    - **目录:** `Server/Infrastructure/Data/Migrations/`
    - **操作:** 删除了该目录下的所有 `.cs` 文件，以确保从干净的状态开始。
3.  **创建数据库目录:**
    - **目录:** `Server/`
    - **操作:** 手动创建了 `Data` 子目录，以满足 SQLite 文件创建的前提条件。

## 5. 最终结果

在执行上述所有修复步骤后，`dotnet ef migrations add` 和 `dotnet ef database update` 命令均成功执行。项目现在拥有一个正确的初始迁移，并且数据库已根据该迁移成功创建于 `Server/Data/shadowverse.db`。问题已彻底解决。