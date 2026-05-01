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
- 可以通过统一的 OCR 文字触发功能识别指定屏幕区域内的文字，默认提供 `YOU DIED` 文字触发。
- 音量、触发键、截图键、检测区域、音频路径都会保存到本机配置，下次打开自动载入。

## 使用方法

1. 解压 Release 包后运行 `AOUU.exe`。
2. 配置主音频：可以选择单个音频文件，也可以选择包含音频的文件夹。
3. 按需配置“左键音效”和“右键音效”。不配置则不会播放对应音效。
4. 配置技能触发键和截图键。
5. 进入训练场或合适场景，按照提示框选技能区域和血条区域。
6. 正常进入游戏后，触发技能检测成功时会播放主音频。
7. 主音频仍在播放期间，按鼠标左键或右键会播放对应额外音效。

## OCR文字触发

AOUU 支持通过 OCR 识别用户框选区域里的文字，并在检测到指定文字时播放对应音频。默认配置包含一个示例触发器：

```json
{
  "textTriggers": [
    {
      "enabled": true,
      "region": null,
      "text": "YOU DIED",
      "musicPath": "assets/audio/default.wav",
      "scanIntervalMs": 500,
      "cooldownSeconds": 5
    }
  ]
}
```

界面里的“OCR文字触发”区域可以配置：

- 是否启用 OCR 文字触发。
- OCR 文字识别区域。
- 要检测的文字，默认是 `YOU DIED`，检测时不区分大小写。
- “触发音频”按钮选择检测到文字后播放的 `.mp3` / `.wav`。
- 扫描间隔毫秒数。
- 冷却秒数，避免同一段文字连续出现在画面上时重复播放。

检测会忽略大小写、空格和换行，并容忍常见 OCR 错误，例如 `Y0U DIED`、`YOU D1ED`、`YOU DlED`。

当前版本使用内置的 Tesseract OCR 引擎进行英文文字识别，不需要另外安装 Tesseract GUI 或命令行程序。项目中的 `tessdata/eng.traineddata` 是 Tesseract 的英文 OCR 数据文件，用来告诉 OCR 引擎如何识别英文字符。

AOUU 会随项目打包英文数据文件：

```text
AOUU.exe
tessdata/
  eng.traineddata
```

AOUU 不依赖当前工作目录，会按程序运行目录查找：

```text
AppContext.BaseDirectory\tessdata\eng.traineddata
```

开发运行时路径通常是：

```text
bin\Debug\net8.0-windows\tessdata\eng.traineddata
```

如果文件缺失，AOUU 会在状态栏显示正在查找的完整路径，并暂停 OCR 扫描；其他功能仍可正常使用，也不会自动取消勾选“OCR文字触发”。

发布包会自动复制项目中 `tessdata` 文件夹下的 `.traineddata` 文件。

为了更适合 `YOU DIED` 这类较短的英文游戏文字，AOUU 会在识别前放大截图、增强对比度、二值化文字，并使用 Tesseract 的英文 `eng` 语言、单行文本模式和大写英文/数字白名单。

手动测试方式：

1. 确认 `tessdata\eng.traineddata` 存在。
2. 点击“设置OCR文字区域”，框选 `YOU DIED` 或其他目标文字会出现的位置。
3. 在“目标文字”里填 `YOU DIED`。
4. 点击“触发音频”选择一段测试音频。
5. 启用“OCR文字触发”。
6. 打开一张包含大字 `YOU DIED` 的图片，或在游戏中进入出现该文字的画面。
7. 等待约 1-2 秒，如果 OCR 识别成功，AOUU 会播放配置的触发音频。

排查建议：

- 如果提示 OCR 不可用，确认 `tessdata\eng.traineddata` 位于状态栏提示的路径。
- 如果不触发，尽量缩小 `YOU DIED` 区域，只保留死亡文字和少量边缘。
- 如果 OCR 读不清，尝试提高游戏亮度或重新框选文字更清晰的位置。
- 如果误触发，缩小 `YOU DIED` 区域，避免包含其他固定英文 UI。
- 如果死亡音乐不存在或路径失效，AOUU 会显示提示，不会改播普通主音频。

## 让 Discord 接收到 AOUU 的声音
- 目前暂时忽视这一列

AOUU 不能直接把普通扬声器输出伪装成麦克风，Windows 需要一个虚拟音频设备来完成这一步。常见做法是安装 VB-Audio Virtual Cable、VoiceMeeter 或类似软件。

以 VB-Audio Virtual Cable 为例：

1. 安装虚拟声卡后重启 AOUU。
2. 在 AOUU 的“音频输出设备”中选择 `CABLE Input`。
3. 在 Discord 的语音设置里，把输入设备设置为 `CABLE Output`。
4. 触发 AOUU 播放音频时，Discord 就会像收到麦克风声音一样收到这段音频。

如果列表里看不到虚拟声卡，点击“刷新设备”，或确认虚拟声卡已经在 Windows 声音设置中启用。

## 使用 Soundpad 送进 Discord / OOPZ
 现在最大的问题就是soundpad要钱

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
oopz请在个人设置里关闭智能降噪那一列的东西 不然会特别糊 discord没测不知道
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
