# 电子课表

面向学校教室与希沃交互式白板的 Windows 电子课表。程序以悬浮横条的形式显示在屏幕顶部，集中展示日期、当前课程、课程进度、下一节课和实时时钟；平时驻留在系统托盘，不占用任务栏，也不会主动抢占正在使用的软件焦点。

> 当前项目面向 Windows x64，使用 .NET 8 Windows Forms 开发。

## 功能一览

- **顶部悬浮课表**：屏幕顶部居中显示，半透明圆角样式。
- **课程状态**：显示日期、星期、秒级时钟、当前课程、课程进度条和下一节课。
- **当天课表**：点击横条展开或收起当天课程列表。
- **全屏自动隐藏**：检测到全屏或覆盖工作区的授课窗口时自动隐藏，退出后自动恢复。
- **上下课提醒**：支持预备铃、上课铃和下课铃；提醒期间横条显示提示横幅。即使横条因全屏而隐藏，提醒仍会继续工作。
- **系统托盘**：从托盘菜单编辑课表、展开/收起、开关提醒、试听铃声、设置当前用户开机启动、重新载入配置或退出。
- **课表编辑器**：按星期维护课程名称和起止时间，保存时只更新当前选中的星期。
- **临时调课**：为指定日期修改、取消或增加课程，不改变常规周课表。
- **Excel 导入**：支持导入 .xlsx 文件，也可以使用仓库中的课表模板。
- **单实例运行**：重复启动时不会再打开一个程序实例。

## 快速开始

1. 下载并解压「电子课表-轻量版-win-x64」构建产物。
2. 将 电子课表.exe 与 schedule.json 放在同一个文件夹中（构建产物已同时提供这两个文件）。
3. Windows 电脑需要安装 .NET 8 Desktop Runtime x64；已安装 .NET 8 SDK 的电脑通常已具备所需运行环境。
4. 双击 电子课表.exe。程序启动后会显示在屏幕顶部，并在系统托盘中保留图标。
4. 右键托盘中的绿色「课」字图标，可以打开菜单。

退出程序请使用托盘菜单中的「退出」，不要通过任务管理器结束进程。

### 托盘菜单

| 菜单项 | 作用 |
| --- | --- |
| 编辑课表… | 打开课表编辑器 |
| 展开/收起课表 | 展开或收起当天课程列表 |
| 上下课提醒 | 开启或关闭所有铃声提醒 |
| 试听铃声 | 播放一次上课提示音 |
| 开机自动启动 | 设置或取消当前 Windows 用户的开机启动 |
| 重新载入课表 | 重新读取程序目录中的 schedule.json |
| 退出 | 关闭程序 |

## 编辑常规课表

在托盘菜单中选择「编辑课表…」。

1. 在左侧选择星期。
2. 在课程列表中设置每节课的上课时间、下课时间和课程名称。
3. 使用「添加一节课」增加课程，使用对应行的「删除」移除课程。
4. 点击「保存」写入配置。

保存某一天时，不会覆盖其他星期的课程。若同一天的课程时间重叠，编辑器会阻止保存并指出冲突课程；打开编辑器时也会提示现有课表中存在重叠的星期。每次成功保存前会先生成上一版配置备份 `schedule.json.bak`。修改后可以通过托盘菜单的「重新载入课表」立即重新读取配置。

## 临时调课

在课表编辑器中打开「临时调课…」，选择需要调整的日期：

- 将课程名称改为新名称：当天该时间段改上新课程。
- 将课程名称清空：当天取消该时间段的课程。
- 在原课表之外增加时间段：可为当天增加一节课。
- 「写回原课表」可将当前显示内容恢复为常规课表内容。
- 「清除该日调课」可清除该日期保存的全部临时调整。

临时调课只对指定日期生效，不会修改常规周课表。当天存在调课记录时，顶部日期旁会显示「调」标记。

## 从 Excel 导入

在编辑器中选择「从 Excel 导入…」，或者将 .xlsx 文件拖到 电子课表.exe 上，也可以通过命令行传入文件路径。

导入表格需要满足以下格式：

- 文件格式为 .xlsx，不支持旧版 .xls。
- 工作表中需要有星期表头，支持「周一」至「周日」或「星期一」至「星期日」，至少识别到三个星期列。
- 每节课所在行需要在左侧前四列之一填写时间段，例如 08:00-08:45。
- 支持使用连字符、波浪线或「至」「到」分隔起止时间。
- 星期列对应的单元格填写课程名称；空白单元格不会导入课程。

导入会替换当前整份常规课表，程序会先显示确认提示。确认后请检查导入结果并点击「保存」。

仓库中的 课表模板.xlsx 可作为填写起点。请根据学校实际作息修改时间段，并填写各星期的课程名称。

### 命令行启动

```powershell
# 直接打开课表编辑器
电子课表.exe --edit

# 打开编辑器并导入指定的 xlsx 文件
电子课表.exe "D:\课表.xlsx"
```

## 配置文件

程序使用与可执行文件位于同一目录的 schedule.json。编辑器保存课表、临时调课和提醒开关时会更新该文件。

配置结构示例：

```json
{
  "title": "电子课表",
  "appearance": {
    "opacity": 0.9,
    "animateExpand": true
  },
  "reminders": {
    "enabled": true,
    "prepareMinutes": 2,
    "prepare": true,
    "start": true,
    "end": true,
    "bannerSeconds": 8
  },
  "lessons": [
    {
      "day": 1,
      "start": "08:00",
      "end": "08:45",
      "name": "语文"
    }
  ],
  "overrides": [
    {
      "day": 1,
      "start": "08:00",
      "end": "08:45",
      "name": "数学",
      "date": "2026-11-16"
    }
  ]
}
```

| 字段 | 含义 |
| --- | --- |
| appearance.opacity | 横条不透明度，范围为 0.3–1.0，默认 0.9 |
| appearance.animateExpand | 展开/收起时是否启用动画 |
| reminders.enabled | 上下课提醒总开关 |
| reminders.prepareMinutes | 预备铃提前分钟数，设为 0 可关闭预备铃时间触发 |
| reminders.prepare | 是否启用预备铃 |
| reminders.start | 是否启用上课铃 |
| reminders.end | 是否启用下课铃 |
| reminders.bannerSeconds | 提醒横幅显示时长，默认 8 秒 |
| lessons[].day | 星期编号：1 为周一，…，7 为周日 |
| lessons[].start / end | 24 小时制时间，格式为 HH:mm |
| lessons[].name | 课程名称 |
| overrides[].date | 临时调课日期，格式为 yyyy-MM-dd |
| overrides[].name | 临时课程名称；空字符串表示取消该时间段课程 |

建议优先使用程序自带的编辑器修改配置。手动编辑 JSON 后，请确保格式正确，再通过托盘菜单重新载入。

## 自动化测试

项目包含 `tests/ElectronicSchedule.Tests` 测试项目，覆盖每周课程筛选、当前课程判断、临时调课覆盖与取消。GitHub Actions 会先运行测试，全部通过后才发布 Windows x64 程序。

本地运行测试：

```powershell
dotnet test tests/ElectronicSchedule.Tests/ElectronicSchedule.Tests.csproj -c Release
```

## 构建项目

### 环境要求

- Windows 10/11 x64
- .NET 8 SDK
- Visual Studio 2022（可选，安装 .NET 桌面开发工作负载）

项目使用 Windows Forms，目标框架为 net8.0-windows，目标平台为 win-x64。Excel 读取使用 .NET 自带的 ZIP/XML API，没有引入第三方 NuGet 包。

### 本地发布

在项目根目录执行：

```powershell
dotnet publish ElectronicSchedule.csproj -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

发布文件位于：

```text
bin/Release/net8.0-windows/win-x64/publish/
```

项目文件配置为单文件、依赖框架发布。目标电脑需要安装 .NET 8 Desktop Runtime x64；这样生成的程序包明显小于自包含版本。发布目录中会包含可执行程序及随程序复制的 schedule.json。

### GitHub Actions

仓库已配置 GitHub Actions 工作流：推送到 main 分支，或在 Actions 页面手动运行工作流，会执行 Windows x64 发布，并上传名为「电子课表-轻量版-win-x64」的构建产物（Artifact）。构建产物同时包含 `电子课表.exe` 和 `schedule.json`，下载后解压即可将两者放在同一目录运行。

目前工作流负责构建并上传 Artifact；下载构建产物请进入仓库的 Actions 页面，打开对应运行记录，在 Artifacts 区域下载。

## 日志与运行说明

- 程序使用单实例互斥锁，重复启动不会创建第二个实例。
- 异常日志写入：%LOCALAPPDATA%\ElectronicSchedule\error.log。
- 当前悬浮横条布局以主显示器为基准。
- 开机启动设置写入当前 Windows 用户的注册表启动项，不需要管理员权限。

## 项目结构

| 文件 | 用途 |
| --- | --- |
| Program.cs | 程序入口、单实例控制和命令行参数 |
| TopBarForm.cs | 顶部课表横条、托盘菜单和提醒逻辑 |
| OverlayForm.cs | 悬浮窗口、DPI 适配及全屏隐藏检测 |
| Schedule.cs | 课表数据模型、读取、保存和课程查询 |
| EditorForm.cs | 常规课表编辑器 |
| OverrideForm.cs | 指定日期的临时调课窗口 |
| LessonList.cs | 展开后的课程列表 |
| Chime.cs | 程序内生成并播放提示音 |
| AutoStart.cs | 当前用户开机启动管理 |
| XlsxImporter.cs / XlsxReader.cs | Excel 课表导入 |
| Log.cs | 异常日志记录 |
| schedule.json | 默认课表和提醒配置 |
| 课表模板.xlsx | Excel 课表填写模板 |

## 许可与致谢

界面交互形态参考了 [ClassIsland](https://github.com/ClassIsland/ClassIsland)。本项目为独立实现，并非 ClassIsland 的官方组件。
