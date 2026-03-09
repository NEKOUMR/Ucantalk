# Ucantalk

[English](../README.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja-JP.md) | [한국어](README.ko-KR.md)

## 文档与教程

- [手册索引](manuals/index.md)
- [中文手册](manuals/manual.zh-CN.md)
- [英文手册](manuals/manual.en-US.md)
- [日文手册](manuals/manual.ja-JP.md)
- [韩文手册](manuals/manual.ko-KR.md)

Ucantalk 是一个基于 WinUI 3 的 Windows 桌面应用，用于 VRChat 风格的语音辅助交流。
它将文字转语音、翻译、语音输入、音频路由、手机控制和内置音频播放器整合在同一个 C# 应用中。

## 功能

- 文本输入与一键 TTS 发送
- TTS 引擎：Edge TTS、GPT-SoVITS、FishAudio
- 带可选 TTS 强制语言的翻译流程
- 语音输入引擎：Sherpa-ONNX、Vosk、Windows 语音输入
- 监听设备与 VB-Cable / VRC 设备双路音频路由
- 带二维码的手机网页控制端
- 最近语音历史与重发
- GPT-SoVITS 角色 / 档案管理
- 内置运行日志查看器

## 技术栈

- C# / .NET 8
- WinUI 3
- NAudio
- WebView2
- Vosk
- sherpa-onnx

## 运行要求

- Windows 10 1809 或更高版本
- 如果不是自包含发布，需安装 .NET 8 Desktop Runtime
- 如果要在 VRChat 中使用虚拟麦克风路由，需要 VB-Cable
- 如果要使用本地音色克隆，需要 GPT-SoVITS 及兼容模型
- 如使用云翻译或云 TTS，可能需要额外 API Key

## 从源码运行

```powershell
dotnet restore
dotnet build .\VRC_cantalkcn.csproj -r win-x64
dotnet run --project .\VRC_cantalkcn.csproj
```

## 构建安装包

本项目使用 Inno Setup 生成常规 Windows 安装包。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-setup.ps1
```

如果 `ISCC.exe` 不在 `PATH` 中，可以手动指定：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-setup.ps1 -IsccPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

生成的安装包位于 `artifacts\installer\`。

## 外部依赖

此仓库只包含应用源码。
以下大体积运行资源不建议直接提交到仓库，请按需单独准备：

- GPT-SoVITS 后端和模型权重
- FFmpeg
- Sherpa-ONNX 模型
- 私有 API Key 与个人配置文件

## 仓库说明

公开仓库前请不要提交以下内容：

- 个人配置文件
- 运行日志
- 模型文件（`.onnx`、`.ckpt`、`.pth`）
- 安装包与编译产物
- 签名证书

项目根目录已经包含 `.gitignore`。

## 许可证

MIT，见 [LICENSE](../LICENSE)。
