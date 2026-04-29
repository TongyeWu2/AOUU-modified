# AOUU

AOUU 是一个本地 WinForms 小工具，用于在《黑夜君临》中识别执行者释放大招，并播放你配置的音频。它不修改游戏文件，也不与游戏进程进行数据交互。

## Attribution

This project is a modified version of Sulfoxide319/AOUU:

https://github.com/Sulfoxide319/AOUU

Changes in this version include audio output device selection and updated documentation.

## 功能

- 监听自定义触发键，检测技能区域和血条区域后播放主音频。
- 主音频支持单个 `.mp3` / `.wav` 文件，也支持选择文件夹。
- 当路径是文件夹时，会从该文件夹内随机抽选一个 `.mp3` / `.wav` 播放。
- 主音频播放期间，按下鼠标左键或右键可以分别播放额外音效。
- 左键音效和右键音效都可以单独配置路径，默认留空。
- 左键音效和右键音效各自有 1 秒冷却，避免连续触发太密集。
- 可以选择音频输出设备，配合虚拟声卡把 AOUU 播放的声音送进 Discord 等语音软件。
- 可以启用 Soundpad 模式，让 AOUU 触发 Soundpad 播放指定序号的声音到扬声器和麦克风。
- 音量、触发键、截图键、检测区域、音频路径都会保存到本机配置，下次打开自动载入。

## 使用方法

1. 解压 Release 包后运行 `AOUU.exe`。
2. 配置主音频：可以选择单个音频文件，也可以选择包含音频的文件夹。
3. 按需配置“左键音效”和“右键音效”。不配置则不会播放对应音效。
4. 配置技能触发键和截图键。
5. 进入训练场或合适场景，按照提示框选技能区域和血条区域。
6. 正常进入游戏后，触发技能检测成功时会播放主音频。
7. 主音频仍在播放期间，按鼠标左键或右键会播放对应额外音效。

## 让 Discord 接收到 AOUU 的声音

AOUU 不能直接把普通扬声器输出伪装成麦克风，Windows 需要一个虚拟音频设备来完成这一步。常见做法是安装 VB-Audio Virtual Cable、VoiceMeeter 或类似软件。

以 VB-Audio Virtual Cable 为例：

1. 安装虚拟声卡后重启 AOUU。
2. 在 AOUU 的“音频输出设备”中选择 `CABLE Input`。
3. 在 Discord 的语音设置里，把输入设备设置为 `CABLE Output`。
4. 触发 AOUU 播放音频时，Discord 就会像收到麦克风声音一样收到这段音频。

如果列表里看不到虚拟声卡，点击“刷新设备”，或确认虚拟声卡已经在 Windows 声音设置中启用。

## 使用 Soundpad 送进 Discord

Soundpad 本身支持把声音播放到麦克风，也支持通过命令行远程控制播放指定序号的声音。AOUU 的 Soundpad 模式会调用：

```text
Soundpad.exe -rc DoPlaySound(序号,true,true)
```

其中两个 `true` 表示同时播放到扬声器和麦克风。

使用步骤：

1. 在 Soundpad 里导入你要播放的音频。
2. 记住该声音在 Soundpad 列表里的序号，例如第 1 个声音。
3. 在 AOUU 勾选“使用 Soundpad”，必要时点击“选择 Soundpad”定位 `Soundpad.exe`。
4. 在“声音序号”里填 Soundpad 列表里的序号。
5. 在 Discord 中正常选择你的麦克风输入设备，不需要把输入改成虚拟声卡。

如果 Soundpad 是 Steam 版，常见路径是：

```text
C:\Program Files (x86)\Steam\steamapps\common\Soundpad\Soundpad.exe
```

## 音频路径说明

配置音频来源时会询问：

- 选择文件夹：点击“是”。
- 选择单个音频文件：点击“否”。
- 取消配置：点击“取消”。

支持格式：

- `.mp3`
- `.wav`

如果选择的是文件夹，AOUU 只会从该文件夹当前层级中随机抽选音频文件，不会递归扫描子文件夹。

## 配置保存

用户配置会保存到：

```text
%LOCALAPPDATA%\AOUU\config.json
```

这意味着重新打开程序后，音量、音频路径、快捷键和区域配置会继续沿用上一次的设置。

## 运行要求

默认 Release 包面向已安装 .NET 8 桌面运行时的 Windows 用户。

如果无法启动，请安装 Microsoft .NET 8 Desktop Runtime。

## 免责声明

本工具仅用于个人娱乐和本地辅助播放音频，不修改游戏客户端，不注入游戏进程，也不读取游戏内部数据。请自行遵守相关游戏和平台规则。

## License

MIT
