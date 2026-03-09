# Ucantalk

[English](README.md) | [简体中文](docs/README.zh-CN.md) | [日本語](docs/README.ja-JP.md) | [한국어](docs/README.ko-KR.md)

## Documentation / 教程 / マニュアル / 매뉴얼

- [Manual index / 手册索引 / マニュアル索引 / 매뉴얼 색인](docs/manuals/index.md)
- [Chinese manual / 中文手册 / 中国語マニュアル / 중국어 매뉴얼](docs/manuals/manual.zh-CN.md)
- [English manual / 英文手册 / 英語マニュアル / 영어 매뉴얼](docs/manuals/manual.en-US.md)
- [Japanese manual / 日文手册 / 日本語マニュアル / 일본어 매뉴얼](docs/manuals/manual.ja-JP.md)
- [Korean manual / 韩文手册 / 韓国語マニュアル / 한국어 매뉴얼](docs/manuals/manual.ko-KR.md)

Ucantalk is a WinUI 3 desktop app for VRChat-style speech assistance on Windows.
It combines text-to-speech, translation, speech input, audio routing, mobile control, and a built-in audio player in a single C# application.

## Features

- Text input and one-click TTS send
- TTS engines: Edge TTS, GPT-SoVITS, FishAudio
- Translation workflow with optional forced TTS language
- Speech input engines: Sherpa-ONNX, Vosk, Windows speech input
- Audio routing for monitor device and VB-Cable / VRC device
- Mobile web control with QR code
- Recent speech history and replay
- Role/profile management for GPT-SoVITS presets
- Built-in runtime log viewer

## Tech Stack

- C# / .NET 8
- WinUI 3
- NAudio
- WebView2
- Vosk
- sherpa-onnx

## Requirements

- Windows 10 1809 or later
- .NET 8 Desktop Runtime when running from source without self-contained publish
- VB-Cable if you want virtual microphone routing in VRChat
- GPT-SoVITS and compatible models if you want local voice cloning
- Optional API keys for translation or cloud TTS services

## Run From Source

```powershell
dotnet restore
dotnet build .\VRC_cantalkcn.csproj -r win-x64
dotnet run --project .\VRC_cantalkcn.csproj
```

## Build Installer

This project uses Inno Setup for the normal Windows installer.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-setup.ps1
```

If `ISCC.exe` is not in `PATH`, pass the location manually:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-setup.ps1 -IsccPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

The generated installer will be placed in `artifacts\installer\`.

## External Dependencies

This repository contains the application source code only.
Large external runtime assets should not be committed to the repository.
Prepare or install them separately as needed:

- GPT-SoVITS backend and model weights
- FFmpeg
- Sherpa-ONNX models
- Any private API keys or personal configuration files

## Repository Notes

Before publishing the repository, keep these files out of source control:

- personal config files
- runtime logs
- model files (`.onnx`, `.ckpt`, `.pth`)
- generated installers and build artifacts
- signing certificates

A project-level `.gitignore` is already included for this.

## License

MIT. See [LICENSE](LICENSE).
