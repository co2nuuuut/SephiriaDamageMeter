# SephiriaDamageMeter

[English](README.md) | **简体中文**

《Sephiria（赛菲莉娅）》的粉丝自制实时伤害统计 AddOn。

> **非官方粉丝项目，与 TEAM HORAY 无关。**
> 需要拥有正版 Sephiria。仓库只包含原创 Mod 代码，不包含游戏代码、素材、字体或反编译产物。

- 测试环境：Sephiria `1.0.33`（Unity `6000.3.21f1`）
- 不需要 BepInEx / Harmony 或其他加载器，使用游戏自带 `AddOns` 加载
- 许可证：MIT（仅 Mod 源码）

![status](https://img.shields.io/badge/status-playable-brightgreen)
![license](https://img.shields.io/badge/license-MIT-blue)

## 功能

- 小地图下方的 Brotato 风格紧凑伤害条。
- 按来源显示累计伤害、占比、元素色比例条。
- `F7` 战后总结页：按玩家分列显示总伤害、队伍占比、DPS、Top 来源。
- 单人 / 多人：每位玩家一个块，块头是头像、名字、总伤害、队伍占比，块内是该玩家 Top 4 来源。
- 直接读取游戏服务端同步的 `dealsStatistics` / `dealsStatistics_LastLocation`，不需要自定义网络协议。
- 可拖动；位置保存到 `config.json`，双击顶部恢复跟随小地图。

## 下载

从 **GitHub Releases** 下载最新的 `SephiriaDamageMeter-vX.Y.Z.zip`，或者看下面的“从源码构建”。

## 安装

1. Steam 库里右键 **Sephiria** → **管理** → **浏览本地文件**。
2. 游戏目录里没有 `AddOns` 就新建一个 `AddOns` 文件夹。
3. 把 `SephiriaDamageMeter` 整个文件夹复制进 `AddOns`。
4. 最终路径必须是：

   ```text
   Sephiria\AddOns\SephiriaDamageMeter\SephiriaDamageMeter.dll
   ```

5. 启动游戏，进入战斗后小地图下方会出现伤害条。

完整的安装、操作、参数调整和排错说明见 [使用指南.md](使用指南.md)。

## 快捷键

| 按键 | 功能 |
| --- | --- |
| `F7` | 打开 / 关闭战后总结页 |
| `F8` | 切换本层 / 全程 |
| `F9` | 显示 / 隐藏伤害条 |
| `F10` | 重新读取 `config.json` |

## 配置

完整配置表见 [使用指南.md](使用指南.md)。常改项：

- `panelWidth`、`fontSize`、`backgroundOpacity` — 尺寸和不透明度。
- `maxSourceRowsSolo`、`partySourcesPerPlayer`、`maxPlayerBlocks` — 显示多少行 / 多少玩家。
- `totalBarColor` — 单人总伤害条颜色（`#RRGGBB`）。
- `hideWhenEmpty`、`hideOutsideBattle` — 显示时机。
- `dpsUseBattleTime` — DPS 用战斗时间还是停留时间。
- `hotkeyToggle*` — 快捷键。

修改 `config.json` 后回游戏按 `F10` 生效，不需要重启。

## 多人联机

- 单人和联机都能用；每个玩家可以单独安装。
- 服务端写入 `PlayerAvatar.dealsStatistics`，Mirror 自动同步到所有客户端，不需要自定义 RPC。
- 装了 Mod 的客户端都能看到全队数据。
- 兼容 SephiriaUnlocker 等扩大房间人数的 Mod；超过显示上限会折叠为“另有 N 名玩家”。

## 技术原理

Mod 读取 `PlayerAvatar.dealsStatistics` 和 `PlayerAvatar.dealsStatistics_LastLocation`，这是游戏结算界面“造成伤害”使用的同一份 Mirror `SyncDictionary<DamageKey, float>`。服务端在 `PlayerAvatar.AddDealStat` 中写入，Mirror 自动同步到所有客户端。面板使用独立的 Overlay Canvas 和 uGUI，不添加 `GraphicRaycaster`，所以不会挡住游戏点击。

## 兼容性

- SephiriaUnlocker / 扩大房间人数：支持；超过上限会折叠玩家块。
- SephiriaReconnect：没有代码级联动；重连 / 检查点恢复后游戏会从跑团存档恢复 `dealsStatistics`，伤害条刷新显示恢复后的数据。
- 游戏更新可能导致字段访问失效；遇到问题请附 `Player.log` 反馈。

## 从源码构建

需要 Windows、PowerShell，以及本机安装的 Sephiria。

```powershell
# 自动检测 Steam 安装位置
.\build.ps1

# 或显式指定游戏目录
.\build.ps1 -SephiriaDir "D:\Steam\steamapps\common\Sephiria"
```

脚本使用游戏自带的 `Sephiria_Data\Managed` 程序集编译；首次运行会自动下载 Roslyn `csc`。输出在 `dist\SephiriaDamageMeter\`，复制到 `Sephiria\AddOns\` 即可。

## 目录结构

```text
SephiriaDamageMeter/
├─ src/SephiriaDamageMeter/   # Mod 源码
├─ build.ps1                  # 构建脚本
├─ config.example.json        # 默认配置
├─ metadata.json              # AddOns 元数据
├─ README.md                  # English
├─ README.zh-CN.md            # 简体中文
├─ USER_GUIDE.en.md           # English guide
├─ 使用指南.md                # 中文指南
├─ LICENSE
└─ CHANGELOG.md
```

## 免责声明

本项目为非官方粉丝 Mod，与 TEAM HORAY 无关，也未获得其背书或支持。仓库只包含原创 Mod 代码，不包含游戏本体代码、素材或反编译产物；使用需要拥有正版游戏。请勿把游戏文件或反编译代码与本项目一起分发。

## License

[MIT](LICENSE) — 仅适用于本仓库的原创 Mod 代码。Sephiria 及其相关素材归 TEAM HORAY 所有。
