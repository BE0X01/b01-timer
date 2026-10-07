# B01 Timer 0.1 QA 보고서

검증일: 2026-10-07. 결과: 승인. 앱 기능 요구사항에서 열린 결함이 없어요.

검증한 소스는 `3f2c5ba2b532db1e136792f145d1f7ca41b2d89b`이에요. 실제 Windows 빌드와 GUI 실행은 [최종 Actions 실행](https://github.com/BE0X01/b01-timer/actions/runs/37568435561)에서 확인했고, 배포는 [GitHub Release v0.1](https://github.com/BE0X01/b01-timer/releases/tag/v0.1)에서 확인했어요.

## 검증 결과

| 구분 | 결과 | 검증 내용 |
| --- | --- | --- |
| 개발자 Core 테스트 | 34개 통과 | 시간 입력, countdown, pause/resume/reset, 설정과 INI 저장 |
| 독립 QA Core 테스트 | 39개 통과 | 입력 단위 보존, 경계값, 모든 지원 시간 round-trip, INI와 migration 회귀 |
| 실제 Windows GUI 테스트 | 62개 통과 | 실행한 EXE를 외부 UIAutomation·키보드·실제 마우스 클릭으로 검증 |
| 실제 화면 검토 | 승인 | Dark/Light, 두 테마의 dialog, 우클릭 메뉴, 작은 창의 오류 표시, portable 실행 화면 |
| Release 자동화 | 통과 | 최초 v0.1 게시와 같은 버전의 후속 빌드 갱신을 모두 실제 확인 |

총 135개 검증이 통과했고 실패는 없어요. Core 73개는 Linux에서도 실행해 통과했어요.

## 사용자 요구사항

- 시간은 hh:mm:ss로 표시해요. 시 `40` → `40:00:00`, 분 `20` → `00:20:00`, 분 `8` → `00:08:00`, 분 `408` → `04:08:00`, 초 `123000` → `12:30:00`을 실제 GUI에서 검증했어요.
- 즐겨찾기 클릭은 시간을 설정하고 대기해요. 자동으로 실행하지 않아요. 실행 중 선택하면 새 시간을 설정하고 멈춰요.
- 실행·일시정지·재개·리셋·0초 도달을 검증했어요. 리셋은 가장 최근에 설정한 시간과 대기 상태로 돌아가요.
- + 새창, 즐겨찾기 추가·Edit·Remove, Enter 저장, Cancel과 Escape 취소를 검증했어요. preset dialog가 열려 있는 동안에도 실행 중인 countdown은 계속 진행해요.
- Record는 준비 중인 비활성 버튼으로 유지해요.
- Dark/Light 버튼과 재실행 후 테마 보존을 확인했어요. 실제 화면에서 + 아이콘과 Edit/Remove 전체 텍스트가 정상 표시되는 것을 확인했어요.

## EXE 옆 INI 검증

EXE만 임시 portable 폴더로 복사하고 `--settings-dir` 없이 실행했어요. 작업 폴더는 EXE 폴더와 다르게 지정해 경로 선택도 확인했어요.

- 첫 실행 시 EXE와 같은 폴더에 `B01Timer.ini`가 생성돼요.
- `[Settings]`의 Theme와 LastTime, `[Presets]`의 Count, `[Preset1]` 이후의 Id와 Time을 사람이 읽을 수 있는 형태로 저장해요.
- 즐겨찾기·테마·설정 시간이 INI에 기록되고 EXE 재실행 후 유지돼요.
- Count=0은 빈 즐겨찾기를 유지해요. 잘못된 시간·테마·preset 값은 안전한 기본값으로 복구해요.
- 기존 AppData `settings.json`이 있고 INI가 없는 경우 처음 한 번 가져와요. JSON 원본은 보존해요. INI가 생긴 뒤에는 JSON이 바뀌어도 다시 가져오지 않아요.
- `--settings-dir` 테스트 override도 해당 폴더의 `B01Timer.ini`를 사용해요.
- migration 테스트가 건드린 기존 AppData 파일은 테스트 종료 후 원상복구해요.

INI Core 검증에는 순서·ID 보존, 빈 목록, 잘못된 INI/JSON, INI 우선 적용, 임시 파일 정리도 포함돼요. 시간 입력은 지원하는 전체 360,000초를 HHMMSS로 변환해 다시 입력하는 검증을 통과했어요.

## Release 검증

실제 두 번의 Windows CI에서 135개 검증을 통과한 뒤 ZIP과 SHA256SUMS를 생성하고 Release에 자동 업로드했어요. ZIP 안 EXE가 검증한 EXE와 같은 SHA256인지 패키징 단계에서 확인해요.

Release 자산은 `B01Timer.exe`, `B01Timer-0.1-windows-x64.zip`, `SHA256SUMS.txt`, `LICENSE`, `THIRD-PARTY-NOTICES.txt`예요. 두 번째 실행에서 v0.1 태그가 위 소스 commit으로 갱신된 실제 API 응답과 Release 재업로드 성공을 원본 로그에서 확인했어요. 이후 main의 Windows 빌드와 수동 main 실행도 같은 workflow를 사용해요.

디렉터는 게시된 최신 ZIP을 직접 내려받아 59,417,674 bytes와 모든 entry의 CRC를 확인했어요. ZIP과 내부 EXE의 SHA256이 GitHub 자산 digest와 일치해요. ZIP에는 EXE와 두 라이선스 문서만 포함되며 INI는 첫 실행 때 생성돼요.

| 파일 | 확인한 SHA256 |
| --- | --- |
| B01Timer-0.1-windows-x64.zip | `27c80454bae83998da3e4d275fe61632af2164c1e15c310d4a5707e34cc6d74f` |
| B01Timer.exe | `f8c12b7d59aaefbf8f196bc61e14393ab11c439549183c008f7a7c29bb4c26b8` |

## 검증 범위

실제 EXE 검증은 Windows Server 2025의 x64 호스팅 데스크톱에서 수행했어요. 완료 사운드 호출은 소스에서 확인했으며 스피커에서 들리는 소리와 150%/200% 화면 배율은 독립 측정하지 않았어요. Record 기능은 요청대로 구현하지 않았어요.
