# EchoStem

**中文** | [English](#english)

EchoStem 是 [digimezzo/Dopamine-Windows](https://github.com/digimezzo/Dopamine-Windows)（即 Dopamine 2 音乐播放器）的**非官方个人 fork**，仅为本人使用习惯而改造。
本项目**以个人使用为主要设计方向**：功能取舍、界面调整与默认行为都围绕单人日常听歌场景，不追求通用性，也不保证与上游特性保持同步。

[![Build & Release](https://github.com/iCCat001/EchoStem/actions/workflows/build-release.yml/badge.svg)](https://github.com/iCCat001/EchoStem/actions/workflows/build-release.yml)
[![License: GPL v3](https://img.shields.io/badge/License-GPLv3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

> EchoStem 基于 Dopamine，遵循 **GNU GPL v3** 许可证。原项目版权归 Digimezzo 所有。
> **捐款请前往原项目**（Digimezzo / Dopamine），本 fork 不接受也不处理任何捐款。

---

## 相对于原版 Dopamine 的主要改动

### 音频内核

- **播放内核从 CSCore + FFmpeg 迁移到 [NAudio](https://github.com/naudio/NAudio)**。
  使用 `NAudio` / `NAudio.Flac` / `NAudio.Vorbis`（NVorbis）/ `NLayer` 等组件解码与输出，打包清单（`PackagerConfiguration.xml`、`Dopamine.wxs`）已同步更新。

### 触摸优化（面向 Surface 等触屏设备）

- 针对触屏使用场景整体优化，可在 **设置 → 功能** 中开关：
  - 更高的列表行、更宽的滚动条与滑块，便于手指点按。
  - 播放控制常驻显示、歌词点击展开、关闭悬浮提示等。
  - 主题切换时相关的悬停/选中配色会同步刷新。

### 界面与交互

- **歌手页重做**：改为双栏布局，与专辑页操作逻辑一致（点击歌手滑出曲目栏、浮层封面、双击播放、一键播放全部 / 加入正在播放队列）。
- **正在播放页显示音质**：格式 · 位深（FLAC）· 码率 · 采样率，例如 `FLAC · 24bits · 1411kb/s · 44.1kHz`。
- **歌词体验**：平滑滚动、播放行居中定位、暂停/恢复时控制条歌词显示、纯音乐占位提示、网易云歌词翻译。
- **专辑封面自动补全**优先使用网易云音乐来源。
- 迷你播放器频谱对齐、全局若干 UI 细节修复。
- **隐藏/禁用** 了个人用不到的 Folders 与 Genres 页面（源码保留，便于日后恢复）。

### 本地化

- 列表排序**支持中文拼音**（`PinyinUtils`）。
- 中文界面（`ZH-CN.xml`）文案完善，其余语言沿用上游。

### 稳定性与性能

- 修复多处**内存泄漏**（频谱控件事件订阅、外部控制服务、通知服务线程等）。
- 修复**文件夹监视器（FolderWatcher）**崩溃。
- 修复**批量从播放列表删除歌曲**时的崩溃：播放统计回调与队列的并发竞态、队列 `playbackOrder` 索引失效导致的 `IndexOutOfRange`。
- 删除正在播放/被占用的文件时进行**重试**，避免删除失败。
- 优化歌手页等页面的加载性能。

> 首次运行仍需要 Windows 10 SDK 提供的关键引用（见下方「构建」）。

---

## 构建

本项目为传统的非 SDK 格式 `.csproj`（`packages.config`），请使用 **Visual Studio / MSBuild** 构建，而非 `dotnet build`。

必需的两项外部引用（上游 Dopamine 同样依赖）：

1. **`Windows.winmd`**：来自 Windows 10 SDK。将 `C:\Program Files (x86)\Windows Kits\10\UnionMetadata\<版本>\Windows.winmd`
   拷贝到其上级目录，使其位于 `C:\Program Files (x86)\Windows Kits\10\UnionMetadata\Windows.winmd`。
2. **`System.Runtime.WindowsRuntime.dll`**：确保其存在于
   `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore\v4.5\`。

满足上述依赖后，使用 MSBuild 构建：

```powershell
nuget restore Dopamine.sln
msbuild Dopamine.sln /p:Configuration=Release "/p:Platform=Any CPU" /m
```

## 自动发布

仓库包含 GitHub Actions 工作流 [`.github/workflows/build-release.yml`](.github/workflows/build-release.yml)：

- 推送形如 `v2.0.10` / `v2.0.10-beta1` 的 **tag** 时自动触发（也可手动 `workflow_dispatch`）。
- 在 CI 中自动准备上述 SDK 引用、还原 NuGet 包、以 Release 配置编译。
- 产出 **Portable ZIP**（`Dopamine-Portable-<版本>.zip`）并自动创建 GitHub Release。
- 含 `-` 的 tag（如 `-beta1`）会被标记为 **Pre-release**。

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

- **Audio engine migrated from CSCore + FFmpeg to [NAudio](https://github.com/naudio/NAudio)** (`NAudio.Flac`, `NAudio.Vorbis`/NVorbis, `NLayer`); packaging manifest updated accordingly.
- **Touch optimization** for touch-screen devices (e.g. Surface), toggleable in Settings → Features: taller list rows, wider scrollbars/thumbs, always-visible playback controls, tap-to-reveal lyrics, no tooltips, theme-aware hover/selection colors.
- **Artists page redesigned** into a two-column layout matching the Albums page (slide-out track pane, floating cover, double-click to play, play-all / add-to-now-playing).
- **Audio quality shown on the Now Playing page** (`Format · bits · bitrate · samplerate`, e.g. `FLAC · 24bits · 1411kb/s · 44.1kHz`).
- **Lyrics**: smooth scrolling, centered current line, control-bar lyrics on pause/resume, instrumental placeholder, NetEase lyric translations.
- **Album art auto-fill** prefers NetEase Cloud Music as its source.
- **Chinese Pinyin sorting** in lists; improved Simplified Chinese localization.
- **Folders and Genres pages hidden/disabled** (source kept, easy to re-enable).
- **Stability & performance fixes**: several memory leaks, a FolderWatcher crash, and crashes when bulk-deleting tracks from the Now Playing playlist (playback-counter/queue concurrency race, stale `playbackOrder` indices), plus retry-on-lock when deleting files.

### Build

Legacy non-SDK `.csproj` (`packages.config`) — build with **Visual Studio / MSBuild**, not `dotnet build`.

Required external references (same as upstream):

1. **`Windows.winmd`** from the Windows 10 SDK, copied to
   `C:\Program Files (x86)\Windows Kits\10\UnionMetadata\Windows.winmd`.
2. **`System.Runtime.WindowsRuntime.dll`** under
   `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETCore\v4.5\`.

```powershell
nuget restore Dopamine.sln
msbuild Dopamine.sln /p:Configuration=Release "/p:Platform=Any CPU" /m
```

### Automated release

The [`.github/workflows/build-release.yml`](.github/workflows/build-release.yml) workflow builds a **Portable ZIP** and creates a GitHub Release when a `v*` tag is pushed (tags containing `-`, e.g. `-beta1`, are marked as Pre-release).

### Disclaimer

- This is a **personal, unofficial fork**, not affiliated with or endorsed by Digimezzo or the upstream Dopamine project.
- Do not report issues with this fork to upstream.
- Licensed under **GPL v3**, see [LICENSE](LICENSE).
