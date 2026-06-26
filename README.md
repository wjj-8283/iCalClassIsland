# iCalClassIsland

在 [ClassIsland](https://github.com/ClassIsland/ClassIsland) 上显示来自 iCal 文件的日程安排。

## 功能

- 加载 iCal（.ics）文件中的日程事件
- 在主界面上显示当天的非全天事件
- 实时显示当前事件的进度、剩余时间等信息
- 显示后续事件列表
- 模仿 ClassIsland 原生课程表组件的风格

## 使用方法

1. 在 ClassIsland 中安装本插件
2. 打开设置 → iCal 日程，配置 iCal 文件路径
3. 将「iCal 日程」组件添加到主界面布局中
4. 通过组件设置调整显示选项（额外信息类型、进度条、倒计时等）

## 支持的 iCal 格式

- 本地时间格式：`DTSTART:20260101T090000`
- UTC 时间格式：`DTSTART:20260101T090000Z`
- 自动忽略全天事件（日期格式）
- 支持行折叠（RFC 5545）

## 开发

基于 ClassIsland 插件 SDK 开发，使用 Avalonia UI 框架。

```bash
dotnet build          # 编译
dotnet publish -p:CreateCipx=true  # 打包为 .cipx 插件包
```
