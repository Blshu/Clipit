# Clipit

![License](https://img.shields.io/badge/license-GPL--3.0-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20(x64)-0078D6.svg)
![Framework](https://img.shields.io/badge/.NET-8.0%20%7C%20WinUI%203-512BD4.svg)

**Clipit**은 WinUI 3 기반으로 제작된 가볍고 빠른 Windows용 포터블 클립보드 히스토리 유틸리티입니다.  
별도의 설치 과정이나 인증서 등록 없이 압축 해제 후 바로 실행할 수 있습니다.

---

## 주요 기능

- **빠른 전역 단축키 호출:** 어디서든 `Alt + V` (충돌 시 `Ctrl + Shift + V`)로 즉시 호출
- **키보드 중심 탐색:** 마우스 없이 `↑` / `↓` 방향키와 `Enter`만으로 즉시 복사 및 창 닫기
- **실시간 검색 필터링:** 저장된 클립보드 내역을 키워드로 빠르게 검색
- **중요 항목 고정(Pin) 및 일괄 삭제:** 자주 쓰는 문구는 고정하고 나머지 내역만 한 번에 정리
- **시스템 트레이 상주:** 창을 닫아도 백그라운드에서 가볍게 대기하며 트레이 아이콘으로 제어 가능
- **완전 포터블(Unpackaged):** Windows App SDK가 내장되어 추가 런타임 설치 불필요

---

## 단축키 안내

| 단축키 | 동작 |
| :--- | :--- |
| `Alt + V` | Clipit 창 열기 / 숨기기 |
| `↑` / `↓` | 클립보드 항목 위·아래 이동 |
| `Enter` | 선택한 항목을 클립보드에 복사하고 창 닫기 |
| `Esc` | 설정 창 닫기 또는 Clipit 창 숨기기 |

---

## 다운로드 및 실행 방법

1. 오른쪽 **[Releases](https://github.com/Blshu/Clipit/releases)** 페이지에서 최신 버전의 `Clipit-v1.0.0-win-x64.zip` 파일을 다운로드합니다.
2. 원하는 폴더에 압축을 풉니다.
3. `Clipit.exe`를 실행하면 시스템 트레이에 아이콘이 등록되며, 이후 `Alt + V`로 언제든 사용할 수 있습니다.

> **실행 시 Windows SmartScreen 또는 백신 경고가 뜨는 경우**  
> 개인 오픈소스 프로젝트로 상용 코드 서명 인증서가 적용되어 있지 않아 초기 실행 시 경고가 표시될 수 있습니다. SmartScreen 창에서 **[추가 정보] → [실행]**을 누르거나 백신 예외 처리를 진행해 주세요.

---

## 빌드 환경

- **Language:** C# (.NET 8.0)
- **UI Framework:** Windows App SDK (WinUI 3) & WinUIEx
- **Target OS:** Windows 10 (1809 이상) / Windows 11 (x64)

---

## 라이선스

이 프로젝트는 **[GNU General Public License v3.0 (GPL-3.0)](LICENSE)** 라이선스를 따릅니다.  
소스 코드를 수정하거나 재배포할 경우 동일한 GPL-3.0 라이선스로 소스 코드를 공개해야 합니다.