<div align="center">

# Coloring Voxels Tool

**《Coloring Voxels》的非官方辅助工具 —— 一键安装，开箱即用**

自动定位游戏目录、部署文件、启动游戏；进入任意关卡后按 <kbd>F1</kbd> 打开游戏内面板。

[![Release](https://img.shields.io/github/v/release/dark-but-spark/ColoringVixels-Tool?label=%E6%9C%80%E6%96%B0%E7%89%88%E6%9C%AC&color=5682ff)](https://github.com/dark-but-spark/ColoringVixels-Tool/releases)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%20x64-0078d4.svg)](#系统要求)
[![Build](https://img.shields.io/github/actions/workflow/status/dark-but-spark/ColoringVixels-Tool/build.yml?label=build)](https://github.com/dark-but-spark/ColoringVixels-Tool/actions)

</div>

---

## 简介

支持的游戏：《Coloring Voxels》（Steam AppID **3634590**，BepInEx 插件 + 游戏内面板）。

工具由两部分组成：

| 组件 | 说明 |
| --- | --- |
| **安装器** `ColoringVoxelsTool-Setup.exe` | 单文件、免安装、可视化。自动检测游戏目录 → 部署文件 → 启动游戏，并支持检查更新。 |
| **插件** `ColoringVoxelsTool.dll` | 游戏内面板（<kbd>F1</kbd>）：分层一键涂完 / 拟人涂色 / 定时连涂、当前层预览、颜色高亮、游戏设置镜像、内容解锁、等级统计、提醒播报等。 |

插件基于 **BepInEx 5（x64）** 与 **Harmony** 运行，不改动游戏本体文件（除注入所需的 `winhttp.dll` / `BepInEx/`）。

> 本工具由 2D 版《Coloring Pixels》辅助工具移植而来，正陆续把功能接到 3D 体素版上；已迁移的功能见下，未迁移的（人工辅助屏幕扫描、设置界面按钮注入、悬浮 HUD、字段调试等）会逐步补上。

## 特性

- **一键自动化**：安装器自动完成「找目录 → 部署 → 启动」，无需手动解压、复制文件。
- **智能目录检测**：运行中的进程 → Steam 库（含 `libraryfolders.vdf`）→ 常见路径 → 全盘深度扫描，并读取 PE 头校验位数（本工具仅支持 64 位主程序）。
- **安全可逆**：安装时自动备份被覆盖的文件，卸载时一键还原；也可连同 BepInEx 本体一起移除。
- **自动完成（按游戏「一次只涂一层」的规则分层处理）**：
  - **一键涂完**：<kbd>F2</kbd> 或用面板按钮，一键涂满**当前层**。
  - **拟人涂色**：<kbd>F3</kbd> 逐层推进，层内按颜色分块 + 块间就近 + 块内蛇形扫行，带手速抖动与随机停顿；可调手速、笔触长度、就近分块、停笔概率，支持「先涂大面积颜色」「只涂当前颜色」。落笔走游戏自己的涂色逻辑，所以计数与存档与手涂一致。
  - **定时自动化**：设定总时长与换图间隔，挂机连涂多张图；速度可选「慢 / 中 / 快」预设或自定义三项参数。
  - **立即保存**：<kbd>F5</kbd> 写盘。
- **当前层预览**：面板内显示当前层的整层缩略图（读游戏体素数据，不截图）：已涂为真彩、未涂为可关闭的暗色底稿。
- **画布颜色高亮**：把当前选中颜色的待涂体素高亮出来（只处理当前层）；高亮颜色 / 不透明度可调，模式分「关闭 / 持续 / 闪烁」三档，<kbd>F6</kbd> 循环切换。
- **游戏设置镜像**：把游戏设置里的开关与音量滑条搬进面板；一键在「推荐预设 / 游戏出厂默认」之间切换，可「撤销上次改动」「把当前设置存为预设」「套用自定义预设」。
- **内容解锁**：显示全部世界、标记全部完成、解锁全部 Steam 成就；配合补丁放行全部 DLC。
- **等级与统计**：两条互不相干的经验线（自动绘图 / 人工辅助）、单图计时、按天使用统计，全部存在漫游目录，不随游戏目录清理。
- **提醒与播报**：倒计时提醒（到点弹窗 + 系统语音，可循环）、累计涂色每满 1 小时的趣味横幅。
- **插件日志**：日志同时写进 `%APPDATA%\ColoringVoxelsTool\logs\`（按天一个文件、自动保留最近 7 份）。
- **体验增强**：双主题（柔光马卡龙 / 深靛霓虹）、界面自适配、中文字形兜底、删除存档二次确认。
- **命令行模式**：`--silent`、`--detect-only`、`--uninstall` 等，便于脚本化部署。

## 快速开始

### 系统要求

- Windows 10 / 11（x64）
- 已安装 Steam 版的《Coloring Voxels》（64 位主程序）
- 无需安装 .NET 运行时（插件随游戏自带的 Mono 运行；安装器使用系统自带的 .NET Framework）

### 一键安装

1. 前往 [**Releases**](https://github.com/dark-but-spark/ColoringVixels-Tool/releases) 下载最新版 `ColoringVoxelsTool-Setup-vX.Y.Z.exe`。
2. 关闭正在运行的游戏。
3. 双击运行安装器：
   - 若有安全软件拦截，请选择「允许」/「仍要运行」。
   - 若游戏装在 `Program Files` 等受保护目录，安装器会提示以管理员身份重启。
4. 确认游戏目录后点击「**一键安装**」，等待进度条走完。
5. 安装器会**自动启动游戏**（Steam 版会交给 Steam 拉起，避免直启 exe 闪退）：进入任意关卡后按 <kbd>F1</kbd> 打开面板。

> 首次启动游戏时 BepInEx 会初始化，可能比平时稍慢，属正常现象。

### 卸载

- 重新运行安装器，点击「**卸载**」；
- 或在「卸载」弹窗中勾选「同时移除 BepInEx 本体」以彻底清理。

卸载只会移除工具文件与（可选的）BepInEx 本体，不会触碰游戏存档与等级进度。

## 使用说明

### 快捷键

全部可在「设置 → 快捷键」里改：

| 按键 | 功能 | 备注 |
| --- | --- | --- |
| <kbd>F1</kbd> | 打开 / 关闭面板 | |
| <kbd>F2</kbd> | 一键涂完当前层 | |
| <kbd>F3</kbd> | 开始 / 停止拟人涂色 | |
| <kbd>F5</kbd> | 立即保存当前关卡 | |
| <kbd>F6</kbd> | 循环切换颜色高亮（关闭 → 闪烁 → 持续） | |

> 人工辅助相关热键（<kbd>F4</kbd> / <kbd>F7</kbd>–<kbd>F12</kbd> / <kbd>Q</kbd>）为移植预留，当前 3D 版**尚未启用**。

### 面板

面板为单页、可滚动，分区如下：

- **关卡**：世界 / 关卡、当前层、当前颜色、剩余体素，以及总进度条。
- **预览（当前层）**：当前层的整层缩略图，可切换「显示未涂底稿」。
- **自动完成**：一键涂完本层、拟人涂色开关、立即保存；手速滑条；「只涂当前颜色」。
- **自动化**：开始 / 停止（总时长、连续涂图、换图间隔、速度预设取自配置）。
- **解锁**：显示全部世界、解锁全部成就、标记全部完成。
- **游戏设置**：恢复游戏默认 / 应用推荐预设 / 撤销上次改动 / 存为预设 / 套用自定义预设，以及全部开关与音量滑条。

> 更完整的页签（首页 / 人工辅助 / 调试等）仍在迁移中。

### 配置文件

《Coloring Voxels》插件首次运行后会把默认配置写入：

```
<游戏目录>\BepInEx\config\coloringvoxels.cheatsuite.cfg
```

可直接编辑该文件修改热键、参数与默认值（面板内的改动也会实时写回）。主要分组：`0-通用`、`1-解锁`、`2-显示`、`3-拟人涂色`、`4-人工辅助`、`5-自动化`、`6-面板`、`8-推荐预设`、`9-快捷键`、`A-功能开关`、`B-反馈`、`C-提醒与播报`。

**等级与用户资料存档**（等级、经验值、各项统计、单图计时、预设与备份）刻意存在漫游目录，不随游戏目录一起被清理：

```
%APPDATA%\ColoringVoxelsTool\
```

`BepInEx\config` 会随「覆盖安装 / 卸载插件 / 验证游戏文件完整性 / 重装游戏」一起消失，
所以等级等数据不放在那里，更新版本不会重置进度。

## 命令行参数

安装器同时支持无人值守模式，便于脚本批量部署：

```text
ColoringVoxelsTool-Setup.exe [选项]

  --dir=<路径>        指定游戏目录（跳过自动检测）
  --game=cv           指定目标游戏（本工具只有一款，纯兼容旧快捷方式）
  --silent            静默安装，不显示界面
  --detect-only       只检测游戏目录并退出
  --uninstall         卸载本插件
  --remove-bepinex    卸载时一并移除 BepInEx 本体
  --no-restore        卸载时不还原备份
  --no-launch         安装完成后不启动游戏
  --no-backup         不备份被覆盖的文件
  --force             强制覆盖所有文件
  --deep              深度扫描所有磁盘查找游戏
  --log=<文件>        指定日志文件路径
  -h, --help          显示帮助
```

退出码：`0` 成功，`1` 执行失败，`2` 找不到 / 无效的游戏目录，`3` 游戏正在运行。

示例：

```powershell
# 静默安装到指定目录，不启动游戏
.\ColoringVoxelsTool-Setup.exe --silent --dir="C:\Program Files (x86)\Steam\steamapps\common\Coloring Voxels" --no-launch

# 只检测游戏目录
.\ColoringVoxelsTool-Setup.exe --detect-only
```

## 常见问题

<details>
<summary><b>游戏里按 F1 没有反应？</b></summary>

- 确认已**进入关卡**（主菜单下面板逻辑不生效）。
- 检查游戏目录下是否存在 `winhttp.dll`、`BepInEx\core\BepInEx.Preloader.dll`、`BepInEx\plugins\ColoringVoxelsTool.dll`。
- 查看 `BepInEx\LogOutput.log`，搜索 `Coloring Voxels Tool` 是否成功加载。

</details>

<details>
<summary><b>安装器提示「目录不可用」或找不到游戏？</b></summary>

- 请选择包含游戏主程序 `ColoringVoxels.exe` 的那一层目录。
- 或使用 `--deep` 参数进行全盘深度扫描。
- 32 位主程序不受支持：本插件基于 BepInEx 5（x64），仅支持 64 位版本。

</details>

<details>
<summary><b>一键涂完 / 拟人 / 自动化会写存档，会不会把进度弄坏？</b></summary>

这些功能会把结果写进当前关卡存档，规则与手涂完全一致（底层走游戏自己的涂色逻辑）。建议在使用前用「保存」功能留一份进度、或先手动保存一次；本工具不会删除你的存档，卸载也不影响存档。

</details>

<details>
<summary><b>安装 / 卸载失败、提示没有权限？</b></summary>

游戏若安装在 `Program Files` 等受保护目录，请以管理员身份重新运行安装器（安装器在遇到权限问题时也会主动询问）。

</details>

<details>
<summary><b>会影响其它 Mod 或游戏存档吗？</b></summary>

- 只卸载本插件时，其它基于 BepInEx 的 Mod 不受影响；BepInEx 本体仅在勾选对应选项时才移除。
- 卸载不会删除游戏存档，但安装时被覆盖的文件会在卸载时按选项还原。

</details>

<details>
<summary><b>会误封账号吗？</b></summary>

本插件仅在本地修改单机游戏的运行时数据，不注入网络流量、不修改游戏文件。但**任何第三方工具都存在风险**，请自行评估后使用。

</details>

## 从源码构建

详细步骤见 [docs/BUILDING.md](docs/BUILDING.md)。最简流程：

```powershell
# 一键构建：编译插件 -> 组装部署包 -> 编译安装器 -> 生成 SHA256
.\scripts\build-release.ps1

# 产物：dist\ColoringVoxelsTool-Setup-v<版本>.exe 与 dist\SHA256SUMS.txt
```

> 编译插件需要引用游戏自身的 `Assembly-CSharp.dll`，因此本机需安装《Coloring Voxels》，
> 或直接使用仓库内已随包分发的 `artifacts\ColoringVoxelsTool.dll`（`build-release.ps1 -SkipMod`）。

## 目录结构

```
ColoringVoxelsTool/
├─ src/ColoringVoxelsTool/      插件源码（C#，BepInEx + Harmony）
│  ├─ GameApi.cs                游戏数据访问层（Builder / 体素 / 分层）
│  ├─ AutoPainter.cs            分层拟人涂色引擎
│  ├─ AutoScheduler.cs          定时 / 连涂自动化
│  ├─ CheatPanel.cs             游戏内面板（IMGUI）
│  └─ Assist*.cs                人工辅助引擎（2D 版遗留，暂未迁移）
├─ installer/                   安装器源码（C# 5 / WinForms，单文件）
│  └─ payload/                  部署包覆盖层（定制 doorstop_config.ini 等）
├─ scripts/                     构建脚本（PowerShell）
│  ├─ build-changelog.ps1       由 RELEASE_NOTES.md / FEATURES.md 生成公告与功能总览源码
│  ├─ build-mod.ps1             编译插件
│  ├─ build-payload.ps1         组装 BepInEx + 插件的部署包
│  ├─ build-installer.ps1       编译单文件安装器
│  ├─ build-release.ps1         一键发布
│  ├─ publish.ps1               提交 / 推送 / 打标签发布
│  └─ make-icon.ps1             生成应用图标
├─ vendor/bepinex-x64/          随仓库分发的 BepInEx 5 x64 运行时
├─ artifacts/                   已编译的插件 DLL（随仓库提交，供 CI / 无游戏环境使用）
├─ assets/icon.ico              安装器图标
├─ docs/                        使用 / 构建文档，以及开发心路历程
├─ RELEASE_NOTES.md             更新公告（唯一来源，同时是 Release 正文）
├─ FEATURES.md                  全功能总览（首次安装 / 首次启动时展示一次）
├─ VERSION                      版本号（唯一来源）
└─ Directory.Build.props        全局 MSBuild 属性
```

> 想知道这个工具是怎么一步步长出来的：**[开发心路历程](docs/CHRONICLE.md)**。

## 免责声明

- 本项目仅供**学习、交流与单机娱乐**使用，请勿用于任何商业或非法用途。
- 请在下载后 **24 小时内**自行删除相关内容；因使用本工具产生的一切后果由使用者自负。
- 本项目与《Coloring Voxels》的开发商、发行商及 Steam 无关，游戏相关的全部权利归其所有者所有。
- 请支持正版游戏。

## 许可证

本项目基于 [MIT License](LICENSE) 开源。

若本工具基于或包含第三方作品，请一并遵守其原始授权与声明；发布前请确认你拥有相应的分发权利。
