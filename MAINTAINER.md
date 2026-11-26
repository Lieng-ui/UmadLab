# UmadLab — Dalamud 第三方插件仓库

这是 UmadLab 的插件仓库骨架。把整个 `repo/` 目录的内容推到你的 Git 仓库根目录，
用户就能通过「第三方仓库」安装。

## 目录结构

```
repo/
├── pluginmaster.json      ← 仓库清单（用户添加的就是它的 URL）
├── UmadLab/
│   └── latest.zip         ← 插件本体（含 UmadLab.dll / 依赖 / 清单）
└── make_repo.py           ← 重新生成清单 + 替换 latest.zip
```

## 一次性准备

1. 在 GitHub 建一个公开仓库（例如 `UmadLab`）。
2. 把本目录内容放到仓库根目录并推送。
3. 决定清单的访问地址，二选一：

   | 方式 | 清单 URL | 特点 |
   |---|---|---|
   | `raw.githubusercontent.com` | `https://raw.githubusercontent.com/<用户>/<仓库>/master/pluginmaster.json` | 最简单；**国服经常 404/被墙** |
   | GitHub Pages | `https://<用户>.github.io/<仓库>/pluginmaster.json` | 稳定；需要开 Pages |
   | jsDelivr CDN | `https://cdn.jsdelivr.net/gh/<用户>/<仓库>@master/pluginmaster.json` | 国内可访问性通常最好；有缓存（约 12 小时） |

4. 用真实地址重新生成清单：

   ```powershell
   cd repo
   python make_repo.py --base-url "https://cdn.jsdelivr.net/gh/<用户>/<仓库>@master"
   ```

   脚本会：复制 zip 到 `UmadLab/latest.zip`、写入 `DownloadLinkInstall/Update/Testing`、
   刷新 `LastUpdate` 时间戳、打印最终清单 URL。

5. 推送。用户侧：`/xlsettings` → **Experimental** → **第三方仓库** → 粘贴清单 URL → 保存 →
   在 `/xlplugins` 里搜索 `UmadLab` 安装。

## 每次发新版

1. 改 `AnoMech/AnoMech.csproj` 的 `<Version>`（AssemblyVersion 必须和 DLL 一致）。
2. 重新构建并打包（`UmadLab-x.y.z.w-local.zip`）。
3. 跑一次 `python make_repo.py --base-url ...`，把新 zip 替换成 `latest.zip`。
4. 在 `pluginmaster.json` 里把 `AssemblyVersion` 同步成新版本（脚本里 `PLUGIN` 常量或直接改 json）。
5. 推送。Dalamud 会对已安装用户提示更新（比对 `AssemblyVersion` 与 `DownloadLinkUpdate`）。

## 清单字段说明

| 字段 | 说明 |
|---|---|
| `InternalName` | 内部唯一标识，**必须与 DLL 清单一致**，改名等于换插件 |
| `AssemblyVersion` | 版本号，Dalamud 靠它判断是否有更新 |
| `DalamudApiLevel` | 15（对应 Dalamud 15.x） |
| `MinimumDalamudVersion` | 低于此版本的 Dalamud 不允许安装 |
| `DownloadLinkInstall` / `Update` / `Testing` | 指向 zip 的直链，三处通常相同 |
| `IsTestingExclusive` | true 时只在「测试版插件」里显示 |
| `LastUpdate` | Unix 时间戳（秒） |
| `RepoUrl` | 源码仓库地址，显示在插件详情 |
| `IconUrl` | 可选，插件图标（建议 64×64 png） |

## 注意

- zip **根目录**必须直接放 `UmadLab.dll` 和 `UmadLab.json`，不要再套一层文件夹。
- 清单里不要写 `DownloadCount` 之外的运行时字段；`DownloadCount` 保持 0 即可。
- 国服环境下 `raw.githubusercontent.com` 经常不可达，优先考虑 jsDelivr 或 Pages。
- 许可证为 AGPL-3.0：发布时保留 `LICENSE.md` 与 `NOTICE.txt`（上游署名）。
