# AOUU-modified by 桐雀喜欢咕咕咕

AOUU-modified 是一个本地 WinForms 小工具，用于在《黑夜君临》中识别执行者释放大招、武器战技释放、人物死亡，并播放你配置的音频。它不修改游戏文件，也不与游戏进程进行数据交互。

## Attribution

这个插件是在 Sulfoxide319/AOUU 的基础上改进完成：

原插件：https://github.com/Sulfoxide319/AOUU

部分音效点子来源于 洛斯里克大厨粉丝群群u 

## 功能

- 支持“大招音效”：多个大招共享一个识别区域，但只有当前选中的大招会参与匹配和触发。

- 支持“战技音效”：多个战技共享一个识别区域，每个战技有自己的模板图片、音效文件和匹配阈值。

- 音效支持单个 `.mp3` / `.wav` 文件，也支持选择文件夹。

- 可以通过统一的 OCR 文字触发功能识别指定屏幕区域内的文字，默认提供 `YOU DIED` 文字触发。

- 支持“按键音效”：无需 OCR、图像识别或截图区域，按下 3 个可配置按键中的任意一个即可播放对应音频，并带有独立冷却时间。

  

## 使用方法
```
补充
目前图像识别还有问题 推荐将所有角色大招阈值调成0.2
不知道为什么有些角色的大招图片识别度就特别低。。。不理解（挠头
```
#### 前置

需要.NET 8：https://dotnet.microsoft.com/en-us/download/dotnet/8.0

下载安装后在文件夹里找到exe文件，双击即可打开

```
AOUU\bin\Debug\net8.0-windows
```

### 让oopz连麦好友听见

打开游戏 启动插件 在oopz右上角：播放伴奏 选择音源里找到 ┗|｀O′|┛ 嗷~~ 即可

如果没有就等一会再看

#### 初始化：设置识别区域\按键

点击任何“设置区域”按钮后，会进入可调整框选模式：先按住左键拖出矩形，松开后不会立刻保存；可以拖动矩形内部移动位置，也可以拖动边缘或四角调整大小。调整完成后按 `Enter` 或点击“确认”保存；按 `Esc` 或点击“取消”放弃本次框选，并保留原来的区域配置。

点击 **设置大招区域** 把大招UI图标框住

- 选中区域应大于等于模板图片，模板图片在：

 ```
   \AOUU\assets\SkillPic
 ```

​	但尽量精准以提高识别度

- 设置大招按键
- 按键和区域是共享的 只用设置一个角色的 其他角色都能使用

点击 **设置战技区域** 把战技UI图标框住

- 选中区域应大于等于模板图片，模板图片在：
 ```
   \AOUU\assets
 ```

​	但尽量精准以提高识别度

- 设置战技按键
- 按键和区域是共享的 只用设置一个角色的 其他角色都能使用
  - **如果你的战技区域框的是左手 只能识别左手上的战技 右手同理**
  

#### 初始化： 设置死亡区域

- 设置OCR文字区域快捷键（不设置也可以 只是后面比较麻烦）
- 让你的人物在训练场死一次 然后选中YOU DIED出现的地方
- 模板图片在：

```
AOUU\assets
```

同样，选中区域应大于等于模板图片 但尽量精准以提高识别度



**大部分角色大招没有配置对应音效 请自行添加（因为我没灵感了） 具体看功能介绍-自定义**

## 功能介绍

### 大招音效

#### 使用 

当你使用对应人物时， 在大招音效里切换到对应人物

由于游戏UI是半透明的 所以可能出现识别失败的情况发生 我自己平时游玩的时候是90%+的成功率

如果按大招后经常没有反应：

1. 确认大招区域设置正确

2. 查看插件最下面的匹配度 如果过低（0.1- 0.3）再次确认大招区域设置正确 还是不行 就调低匹配阈值

   **注意大招音效有冷却时间**

### 自定义

#### 更改音效

点击 选择音效 选择你喜欢的音效文件即可 

本压缩包的

```
AOUU\assets\audio\╔±├╪╥⌠╨º┐Γ
```

里内置了 meme常用的神秘音效

来自： https://www.bilibili.com/video/BV1QbwbeYE2g

乱码是因为我电脑的问题（ 建议和视频对应着看 或者自行去视频提供的百度网盘里下载音效文件

提供一些我常用的处理音效的网址：

视频转MP3

https://www.freeconvert.com/zh/convert/video-to-mp3

音频剪辑：

https://vocalremover.org/zh/cutter



我也在征集音效点子 如果你有好点子可以发给我

联系方式见文档最后

 #### 新建人物模板

- 点击 新增大招（最后一个字被挡住了）

- 点击 选择大招 进入

  ```
  AOUU\assets\SkillPic
  ```

  选择对应人物的大招满充能图片

- 选择音效

### 战技音效

#### 使用 

由于识别的是武器+战技 所以只有武器和战技都满足条件时才能触发音频

当你使用对应战技时， 插件会自动匹配 **所有的战技模板**， 找到最相似的一个且匹配度大于等于阈值并播放音效

这意味着你不需要像人物大招一样在更换战技时手动在插件里更换选定战技



由于游戏UI是半透明的 所以可能出现识别失败的情况发生 我自己平时游玩的时候是90%+的成功率

实测 武器背景颜色大概率不会影响识别结果 但确实有可能会识别失败

如果按战技后经常没有反应：

1. 确认战技区域设置正确

2. 查看插件最下面的匹配度 如果过低（0.1- 0.3）再次确认战技区域设置正确 还是不行 就调低匹配阈值

   **注意战技音效有冷却时间**

### 自定义

#### 更改音效

详情请见大招 - 自定义 - 更改音效

#### 新建战技模板

- 点击 新增战技（最后一个字被挡住了）
- 需要你自行添加对应战技图片，处理图片方法我放到后面讲
- 选择战技 选择你处理好的图片 

- 选择音效

##### 处理战技图片

以下是我的个人方法 总之目的是**让你的战技UI图片的大小 = 你选中的战技识别区域**

在压缩包里的 处理图片 的PowerPoint里 有一个蓝色的正方形模板

最后的战技图片应与这个模板一个大小

1. 截图
2. 在PowerPoint里复制你的游戏截图 裁剪或者等比变换游戏截图使得战技UI图片（战技UI的例子请见asset文件夹里的bmbs和dududa文件）和蓝色模板一样大 拖动蓝色正方形模板使得模板正好位于战技UI图片上方且刚好覆盖战技UI图片
3. 先选中战技UI图片， shift同时选中模板 点击上面栏里的形状格式然后用merge shape里的intersect（相交）裁剪出战技UI图片
4. 导出as picture 选择pdf格式

*还有一种方式是私信我告诉我你的点子（ 我如果有空会帮忙处理）（小声*

### 死亡音效

如果你配置得当 那么在你死亡（就是要丢魂显示you died之后） 会播放预设的音频

如果没有触发 检测OCR区域

#### 自定义

见 大招音效 - 自定义 - 更改音效

### 按键音效

按键音效是独立功能，不需要设置 OCR 区域，也不会做图像识别或截图检测。

使用方法：

1. 在主界面的 **按键音效** 区域勾选“启用按键音效”。
2. 分别点击 **按键1 / 按键2 / 按键3**，录制每一组要监听的触发按键。
3. 分别点击 **音频1 / 音频2 / 音频3**，选择对应的 `.mp3` / `.wav` 文件或包含音频的文件夹。
4. 按下对应按键时，程序会直接播放对应音频。三组按键各自记录冷却时间，默认冷却为 1 秒，用来避免按住按键时反复播放。

按键音效设置会保存到 `%LOCALAPPDATA%\AOUU\config.json` 的 `KeyAudioTrigger` 节点中，下次打开程序会自动读取。

### 键盘、组合键和手柄绑定

新版按键绑定统一保存为 `InputBinding` 结构，同时继续保留旧版 `Hotkey` / `HotkeyName` / `Key1` 等字段，旧配置会在读取时自动迁移。

绑定方式：

1. 点击界面里的“大招按键”“战技按键”“截图键”“按键1/2/3”等设置按钮。
2. 点击“开始录制”。
3. 键盘单键：直接按目标键，例如 `1`、`F`、`NumPad 1`。
4. 组合键：按住 `Ctrl` / `Alt` / `Shift` 后再按目标键，例如 `Ctrl + 1`、`Ctrl + Shift + F`、`Alt + NumPad 3`。
5. 手柄按键：连接 Xbox / XInput 手柄后，点击“开始录制”，然后按下手柄按钮，例如 `Gamepad A`、`Gamepad RB`、`Gamepad DPad Up`。

支持的手柄输入包括：`A` / `B` / `X` / `Y`、`LB` / `RB`、`LT` / `RT`、`Start` / `Back`、方向键上/下/左/右、左摇杆按下、右摇杆按下。

配置文件中的新字段：

- 顶层：`TriggerInput`、`RegionCaptureInput`
- `UltHotkeyTrigger` / `ImageHotkeyTrigger`：`HotkeyInput`
- `KeyAudioTrigger`：`Input1`、`Input2`、`Input3`
- `RegionCaptureHotkeys`：`SkillRegionInput`、`HealthRegionInput`、`OcrTextRegionInput`

每个输入结构包含：

```json
{
  "Kind": 0,
  "KeyCode": 49,
  "Modifiers": 1,
  "DisplayName": "Ctrl + 1"
}
```

`Kind` 为键盘或手柄输入，`KeyCode` 保存虚拟键码或内部手柄按钮码，`Modifiers` 保存 Ctrl / Alt / Shift 组合，`DisplayName` 用于界面显示。旧版配置如果只有 `Hotkey` 和 `HotkeyName`，会按旧键码自动生成新的 `HotkeyInput`。

测试建议：

1. 绑定顶部数字键 `1`，按主键盘 `1`，确认对应音效或触发功能生效。
2. 绑定小键盘 `NumPad 1`，按小键盘 `1`，确认显示为 `NumPad 1` 且能触发。
3. 绑定 `Ctrl + 1`、`Ctrl + Shift + F`、`Alt + NumPad 3`，分别按下同样组合，确认只有完整组合触发。
4. 绑定 `Gamepad A`、`Gamepad RB` 或 `Gamepad DPad Up`，按下对应 Xbox / XInput 手柄按钮，确认触发。

## 配置保存

用户配置会保存到：

```text
%LOCALAPPDATA%\AOUU\config.json
```

这意味着重新打开程序后，音量、音频路径、快捷键和区域配置会继续沿用上一次的设置。

首次启动时，如果这个用户配置文件还不存在，程序会把随软件一起打包的默认预设复制过来：

```text
assets/default_config.json
```

默认预设可以包含音频路径、模板图片路径、快捷键、OCR/图像区域、按键音效、冷却时间、阈值和启用状态。预设里的内置资源路径使用相对路径，例如 `assets/audio/default.wav`、`templates/defaults/skill_ready.png`；程序读取时会按应用程序所在目录解析成实际路径。已有用户配置不会被默认预设覆盖。

#### 联系方式

QQ:3176134363



## 免责声明

本工具仅用于个人娱乐和本地辅助播放音频，不修改游戏客户端，不注入游戏进程，也不读取游戏内部数据。请自行遵守相关游戏和平台规则。

## Preset and Gamepad Notes

User settings are stored in `%LOCALAPPDATA%\AOUU\config.json`.

On first launch, if that user config does not exist, AOUU copies `assets/default_config.json` into `%LOCALAPPDATA%\AOUU\config.json` and then loads the copied file. Existing user configs are never overwritten by the bundled preset.

Bundled audio and image asset paths in `assets/default_config.json` should be relative to the app directory, such as `assets/audio/default.wav`, `assets/SkillPic/Zhui.png`, or `templates/defaults/skill_ready.png`. Absolute paths are still supported for user-selected files and are kept unchanged when loading config.

To export the current local config into the bundled default preset, run this from the `AOUU` directory:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\export-default-config.ps1
```

The helper reads `%LOCALAPPDATA%\AOUU\config.json`, converts bundled project asset paths to relative paths, and writes `assets/default_config.json`.

Gamepad bindings support XInput buttons `A` / `B` / `X` / `Y`, `LB` / `RB`, `LT` / `RT`, `Back` / `Start`, stick buttons, and DPad directions. Gamepad combos are controller-only: press the first gamepad button while recording, then add any other buttons within the short capture window before releasing or waiting for the timeout. Keyboard + gamepad mixed combos are not supported. Captured gamepad combos display as `Gamepad: LB + RB`.

## License

MIT
