# iCalClassIsland

在 [ClassIsland](https://github.com/ClassIsland/ClassIsland) 上显示来自 iCal（.ics）文件的日程安排，主要用于大学生的课表。

浅色模式
![screenshot](src/Screenshot_light.png)
深色模式
![screenshot2](src/Screenshot_dark.png)

## 功能

### 主界面组件
- 水平排列显示当天所有非全天事件
- 实时进度条
- 当前事件高亮（主题背景色），已结束事件灰色、未开始事件黑色
- 额外信息显示（5 种模式）：时间范围、已过时间、剩余时间、事件时长、完成百分比
- 倒计时模式：剩余时间低于阈值时切换为倒计时框
- 隐藏已结束事件开关
- 组件间距可调

### 明天日程
- 模仿 ClassIsland 课程表组件的「明天」标记
- 三种模式：不显示 / 占位时显示（无事件或全部结束后）/ 始终显示

### 多 iCal 文件支持
- 支持同时加载多个 .ics 文件，自动合并并按时间排序
- 时间重叠时段内的事件各自独立显示，同时活跃

### 日历视图
- ClassIsland 设置页中增加「日历视图」选项卡
- 周视图：7 列（周一至周日），每列显示当天全部日程
- 可前后翻周，今日标题蓝色高亮

### 设置页面
- 多文件管理：添加/移除 iCal 文件
- 自动刷新间隔配置
- 状态信息显示（文件总数、当天事件数、缺失文件提示）

## 支持的 iCal 格式

- 本地时间：`DTSTART:20260101T090000`
- UTC 时间：`DTSTART:20260101T090000Z`
- TZID 时区参数：`DTSTART;TZID=Asia/Shanghai:20260101T090000`
- 自动忽略全天事件（`VALUE=DATE` 格式或跨零时事件）
- 支持 `RRULE:FREQ=WEEKLY` 每周重复规则（INTERVAL / UNTIL / COUNT）
- 支持 RFC 5545 行折叠

## 使用方法

1. 在 ClassIsland 中安装本插件
2. 打开设置 → iCal 日程，添加 iCal 文件
3. 将「iCal 日程」组件添加到主界面布局中
4. 通过组件设置调整显示选项

## 开发

基于 ClassIsland 插件 SDK 开发，使用 Avalonia UI 框架。

```bash
dotnet build                              # 编译
dotnet publish -p:CreateCipx=true          # 打包为 .cipx 插件包
```

## 开源协议
本软件采用GPLv3开源，图标来自米游社
