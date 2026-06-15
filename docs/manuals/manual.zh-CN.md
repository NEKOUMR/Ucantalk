# Ucantalk 使用手册

作者：NEKO_UMR  
日期：2026年3月  
版本：C# 重构版（基于 WinUI 3 / .NET 8）  
适用系统：Windows 10 (17763) 及以上，支持 x86 / x64 / ARM64

## 交流与反馈

- 中国大陆用户：QQ群 `1040186268`（使用问题或 Bug 反馈建议加群）。
- 非中国大陆用户：请直接在 GitHub 提 Issue，或发送邮件至 `NEKO_UMR@qq.com`。
- 遇到问题时，建议先查看本手册最后的 FAQ。
- 你也可以把这份手册交给 AI，让它根据手册内容协助排查问题。

## 第一章：安装与初始化

与旧版 Python 版相比，新版已经不再需要 Python 环境，也不需要手动安装插件。大部分功能已内置，安装后即可使用。

### 1. 安装软件

1. 运行 `Ucantalk_Setup.exe`。
2. 按安装向导完成安装。
3. 程序会自动安装 .NET 8 Desktop Runtime 和 VC++ 依赖。
4. 安装完成后，从开始菜单或桌面快捷方式启动软件。

> 建议：右键软件图标 -> 属性 -> 兼容性 -> 勾选“以管理员身份运行此程序”，避免游戏全屏时全局热键失效。

### 2. 安装虚拟声卡（VRChat 用户必做）

1. 下载 VB-Cable：<https://vb-audio.com/Cable/index.htm>
2. 解压后右键 `VBCABLE_Setup_x64.exe`，选择“以管理员身份运行”。
3. 点击 `Install Driver`。
4. 安装完成后重启电脑。

### 3. 检查 FFmpeg（已内置）

- 软件目录下 `tools/ffmpeg/` 已内置 `ffmpeg.exe`。
- 不要删除或移动这个文件，否则音频播放器和语音合成功能会失效。

### 4. 语音识别模型（已内置）

- 软件已在 `tools/sherpa-default/` 内置高质量中文离线语音识别模型。
- 首次使用语音识别时，直接在“语音识别”页面选择 `Sherpa-ONNX（默认模型）` 即可。
- 当前内置的是中文模型，其他语言模型请参考第四章。

### 5. 启动软件

- 从开始菜单或桌面快捷方式双击启动 `Ucantalk`。
- 同样建议勾选“以管理员身份运行此程序”。

## 第二章：音频设置（关键步骤）

Ucantalk 内置双通道音频路由，可以把声音同时发给你自己和 VRChat，而不需要通过 OBS 中转。

### 2.1 软件内部设置

打开软件后，进入：`设置 -> 音频路由`

- **监听设备（给自己听）**
  - 选择物理耳机或音响，例如 `Speakers (Realtek Audio)`。
  - 用于本地监听 AI 语音和播放器内容。
- **VRC 设备（给麦克风）**
  - 选择 `CABLE Input (VB-Audio Virtual Cable)`。
  - 用于把声音送进游戏。

如果找不到 `CABLE Input`，先点击右侧“刷新”。如果刷新后仍然没有，通常是驱动没有安装成功，或者安装后没有重启电脑。

### 2.2 VRChat 内部设置

进入 `VRChat -> Audio -> Microphone`，将输入设备设置为：

`CABLE Output (VB-Audio Virtual Cable)`

## 第三章：界面总览与核心功能

### 导航栏功能

- **主页**：文字输入、发送、最近记录
- **设置**：外观、音频路由、热键、代理等基础设置
- **语音引擎**：TTS 引擎选择、角色档案管理
- **插件**：扩展功能入口
- **翻译设置**：翻译引擎、目标语言、主翻译语言
- **语音识别**：识别引擎、触发模式、高级参数
- **音频播放器**：BGM / 音效播放，支持双路输出
- **日志**：运行日志查看与排障

### 1. 主页

- 在“输入要说的话”文本框中输入内容。
- 点击“发送并朗读”，或按发送热键。
- 软件会合成语音，并同时播放给你自己和 VRChat。
- 如果开启自动发送，语音识别结果会自动触发发送。
- 如果开启最近记录，历史内容会显示在主页下方，可点击重发。

### 2. 设置

#### 外观

- 主题：跟随系统 / 浅色 / 深色
- 背景图片：可自定义背景图
- 背景模糊：调节背景虚化程度

#### 热键

- 全局唤醒热键：激活或置顶窗口
- 语音识别热键：触发或停止语音识别
- 发送热键：发送当前输入内容

#### 代理

- 如需代理访问 API，可填写类似 `http://127.0.0.1:7890` 的代理地址。

### 3. 语音引擎（TTS）

#### Edge TTS

- 适合新手，开箱即用。
- 支持多语言、多音色。
- 可调节语速和音调。

#### GPT-SoVITS

- 用于本地音色克隆。
- 使用前需要先启动 GPT-SoVITS 后台 API（见第六章）。
- 默认 API 地址：`http://127.0.0.1:9880`
- 建议开启“去除标点”，可缓解说话卡顿和长停顿问题。

#### FishAudio

- 云端语音服务。
- 需要填写 FishAudio API Key 和参考音色 ID。

#### 角色档案

每个角色档案会保存一整套 GPT-SoVITS 配置，包括：

- GPT 模型路径（`.ckpt`）
- SoVITS 模型路径（`.pth`）
- 参考音频路径（`.wav` / `.mp3`，建议 3 至 10 秒）
- 参考文本

支持新建、重命名、删除和切换角色档案。

### 4. 翻译设置

可用翻译引擎：

- 通用 AI 大模型（兼容 OpenAI 格式 API，例如智谱、DeepSeek、通义千问）
- Google 翻译
- DeepL

说明：

- 最多可配置 3 个目标语言。
- 翻译结果会以 `|` 分隔显示。
- 设置主翻译语言后，TTS 会优先朗读翻译后的文本。

#### 智谱 AI（BigModel）配置示例

1. 打开官网：<https://www.bigmodel.cn/invite?icode=8yoAYe%2BraIucAssS7dftTeZLO2QH3C0EBTSr%2BArzMw4%3D>
2. 注册并登录账号。
3. 在右上角头像菜单中进入“API 密钥”。
4. 创建新的 API Key 并复制。
5. 回到 Ucantalk 的“翻译设置”：
   - API 地址填写：`https://open.bigmodel.cn/api/paas/v4/`
   - 模型名称填写：`glm-4-flash`
   - 粘贴 API Key 并保存。

### 5. 语音识别

支持的引擎：

- **Windows**：系统内置，无需额外配置
- **Vosk**：本地离线，需要手动下载模型
- **Sherpa-ONNX**：本地离线，精度高，支持 CPU / CUDA / DirectML

触发模式：

- **Toggle**：按一次开始，再按一次停止
- **PTT（长按说话）**：按住说话，松开发送
- **连续识别**：自动检测停顿并发送

Sherpa-ONNX 高级选项：

- 运算后端：CPU / CUDA / DML
- 线程数：根据 CPU 核心数调整
- 解码方式：`greedy_search` 或 `modified_beam_search`

### 6. 音频播放器

- 支持双路输出
- 支持 MP3、FLAC、WAV 等常见格式
- 适合播放 BGM、角色语音包、音效
- 自动读取设置中的音频路由设备

### 7. 手机输入功能

- 主页会显示二维码
- 用手机扫描二维码即可进入手机控制页
- 手机和电脑必须连接到同一个局域网 / Wi‑Fi
- 手机端可以输入文字并直接发送，不需要切出游戏

## 第四章：Sherpa-ONNX 模型说明

Sherpa-ONNX 是软件默认的离线语音识别引擎，支持 CPU / NVIDIA GPU / DirectML，多语言支持强，完全本地运行。

### 4.1 模型总览

- 官方模型列表：<https://k2-fsa.github.io/sherpa/onnx/pretrained_models/index.html>
- GitHub 发布页：<https://github.com/k2-fsa/sherpa-onnx/releases>

### 4.2 推荐模型

#### 中文（内置）

- 已内置：`sherpa-onnx-streaming-zipformer-zh-int8`
- 可选替换：
  - `sherpa-onnx-streaming-paraformer-bilingual-zh-en`
  - `sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20`

#### 英文

- 推荐：`sherpa-onnx-streaming-zipformer-en-2023-06-21`

#### 日文

- 推荐多语言：`sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17`
- 纯日语：`sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01`

#### 韩文

- 推荐：`sherpa-onnx-streaming-zipformer-korean-2024-06-16`

#### 多语言用户推荐

- `SenseVoice`：一个模型同时支持中文、英文、日文、韩文、粤语，自动识别语种。

### 4.3 下载与安装步骤

1. 下载对应模型压缩包（通常为 `.tar.bz2`）。
2. 用 7-Zip 或 WinRAR 解压。
3. 把模型文件夹放到纯英文路径下，例如 `C:\sherpa-models\...`。
4. 在软件中进入 `语音识别 -> Sherpa 模型路径`，选择包含 `.onnx` 文件和 `tokens.txt` 的文件夹。
5. 保存后重新启动语音识别。

### 4.4 运算后端建议

- CPU：兼容性最好，默认推荐
- CUDA：适合 NVIDIA 显卡，速度最快
- DML：适合 AMD / Intel 等非 NVIDIA 显卡

### 4.5 模型文件结构说明

常见文件包括：

- `encoder-*.onnx`
- `decoder-*.onnx`
- `joiner-*.onnx`
- `tokens.txt`

软件会自动识别模型类型，不需要手动逐个指定文件。

## 第五章：Vosk 离线模型配置（可选）

如果你想使用 Vosk，而不是内置的 Sherpa-ONNX，需要手动下载模型。

### 下载地址

<https://alphacephei.com/vosk/models>

### 配置步骤

1. 下载并解压模型。
2. 进入 `语音识别` 页面，识别引擎选择 `Vosk`。
3. 选择包含 `model.conf` 的文件夹。
4. 建议把模型放在纯英文路径下，避免因中文或空格导致加载失败。

## 第六章：GPT-SoVITS 下载与后台设置（进阶）

如果你要使用 GPT-SoVITS 的本地克隆音色模式，必须先启动后台 API 服务和下载python
下载python:https://www.python.org/ftp/python/3.12.10/python-3.12.10-amd64.exe


### 1. 下载整合包

- 官方发布页：<https://github.com/RVC-Boss/GPT-SoVITS/releases>
- 中国大陆下载源 1：<https://www.modelscope.cn/models/FlowerCry/gpt-sovits-7z-pacakges/resolve/master/GPT-SoVITS-v2pro-20250604.7z>
- 中国大陆下载源 2：<https://hf-mirror.com/lj1995/GPT-SoVITS-windows-package/resolve/main/GPT-SoVITS-v2pro-20250604.7z?download=true>

### 2. 启动 API 服务

注意：Ucantalk 连接的是 `9880` 端口的 API，不是 `9872` 端口的 WebUI。

如果整合包里没有现成的 `api.bat`，可以在 GPT 根目录新建一个：

```bat
@echo off
runtime\python.exe api_v2.py
pause
```

启动成功后，黑色窗口中应出现：

`Uvicorn running on http://0.0.0.0:9880`

### 3. 准备模型

在 Ucantalk 的 `语音引擎` 页面中填写：

- GPT 模型（`.ckpt`）
- SoVITS 模型（`.pth`）
- 参考音频（`.wav` / `.mp3`，建议 3 至 10 秒）
- 参考文本

## 第七章：常见问题（FAQ）

### Q1: 提示“API 连接失败”或 “Connection Error”

- 确认 GPT-SoVITS 后台已启动。
- 确认不是 WebUI，而是 API 服务。
- 确认地址是 `http://127.0.0.1:9880`。
- 如使用代理，请在设置中填写代理地址或临时关闭系统代理。

### Q2: GPT-SoVITS 说话卡顿或中间长停顿

- 原因通常是模型对标点敏感。
- 建议在 `语音引擎` 页面开启“去除标点”。

### Q3: GPT-SoVITS 一合成就没声音或闪退

- 某些新显卡与整合包内置 CUDA 版本兼容性较差。
- 可在 `GPT_SoVITS/configs/tts_infer.yaml` 中把 `device: "cuda"` 改为 `device: "cpu"`。
- 修改后重启 API。

### Q4: 找不到 CABLE Input 设备

- 先重启电脑。
- 如果仍无设备，请重新以管理员身份安装 VB-Cable。

### Q5: 提示“模型加载失败: Failed to create a model”

- 说明当前识别引擎设为 Vosk，但给定路径中没有有效模型。
- 切回 `Sherpa-ONNX` 或 `Windows`，或重新选择直接包含 `model.conf` 的文件夹。
- 同时避免模型路径中出现中文或空格。

### Q6: 翻译没有生效 / 显示 `NoKey`

- 确认已填写有效 API Key。
- 推荐使用智谱 AI 的 `glm-4-flash`。

### Q7: 热键在游戏全屏时失效

- 请将 Ucantalk 设置为“以管理员身份运行”。

### Q8: 手机扫码后网页打不开

可能原因：

- 电脑当前网络被设置为“公用网络”
- Windows 防火墙拦截端口
- 手机和电脑不在同一 Wi‑Fi

排查建议：

1. 把当前网络改为“专用网络”。
2. 在 Windows 防火墙中允许 `Ucantalk.exe` 通信。
3. 确认手机和电脑接入的是同一个局域网。

### Q9: VB-Cable 采样率报错

- 打开 Windows 声音控制面板。
- 找到 `CABLE Input -> 属性 -> 高级`。
- 把默认格式设为 `2通道，16位，44100 Hz`。
- 重启软件。

### Q10: Sherpa-ONNX 识别很慢

- 增大线程数。
- 如果有 NVIDIA 显卡，可切换到 CUDA。

### Q11: GPT-SoVITS 占用太大，导致 VRChat 卡顿

- 原因通常是默认使用 CUDA，占用显卡资源高。
- 处理方式同 Q3，改用 CPU。

### Q12: 软件显示发送成功，但没有声音

请依次检查：

1. 音频路由是否正确配置。
2. VRChat 麦克风是否切换为 `CABLE Output`。
3. 软件内音量是否不是 0。

## 附录：配置文件位置

配置文件默认位置：

`C:\Users\<你的用户名>\AppData\Roaming\Ucantalk\config.json`

日志目录：

`C:\Users\<你的用户名>\AppData\Roaming\Ucantalk\logs\`

如果遇到严重问题，可以尝试删除 `config.json`，把所有设置恢复为默认值。

本文档适用于 Ucantalk C# 重构版（WinUI 3 / .NET 8），不适用于旧版 Python 版手册。
