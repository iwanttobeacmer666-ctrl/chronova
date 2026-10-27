<div align="center">
  <img src="assets/时砂.png" width="112" alt="时砂：沙漏与时钟图标">
  <h1>时砂 · Chronova</h1>
  <p>把时间、待办和天气，留在桌面的一角。</p>
  <p>Windows x64 · 便携运行 · 手动选择地区 · 10种界面语言</p>
  <p><a href="https://github.com/iwanttobeacmer666-ctrl/chronova/releases/latest">下载便携版</a> · <a href="docs/read.md">使用教程</a></p>
</div>

时砂是一款小巧的桌面时钟。平时只显示时间和天气，需要时再点开备忘录、秒表、倒计时或设置。窗口没有白色系统标题栏，按住顶部空白处即可拖动。

## 关于权限和隐私

**天气地区由你在设置里选择，程序不会替你定位。**

- 不申请定位权限，不读取GPS，也不通过IP推测所在城市。
- 不读取摄像头、麦克风、通讯录或浏览记录。不需要注册账号、登录，也不需要填写天气API密钥。
- 提醒任务和设置保存在程序旁的 `app/data`，不会发送到天气接口。分享包不附带开发者的任务、设置或地区缓存。
- 日常使用无需管理员权限。桌面快捷方式和开机启动都由用户主动选择；开启开机启动后，只在当前用户的启动文件夹创建快捷方式。

程序并非完全离线：天气查询会把**手选地区的代表点坐标**发送给 Open-Meteo；中国省市区县列表会向阿里云 DataV 请求对应的行政区代码。第三方服务会收到正常网络请求所带的连接信息，但程序不会利用这些信息自动选择城市。点击天气卡中的数据来源链接，会打开默认浏览器。

## 下载后怎么打开

到 [Releases](https://github.com/iwanttobeacmer666-ctrl/chronova/releases/latest) 下载 **Chronova-v1.0.0-windows-x64-portable.zip**，全部解压，打开 `app` 文件夹，双击 **时砂.exe**。

便携包已附带 .NET 10.0.11 运行组件，无需另装 .NET。请保留完整文件夹，尤其是 `app/runtime`，不要只复制一个 exe，也不要直接在压缩包里运行。把文件夹放在自己有写入权限的位置，程序才能保存任务和设置。

这是 Windows x64 版本，不适用于 macOS、Linux 或32位 Windows；Windows ARM 尚未验证。更详细的操作见包内 `read.md`，也可以直接阅读[使用教程](docs/read.md)。

## 可以做什么

| 功能 | 使用方式 |
| --- | --- |
| 时钟 | 24小时制时间、日期与星期；默认北京时间，可在设置里更换时区 |
| 备忘提醒 | 选择日期和时间，支持单次或每日重复，任务可编辑、完成和删除 |
| 秒表 | 开始、暂停、继续、重置；收起面板后继续运行 |
| 倒计时 | 按时、分、秒设置，到点弹窗并播放系统提示音 |
| 天气 | 图标、地区名称、当地今日最低至最高温、风向和蒲福风级 |
| 中国地区 | 省／直辖市 → 市／地区 → 区／县，也可停在较高一级；不列乡镇和街道 |
| 语言 | 简体中文、繁体中文、英语、日语、韩语、西班牙语、法语、德语、葡萄牙语、俄语 |

时区和天气地区是分开设置的。选择洛杉矶时区后，新建任务中的09:00就是洛杉矶上午9点；已有提醒只换算显示时间，实际提醒时刻不变。每日任务按创建时的时区重复，并处理夏令时变化。

天气通常每15分钟更新。连不上时，会显示“天气暂时连不上，稍后再试。”，清除旧温度和风力，5分钟后重试。中国行政区名单以 DataV 现有数据为准，没有下级数据的地区只显示已有层级；天气取所选区域的代表点，不代表区域内每一处的天气都相同。

## 提醒运行与本地数据

右上角“×”会把窗口收进托盘，提醒和计时继续运行。双击托盘图标可打开，右键可彻底退出。**彻底退出会重置秒表和倒计时。**

关机、睡眠或彻底退出时无法当场提醒，下次打开或唤醒后会补提醒。每日任务错过多次时，只补最近一次。声音是否听得到，还取决于Windows的音量和静音设置。

备份时先从托盘退出，再复制整个程序文件夹，或单独备份 `app/data`。发布包与源码仓库都排除了个人数据。移动文件夹前，先关闭开机启动，搬到新位置后再重新创建快捷方式或开启启动项。

## 从源码构建

需要 Windows 和已安装的 .NET 10 SDK。项目使用 WPF，没有第三方 NuGet 包；`NuGet.Config` 清空了外部包源。

```powershell
dotnet build 时砂.csproj -c Release
dotnet publish 时砂.csproj -c Release --no-self-contained -o app
```

上述命令生成的普通版本需要本机有 .NET 10 Desktop Runtime。Releases里的便携包还附带独立运行组件，采用[应用相对目录查找运行时](https://learn.microsoft.com/en-us/dotnet/core/deploying/)的方式，不依赖全局 .NET 安装。

已有可用的 Python 3 时，还可运行翻译覆盖检查；无需为运行程序安装 Python。

```powershell
dotnet run --project tests/Clock.Tests.csproj -c Release
dotnet run --project tests/ui/Layout.Tests.csproj -c Release
python tests/check_localization.py
```

已通过78项逻辑检查、19项界面检查和10种语言的文本覆盖检查。真实联网验证过北京16个区、武汉13个区，以及海淀区天气。便携包也经过压缩完整性、中文路径解压和包内运行组件加载检查。尚未在接收者电脑、全新Windows或ARM设备上实测；开机启动、睡眠唤醒和扬声器发声也未做跨设备验证。

## 数据来源与运行组件

- 天气：[Open-Meteo](https://open-meteo.com/)，免费接口用于个人非商业使用。
- 城市名称及坐标：[GeoNames / Open-Meteo Geocoding](https://open-meteo.com/en/docs/geocoding-api)。
- 中国行政区及代表点：[阿里云 DataV.GeoAtlas](https://datav.aliyun.com/portal/school/atlas/area_selector)。
- 便携包内 .NET 组件的微软许可与第三方声明：`app/runtime/LICENSE.txt`、`app/runtime/ThirdPartyNotices.txt`。
