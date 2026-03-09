# Ucantalk User Manual

Author: NEKO_UMR  
Date: March 2026  
Version: C# rewrite edition (based on WinUI 3 / .NET 8)  
Supported systems: Windows 10 (17763) and later, x86 / x64 / ARM64

## Contact and Feedback

- Mainland China users: QQ group `1040186268` for usage questions and bug reports.
- Users outside mainland China: please open a GitHub issue or email `NEKO_UMR@qq.com`.
- Before asking for help, check the FAQ at the end of this manual.
- You can also give this manual to an AI assistant and let it guide you through troubleshooting.

## Chapter 1: Installation and Initialization

Compared with the old Python version, the new version no longer requires a Python environment or manual plugin installation. Most features are already built in.

### 1. Install the app

1. Run `Ucantalk_Setup.exe`.
2. Finish the installation wizard.
3. The installer will deploy .NET 8 Desktop Runtime and VC++ dependencies automatically.
4. Launch Ucantalk from the Start menu or desktop shortcut.

> Recommendation: open the app shortcut properties, go to Compatibility, and enable “Run this program as administrator” to avoid global hotkey issues in fullscreen games.

### 2. Install the virtual audio cable (required for VRChat users)

1. Download VB-Cable: <https://vb-audio.com/Cable/index.htm>
2. Extract it, then right-click `VBCABLE_Setup_x64.exe` and choose “Run as administrator”.
3. Click `Install Driver`.
4. Restart Windows after installation.

### 3. Check FFmpeg (bundled)

- `ffmpeg.exe` is included in `tools/ffmpeg/`.
- Do not delete or move it. The audio player and TTS playback depend on it.

### 4. Speech recognition model (bundled)

- A high-quality offline Chinese Sherpa-ONNX model is included in `tools/sherpa-default/`.
- For first use, select `Sherpa-ONNX (default model)` in the Speech Input page.
- Only Chinese is bundled by default. For other languages, see Chapter 4.

### 5. Launch the app

- Double-click `Ucantalk` from the Start menu or desktop shortcut.
- Running the app as administrator is still recommended.

## Chapter 2: Audio Setup (Important)

Ucantalk includes dual-channel audio routing. It can send audio to your local headphones and to VRChat at the same time, without OBS as a middle layer.

### 2.1 Configure inside Ucantalk

Open: `Settings -> Audio Routing`

- **Monitor Device**
  - Select your physical speakers or headphones, for example `Speakers (Realtek Audio)`.
  - This is what you hear locally.
- **VRC Device**
  - Select `CABLE Input (VB-Audio Virtual Cable)`.
  - This is what gets sent into the game.

If `CABLE Input` is missing, click Refresh first. If it still does not appear, the driver probably failed to install or the PC was not restarted.

### 2.2 Configure inside VRChat

Open `VRChat -> Audio -> Microphone` and set the microphone device to:

`CABLE Output (VB-Audio Virtual Cable)`

## Chapter 3: Interface Overview and Core Features

### Navigation

- **Home**: text input, send, recent history
- **Settings**: appearance, audio routing, hotkeys, proxy, and general options
- **Speech Engine**: TTS engine selection and character profile management
- **Plugins**: entry point for extra features
- **Translation**: translation engines, target languages, and primary translation language
- **Speech Input**: recognition engines, trigger modes, advanced options
- **Audio Player**: BGM / sound effect playback with dual output
- **Logs**: runtime logs and troubleshooting

### 1. Home

- Type the sentence into the input box.
- Click `Send and Speak` or use the send hotkey.
- Ucantalk synthesizes speech and plays it to both you and VRChat.
- If auto-send is enabled, speech recognition results will be sent automatically.
- If recent history is enabled, recognized or sent lines appear on the Home page and can be replayed.

### 2. Settings

#### Appearance

- Theme: System / Light / Dark
- Background image: optional custom background
- Background blur: adjusts background blur strength

#### Hotkeys

- Global wake hotkey: activate or bring the window to front
- Speech recognition hotkey: start or stop recognition
- Send hotkey: send the current input text

#### Proxy

- If you need a proxy for APIs, enter an address such as `http://127.0.0.1:7890`.

### 3. Speech Engine (TTS)

#### Edge TTS

- Best choice for beginners.
- Ready to use with no extra setup.
- Supports many languages and voices.
- Speed and pitch can be adjusted.

#### GPT-SoVITS

- Used for local voice cloning.
- You must start the GPT-SoVITS API service first. See Chapter 6.
- Default API address: `http://127.0.0.1:9880`
- Enabling punctuation cleanup is recommended to reduce long pauses and stutter.

#### FishAudio

- Cloud TTS service.
- Requires a FishAudio API key and reference voice ID.

#### Character Profiles

Each profile stores one complete GPT-SoVITS setup:

- GPT model path (`.ckpt`)
- SoVITS model path (`.pth`)
- Reference audio (`.wav` / `.mp3`, ideally 3 to 10 seconds)
- Reference transcript

Profiles can be created, renamed, deleted, and switched.

### 4. Translation

Supported translation backends:

- Universal AI models that are compatible with the OpenAI API format
- Google Translate
- DeepL

Notes:

- Up to 3 target languages can be configured.
- Translations are displayed separated by `|`.
- If a primary translation language is selected, TTS will read the translated result instead of the original text.

#### BigModel setup example

1. Visit <https://www.bigmodel.cn/invite?icode=8yoAYe%2BraIucAssS7dftTeZLO2QH3C0EBTSr%2BArzMw4%3D>
2. Register and sign in.
3. Open `API Keys` from the profile menu.
4. Create a new key and copy it.
5. In Ucantalk Translation settings:
   - API URL: `https://open.bigmodel.cn/api/paas/v4/`
   - Model: `glm-4-flash`
   - Paste the API key and save.

### 5. Speech Input

Supported engines:

- **Windows**: built into the system
- **Vosk**: offline, requires manual model download
- **Sherpa-ONNX**: offline, high accuracy, supports CPU / CUDA / DirectML

Trigger modes:

- **Toggle**: press once to start, press again to stop
- **PTT**: hold to talk, release to send
- **Continuous**: automatically detects pauses and sends text

Advanced Sherpa-ONNX options:

- Backend: CPU / CUDA / DML
- Threads: tune based on CPU core count
- Decoder: `greedy_search` or `modified_beam_search`

### 6. Audio Player

- Supports dual output
- Plays MP3, FLAC, WAV, and more
- Useful for BGM, voice packs, and sound effects
- Automatically follows your configured audio routing

### 7. Mobile Input

- A QR code is shown on the Home page.
- Scan it with your phone to open the mobile control page.
- Your phone and PC must be on the same local network / Wi‑Fi.
- You can type and send text from the phone without tabbing out of the game.

## Chapter 4: Sherpa-ONNX Model Guide

Sherpa-ONNX is the default offline speech recognition engine in Ucantalk. It runs locally and supports CPU, NVIDIA CUDA, and DirectML.

### 4.1 Model index

- Official model list: <https://k2-fsa.github.io/sherpa/onnx/pretrained_models/index.html>
- GitHub releases: <https://github.com/k2-fsa/sherpa-onnx/releases>

### 4.2 Recommended models

#### Chinese (bundled)

- Bundled model: `sherpa-onnx-streaming-zipformer-zh-int8`
- Optional replacements:
  - `sherpa-onnx-streaming-paraformer-bilingual-zh-en`
  - `sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20`

#### English

- Recommended: `sherpa-onnx-streaming-zipformer-en-2023-06-21`

#### Japanese

- Recommended multilingual model: `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17`
- Japanese-only model: `sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01`

#### Korean

- Recommended: `sherpa-onnx-streaming-zipformer-korean-2024-06-16`

#### Recommended for multi-language users

- `SenseVoice`: one model for Chinese, English, Japanese, Korean, and Cantonese with automatic language detection.

### 4.3 Download and install

1. Download the correct `.tar.bz2` package.
2. Extract it with 7-Zip or WinRAR.
3. Place the model folder in an English-only path such as `C:\sherpa-models\...`.
4. In Ucantalk, open `Speech Input`, choose `Sherpa-ONNX`, and select the folder containing `.onnx` files and `tokens.txt`.
5. Save and restart speech recognition.

### 4.4 Backend suggestions

- CPU: best compatibility
- CUDA: fastest on NVIDIA GPUs
- DML: suitable for AMD / Intel or non-NVIDIA acceleration

### 4.5 Typical model file structure

Common files include:

- `encoder-*.onnx`
- `decoder-*.onnx`
- `joiner-*.onnx`
- `tokens.txt`

Ucantalk detects the model type automatically.

## Chapter 5: Vosk Offline Model Setup (Optional)

If you prefer Vosk instead of Sherpa-ONNX, download the model manually.

### Download page

<https://alphacephei.com/vosk/models>

### Setup steps

1. Download and extract the model.
2. In `Speech Input`, choose `Vosk`.
3. Select the folder that contains `model.conf`.
4. Keep the model in an English-only path if possible.

## Chapter 6: GPT-SoVITS Download and API Setup (Advanced)

To use local voice cloning, you must start the GPT-SoVITS API service first.

### 1. Download the package

- Official release page: <https://github.com/RVC-Boss/GPT-SoVITS/releases>
- China mirror 1: <https://www.modelscope.cn/models/FlowerCry/gpt-sovits-7z-pacakges/resolve/master/GPT-SoVITS-v2pro-20250604.7z>
- China mirror 2: <https://hf-mirror.com/lj1995/GPT-SoVITS-windows-package/resolve/main/GPT-SoVITS-v2pro-20250604.7z?download=true>

### 2. Start the API service

Ucantalk uses the API on port `9880`, not the WebUI on port `9872`.

If the package does not include `api.bat`, create one in the GPT root folder:

```bat
@echo off
runtime\python.exe api_v2.py
pause
```

Success indicator:

`Uvicorn running on http://0.0.0.0:9880`

### 3. Prepare the model files

Fill the following in the `Speech Engine` page:

- GPT model (`.ckpt`)
- SoVITS model (`.pth`)
- Reference audio (`.wav` / `.mp3`, ideally 3 to 10 seconds)
- Reference transcript

## Chapter 7: FAQ

### Q1: “API connection failed” or “Connection Error”

- Make sure the GPT-SoVITS backend is running.
- Make sure you started the API service, not the WebUI.
- Check that the address is `http://127.0.0.1:9880`.
- If you use a proxy, set it in Ucantalk or disable the system proxy temporarily.

### Q2: GPT-SoVITS speech stutters or pauses too long

- The model may be too sensitive to punctuation.
- Enable punctuation cleanup in `Speech Engine`.

### Q3: GPT-SoVITS has no sound or crashes during synthesis

- Some newer GPUs are not fully compatible with the bundled CUDA version.
- Edit `GPT_SoVITS/configs/tts_infer.yaml` and change `device: "cuda"` to `device: "cpu"`.
- Restart the API after editing.

### Q4: `CABLE Input` does not appear

- Restart Windows first.
- If it still does not appear, reinstall VB-Cable as administrator.

### Q5: “Model loading failed: Failed to create a model”

- This usually means the engine is set to Vosk but the selected folder does not contain a valid model.
- Switch back to `Sherpa-ONNX` or `Windows`, or reselect a folder containing `model.conf`.
- Avoid model paths with non-English characters or spaces.

### Q6: Translation does not work / shows `NoKey`

- Make sure a valid API key is configured.
- `glm-4-flash` on BigModel is a good default choice.

### Q7: Hotkeys do not work in fullscreen games

- Run Ucantalk as administrator.

### Q8: The mobile web page does not open after scanning the QR code

Possible reasons:

- The PC network is set to Public instead of Private
- Windows Firewall blocks the app port
- The phone and PC are not connected to the same Wi‑Fi

Check the following:

1. Change the network profile to Private.
2. Allow `Ucantalk.exe` through Windows Firewall.
3. Make sure both devices are on the same LAN.

### Q9: VB-Cable sample rate error

- Open the Windows sound control panel.
- Open `CABLE Input -> Properties -> Advanced`.
- Set the default format to `2 channel, 16 bit, 44100 Hz`.
- Restart Ucantalk.

### Q10: Sherpa-ONNX is slow

- Increase the thread count.
- If you have an NVIDIA GPU, switch the backend to CUDA.

### Q11: GPT-SoVITS uses too many resources and VRChat becomes laggy

- It is probably using CUDA by default.
- Follow the same fix as Q3 and switch the device to CPU.

### Q12: The app says “sent successfully” but there is no sound

Check the following in order:

1. Audio routing is configured correctly.
2. VRChat microphone is set to `CABLE Output`.
3. The in-app volume is not set to 0.

## Appendix: Configuration File Locations

Default config file:

`C:\Users\<YourUserName>\AppData\Roaming\Ucantalk\config.json`

Log folder:

`C:\Users\<YourUserName>\AppData\Roaming\Ucantalk\logs\`

If something becomes badly broken, you can delete `config.json` to reset all settings.

This manual applies to the C# WinUI 3 / .NET 8 edition of Ucantalk, not the old Python edition.
