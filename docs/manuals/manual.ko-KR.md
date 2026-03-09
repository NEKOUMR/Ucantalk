# Ucantalk 사용 설명서

작성자: NEKO_UMR  
작성일: 2026년 3월  
버전: C# 재구성판 (WinUI 3 / .NET 8 기반)  
지원 환경: Windows 10 (17763) 이상, x86 / x64 / ARM64

## 문의 및 피드백

- 중국 본토 사용자: QQ 그룹 `1040186268`
- 중국 본토 외 사용자: GitHub Issue를 등록하거나 `NEKO_UMR@qq.com` 으로 메일을 보내십시오.
- 질문하기 전에 문서 마지막의 FAQ를 먼저 확인하십시오.
- 이 설명서를 AI에게 넘겨 문제 해결을 보조하게 할 수도 있습니다.

## 1장: 설치 및 초기화

기존 Python 버전과 달리, 새 버전은 Python 환경이나 수동 플러그인 설치가 필요하지 않습니다. 주요 기능은 대부분 기본 내장되어 있습니다.

### 1. 프로그램 설치

1. `Ucantalk_Setup.exe` 를 실행합니다.
2. 설치 마법사에 따라 설치를 완료합니다.
3. .NET 8 Desktop Runtime 및 VC++ 의존성은 자동으로 설치됩니다.
4. 설치 후 시작 메뉴 또는 바탕화면 바로가기로 프로그램을 실행합니다.

> 권장: 바로가기 속성 -> 호환성 -> “관리자 권한으로 이 프로그램 실행”을 켜 두면 전체 화면 게임에서 전역 핫키가 덜 실패합니다.

### 2. 가상 사운드 카드 설치 (VRChat 사용자 필수)

1. VB-Cable 다운로드: <https://vb-audio.com/Cable/index.htm>
2. 압축을 푼 뒤 `VBCABLE_Setup_x64.exe` 를 우클릭하고 “관리자 권한으로 실행”을 선택합니다.
3. `Install Driver` 를 클릭합니다.
4. 설치 후 PC를 재부팅합니다.

### 3. FFmpeg 확인 (기본 포함)

- `tools/ffmpeg/` 안에 `ffmpeg.exe` 가 포함되어 있습니다.
- 삭제하거나 이동하면 오디오 플레이어와 음성 합성이 동작하지 않습니다.

### 4. 음성 인식 모델 (기본 포함)

- `tools/sherpa-default/` 에 고품질 중국어 오프라인 음성 인식 모델이 포함되어 있습니다.
- 처음 사용할 때는 `Sherpa-ONNX (기본 모델)` 을 선택하면 됩니다.
- 기본 포함 모델은 중국어용입니다. 다른 언어는 4장을 참고하십시오.

### 5. 프로그램 실행

- 시작 메뉴 또는 바탕화면 바로가기로 `Ucantalk` 을 실행합니다.
- 관리자 권한 실행 설정을 권장합니다.

## 2장: 오디오 설정 (중요)

Ucantalk 에는 듀얼 채널 오디오 라우팅이 내장되어 있어, 자신의 청취 장치와 VRChat 쪽으로 동시에 음성을 보낼 수 있습니다. OBS를 중간에 둘 필요가 없습니다.

### 2.1 Ucantalk 내부 설정

`설정 -> 오디오 라우팅` 으로 이동합니다.

- **모니터 장치**
  - 예: `Speakers (Realtek Audio)`
  - 내가 직접 듣는 출력 장치입니다.
- **VRC 장치**
  - `CABLE Input (VB-Audio Virtual Cable)` 을 선택합니다.
  - 게임으로 보내는 출력입니다.

`CABLE Input` 이 보이지 않으면 먼저 새로 고침을 눌러 보십시오. 그래도 없으면 드라이버 설치 실패 또는 재부팅 누락 가능성이 큽니다.

### 2.2 VRChat 내부 설정

`VRChat -> Audio -> Microphone` 에서 입력 장치를 다음으로 설정합니다.

`CABLE Output (VB-Audio Virtual Cable)`

## 3장: 화면 구성 및 핵심 기능

### 내비게이션

- **홈**: 텍스트 입력, 전송, 최근 기록
- **설정**: 외형, 오디오 라우팅, 핫키, 프록시 등 기본 설정
- **음성 엔진**: TTS 엔진 선택, 캐릭터 프로필 관리
- **플러그인**: 확장 기능 입구
- **번역 설정**: 번역 엔진, 대상 언어, 주 번역 언어
- **음성 인식**: 인식 엔진, 트리거 모드, 고급 설정
- **오디오 플레이어**: BGM / 효과음 재생, 듀얼 출력 지원
- **로그**: 실행 로그 및 문제 진단

### 1. 홈

- 입력창에 텍스트를 입력합니다.
- `전송 및 읽기` 버튼을 누르거나 전송 핫키를 사용합니다.
- 음성은 자신과 VRChat 쪽으로 동시에 재생됩니다.
- 자동 전송이 켜져 있으면 음성 인식 결과가 자동 전송됩니다.
- 최근 기록이 켜져 있으면 이전 문장을 클릭해 다시 보낼 수 있습니다.

### 2. 설정

#### 외형

- 테마: 시스템 / 라이트 / 다크
- 배경 이미지: 사용자 지정 가능
- 배경 흐림: 배경 블러 강도 조절

#### 핫키

- 전역 깨우기 핫키
- 음성 인식 핫키
- 전송 핫키

#### 프록시

- API 접근에 프록시가 필요하면 `http://127.0.0.1:7890` 형식으로 입력합니다.

### 3. 음성 엔진 (TTS)

#### Edge TTS

- 초보자에게 적합
- 별도 설정 없이 바로 사용 가능
- 다국어 / 다화자 지원
- 속도와 음조 조절 가능

#### GPT-SoVITS

- 로컬 음색 복제용
- 사용 전에 GPT-SoVITS API 서비스 실행이 필요합니다. 6장을 참고하십시오.
- 기본 API 주소: `http://127.0.0.1:9880`
- 구두점 제거를 켜 두면 긴 정지나 끊김을 줄이는 데 도움이 됩니다.

#### FishAudio

- 클라우드 TTS 서비스
- API Key 와 참조 음색 ID 가 필요합니다.

#### 캐릭터 프로필

각 프로필에는 다음 정보가 저장됩니다.

- GPT 모델 경로 (`.ckpt`)
- SoVITS 모델 경로 (`.pth`)
- 참조 음성 (`.wav` / `.mp3`, 3~10초 권장)
- 참조 텍스트

프로필은 생성, 이름 변경, 삭제, 전환이 가능합니다.

### 4. 번역 설정

지원 번역 백엔드:

- OpenAI 호환 형식의 범용 AI 모델
- Google 번역
- DeepL

설명:

- 대상 언어는 최대 3개까지 설정할 수 있습니다.
- 결과는 `|` 로 구분되어 표시됩니다.
- 주 번역 언어를 설정하면 TTS 는 번역된 결과를 우선 읽습니다.

#### BigModel 설정 예시

1. <https://www.bigmodel.cn/invite?icode=8yoAYe%2BraIucAssS7dftTeZLO2QH3C0EBTSr%2BArzMw4%3D> 방문
2. 회원가입 및 로그인
3. 프로필 메뉴에서 `API 密钥` 진입
4. 새 API Key 생성 및 복사
5. Ucantalk 번역 설정에서 다음 입력
   - API URL: `https://open.bigmodel.cn/api/paas/v4/`
   - 모델명: `glm-4-flash`
   - API Key 붙여넣기 후 저장

### 5. 음성 인식

지원 엔진:

- **Windows**: 시스템 기본 제공
- **Vosk**: 오프라인, 수동 모델 다운로드 필요
- **Sherpa-ONNX**: 오프라인, 고정확도, CPU / CUDA / DML 지원

트리거 모드:

- **Toggle**: 한 번 눌러 시작, 다시 눌러 종료
- **PTT**: 누르는 동안 말하고 떼면 전송
- **연속 인식**: 멈춤을 감지해 자동 전송

Sherpa-ONNX 고급 설정:

- 백엔드: CPU / CUDA / DML
- 스레드 수
- 디코더: `greedy_search` / `modified_beam_search`

### 6. 오디오 플레이어

- 듀얼 출력 지원
- MP3 / FLAC / WAV 등 재생 가능
- BGM, 음성팩, 효과음 재생에 적합
- 오디오 라우팅 설정을 자동으로 따름

### 7. 모바일 입력

- 홈 화면에 QR 코드가 표시됩니다.
- 휴대폰으로 스캔하면 모바일 제어 페이지를 열 수 있습니다.
- 휴대폰과 PC 는 같은 Wi‑Fi / 로컬 네트워크에 있어야 합니다.
- 게임 화면을 벗어나지 않고 휴대폰에서 텍스트를 전송할 수 있습니다.

## 4장: Sherpa-ONNX 모델 안내

Sherpa-ONNX 는 Ucantalk 기본 오프라인 음성 인식 엔진입니다. CPU / NVIDIA CUDA / DirectML 을 지원하며 완전히 로컬에서 동작합니다.

### 4.1 모델 목록

- 공식 목록: <https://k2-fsa.github.io/sherpa/onnx/pretrained_models/index.html>
- GitHub Releases: <https://github.com/k2-fsa/sherpa-onnx/releases>

### 4.2 추천 모델

#### 중국어 (기본 포함)

- 포함 모델: `sherpa-onnx-streaming-zipformer-zh-int8`
- 교체 후보:
  - `sherpa-onnx-streaming-paraformer-bilingual-zh-en`
  - `sherpa-onnx-streaming-zipformer-bilingual-zh-en-2023-02-20`

#### 영어

- 추천: `sherpa-onnx-streaming-zipformer-en-2023-06-21`

#### 일본어

- 다국어 추천: `sherpa-onnx-sense-voice-zh-en-ja-ko-yue-2024-07-17`
- 일본어 전용: `sherpa-onnx-zipformer-ja-reazonspeech-2024-08-01`

#### 한국어

- 추천: `sherpa-onnx-streaming-zipformer-korean-2024-06-16`

#### 다국어 사용자 추천

- `SenseVoice`: 중국어, 영어, 일본어, 한국어, 광둥어를 하나의 모델로 처리합니다.

### 4.3 다운로드 및 설치

1. 해당 `.tar.bz2` 파일 다운로드
2. 7-Zip 또는 WinRAR 로 압축 해제
3. `C:\sherpa-models\...` 같은 영문 경로에 배치
4. `음성 인식 -> Sherpa 모델 경로` 에서 `.onnx` 와 `tokens.txt` 가 들어 있는 폴더 선택
5. 저장 후 음성 인식 재시작

### 4.4 백엔드 권장

- CPU: 호환성 우선
- CUDA: NVIDIA 에서 가장 빠름
- DML: AMD / Intel 등에 적합

### 4.5 일반적인 파일 구조

- `encoder-*.onnx`
- `decoder-*.onnx`
- `joiner-*.onnx`
- `tokens.txt`

프로그램이 모델 유형을 자동 감지합니다.

## 5장: Vosk 오프라인 모델 설정 (선택)

Sherpa-ONNX 대신 Vosk 를 쓰려면 모델을 직접 다운로드해야 합니다.

### 다운로드 페이지

<https://alphacephei.com/vosk/models>

### 설정 절차

1. 모델을 다운로드하고 압축 해제
2. `음성 인식` 페이지에서 `Vosk` 선택
3. `model.conf` 가 들어 있는 폴더 지정
4. 가능하면 영문 전용 경로 사용

## 6장: GPT-SoVITS 다운로드 및 API 설정 (고급)

로컬 음색 복제를 사용하려면 먼저 GPT-SoVITS API 를 실행해야 합니다.

### 1. 패키지 다운로드

- 공식 릴리스: <https://github.com/RVC-Boss/GPT-SoVITS/releases>
- 중국 미러 1: <https://www.modelscope.cn/models/FlowerCry/gpt-sovits-7z-pacakges/resolve/master/GPT-SoVITS-v2pro-20250604.7z>
- 중국 미러 2: <https://hf-mirror.com/lj1995/GPT-SoVITS-windows-package/resolve/main/GPT-SoVITS-v2pro-20250604.7z?download=true>

### 2. API 시작

Ucantalk 이 사용하는 것은 `9880` 포트 API 이며, `9872` 포트 WebUI 가 아닙니다.

`api.bat` 가 없다면 GPT 루트에 다음 파일을 만드십시오.

```bat
@echo off
runtime\python.exe api_v2.py
pause
```

성공하면 다음이 표시됩니다.

`Uvicorn running on http://0.0.0.0:9880`

### 3. 모델 준비

`음성 엔진` 페이지에 다음을 입력합니다.

- GPT 모델 (`.ckpt`)
- SoVITS 모델 (`.pth`)
- 참조 음성 (`.wav` / `.mp3`, 3~10초 권장)
- 참조 텍스트

## 7장: FAQ

### Q1: “API 연결 실패” 또는 “Connection Error”

- GPT-SoVITS 백엔드가 실행 중인지 확인
- WebUI 가 아니라 API 를 실행했는지 확인
- 주소가 `http://127.0.0.1:9880` 인지 확인
- 프록시를 쓰면 Ucantalk 에 입력하거나 시스템 프록시를 잠시 끄기

### Q2: GPT-SoVITS 음성이 끊기거나 멈춤이 길다

- 구두점에 민감한 경우가 많습니다.
- `음성 엔진` 에서 구두점 제거를 켜십시오.

### Q3: 합성 시 무음 또는 크래시

- 최신 GPU 와 번들 CUDA 버전의 호환성 문제일 수 있습니다.
- `GPT_SoVITS/configs/tts_infer.yaml` 에서 `device: "cuda"` 를 `device: "cpu"` 로 바꾸고 API 를 재시작하십시오.

### Q4: `CABLE Input` 장치가 안 보인다

- 먼저 PC 재부팅
- 그래도 안 되면 VB-Cable 을 관리자 권한으로 다시 설치

### Q5: “Failed to create a model” 오류

- Vosk 로 설정되어 있는데 유효한 모델 폴더가 선택되지 않았을 가능성이 큽니다.
- `Sherpa-ONNX` 또는 `Windows` 로 바꾸거나 `model.conf` 가 들어 있는 폴더를 다시 선택하십시오.
- 경로에 한글이나 공백은 피하는 것이 좋습니다.

### Q6: 번역이 동작하지 않거나 `NoKey` 가 표시된다

- 유효한 API Key 가 있는지 확인
- 기본 추천은 BigModel `glm-4-flash`

### Q7: 게임 전체 화면에서 핫키가 안 먹는다

- Ucantalk 을 관리자 권한으로 실행하십시오.

### Q8: QR 코드를 스캔해도 모바일 페이지가 안 열린다

주요 원인:

- 네트워크 프로필이 Public 으로 설정됨
- Windows Firewall 이 포트를 차단함
- 휴대폰과 PC 가 같은 Wi‑Fi 가 아님

해결 순서:

1. 네트워크를 Private 으로 변경
2. `Ucantalk.exe` 를 Windows Firewall 예외에 추가
3. 두 기기가 같은 LAN 에 있는지 확인

### Q9: VB-Cable 샘플레이트 오류

- Windows 사운드 제어판 열기
- `CABLE Input -> 속성 -> 고급` 으로 이동
- `2채널 / 16비트 / 44100 Hz` 로 변경
- Ucantalk 재시작

### Q10: Sherpa-ONNX 가 느리다

- 스레드 수를 늘리기
- NVIDIA GPU 가 있으면 CUDA 로 전환

### Q11: GPT-SoVITS 자원 사용량이 너무 커서 VRChat 이 버벅인다

- 기본적으로 CUDA 를 사용 중일 가능성이 큽니다.
- Q3 와 같은 방식으로 CPU 로 전환하십시오.

### Q12: 전송 성공이라고 나오지만 소리가 없다

다음 순서대로 확인하십시오.

1. 오디오 라우팅이 올바른지
2. VRChat 마이크가 `CABLE Output` 인지
3. 앱 내부 볼륨이 0 이 아닌지

## 부록: 설정 파일 위치

설정 파일:

`C:\Users\<사용자이름>\AppData\Roaming\Ucantalk\config.json`

로그 폴더:

`C:\Users\<사용자이름>\AppData\Roaming\Ucantalk\logs\`

심각한 문제가 생기면 `config.json` 을 삭제해 모든 설정을 초기화할 수 있습니다.

이 문서는 Ucantalk 의 C# WinUI 3 / .NET 8 판 기준이며, 예전 Python 판에는 적용되지 않습니다.
