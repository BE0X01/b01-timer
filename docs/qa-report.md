# B01 Timer QA 보고서

검증일: 2026-10-07. 최종 Windows 검증 199개가 통과했고 검증 범위에서 열린 기능 결함은 없어요.

검증한 소스는 `01c3b50651bd1bb654461473fc995a269b2ce9ef`이에요. 실제 Windows 빌드·GUI 실행은 [Actions 실행 37579922318](https://github.com/BE0X01/b01-timer/actions/runs/37579922318), 배포는 [Version 0.1](https://github.com/BE0X01/b01-timer/releases/tag/v0.1)에서 확인했어요. 배포 태그 `v0.1`의 실제 commit도 이 소스와 일치해요. 이후 문서만 수정한 main commit은 배포 소스를 변경하지 않아요.

## 최종 검증 결과

| 구분 | 결과 | 검증 내용 |
| --- | --- | --- |
| Core 테스트 | 57개 통과 | 시간 입력, countdown, pause/resume/reset, INI, 완료음 파형, Record 누적·정규화·저장 |
| 독립 QA Core 테스트 | 39개 통과 | 입력 단위 보존, 경계값, 전체 지원 시간 round-trip, INI 및 migration 회귀 |
| Windows WPF appearance 테스트 | 15개 통과 | 내장 Pretendard, tabular 숫자, 세 액션 glyph의 크기·중심, 36 DIP 테마 버튼, stroke 제거, filled Pause, live countdown, 고정 badge |
| 실제 Timer GUI 테스트 | 62개 통과 | EXE를 외부 UIAutomation·키보드·실제 마우스 클릭으로 조작 |
| 실제 Record GUI 테스트 | 26개 통과 | 서로 다른 실행 파일의 실제 전경 포커스, 탭 전환, Reset·재시작·5개 제한·day/week 표시 |
| 화면 검토 | 완료 | Timer PNG 11개와 Record PNG 5개, 총 16개 |
| 패키징 및 Release | 통과 | 검증 EXE와 패키지 hash 일치, ZIP만 게시, 제목과 태그 확인, 게시 ZIP 직접 다운로드 검증 |

Actions의 모든 빌드·검증·패키징·게시 단계가 성공했어요. 실제 Timer/Record 결과 JSON의 실패 항목은 각각 0개예요.

## 화면과 상호작용

- Play/Pause/Reset은 20 DIP Viewbox를 사용하며 실제 ink의 최대 직경을 Reset에 맞췄어요. Play·Pause·Reset의 중심 오차와 크기 차이가 0.05 DIP 미만인 것을 실제 WPF 좌표로 확인했어요.
- 테마 버튼은 36 × 36 DIP로 확대했어요. Dark/Light 모두 액션 버튼의 focus stroke가 없고 Pressed opacity를 사용하지 않아요. 실제 Idle/Hover/Pressed/Paused PNG에서도 확인했어요.
- Pause는 채워진 두 막대이며 가운데 틈은 비어 있어요. 실제 geometry와 Running PNG로 확인했어요.
- Pretendard Regular/SemiBold의 실제 GlyphTypeface URI가 내장 OTF 자산으로 resolve돼요. Timer와 Record 숫자는 tabular alignment를 사용해요.
- Running 숫자는 읽기 전용 표시로 갱신하며 편집용 입력과 분리돼요. 실제 dispatcher message loop에서 숫자 감소를 확인했고 실제 EXE의 countdown/pause/resume 회귀도 통과했어요.
- day/week badge를 긴 값으로 바꿔도 숫자 위치와 시간 패널 크기가 동일한 것을 WPF에서 확인했어요. 실제 EXE에서도 `1d`, `1w`, `1w 1d`와 하루 단위 clock wrap을 확인했어요.
- 완료음의 44.1kHz mono 16-bit PCM을 독립 분석해 6개의 beep, 두 묶음 사이 긴 간격, 각 묶음의 긴 마지막 음을 확인했어요. 총 1.62초이며 Reset·새 시간 설정·재시작·창 닫기에서 중단해요.

Timer 화면 검토에는 두 테마, Add/Edit dialog, context menu, 최소 창 크기의 오류 표시, EXE 옆 INI 실행 화면이 포함돼요. Record는 2개 등록 화면과 5개 등록·day/week 화면을 검토했어요. 일부 Record 증거는 테스트 과정의 타이틀 바 더블클릭으로 최대화된 창에서 촬영됐으며, 일반 창의 badge 배치 불변은 appearance 검증으로 별도 확인했어요.

## Timer 회귀

- 시 `40` → `40:00:00`, 분 `20` → `00:20:00`, 분 `8` → `00:08:00`, 분 `408` → `04:08:00`, 초 `123000` → `12:30:00`을 실제 GUI에서 확인했어요.
- 분·초 overflow 정규화, 상위 단위 보존, 최대 시간 초과 입력 거절, Escape 복구와 잘못된 입력 후 실제 버튼 클릭을 검증했어요. 전체 지원 360,000개 시간을 HHMMSS로 변환해 다시 입력하는 Core 검증도 통과했어요.
- 즐겨찾기 클릭은 시간만 설정하고 대기해요. 실행 중 클릭하면 새 시간을 설정하고 멈춰요. Reset은 가장 최근에 설정한 시간으로 돌아가 Ready가 돼요.
- 실행·일시정지·재개·완료, + 새창, Add/Edit/Remove, Enter 저장, Cancel/Escape 취소, dialog가 열린 동안 countdown 지속을 확인했어요.
- 재실행 후 테마·즐겨찾기·설정 시간이 보존돼요. 완료한 countdown을 재실행 후 자동으로 재개하지 않아요.

## Record 검증

실제 WinForms probe 프로그램 5개를 서로 다른 EXE로 실행하고 전경 창을 바꾸며 측정했어요.

- 최초 Record는 +만 표시하며 Reset은 비활성이에요. Title과 프로그램 선택이 없으면 Add할 수 없어요. 칩은 입력한 제목을 표시해요.
- 등록된 전경 프로그램은 자동으로 시간을 누적해요. 선택하지 않은 칩의 프로그램도 전경일 때 계속 누적하고, B01 Timer 자체나 미등록 프로그램이 전경일 때는 누적하지 않아요.
- Reset은 선택한 기록만 0초로 만들고 다른 기록은 유지해요. Record에는 Play/Pause를 표시하지 않아요.
- Timer는 Record 탭에서 계속 감소하고, Record는 Timer 탭에서도 계속 누적해요.
- 앱 재실행 후 프로그램 등록과 두 기록이 유지되며 앱이 닫혀 있던 시간을 더하지 않아요. 대상 프로그램을 종료·재실행해도 실행 파일 경로가 같으면 같은 기록을 이어가요.
- 5개 등록 후 +가 비활성이에요. 실제 EXE에 경계값 INI를 로드해 `1d` / `1w` / `1w 1d`를 확인했어요. 각각 `00:00:00`, `00:00:00`, `01:01:01`로 하루 내 시각을 표시해요.
- Core는 100ns tick 누적, 짧은 포커스 구간의 합산, 선택과 무관한 측정, Reset 분리, 큰 값과 잘못된 등록 정규화, INI round-trip을 검증해요.

## EXE 옆 INI

EXE만 임시 portable 폴더로 복사하고 설정 경로 override 없이 실행했어요. 작업 폴더를 다른 곳으로 지정해도 EXE 폴더에 `B01Timer.ini`가 생성되는 것을 확인했어요.

기존 `[Settings]`의 Theme/LastTime, `[Presets]`의 Count, `[Preset1]` 이후의 Id/Time을 보존해요. Record는 `[Records]` Count, `[Record1]` 이후 Id/Title/ExecutablePath/ElapsedTicks와 `[Settings]` SelectedRecordId를 추가해요. 누적은 표시보다 정밀한 100ns 단위로 저장하며 활성 측정 중 매초, 등록 변경·Reset·정상 종료에서도 atomic save해요.

Count=0, 잘못된 INI 값의 복구, INI 우선 적용, 기존 AppData JSON의 최초 1회 가져오기와 원본 보존을 실제 EXE로 검증했어요. migration 테스트가 건드린 기존 AppData 파일은 종료 후 복구해요.

## Release 및 ZIP

GitHub README 제목은 `B01 Timer`, Release 제목은 `Version 0.1`이에요. 성공한 main EXE 빌드가 검증 ZIP만 게시하도록 자동화했고 기존 별도 EXE/checksum/license 자산은 제거했어요. Release API의 업로드 자산은 `B01Timer-0.1-windows-x64.zip` 하나예요.

[게시 ZIP](https://github.com/BE0X01/b01-timer/releases/download/v0.1/B01Timer-0.1-windows-x64.zip)을 직접 다운로드해 61,545,097 bytes, 모든 entry의 CRC, GitHub asset digest 및 내부 checksum manifest를 확인했어요. ZIP에는 `B01Timer.exe`, `LICENSE`, `THIRD-PARTY-NOTICES.txt`, `SHA256SUMS.txt` 네 파일이 있고 INI는 첫 실행 시 생성돼요. 내부 manifest의 세 파일 hash가 모두 일치해요.

| 파일 | 확인한 SHA256 |
| --- | --- |
| B01Timer-0.1-windows-x64.zip | `5381b9b008fe1bdd6301a45b982d610ff7c08dbf6cb31ca027a333774cf4733f` |
| B01Timer.exe | `bc93f9ddad049c213bfc1370b4d0a6c3a847823bfe5b61ae08925afa2e6943cd` |

## 검증 범위

실제 EXE 검증은 Windows Server 2025 x64의 호스팅 데스크톱에서 수행했어요. Sleep/hibernation 제외용 awake clock과 세션 잠금 알림 처리는 구현돼 있지만 CI에서 실제 절전·최대절전·세션 잠금 상태를 강제로 만들지는 않았어요. 완료음의 파형과 completion 동작은 검증했고 실제 스피커 청취 및 150%/200% 화면 배율은 독립 측정하지 않았어요.

현재 앱 수정과 소스 매핑은 디자인 문서에 반영했어요. 기존 Figma 파일의 최신 Record 화면·아이콘 크기·폰트·상태 갱신은 연결의 MCP quota 제한으로 아직 반영하지 못한 별도 디자인 작업이며, 완료한 것으로 표시하지 않아요.
