# B01 Timer QA 보고서

## Version 0.3 검증

검증일: 2026-10-07. 소스 [93ed9a672fba5500b462e54680df2c7bb41910e3](https://github.com/BE0X01/b01-timer/commit/93ed9a672fba5500b462e54680df2c7bb41910e3)를 [Actions 실행 37590206218](https://github.com/BE0X01/b01-timer/actions/runs/37590206218)에서 빌드·검증한 뒤 [Version 0.3](https://github.com/BE0X01/b01-timer/releases/tag/v0.3)로 게시했어요. `v0.3` 태그의 commit은 검증 소스와 일치하고 실제 EXE FileVersion은 `0.3.0.0`이에요.

| 구분 | 통과 |
| --- | --- |
| Core | 57개 |
| 독립 QA Core | 39개 |
| Windows WPF appearance | 31개 |
| 실제 Timer GUI | 76개 |
| 실제 Record GUI | 32개 |

총 235개가 통과했고 실패는 0개예요. QA는 실행 로그와 실제 Timer/Record 결과 JSON, 내려받은 QA artifact의 digest·CRC를 독립 확인했어요.

- 실행 중 시간·테마·즐겨찾기·Record 등록·선택·Reset·누적은 메모리에서만 갱신하고, 기존 INI의 bytes·hash·수정 시간이 바뀌지 않는 것을 확인했어요. 첫 실행과 legacy JSON 가져오기에서도 INI는 정상 종료 후에만 생성돼요.
- 정상 종료 때 한 번 atomic save하고 다음 실행에서 설정과 기록을 복원해요. Enter 없이 남겨 둔 유효한 시간 입력도 종료 직전에 확정돼요. 강제 종료 후에는 이번 세션 변경 대신 직전 정상 종료 때 저장한 설정을 복원하는 것을 실제 EXE로 확인했어요.
- 마지막 미처리 전경 구간 123.4567ms를 종료 때 정확히 정산해 저장하는 WPF 회귀가 통과했어요. 선택한 Record 삭제는 남은 첫 기록으로 즉시 전환하고, 선택하지 않은 삭제는 현재 선택을 유지하며, 마지막 기록 삭제는 선택을 비우고 Reset을 비활성화해요.
- 기존 공통 clock의 7개 비편집·복귀 상태 RGBA 픽셀과 2자리 편집을 포함한 9개 숫자 ink bounds가 이번 빌드에서도 동일했어요. 클릭·Tab·Enter와 실행 중 숫자 클릭 일시정지, live countdown 회귀도 유지돼요.

Release에는 [B01Timer-0.3-windows-x64.zip](https://github.com/BE0X01/b01-timer/releases/download/v0.3/B01Timer-0.3-windows-x64.zip) 하나를 게시했어요. 직접 다운로드한 ZIP은 61,545,383 bytes이며 모든 entry의 CRC와 정확한 세 파일(`B01Timer.exe`, `LICENSE`, `THIRD-PARTY-NOTICES.txt`)을 확인했어요. `SHA256SUMS.txt`는 ZIP에 없고, QA가 publish 폴더에 남겨 둔 checksum 파일도 압축에 포함되지 않았어요. 패키징 단계는 세 파일의 content hash를 각각 검사했고, 게시 ZIP의 EXE hash는 QA가 검증한 빌드 EXE와 일치하며 두 라이선스 내용도 소스와 일치해요. 외부 CI checksum artifact는 유지했어요.

| 파일 | 확인한 SHA256 |
| --- | --- |
| B01Timer-0.3-windows-x64.zip | `9ce1bac0ad0421cba47f7387f842ee8f98a389ad3b85beb06a24fc4170ce2a22` |
| B01Timer.exe | `7ca98950305979cd1f5dbee2f54d5db22d4e30f884330fd582b93c4bfdce2e9d` |

UI 배치는 바꾸지 않았어요. 아래에 기록한 최대화 하단 버튼 가시성, 실제 절전·최대절전·세션 잠금, 스피커 청취, 150%/200% 화면 배율과 기존 Figma 갱신의 검증 한계는 유지해요.

## 이전 Version 0.2 시간표시 수정 검증

검증일: 2026-10-07. 소스 [5b804e3bdb4d2e2d458f4b7ff4a9aa20defef2b9](https://github.com/BE0X01/b01-timer/commit/5b804e3bdb4d2e2d458f4b7ff4a9aa20defef2b9)를 [Actions 실행 37586932079](https://github.com/BE0X01/b01-timer/actions/runs/37586932079)에서 빌드·검증한 뒤 기존 [Version 0.2](https://github.com/BE0X01/b01-timer/releases/tag/v0.2)와 ZIP을 수정본으로 갱신했어요. `v0.2` 태그는 이 검증 소스를 가리키고 EXE FileVersion은 `0.2.0.0`이에요.

Timer의 대기·정지와 실행, Record의 대기와 측정이 같은 숫자·콜론·라벨 표시 트리를 사용해요. 대기 숫자를 그리던 TextBox 내부 2 DIP 테두리는 편집 내용과 별도 장식 overlay로 분리하여 baseline을 밀지 않게 했어요. Running에서는 입력 control을 Collapsed하여 기존 IME/UIA text-store 정지 회피를 유지해요.

| 구분 | 통과 |
| --- | --- |
| Core | 57개 |
| 독립 QA Core | 39개 |
| Windows WPF appearance | 24개 |
| 실제 Timer GUI | 66개 |
| 실제 Record GUI | 26개 |

총 212개가 통과했고 실패는 0개예요. 같은 `12:34:56`의 대기·실행·정지·Record 대기·측정·재개·편집 종료 후 복귀, 총 7개 상태의 324 × 100 clock 영역 RGBA 픽셀이 완전히 같아요. Hours와 Tab으로 이동한 Minutes의 2자리 편집을 포함한 9개 화면에서도 세 숫자의 실제 ink bounds가 모두 동일했어요. 실제 EXE의 숫자 클릭·Tab·Enter, 실행 중 숫자 클릭 후 일시정지·입력 focus, live countdown 회귀도 통과했어요. 디자이너는 Windows PNG 11개를 검토하고 시간표시 통일 계약을 승인했어요.

게시 [B01Timer-0.2-windows-x64.zip](https://github.com/BE0X01/b01-timer/releases/download/v0.2/B01Timer-0.2-windows-x64.zip)은 61,545,720 bytes예요. 직접 다운로드한 ZIP의 CRC, 네 파일(`B01Timer.exe`, `LICENSE`, `THIRD-PARTY-NOTICES.txt`, `SHA256SUMS.txt`), manifest의 세 파일 hash를 확인했고 내부 EXE hash는 QA가 검증한 빌드 EXE와 일치해요.

| 파일 | 확인한 SHA256 |
| --- | --- |
| B01Timer-0.2-windows-x64.zip | `4a3aa4249165c8b91cd8d3a03a2b551fc4d787fba3824eded4239762f85d5a7b` |
| B01Timer.exe | `2d96de29688323248fc134db850257cb3be66259638a75af3595fded810775dc` |

아래에 기록한 최대화 하단 버튼 가시성, 실제 절전·최대절전·세션 잠금, 스피커 청취, 150%/200% 화면 배율과 기존 Figma 갱신의 검증 한계는 유지해요.

## 이전 Version 0.2 검증 (첫 빌드)

다음은 최초 0.2 게시 시점의 역사적 증거예요. 현재 `v0.2` 태그와 다운로드 ZIP은 위 시간표시 수정본으로 갱신됐어요.

검증일: 2026-10-07. 소스 [956348b7ea258ec64d770f62bbb94504b9c8352e](https://github.com/BE0X01/b01-timer/commit/956348b7ea258ec64d770f62bbb94504b9c8352e)를 [Actions 실행 37584619801](https://github.com/BE0X01/b01-timer/actions/runs/37584619801)에서 빌드·검증한 뒤 [Version 0.2](https://github.com/BE0X01/b01-timer/releases/tag/v0.2)로 게시했어요. 당시 `v0.2` 태그의 commit도 검증 소스와 일치했어요. 기능·UI는 변경하지 않고 버전 메타데이터와 관련 문서만 갱신했어요.

Windows 검증은 총 199개가 통과했고 실패는 0개예요. QA가 로그와 실제 Timer/Record 결과 JSON을 독립 확인했으며 자동 검사와 일반 창 크기 화면 검토에서 실패는 없어요.

| 구분 | 통과 |
| --- | --- |
| Core | 57개 |
| 독립 QA Core | 39개 |
| Windows WPF appearance | 15개 |
| 실제 Timer GUI | 62개 |
| 실제 Record GUI | 26개 |

실제 EXE의 FileVersion은 기대값과 같은 `0.2.0.0`이에요. 빌드·GUI 검증·패키징·Release 게시 단계가 모두 성공했고, Release에는 [B01Timer-0.2-windows-x64.zip](https://github.com/BE0X01/b01-timer/releases/download/v0.2/B01Timer-0.2-windows-x64.zip) 하나를 게시했어요.

게시 ZIP을 직접 다운로드해 61,545,075 bytes, 모든 entry의 CRC, 정확한 네 파일(`B01Timer.exe`, `LICENSE`, `THIRD-PARTY-NOTICES.txt`, `SHA256SUMS.txt`), 내부 manifest의 세 파일 hash를 확인했어요. 내부 EXE hash는 QA에서 검증한 빌드 EXE와 일치해요.

| 파일 | 확인한 SHA256 |
| --- | --- |
| B01Timer-0.2-windows-x64.zip | `c1608fab872c369a235ce31000fbbd5e78fc2bc301c4b59c34163f78e8bf68ed` |
| B01Timer.exe | `bed72ec83536d11a9d9eb632762a093d39db6b78c4c74daa760b7f281a7233d5` |

일반 창 화면은 검토했어요. 최대화 Record PNG는 작업표시줄이 Reset 일부를 가리고 있어 최대화 하단 제어의 완전한 가시성은 이번 캡처로 확정하지 않았어요. 아래 이전 검증의 범위와 한계는 유지해요. 실제 절전·최대절전·세션 잠금, 스피커 청취, 150%/200% 화면 배율은 이번에도 별도 측정하지 않았고 기존 Figma 갱신은 별도 디자인 작업으로 남아 있어요.

## 이전 Version 0.1 검증

검증일: 2026-10-07. 최종 Windows 검증 199개가 통과했고 검증 범위에서 열린 기능 결함은 없어요.

검증한 소스는 `01c3b50651bd1bb654461473fc995a269b2ce9ef`이에요. 실제 Windows 빌드·GUI 실행은 [Actions 실행 37579922318](https://github.com/BE0X01/b01-timer/actions/runs/37579922318), 배포는 [Version 0.1](https://github.com/BE0X01/b01-timer/releases/tag/v0.1)에서 확인했어요. 배포 태그 `v0.1`의 실제 commit도 이 소스와 일치해요. 이후 문서만 수정한 main commit은 배포 소스를 변경하지 않아요.

### 최종 검증 결과

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

### 화면과 상호작용

- Play/Pause/Reset은 20 DIP Viewbox를 사용하며 실제 ink의 최대 직경을 Reset에 맞췄어요. Play·Pause·Reset의 중심 오차와 크기 차이가 0.05 DIP 미만인 것을 실제 WPF 좌표로 확인했어요.
- 테마 버튼은 36 × 36 DIP로 확대했어요. Dark/Light 모두 액션 버튼의 focus stroke가 없고 Pressed opacity를 사용하지 않아요. 실제 Idle/Hover/Pressed/Paused PNG에서도 확인했어요.
- Pause는 채워진 두 막대이며 가운데 틈은 비어 있어요. 실제 geometry와 Running PNG로 확인했어요.
- Pretendard Regular/SemiBold의 실제 GlyphTypeface URI가 내장 OTF 자산으로 resolve돼요. Timer와 Record 숫자는 tabular alignment를 사용해요.
- Running 숫자는 읽기 전용 표시로 갱신하며 편집용 입력과 분리돼요. 실제 dispatcher message loop에서 숫자 감소를 확인했고 실제 EXE의 countdown/pause/resume 회귀도 통과했어요.
- day/week badge를 긴 값으로 바꿔도 숫자 위치와 시간 패널 크기가 동일한 것을 WPF에서 확인했어요. 실제 EXE에서도 `1d`, `1w`, `1w 1d`와 하루 단위 clock wrap을 확인했어요.
- 완료음의 44.1kHz mono 16-bit PCM을 독립 분석해 6개의 beep, 두 묶음 사이 긴 간격, 각 묶음의 긴 마지막 음을 확인했어요. 총 1.62초이며 Reset·새 시간 설정·재시작·창 닫기에서 중단해요.

Timer 화면 검토에는 두 테마, Add/Edit dialog, context menu, 최소 창 크기의 오류 표시, EXE 옆 INI 실행 화면이 포함돼요. Record는 2개 등록 화면과 5개 등록·day/week 화면을 검토했어요. 일부 Record 증거는 테스트 과정의 타이틀 바 더블클릭으로 최대화된 창에서 촬영됐으며, 일반 창의 badge 배치 불변은 appearance 검증으로 별도 확인했어요.

### Timer 회귀

- 시 `40` → `40:00:00`, 분 `20` → `00:20:00`, 분 `8` → `00:08:00`, 분 `408` → `04:08:00`, 초 `123000` → `12:30:00`을 실제 GUI에서 확인했어요.
- 분·초 overflow 정규화, 상위 단위 보존, 최대 시간 초과 입력 거절, Escape 복구와 잘못된 입력 후 실제 버튼 클릭을 검증했어요. 전체 지원 360,000개 시간을 HHMMSS로 변환해 다시 입력하는 Core 검증도 통과했어요.
- 즐겨찾기 클릭은 시간만 설정하고 대기해요. 실행 중 클릭하면 새 시간을 설정하고 멈춰요. Reset은 가장 최근에 설정한 시간으로 돌아가 Ready가 돼요.
- 실행·일시정지·재개·완료, + 새창, Add/Edit/Remove, Enter 저장, Cancel/Escape 취소, dialog가 열린 동안 countdown 지속을 확인했어요.
- 재실행 후 테마·즐겨찾기·설정 시간이 보존돼요. 완료한 countdown을 재실행 후 자동으로 재개하지 않아요.

### Record 검증

실제 WinForms probe 프로그램 5개를 서로 다른 EXE로 실행하고 전경 창을 바꾸며 측정했어요.

- 최초 Record는 +만 표시하며 Reset은 비활성이에요. Title과 프로그램 선택이 없으면 Add할 수 없어요. 칩은 입력한 제목을 표시해요.
- 등록된 전경 프로그램은 자동으로 시간을 누적해요. 선택하지 않은 칩의 프로그램도 전경일 때 계속 누적하고, B01 Timer 자체나 미등록 프로그램이 전경일 때는 누적하지 않아요.
- Reset은 선택한 기록만 0초로 만들고 다른 기록은 유지해요. Record에는 Play/Pause를 표시하지 않아요.
- Timer는 Record 탭에서 계속 감소하고, Record는 Timer 탭에서도 계속 누적해요.
- 앱 재실행 후 프로그램 등록과 두 기록이 유지되며 앱이 닫혀 있던 시간을 더하지 않아요. 대상 프로그램을 종료·재실행해도 실행 파일 경로가 같으면 같은 기록을 이어가요.
- 5개 등록 후 +가 비활성이에요. 실제 EXE에 경계값 INI를 로드해 `1d` / `1w` / `1w 1d`를 확인했어요. 각각 `00:00:00`, `00:00:00`, `01:01:01`로 하루 내 시각을 표시해요.
- Core는 100ns tick 누적, 짧은 포커스 구간의 합산, 선택과 무관한 측정, Reset 분리, 큰 값과 잘못된 등록 정규화, INI round-trip을 검증해요.

### EXE 옆 INI

EXE만 임시 portable 폴더로 복사하고 설정 경로 override 없이 실행했어요. 작업 폴더를 다른 곳으로 지정해도 EXE 폴더에 `B01Timer.ini`가 생성되는 것을 확인했어요.

기존 `[Settings]`의 Theme/LastTime, `[Presets]`의 Count, `[Preset1]` 이후의 Id/Time을 보존해요. Record는 `[Records]` Count, `[Record1]` 이후 Id/Title/ExecutablePath/ElapsedTicks와 `[Settings]` SelectedRecordId를 추가해요. 누적은 표시보다 정밀한 100ns 단위로 저장하며 활성 측정 중 매초, 등록 변경·Reset·정상 종료에서도 atomic save해요.

Count=0, 잘못된 INI 값의 복구, INI 우선 적용, 기존 AppData JSON의 최초 1회 가져오기와 원본 보존을 실제 EXE로 검증했어요. migration 테스트가 건드린 기존 AppData 파일은 종료 후 복구해요.

### Release 및 ZIP

GitHub README 제목은 `B01 Timer`, Release 제목은 `Version 0.1`이에요. 성공한 main EXE 빌드가 검증 ZIP만 게시하도록 자동화했고 기존 별도 EXE/checksum/license 자산은 제거했어요. Release API의 업로드 자산은 `B01Timer-0.1-windows-x64.zip` 하나예요.

[게시 ZIP](https://github.com/BE0X01/b01-timer/releases/download/v0.1/B01Timer-0.1-windows-x64.zip)을 직접 다운로드해 61,545,097 bytes, 모든 entry의 CRC, GitHub asset digest 및 내부 checksum manifest를 확인했어요. ZIP에는 `B01Timer.exe`, `LICENSE`, `THIRD-PARTY-NOTICES.txt`, `SHA256SUMS.txt` 네 파일이 있고 INI는 첫 실행 시 생성돼요. 내부 manifest의 세 파일 hash가 모두 일치해요.

| 파일 | 확인한 SHA256 |
| --- | --- |
| B01Timer-0.1-windows-x64.zip | `5381b9b008fe1bdd6301a45b982d610ff7c08dbf6cb31ca027a333774cf4733f` |
| B01Timer.exe | `bc93f9ddad049c213bfc1370b4d0a6c3a847823bfe5b61ae08925afa2e6943cd` |

### 검증 범위

실제 EXE 검증은 Windows Server 2025 x64의 호스팅 데스크톱에서 수행했어요. Sleep/hibernation 제외용 awake clock과 세션 잠금 알림 처리는 구현돼 있지만 CI에서 실제 절전·최대절전·세션 잠금 상태를 강제로 만들지는 않았어요. 완료음의 파형과 completion 동작은 검증했고 실제 스피커 청취 및 150%/200% 화면 배율은 독립 측정하지 않았어요.

현재 앱 수정과 소스 매핑은 디자인 문서에 반영했어요. 기존 Figma 파일의 최신 Record 화면·아이콘 크기·폰트·상태 갱신은 연결의 MCP quota 제한으로 아직 반영하지 못한 별도 디자인 작업이며, 완료한 것으로 표시하지 않아요.
