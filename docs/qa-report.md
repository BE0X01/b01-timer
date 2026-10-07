# B01 Timer 0.1 QA 보고서

검증일: 2026-10-07. 결과: 승인. 앱 기능 요구사항에서 열린 결함이 없어요.

검증한 소스는 `4abd9671f24450b30b74cfbf81be10545b4c2fa8`이에요. 실제 Windows 빌드와 GUI 실행은 [최종 Actions 실행](https://github.com/BE0X01/b01-timer/actions/runs/37572970718)에서 확인했고, 배포는 [GitHub Release v0.1](https://github.com/BE0X01/b01-timer/releases/tag/v0.1)에서 확인했어요.

## 검증 결과

| 구분 | 결과 | 검증 내용 |
| --- | --- | --- |
| 개발자 Core 테스트 | 39개 통과 | 시간 입력, countdown, pause/resume/reset, INI 및 완료음 PCM 파형 |
| 독립 QA Core 테스트 | 39개 통과 | 입력 단위 보존, 경계값, 모든 지원 시간 round-trip, INI와 migration 회귀 |
| Windows WPF appearance 테스트 | 7개 통과 | 내장 Pretendard 실제 glyph URI, 숫자 tabular, Reset 중심, 두 테마 focus stroke 없음, Pause Fill |
| 실제 Windows GUI 테스트 | 62개 통과 | 실행한 EXE를 외부 UIAutomation·키보드·실제 마우스 클릭으로 검증 |
| 실제 화면 검토 | 승인 | Dark/Light, 두 테마의 dialog, 우클릭 메뉴, 작은 창의 오류 표시, portable 실행 화면 |
| Release 자동화 | 통과 | 최초 v0.1 게시와 같은 버전의 후속 빌드 갱신을 모두 실제 확인 |

최신 Windows 빌드에서 총 147개 검증이 통과했고 실패는 없어요. 이전 INI 빌드의 Core 73개 Linux 검증 기록도 유지해요.

## 2026-10-07 수정 요구 검증

- GitHub Release의 실제 제목은 `Version 0.1`이며, 자동화의 create/edit 경로 모두 같은 제목을 사용해요.
- Reset geometry ink center와 실제 버튼 중심 차이가 0.05 DIP 미만인 것을 WPF에서 확인했고 실행한 EXE의 PNG에서도 중심 정렬을 확인했어요.
- 버튼 Focus stroke와 Pressed opacity를 제거했어요. 실제 EXE의 Idle/Hover/마우스 Pressed PNG를 검토해 추가 테두리가 없음을 확인했어요. Record/0초 Start의 Disabled는 유지해요.
- Running 상태의 Pause 두 막대 안쪽이 채워져 있고 가운데 틈은 비어 있는 것을 geometry와 실제 PNG에서 확인했어요.
- 실제 Typeface의 FontUri가 내장 Pretendard Regular/SemiBold OTF로 resolve되는 것을 확인했어요. 숫자는 tabular alignment를 사용하며 별도 폰트 설치가 필요 없어요.
- 완료음은 44.1kHz mono 16-bit PCM이에요. 파형을 10ms 프레임으로 독립 분석해 audible beep 6개, 가운데 긴 간격, 각 묶음의 마지막 음이 더 긴 것을 확인했어요. 총 1.62초이고 세 음 묶음을 두 번 재생해요. Reset·새 시간 설정·재시작·창 닫기는 재생을 멈춰요.
- 실제 GUI 결과 JSON은 62/62, 실패 목록은 비어 있어요. 화면 증거는 기존 7개와 Running Pause/Idle/Hover/Pressed/Paused 상태 4개를 합한 PNG 11개예요.

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

최신 Windows CI에서 147개 검증을 통과한 뒤 ZIP과 SHA256SUMS를 생성하고 기존 v0.1 Release에 자동 업로드했어요. 최초 게시와 동일 버전 재빌드 경로도 이전 실행에서 검증했어요. ZIP 안 EXE가 검증한 EXE와 같은 SHA256인지 패키징 단계에서 확인해요.

Release 자산은 `B01Timer.exe`, `B01Timer-0.1-windows-x64.zip`, `SHA256SUMS.txt`, `LICENSE`, `THIRD-PARTY-NOTICES.txt`예요. 최신 실행에서 v0.1 태그가 위 소스 commit으로 갱신된 실제 API 응답과 Release 재업로드 성공을 원본 로그에서 확인했어요. 이후 main의 Windows 빌드와 수동 main 실행도 같은 workflow를 사용해요.

디렉터는 게시된 최신 ZIP을 직접 내려받아 61,533,947 bytes와 모든 entry의 CRC를 확인했어요. ZIP과 내부 EXE의 SHA256이 GitHub 자산 digest와 일치해요. ZIP에는 EXE와 두 라이선스 문서만 포함되며 INI는 첫 실행 때 생성돼요.

| 파일 | 확인한 SHA256 |
| --- | --- |
| B01Timer-0.1-windows-x64.zip | `ffb7596f1919a68efe6badb9045727f7cd50dc2b3de4573d3ed3a793d404fdcf` |
| B01Timer.exe | `96084d811fcb957b03710da87cea36a8b119d8016454730391217bf92f6139dd` |

## 검증 범위

실제 EXE 검증은 Windows Server 2025의 x64 호스팅 데스크톱에서 수행했어요. 완료음의 PCM 파형과 실제 completion 동작은 검증했지만 호스팅 환경의 스피커 청취와 150%/200% 화면 배율은 독립 측정하지 않았어요. Record 기능은 요청대로 구현하지 않았어요.
