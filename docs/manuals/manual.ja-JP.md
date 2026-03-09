# Ucantalk 利用マニュアル

作成者：NEKO_UMR  
日付：2026年3月  
バージョン：C# 再構築版（WinUI 3 / .NET 8 ベース）  
対応環境：Windows 10 (17763) 以降、x86 / x64 / ARM64

## 連絡先とフィードバック

- 中国本土のユーザー：QQ グループ `1040186268`
- 中国本土以外のユーザー：GitHub Issue を作成するか、`NEKO_UMR@qq.com` にメールしてください。
- 質問する前に、まず本マニュアル末尾の FAQ を確認してください。
- このマニュアルを AI に渡して、トラブルシュートを補助させることもできます。

## 第1章：インストールと初期設定

旧 Python 版と違い、新版では Python 環境も手動プラグイン導入も不要です。主要な機能は最初から組み込まれています。

### 1. アプリのインストール

1. `Ucantalk_Setup.exe` を実行します。
2. セットアップウィザードに従ってインストールします。
3. .NET 8 Desktop Runtime と VC++ 依存関係は自動で導入されます。
4. インストール後、スタートメニューまたはデスクトップのショートカットから起動します。

> 推奨：ショートカットのプロパティ -> 互換性 -> 「管理者としてこのプログラムを実行する」を有効にしてください。フルスクリーン時のグローバルホットキー失敗を減らせます。

### 2. 仮想サウンドカードの導入（VRChat ユーザー必須）

1. VB-Cable をダウンロード：<https://vb-audio.com/Cable/index.htm>
2. 展開後、`VBCABLE_Setup_x64.exe` を右クリックして「管理者として実行」を選択します。
3. `Install Driver` をクリックします。
4. インストール後に PC を再起動します。

### 3. FFmpeg の確認（同梱済み）

- `tools/ffmpeg/` に `ffmpeg.exe` が含まれています。
- 削除や移動をすると、オーディオプレーヤーと音声合成が動作しなくなります。

### 4. 音声認識モデル（同梱済み）

- `tools/sherpa-default/` に高品質な中国語オフライン認識モデルが含まれています。
- 初回は「音声認識」ページで `Sherpa-ONNX（既定モデル）` を選択するだけで使えます。
- 同梱モデルは中国語のみです。その他の言語は第4章を参照してください。

### 5. アプリの起動

- スタートメニューまたはデスクトップショートカットから `Ucantalk` を起動します。
- 管理者として実行する設定を推奨します。

## 第2章：音声設定（重要）

Ucantalk にはデュアルチャンネル音声ルーティングがあり、音声を自分用出力と VRChat 用出力に同時送信できます。OBS を経由する必要はありません。

### 2.1 アプリ内設定

`設定 -> 音声ルーティング` を開きます。

- **モニター用デバイス**
  - 例：`Speakers (Realtek Audio)`
  - 自分で AI 音声やプレーヤー音を聞くための出力先です。
- **VRC デバイス**
  - `CABLE Input (VB-Audio Virtual Cable)` を選択します。
  - ゲーム側へ送る音声に使います。

`CABLE Input` が見つからない場合は、まず更新ボタンを押してください。まだ表示されない場合、ドライバー導入失敗か再起動不足の可能性があります。

### 2.2 VRChat 内設定

`VRChat -> Audio -> Microphone` を開き、入力デバイスを次に設定します。

`CABLE Output (VB-Audio Virtual Cable)`

## 第3章：画面構成と主要機能

### ナビゲーション

- **ホーム**：テキスト入力、送信、最近の履歴
- **設定**：外観、音声ルーティング、ホットキー、プロキシなど
- **音声エンジン**：TTS エンジン設定、キャラクタープロファイル管理
- **プラグイン**：拡張機能の入口
- **翻訳設定**：翻訳エンジン、対象言語、主翻訳言語
- **音声認識**：認識エンジン、起動モード、高度な設定
- **音声プレーヤー**：BGM / 効果音再生、二重出力対応
- **ログ**：実行ログとトラブルシュート

### 1. ホーム

- 入力ボックスにテキストを入力します。
- `送信して読み上げ` を押すか、送信ホットキーを使います。
- 生成された音声は自分と VRChat の両方へ再生されます。
- 自動送信を有効にすると、音声認識結果が自動で送信されます。
- 最近の記録を有効にすると、履歴をホーム下部から再送できます。

### 2. 設定

#### 外観

- テーマ：システム / ライト / ダーク
- 背景画像：任意の画像を設定可能
- 背景ぼかし：背景のぼかし強度を調整

#### ホットキー

- グローバル起動ホットキー
- 音声認識ホットキー
- 送信ホットキー

#### プロキシ

- API をプロキシ経由で使う場合は、`http://127.0.0.1:7890` のような形式で設定します。

### 3. 音声エンジン（TTS）

#### Edge TTS

- 初心者向け
- 追加設定不要
- 多言語・多音声対応
- 話速とピッチを調整可能

#### GPT-SoVITS

- ローカル音声クローン用
- 利用前に GPT-SoVITS の API サービス起動が必要です（第6章参照）
- 既定 API：`http://127.0.0.1:9880`
- 句読点除去を有効にすると、長い無音や引っかかりを減らせます。

#### FishAudio

- クラウド TTS
- API Key と参照音色 ID が必要です。

#### キャラクタープロファイル

各プロファイルには以下が保存されます。

- GPT モデルパス（`.ckpt`）
- SoVITS モデルパス（`.pth`）
- 参照音声（`.wav` / `.mp3`、3〜10秒推奨）
- 参照テキスト

新規作成、リネーム、削除、切り替えに対応しています。

### 4. 翻訳設定

使用可能な翻訳バックエンド：

- OpenAI 互換形式の汎用 AI モデル
- Google 翻訳
- DeepL

補足：

- 対象言語は最大 3 つまで設定できます。
- 結果は `|` で区切って表示されます。
- 主翻訳言語を設定すると、TTS は翻訳後の文を優先的に読み上げます。

#### 智譜 AI（BigModel）の設定例

1. <https://www.bigmodel.cn/invite?icode=8yoAYe%2BraIucAssS7dftTeZLO2QH3C0EBTSr%2BArzMw4%3D> を開く
2. 登録してログインする
3. プロフィールメニューから `API 密钥` に入る
4. 新しい API Key を作成してコピーする
5. Ucantalk 側で以下を設定する
   - API URL：`https://open.bigmodel.cn/api/paas/v4/`
   - モデル名：`glm-4-flash`
   - API Key を貼り付けて保存

### 5. 音声認識

対応エンジン：

- **Windows**：OS 標準
- **Vosk**：オフライン、手動モデル導入が必要
- **Sherpa-ONNX**：オフライン、高精度、CPU / CUDA / DML 対応

起動モード：

- **Toggle**：1回押して開始、もう1回押して停止
- **PTT**：押している間だけ話し、離すと送信
- **連続認識**：無音区間を検出して自動送信

Sherpa-ONNX の高度な設定：

- バックエンド：CPU / CUDA / DML
- スレッド数
- デコーダ：`greedy_search` / `modified_beam_search`

### 6. 音声プレーヤー

- 二重出力対応
- MP3 / FLAC / WAV などを再生可能
- BGM、音声パック、効果音用途向け
- 音声ルーティング設定を自動で利用します

### 7. スマホ入力

- ホーム画面に QR コードが表示されます。
- スマホで読み取るとモバイル操作ページを開けます。
- スマホと PC は同じ Wi‑Fi / LAN に接続されている必要があります。
- ゲームを切り替えずにスマホから文字送信できます。

## 第4章：Sherpa-ONNX モデル解説

Sherpa-ONNX は既定のオフライン音声認識エンジンです。CPU / NVIDIA CUDA / DirectML をサポートし、完全にローカルで動作します。

### 4.1 モデル一覧

- 公式一覧：<https://k2-fsa.github.io/sherpa/onnx/pretrained_models/index.html>
- GitHub Releases：<https://github.com/k2-fsa/sherpa-onnx/releases>

### 4.2 推奨モデル

#### 中国語（同梱）

- 同梱モデル：`sherpa-onnx-streaming-zipformer-zh-int8`
- 置き換え候補：
  - `sherpa-onnx-streaming-paraformer-bilingual-zh-en`
  - `sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20`

#### 英語

- 推奨：`sherpa-onnx-streaming-zipformer-en-2023-06-21`

#### 日本語

- 多言語推奨：`sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17`
- 日本語専用：`sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01`

#### 韓国語

- 推奨：`sherpa-onnx-streaming-zipformer-korean-2024-06-16`

#### 多言語利用者向け

- `SenseVoice`：中国語、英語、日本語、韓国語、広東語を1つのモデルで扱えます。

### 4.3 ダウンロードと導入手順

1. 対応する `.tar.bz2` をダウンロード
2. 7-Zip / WinRAR で解凍
3. `C:\sherpa-models\...` のような英数字のみのパスに配置
4. `音声認識 -> Sherpa モデルパス` で、`.onnx` と `tokens.txt` を含むフォルダを選択
5. 保存して音声認識を再起動

### 4.4 バックエンドの目安

- CPU：互換性最優先
- CUDA：NVIDIA で高速
- DML：AMD / Intel 向け

### 4.5 代表的なファイル構成

- `encoder-*.onnx`
- `decoder-*.onnx`
- `joiner-*.onnx`
- `tokens.txt`

モデル種別は自動検出されます。

## 第5章：Vosk オフラインモデル設定（任意）

Vosk を使いたい場合は、モデルを手動で用意します。

### ダウンロード先

<https://alphacephei.com/vosk/models>

### 設定手順

1. モデルをダウンロードして解凍
2. `音声認識` ページで `Vosk` を選択
3. `model.conf` を含むフォルダを指定
4. 日本語や空白を含まないパスを推奨

## 第6章：GPT-SoVITS の導入と API 起動（上級）

ローカル音声クローンを使うには、先に GPT-SoVITS の API を起動する必要があります。

### 1. パッケージの入手

- 公式：<https://github.com/RVC-Boss/GPT-SoVITS/releases>
- 中国向けミラー 1：<https://www.modelscope.cn/models/FlowerCry/gpt-sovits-7z-pacakges/resolve/master/GPT-SoVITS-v2pro-20250604.7z>
- 中国向けミラー 2：<https://hf-mirror.com/lj1995/GPT-SoVITS-windows-package/resolve/main/GPT-SoVITS-v2pro-20250604.7z?download=true>

### 2. API の起動

Ucantalk が使うのは `9880` 番ポートの API で、`9872` 番ポートの WebUI ではありません。

`api.bat` がない場合は、GPT ルートに以下を作成します。

```bat
@echo off
runtime\python.exe api_v2.py
pause
```

成功すると次が表示されます。

`Uvicorn running on http://0.0.0.0:9880`

### 3. モデルの準備

`音声エンジン` ページで以下を設定します。

- GPT モデル（`.ckpt`）
- SoVITS モデル（`.pth`）
- 参照音声（`.wav` / `.mp3`、3〜10秒推奨）
- 参照テキスト

## 第7章：FAQ

### Q1: 「API 接続失敗」または「Connection Error」

- GPT-SoVITS バックエンドが起動しているか確認
- WebUI ではなく API を起動しているか確認
- アドレスが `http://127.0.0.1:9880` か確認
- プロキシ利用時は Ucantalk 側へ設定するか、システムプロキシを一時的に無効化

### Q2: GPT-SoVITS の発話が引っかかる / 無音が長い

- 句読点に敏感なケースがあります。
- `音声エンジン` で句読点除去を有効にしてください。

### Q3: 合成時に無音またはクラッシュする

- 新しい GPU と同梱 CUDA の相性問題の可能性があります。
- `GPT_SoVITS/configs/tts_infer.yaml` の `device: "cuda"` を `device: "cpu"` に変更し、API を再起動します。

### Q4: `CABLE Input` が出てこない

- PC を再起動
- それでもダメなら VB-Cable を管理者権限で再インストール

### Q5: 「Failed to create a model」

- Vosk に設定されているのに、有効なモデルフォルダが選ばれていない可能性があります。
- `Sherpa-ONNX` または `Windows` に戻すか、`model.conf` を含むフォルダを選び直してください。
- モデルパスに日本語や空白を含めないことを推奨します。

### Q6: 翻訳が効かない / `NoKey` が出る

- 有効な API Key が入っているか確認
- BigModel の `glm-4-flash` を推奨

### Q7: フルスクリーン時にホットキーが効かない

- Ucantalk を管理者として実行してください。

### Q8: QR コードを読んでもモバイルページが開かない

主な原因：

- ネットワークプロファイルが Public
- Windows Firewall がブロックしている
- スマホと PC が別の Wi‑Fi に接続されている

対処：

1. ネットワークを Private に変更
2. `Ucantalk.exe` を Windows Firewall で許可
3. 同一 LAN にいることを確認

### Q9: VB-Cable のサンプルレートエラー

- Windows サウンドコントロールパネルを開く
- `CABLE Input -> プロパティ -> 詳細` を開く
- `2ch / 16bit / 44100 Hz` に変更
- Ucantalk を再起動

### Q10: Sherpa-ONNX が遅い

- スレッド数を増やす
- NVIDIA GPU がある場合は CUDA に切り替える

### Q11: GPT-SoVITS の負荷が高く VRChat が重い

- 既定で CUDA を使っている可能性があります。
- Q3 と同じ方法で CPU に切り替えてください。

### Q12: 送信成功と出るが音が出ない

次を順に確認してください。

1. 音声ルーティングが正しいか
2. VRChat のマイクが `CABLE Output` か
3. アプリ内音量が 0 でないか

## 付録：設定ファイルの場所

設定ファイル：

`C:\Users\<ユーザー名>\AppData\Roaming\Ucantalk\config.json`

ログフォルダ：

`C:\Users\<ユーザー名>\AppData\Roaming\Ucantalk\logs\`

深刻な問題が発生した場合は、`config.json` を削除して初期化できます。

このマニュアルは Ucantalk の C# WinUI 3 / .NET 8 版向けです。旧 Python 版には対応していません。
