# EchoStem

**中文** | [English](#english)

EchoStem 是 [digimezzo/Dopamine-Windows](https://github.com/digimezzo/Dopamine-Windows)（即 Dopamine 2 音乐播放器）的**非官方个人 fork**，仅为本人使用习惯而改造。
本项目**以个人使用为主要设计方向**：功能取舍、界面调整与默认行为都围绕单人日常听歌场景，不追求通用性，也不保证与上游特性保持同步。

[![Build & Release](https://github.com/iCCat001/EchoStem/actions/workflows/build-release.yml/badge.svg)](https://github.com/iCCat001/EchoStem/actions/workflows/build-release.yml)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

> EchoStem 基于 Dopamine，遵循 **GNU GPL v3** 许可证。原项目版权归 Digimezzo 所有。
> **捐款请前往原项目**（Digimezzo / Dopamine），本 fork 不接受也不处理任何捐款。

---

## 相对原版 Dopamine 的主要改动

- **更换播放内核**：从 CSCore + FFmpeg 迁移到 [NAudio](https://github.com/naudio/NAudio)。
- **触摸优化**：面向 Surface 等触屏设备整体优化（更高的列表行、更宽的滚动条、常驻播放控制、点击展开歌词、关闭悬浮提示等），可在 **设置 → 功能** 中开关。
- **稳定性提升**：修复了原版存在的多处**内存泄漏**，以及**批量从播放列表删除歌曲**时的崩溃等问题，显著改善长时间使用的稳定性。
- **歌手页重做**：改为与专辑页一致的双栏布局（点击歌手滑出曲目栏、浮层封面、双击播放、一键播放全部 / 加入正在播放队列）。
- **歌词体验改进**：平滑滚动、播放行居中、控制栏歌词、网易云歌词翻译。
- **正在播放页显示音质**：格式 · 位深 · 码率 · 采样率，例如 `FLAC · 24bits · 1411kb/s · 44.1kHz`。
- **中文排序支持拼音**：列表排序支持中文拼音。
- 隐藏了个人用不到的 **Folders 与 Genres 页面**（源码保留，便于日后恢复）。

## 构建

本项目为传统的非 SDK 格式 `.csproj`（`packages.config`），请使用 **Visual Studio / MSBuild** 构建，而非 `dotnet build`。

所需的 WinRT 引用（`Windows.winmd`、`System.Runtime.WindowsRuntime.dll`）已随仓库内置在 `Dopamine/Libraries/`，**无需额外安装 Windows SDK**。使用 MSBuild 构建：

```powershell
nuget restore Dopamine.sln
msbuild Dopamine/Dopamine.csproj /p:Configuration=Release /p:Platform=AnyCPU /m
```

## 自动发布

仓库包含 GitHub Actions 工作流 [`.github/workflows/build-release.yml`](.github/workflows/build-release.yml)：

- 推送形如 `v2.0.10` / `v2.0.10-beta1` 的 **tag** 时自动触发（也可手动 `workflow_dispatch`）。
- 在 CI 中自动还原 NuGet 包、以 Release 配置编译主程序，并用 **Inno Setup** 生成安装程序。
- 产出并附带以下文件，自动创建标记为 **Latest** 的 GitHub Release：
  - **安装版**：`Dopamine-Setup-<版本>.exe`（单文件安装程序，含文件关联、开始菜单 / 桌面快捷方式与卸载）
  - **便携版**：`Dopamine-Portable-<版本>.zip`

## 安装

运行 `Dopamine-Setup-<版本>.exe` 即可（需要管理员权限，用于写入 Program Files 与文件关联）；安装时会自动结束正在运行的 Dopamine，完成后可勾选立即启动。便携版解压即用。

## 免责声明

- 这是**个人非官方 fork**，与 Digimezzo 及上游 Dopamine 项目**无任何关联**，也未获其背书。
- 使用本 fork 造成的任何问题由使用者自行承担；请勿向上游项目反馈本 fork 的问题。
- 本项目遵循 **GPL v3**，详见 [LICENSE](LICENSE)。

---

## English

EchoStem is an **unofficial, personal fork** of [digimezzo/Dopamine-Windows](https://github.com/digimezzo/Dopamine-Windows) (the "Dopamine 2" music player), customized for my own daily use.
It is **designed primarily for personal use**: features, UI and defaults are tuned for a single-listener workflow. It does not aim for generality and is not kept in sync with upstream.

It is based on Dopamine and licensed under the **GNU GPL v3**. All original copyright belongs to Digimezzo.
**Please donate to the original project** (Digimezzo / Dopamine) — this fork does not accept donations.

### Main changes vs. upstream Dopamine

- **New audio engine**: migrated from CSCore + FFmpeg to [NAudio](https://github.com/naudio/NAudio).
- **Touch optimization** for touch-screen devices (e.g. Surface): taller list rows, wider scrollbars, always-visible playback controls, tap-to-reveal lyrics, no tooltips, etc. Toggleable in Settings → Features.
- **Stability improvements**: fixed several **memory leaks** from the original version and crashes when **bulk-deleting tracks from the Now Playing playlist**, greatly improving reliability during long sessions.
- **Artists page redesigned** into a two-column layout matching the Albums page (slide-out track pane, floating cover, double-click to play, play-all / add-to-now-playing).
- **Improved lyrics experience**: smooth scrolling, centered current line, control-bar lyrics, NetEase lyric translations.
- **Audio quality shown on the Now Playing page** (`Format · bits · bitrate · samplerate`, e.g. `FLAC · 24bits · 1411kb/s · 44.1kHz`).
- **Chinese Pinyin sorting** in lists.
- **Folders and Genres pages hidden/disabled** (source kept, easy to re-enable).

### Build

Legacy non-SDK `.csproj` (`packages.config`) — build with **Visual Studio / MSBuild**, not `dotnet build`.

The required WinRT references (`Windows.winmd`, `System.Runtime.WindowsRuntime.dll`) are **vendored under `Dopamine/Libraries/`**, so no Windows SDK installation is required.

```powershell
nuget restore Dopamine.sln
msbuild Dopamine/Dopamine.csproj /p:Configuration=Release /p:Platform=AnyCPU /m
```

### Automated release

The [`.github/workflows/build-release.yml`](.github/workflows/build-release.yml) workflow compiles the app in Release, builds an installer with **Inno Setup**, and creates a **Latest** GitHub Release with:

- **Installer**: `Dopamine-Setup-<version>.exe` (single-file setup with file associations, Start Menu / desktop shortcuts and uninstaller)
- **Portable**: `Dopamine-Portable-<version>.zip`

### Disclaimer

- This is a **personal, unofficial fork**, not affiliated with or endorsed by Digimezzo or the upstream Dopamine project.
- Do not report issues with this fork to upstream.
- Licensed under **GPL v3**, see [LICENSE](LICENSE).
