# Ucantalk

[English](../README.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja-JP.md) | [한국어](README.ko-KR.md)

Ucantalk は、VRChat スタイルの音声支援コミュニケーション向けに設計された WinUI 3 ベースの Windows デスクトップアプリです。
テキスト読み上げ、翻訳、音声入力、音声ルーティング、スマホ操作、内蔵オーディオプレーヤーを 1 つの C# アプリに統合しています。

## 主な機能

- テキスト入力とワンクリック TTS 送信
- TTS エンジン: Edge TTS、GPT-SoVITS、FishAudio
- TTS 強制言語オプション付き翻訳ワークフロー
- 音声入力エンジン: Sherpa-ONNX、Vosk、Windows 音声入力
- モニター用デバイスと VB-Cable / VRC 用デバイスへの音声ルーティング
- QR コード付きモバイル Web コントロール
- 直近発話履歴と再送
- GPT-SoVITS のロール / プロファイル管理
- 組み込みランタイムログビューア

## 技術スタック

- C# / .NET 8
- WinUI 3
- NAudio
- WebView2
- Vosk
- sherpa-onnx

## 動作要件

- Windows 10 1809 以降
- 自己完結型でない場合は .NET 8 Desktop Runtime
- VRChat で仮想マイクルーティングを使う場合は VB-Cable
- ローカル音声クローンを使う場合は GPT-SoVITS と対応モデル
- クラウド翻訳やクラウド TTS には追加の API Key が必要な場合があります

## ソースから実行

```powershell
dotnet restore
dotnet build .\VRC_cantalkcn.csproj -r win-x64
dotnet run --project .\VRC_cantalkcn.csproj
```

## インストーラーのビルド

このプロジェクトでは通常の Windows インストーラーに Inno Setup を使用します。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-setup.ps1
```

`ISCC.exe` が `PATH` にない場合は、手動で指定できます。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-setup.ps1 -IsccPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

生成されたインストーラーは `artifacts\installer\` に出力されます。

## 外部依存関係

このリポジトリにはアプリケーション本体のソースコードのみを含める想定です。
以下の大きなランタイム資産は必要に応じて別途用意してください。

- GPT-SoVITS バックエンドとモデルウェイト
- FFmpeg
- Sherpa-ONNX モデル
- 非公開 API Key や個人設定ファイル

## リポジトリに関する注意

公開前に次のものをコミットしないでください。

- 個人設定ファイル
- 実行ログ
- モデルファイル（`.onnx`、`.ckpt`、`.pth`）
- インストーラーやビルド成果物
- 署名証明書

ルートには `.gitignore` が用意されています。

## ライセンス

MIT。詳細は [LICENSE](../LICENSE) を参照してください。
