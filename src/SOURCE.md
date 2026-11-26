# 源码说明

本目录是 **UmadLab** 的完整源码快照（C# / Dalamud 插件）。

## 对应哪个发布版本

| 发布版本 | 程序集 | 源码提交 |
|---|---|---|
| `0.1.0.0` | `UmadLab.dll` | 见下方「快照基线」 |

本快照导出时的上游工作树提交：`bca5cd1`（分支 `gemini/minloop-v1`）。
`0.1.0.0` 的二进制即由该工作树构建；之后的提交只改动了清单与仓库文件，
不含代码变更。

## 来源

| 项 | 值 |
|---|---|
| 上游项目 | [AnoMech](https://github.com/anomek/AnoMech)，作者 Anomek |
| 许可证 | AGPL-3.0（见仓库根目录 `LICENSE.md`） |
| 本分支基线 | 上游 `0.5.1.0` + UmadLab 自有改动 |
| 对应源码地址 | <https://github.com/Lieng-ui/UmadLab/tree/master/src> |

UmadLab 相对上游的主要改动：

- 插件身份重命名为 `UmadLab`（程序集、清单、窗口标题），命名空间仍保留 `AnoMech.*`
- 新增 `Scenarios/Umad/P1Full` … `P5Full` 五个完整场景与对应 AI
- 新增 `Scenarios/Umad/FullEncounter/UmadEncounterChain.cs`（P1–P5 串联控制器：标点、倒数、
  小抄开关、阶段存档）
- 场景菜单收敛为每个阶段一个 `Full` 场景，其余专项场景保留源码但不注册

## 构建

需要 .NET 10 SDK 与 Dalamud 开发库（`%AppData%/XIVLauncherCN/addon/Hooks/dev/`）。

```powershell
dotnet build AnoMech/AnoMech.csproj -c Release `
  "-p:DalamudLibPath=$env:APPDATA\XIVLauncherCN\addon\Hooks\dev\"
```

产物在 `AnoMech/bin/Release/`：`UmadLab.dll`、`UmadLab.deps.json`、依赖 DLL。
与清单 `UmadLab.json` 一起打包成 zip 即为发布包（见仓库根目录 `MAINTAINER.md`）。

## 许可证

源码以 **AGPL-3.0** 发布。你可以自由使用、修改、再分发，但衍生作品必须同样以 AGPL-3.0
开源，并保留原作者署名（见仓库根目录 `NOTICE.txt`）。
