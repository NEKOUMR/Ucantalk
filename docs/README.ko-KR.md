# Ucantalk

[English](../README.md) | [简体中文](README.zh-CN.md) | [日本語](README.ja-JP.md) | [한국어](README.ko-KR.md)

Ucantalk은 VRChat 스타일의 음성 보조 커뮤니케이션을 위해 설계된 WinUI 3 기반 Windows 데스크톱 앱입니다.
텍스트 음성 변환, 번역, 음성 입력, 오디오 라우팅, 모바일 제어, 내장 오디오 플레이어를 하나의 C# 애플리케이션으로 통합합니다.

## 주요 기능

- 텍스트 입력 및 원클릭 TTS 전송
- TTS 엔진: Edge TTS, GPT-SoVITS, FishAudio
- 선택적 TTS 강제 언어가 포함된 번역 워크플로
- 음성 입력 엔진: Sherpa-ONNX, Vosk, Windows 음성 입력
- 모니터 장치와 VB-Cable / VRC 장치용 오디오 라우팅
- QR 코드 기반 모바일 웹 제어
- 최근 발화 기록 및 재전송
- GPT-SoVITS 역할 / 프로필 관리
- 내장 런타임 로그 뷰어

## 기술 스택

- C# / .NET 8
- WinUI 3
- NAudio
- WebView2
- Vosk
- sherpa-onnx

## 요구 사항

- Windows 10 1809 이상
- 자체 포함 배포가 아닌 경우 .NET 8 Desktop Runtime
- VRChat에서 가상 마이크 라우팅을 쓰려면 VB-Cable
- 로컬 음성 클론을 쓰려면 GPT-SoVITS 및 호환 모델
- 클라우드 번역 또는 클라우드 TTS를 쓰려면 추가 API Key가 필요할 수 있음

## 소스에서 실행

```powershell
dotnet restore
dotnet build .\VRC_cantalkcn.csproj -r win-x64
dotnet run --project .\VRC_cantalkcn.csproj
```

## 설치 프로그램 빌드

이 프로젝트는 일반적인 Windows 설치 프로그램 생성을 위해 Inno Setup을 사용합니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-setup.ps1
```

`ISCC.exe` 가 `PATH` 에 없으면 직접 경로를 지정할 수 있습니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-setup.ps1 -IsccPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

생성된 설치 프로그램은 `artifacts\installer\` 에 저장됩니다.

## 외부 의존성

이 저장소에는 애플리케이션 소스 코드만 포함하는 것을 권장합니다.
다음과 같은 큰 런타임 자산은 필요할 때 별도로 준비하십시오.

- GPT-SoVITS 백엔드 및 모델 가중치
- FFmpeg
- Sherpa-ONNX 모델
- 개인용 API Key 및 설정 파일

## 저장소 주의 사항

공개 저장소에 다음 파일은 커밋하지 마십시오.

- 개인 설정 파일
- 런타임 로그
- 모델 파일(`.onnx`, `.ckpt`, `.pth`)
- 설치 프로그램 및 빌드 산출물
- 서명 인증서

루트에 `.gitignore` 가 이미 포함되어 있습니다.

## 라이선스

MIT. 자세한 내용은 [LICENSE](../LICENSE) 를 참고하십시오.
