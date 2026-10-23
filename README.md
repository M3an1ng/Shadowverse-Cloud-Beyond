# Shadowverse-Cloud-Beyond

受影之诗启发的网页卡牌项目，包含实时对战、牌组构建、卡牌与能力编辑、任务及调试工具。前端使用原生 JavaScript + Vite，后端使用 ASP.NET Core 8 + SignalR + EF Core SQLite。

## 文档入口

- [项目文档导航](docs/README.md)
- [当前功能与规划边界](docs/01-项目现状.md)
- [开发启动与测试](docs/02-开发与验证.md)
- [架构与代码导航](docs/03-架构与代码导航.md)
- [运行部署与排障](docs/07-运行部署与排障.md)
- [OpenSpec 待核实清单](docs/OpenSpec待核实清单.md)
- [旧文档归档清单](docs/归档清单.md)

## 本地开发

准备好项目依赖后，在两个终端从仓库根目录运行：

```powershell
dotnet run --project "Server/ShadowverseBeyond.Server.csproj" --launch-profile http
```

```powershell
npm run dev
```

浏览器访问 `http://127.0.0.1:5173`；API 和 SignalR 代理到 `http://localhost:5100`。

**注意：后端启动会执行数据库迁移和种子逻辑。** 连接已有数据前先核对配置与备份。依赖准备、真实测试项目路径及旧脚本偏差详见开发文档。

文档基线为 **2026-09-27 的本地工作区**，不是发布承诺。近期 UI 目标、研究及 OpenSpec 提案与当前实现分开维护；本次验证范围见 [验证记录](docs/验证记录.md)。

项目操作约定见 [AGENTS.md](AGENTS.md)。
