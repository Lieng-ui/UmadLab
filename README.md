# UmadLab

绝妖星（Dancing Mad / UMAD）练习实验室

基于 [AnoMech](https://github.com/anomek/AnoMech)（作者 Anomek）的分支版本，AGPL-3.0 许可。

## 安装

将下方地址添加至 Dalamud 第三方仓库：
```
https://raw.githubusercontent.com/Lieng-ui/UmadLab/master/pluginmaster.json
```

国内镜像地址：
```
https://cdn.jsdelivr.net/gh/Lieng-ui/UmadLab@master/pluginmaster.json
```

步骤：

1. 游戏内 `/xlsettings`
2. 切到 **Experimental** 标签
3. 在 **第三方仓库 / Third party repos** 里粘贴上面的地址
4. 保存，打开 `/xlplugins`，搜索 `UmadLab` 安装


## 联动宝宝椅（可选）

UmadLab 可以把模拟出的机制事件转发给 **Triggernometry**（宝宝椅），由它做语音播报和地面画图。这一步可选，不做也不影响练习。

### 一次性设置（3 步，之后自动跟随更新）

1. **打开 Triggernometry 的 Endpoint** —— 在 Triggernometry 设置里启用 Endpoint，端口保持默认 `51423`
2. **添加桥接规则** —— 在 Triggernometry 里添加一个**远程触发器**，粘贴下面的地址：
   ```
   https://cdn.jsdelivr.net/gh/Lieng-ui/UmadLab@master/bridge/UmadLab_Endpoint_Bridge.xml
   ```
   只需添加这一次。之后我们更新桥接规则，你会自动同步。
3. **在 UmadLab 里开启桥接** —— `/ulab` → 设置 → 勾选 **ACT / 宝宝椅桥接 (Endpoint)**，旁边显示「已连通」即表示通了；显示「等待 Endpoint」说明第 1 步没开或端口不对。

> 若 Triggernometry 版本较老、界面里没有「远程触发器」入口，可以下载上面的 XML 手动导入，效果相同 —— 只是以后我们更新桥接时需要重新导入一次。

> 桥接只把日志送进 Triggernometry 内部，不会额外产生 ACT 全局日志，不会干扰 Cactbot。Cactbot 仍会因读取游戏内存而播报，需要安静请在 OverlayPlugin 里单独禁用绝妖星的 raidboss 触发器。

## 源码

本插件以 AGPL-3.0 开源，完整源码在 [`src/`](src/)：

- 修改说明与构建方式：[`src/SOURCE.md`](src/SOURCE.md)
- 上游项目：[AnoMech](https://github.com/anomek/AnoMech)

## 许可与致谢

- 本插件以 **AGPL-3.0** 发布，完整许可证见 [LICENSE.md](LICENSE.md)
- 上游：AnoMech，作者 Anomek —— <https://github.com/anomek/AnoMech>
- 上游署名与修改声明见 [NOTICE.txt](NOTICE.txt)
