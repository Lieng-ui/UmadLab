# UmadLab

绝妖星（Dancing Mad / UMAD）练习实验室 —— 一个 Dalamud 插件。

基于 [AnoMech](https://github.com/anomek/AnoMech)（作者 Anomek）的分支版本，AGPL-3.0 许可。

## 这是什么

在旅馆里召唤出假的队友与 Boss，客户端侧复现绝妖星 P1–P5 的机制，用来单人练习排轴。

- **P1–P5 全流程串联**：一次开打可以连续跑完五个阶段
- **国服野队打法**：AI 队友按国服常见站位处理机制
- **串联器**：配置开怪倒数、标点预设、各阶段结束时间，支持阶段存档/读档
- **P3 / P4 聊天栏小抄**：把真假判断、接线顺位直接打出来

## 安装

把下面的地址加到 Dalamud 的第三方仓库：

```
https://cdn.jsdelivr.net/gh/Lieng-ui/UmadLab@master/pluginmaster.json
```

步骤：

1. 游戏内 `/xlsettings`
2. 切到 **Experimental** 标签
3. 在 **第三方仓库 / Third party repos** 里粘贴上面的地址
4. 保存，打开 `/xlplugins`，搜索 `UmadLab` 安装
5. 重启游戏

## 命令

| 命令 | 作用 |
|---|---|
| `/umadenc` | 打开串联器 |
| `/ulab` | 打开插件窗口（同 `/anomech`） |
| `/anomech` | 打开插件窗口，子命令：`config` `mp` `start` `reset` `leave` |

串联器用法示例：`/umadenc start full d1 cd=5` —— 从 P1 开始整场串联，玩家在 D1 位置，开怪倒数 5 秒。

## 许可与致谢

- 本插件以 **AGPL-3.0** 发布，完整许可证见 [LICENSE.md](LICENSE.md)
- 上游：AnoMech，作者 Anomek —— <https://github.com/anomek/AnoMech>
- 上游署名与修改声明见 [NOTICE.txt](NOTICE.txt)

## 仓库结构

| 路径 | 内容 |
|---|---|
| `pluginmaster.json` | Dalamud 仓库清单 |
| `UmadLab/latest.zip` | 插件发布包 |
| `MAINTAINER.md` | 发版与仓库维护笔记（普通用户不需要看） |
