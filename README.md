# AOUU-modified

AOUU-modified 是一个本地 WinForms 小工具，用于在《黑夜君临》中识别执行者释放大招/武器战技释放/人物死亡，并播放你配置的音频。它不修改游戏文件，也不与游戏进程进行数据交互。

## Attribution

这个插件是在Sulfoxide319/AOUU的基础上改进完成:

原插件：https://github.com/Sulfoxide319/AOUU

## 使用方法
1. 下载并安装.NET8
2. 打开powershell（mac就不知道了orz 建议问问gpt
3. cd到AOUU-modified-main文件夹

ex: cd cd C:\Users\wuton\Downloads\AOUU-modified-main\AOUU-modified-main

4. dotnet publish -c Release
5. exe文件位置在AOUU-modified-main\bin\Release\net8.0-windows

### 死亡音效
1. 点击设置OCR文字区域， 框住YOU DIED会出现的区域， 越精确越好
2. （可选）点击触发音频 选择自己心仪的音乐， 默认唢呐音乐由洛斯里克大厨群的52Hertz提供（关注洛斯里克大厨谢谢喵）
### 战技音效（默认罗泽司斧
1.设置战技区域 目前只支持一只手上的战技

  ex：我选择了左手的区域 那我右手释放战技不会被检测到
  
  战技区域是左下角的UI一整个方框 同样 越精确越好 具体框内样子可参考asset文件夹里的 DUDUDA图片
  
2. 选择战技图片
    参考asset文件夹里的DUDUDA图片
   
3.设置战技按键和触发音频
### 联动soundpad
这项功能能让你用oopz/discord...连麦的朋友也能听见你的音效

不过oopz本身有音乐共享功能 所以可以忽略（我做完了才发现TAT）

还未完成 本身只能和大狗animal联动

1.选择soundpad位置（需有soundpad软件本体）

2.把animal或者其他你想要的音乐丢到soundpad里 在插件里选择这个音乐的序号

```
## 免责声明

本工具仅用于个人娱乐和本地辅助播放音频，不修改游戏客户端，不注入游戏进程，也不读取游戏内部数据。请自行遵守相关游戏和平台规则。

## License

MIT
