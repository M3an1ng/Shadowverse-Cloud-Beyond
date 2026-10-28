# Shadowverse-Cloud-Beyond

受影之诗启发的网页卡牌项目，包含实时对战、牌组构建、卡牌与能力编辑、任务及调试工具。前端使用原生 JavaScript + Vite，后端使用 ASP.NET Core 8 + SignalR + EF Core SQLite。

## 文档入口

- [当前功能与规划边界](docs/01-项目现状.md)
- [开发启动与验证](docs/02-开发与验证.md)
- [架构与代码导航](docs/03-架构与代码导航.md)
- [对战与玩法](docs/04-对战与玩法.md)
- [卡牌创作与 DSL](docs/05-卡牌创作与DSL.md)
- [接口与数据](docs/06-接口与数据.md)
- [运行部署与排障](docs/07-运行部署与排障.md)
- [文档维护](docs/08-文档维护.md)

## 本地开发

安装 Node.js、npm 和 .NET SDK（版本见 [global.json](global.json)，当前为 `8.0.413`），从仓库根目录准备依赖：

```powershell
npm ci
dotnet restore "Server/ShadowverseBeyond.Server.csproj"
```

随后在两个终端从仓库根目录运行：

```powershell
dotnet run --project "Server/ShadowverseBeyond.Server.csproj" --launch-profile http
```

```powershell
npm run dev
```

浏览器访问 `http://127.0.0.1:5173`；API 和 SignalR 代理到 `http://localhost:5100`。

后端启动会执行数据库迁移和种子逻辑，连接字符串和数据库路径见 [接口与数据](docs/06-接口与数据.md)。

## 构建与检查

```powershell
npm run check:encoding
npm run build
dotnet build "Shadowverse-beyond.sln" -c Release
```

运行与发布配置见 [运行部署与排障](docs/07-运行部署与排障.md)。
