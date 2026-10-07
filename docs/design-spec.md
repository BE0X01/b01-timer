# B01 Timer 디자인 사양 0.1 (2026-10-07 수정)

Timer.png의 위에서 아래로 내려오는 탭 → 시간 즐겨찾기 → 큰 디지털 시간 → 우측 하단 제어 구조를 유지한다. Record는 최대 5개의 실행 프로그램에 대해 실제 전경 포커스 시간을 자동 누적하는 활성 탭이다. 탭 전환은 측정 상태를 변경하지 않는다.

## 윈도우와 레이아웃

- 구현: C# WPF, Windows exe. 전체 창 520 × 360 DIP, 최소 460 × 340 DIP. 100%/150%/200% DPI에서도 DIP 배치를 유지한다.
- 커스텀 타이틀 바 32 DIP. 좌측 작은 timer outline(14) + `B01 Timer`(12, Semibold); 우측 minimize/maximize/close 각각 42 × 32. 타이틀 바 빈 영역 드래그, 더블클릭 maximize 지원. 창 제어 버튼도 hover surface를 사용한다. 기본 테두리는 1 DIP, 창 모서리 radius 12.
- 타이틀 바 아래 본문 좌우 padding 24. 상단 y=52, 높이 36: Timer/Record 탭 그룹 왼쪽, 테마 아이콘 버튼(36 × 36, glyph canvas 22) 오른쪽. 탭은 각각 92 × 36, 바깥 radius 10, 안쪽 radius 7.
- 즐겨찾기 행 y=104, 높이 32. 칩 간격 8, 첫 칩부터 가로로 배치하고 끝에 +. 칩이 늘면 WrapPanel을 사용하고 창 본문을 수직으로 확장하거나 scroll하여 하단 버튼을 밀어내지 않게 한다. 기본 15/30/60분은 합리적인 초기값이고 이후 사용자 설정을 저장한다.
- 시간 패널 y=156, 472 × 122, radius 16, 1 DIP border. 중앙에 3개의 클릭 가능한 시간 필드와 콜론 2개. 각 필드 width 92, 콜론 width 24. 숫자 baseline을 맞춘다.
- 숫자: EXE에 내장한 Pretendard Regular 60, Normal. `Typography.NumeralAlignment="Tabular"`로 숫자 폭을 유지한다. UI의 Regular/SemiBold도 같은 Pretendard 폰트 자산을 사용한다. 중앙 정렬, selection highlight는 accent 배경. 숫자 아래 작은 `HOURS`, `MINUTES`, `SECONDS` 각각 9, letter spacing이 가능하면 1, muted. 콜론은 숫자보다 약간 어둡고 같은 baseline.
- 하단 y=298: 좌측 상태 `Ready` / `Running` / `Paused` / `Time’s up` 12, 우측 Play/Pause 48 × 44 + 10 gap + Reset 44 × 44. 버튼 radius 12. 바깥 하단 padding 18.
- 창 리사이즈 시 시간 패널이 가로 확장되고 내부 시간 row는 중앙 정렬한다. 주 액션은 우측 고정. height 변화에는 시간 패널 영역이 늘어나되 숫자를 불필요하게 커지게 하지 않는다.

## 색상 토큰

| 역할 | Dark | Light |
| --- | --- | --- |
| Window background | #181C1F | #F7F8F5 |
| Titlebar background | #181C1F | #F7F8F5 |
| Surface / timer panel | #20262A | #FFFFFF |
| Raised / chips | #282F33 | #EDEFEA |
| Hover surface | #333D42 | #E3E8E1 |
| Border | #353E43 | #DDE2DA |
| Main text | #F1F4EF | #202923 |
| Muted text | #A5B1AA | #64716A |
| Disabled text | #68736D | #9AA59E |
| Accent | #A9CDBA | #27624F |
| Accent foreground | #1C3328 | #FFFFFF |
| Accent hover | #BDDCCB | #1E503F |
| Reserved selected token (현재 미사용) | #2F4439 | #DCEBE1 |
| Error | #F3A8A3 | #AF3C37 |

사실적 그림자, 그라디언트, 글로우는 쓰지 않는다. 창을 띄우는 기본 시스템 그림자는 괜찮다. 정보 위계는 여백, 숫자 크기, 얇은 테두리로 만든다. 테마 변경은 메인/모달/컨텍스트 메뉴/툴팁까지 적용하고 저장한다.

## 컴포넌트와 상태

- Timer 탭: raised surface + main text + Semibold 13. Record도 클릭 가능하다. 현재 탭은 raised surface, 다른 탭은 surface 배경을 사용하고 두 탭 모두 main text다. 진행 상태는 UI 탭과 독립적인 모델에서 보관한다.
- 즐겨찾기 칩: 최소 높이 32, 좌우 padding 12, radius 16, font 12 Semibold, 값 표시는 항상 `hh:mm:ss`(예: 00:25:00). 선택·포커스·눌림은 별도 버튼 외형을 만들지 않는다. Idle은 raised 배경, Hover는 hover surface. 칩을 클릭하면 해당 시간을 설정하고 대기 상태가 되며 자동 실행하지 않는다.
- +: 32 × 32 원형 또는 radius16, border 1, 16px `add` outline. tooltip `Add preset`.
- preset 오른쪽 클릭: `Edit`, `Remove` 메뉴. 각각 최소 높이32, 좌우 padding12, 모서리 radius8, surface+border. Remove는 error 텍스트만 사용하고 즉시 제거한다. 활성 시간은 preset 제거와 무관하게 그대로 남아야 한다.
- Play/Pause: accent solid 배경, accent foreground 20px canvas icon (Play outline / Pause filled), hover accent-hover. 0초에서는 비활성. tooltip/accessibility name을 `Start timer` / `Pause timer`로 바꾼다. Reset: raised 배경, main-text outline. 실제 geometry의 ink center를 24px canvas 중심에 맞춰 배치, tooltip `Reset timer`.
- 테마 버튼: 36 × 36, 배경 투명, hover surface, 22 DIP canvas의 outline glyph. 현재 Dark일 때 sun(전환 목적 Light), 현재 Light일 때 moon(전환 목적 Dark). tooltip도 `Switch to light theme` / `Switch to dark theme`로 전환한다.
- Running: status 앞 accent 작은 점 5px, Pause 아이콘. 숫자는 동일 색상. Paused: muted status, Play 아이콘. 완료: `00:00:00`, `Time’s up`, Play로 전환. 너무 큰 완료 팝업은 만들지 않고 세 음(짧음·짧음·길음) 묶음을 두 번 재생한다. 묶음 사이에 380ms를 두고, Reset/새 시간 설정/재시작/창 닫기는 재생을 중단한다.

## 시간 편집과 행동 계약

필드는 시/분/초를 직접 눌러 편집하는 3개의 입력 영역이다. 평상시 테두리/배경은 패널과 같고 hover 또는 focus 때만 약한 focus 표시가 생긴다. 첫 숫자 입력이 기존 선택 숫자를 대체하고 이후 숫자를 이어 붙인다. Enter/blur로 확정하고 Escape로 이전 값을 복구한다. 숫자 입력 중에는 Space 핫키가 타이머를 시작하지 않는다.

- hour 클릭 후 `40` → `40:현재분:현재초`; `8` → `08` 시간.
- minute 클릭 후 `20` → 현재시간:`20`:현재초.
- minute 클릭 후 `408` → `04:08:현재초`. 오른쪽부터 두 자리씩 끊고 상위 필드로 넘긴다.
- second 클릭 후 `123000` → `12:30:00`. 3-4자리면 MMSS이며 기존 hours를 보존하고, 5-6자리면 HHMMSS 전체를 바꾼다.
- 짧은 입력을 `08`처럼 zero-pad 한다. 시는 두 자리 display로 최대 99이며, 분/초 overflow는 정규화한다(예: minute `90` → 기존시+1:30:현재초). 최종 duration 범위는 00:00:00–99:59:59. 범위를 넘는 입력은 확정하지 않고 조용한 오류문구로 알린다. 조용히 clamp하지 않는다.
- 실행 중 시간 필드를 편집하기 시작하면 먼저 일시정지한다. 편집 확정은 새 duration으로 설정하고 대기 상태로 바뀐다. 실행 중 preset을 선택해도 새 duration을 설정하고 대기 상태가 된다.
- Running의 숫자는 읽기 전용 TextBlock으로 갱신한다. 시·분·초를 클릭하면 일시정지하고 대응하는 입력 필드를 선택한다. Ready/Paused에서는 편집용 TextBox를 표시하며 숫자 입력의 IME는 비활성화한다. Record Title 입력은 IME를 사용할 수 있다.
- Reset은 처음 실행한 duration이 아니라 '가장 최근에 사용자 입력 또는 칩으로 설정한 duration'으로 돌아가며 Ready 상태가 된다. Pause/Resume은 reset base를 바꾸지 않는다.
- 시간입력, preset 추가/수정, 테마 상태는 실제 타이머 모델과 분리한다. preset dialog를 연 동안 실행 중 타이머는 계속 진행한다.

## Add / Edit preset 새창

- 메인 창을 owner로 갖는 중앙 정렬 dialog, 360 × 248 DIP, resize 금지, 같은 테마. 타이틀 바 `Add preset` / `Edit preset`, close만 표시.
- 본문 padding24. 제목 `Add preset` / `Edit preset` 18 Semibold; 아래 문구 `Set a time you use often.` 12 muted.
- y=94에 3개 시간입력(각80 width, 42 height, radius8, surface/border)와 콜론. font24 Pretendard Regular (tabular digits); 작은 Hours/Minutes/Seconds label.
- Main과 동일한 숫자 carry 편집을 사용한다. 0초 preset 저장 금지. 오류는 field 아래 높이16, 11px error 텍스트로 표시하고 레이아웃을 흔들지 않는다.
- 하단 오른쪽 `Cancel`(raised, 76 × 34) / `Add` 또는 `Save`(accent,76 ×34), gap8. Enter는 Add/Save, Escape는 Cancel. 처음 열면 minutes를 focus/select. 저장 후 칩 목록 갱신하며 타이머는 그대로 둔다.
- 즐겨찾기·테마·마지막 설정 시간을 EXE 옆 B01Timer.ini에 저장. INI가 없을 때 기존 AppData JSON을 한 번 가져오며 원본은 보존. 재시작 후 유지. preset edit은 기존 칩 위치를 유지한다.

## 아이콘 출처와 개발 자산

사용자 지정 참고: https://reicon.dev/icons?weight=outline
공식 라이브러리는 24 × 24 grid, Outline/Filled를 제공하고 MIT 라이선스로 안내한다(https://reicon.dev/). 실제 outline SVG는 https://reicon.dev/cdn/reicon.js 의 inline `O` 데이터를 확인하여 아래 파일로 추출했다.

- reicon-play.svg
- reicon-pause.svg
- reicon-restart.svg (Reset)
- reicon-add.svg
- reicon-sun.svg
- reicon-moon.svg

SVG 자산은 src/B01Timer/Assets에 있다. Filled Pause는 기존 Outline glyph의 외곽 두 contour만 사용하고 안쪽 cutout을 제거한 파생 자산이다. 개발자는 SVG의 path `d`를 WPF Path Data로 사용하고 fill-rule=evenodd는 Geometry FillRule=EvenOdd로 맞춘다. Outline weight 자체가 채워진 외곽 path로 표현되므로 Stroke를 추가하지 않고 Fill로 사용해야 모양이 맞는다. 필요시 24px viewBox를 Viewbox로 축소한다. Git repository에 자산과 출처/라이선스를 같이 넣는다. 독립적인 인터페이스 장식용 아이콘을 임의로 늘리지 않는다.

## 접근성 / 최종 QA

- Button, TextBox, Tab 역할을 갖는 실제 WPF control을 사용한다. icon-only control에 AutomationProperties.Name와 tooltip을 설정한다.
- 키보드 Tab 순서는 Timer → theme → presets → + → hours → minutes → seconds → Play/Pause → Reset. Record 탭도 키보드로 접근할 수 있다. Record에서는 제목 칩 → + → Reset 순서다.
- 버튼은 Idle/Hover만 사용하고 focus stroke와 pressed opacity는 표시하지 않는다. 빈 Record의 Reset, 5개 등록 시 +, 0초 Start의 Disabled는 유지한다. 숫자 입력의 Focus ring과 선택 highlight는 편집 위치를 보여주기 위해 유지한다.
- 본문 text는 정상 크기에서 대비 4.5:1 수준을 확보한다. Small label은 muted token을 그대로 사용하고 더 낮은 opacity를 적용하지 않는다.
- 확인: Dark/Light main+dialog+menu, 긴 preset 목록, 0초 Start disabled, 숫자 carry 입력, pause/reset base, modal 중 timer 지속, 150%/200% DPI의 숫자와 아이콘 clipping.

## Record 화면과 저장 계약

- 초기 칩 행은 +만 표시한다. +는 Title 입력과 Running program 드롭다운을 가진 400 × 340 대화상자를 연다. 두 필드가 필요하고 서로 다른 실행 파일을 최대 5개 등록한다. 드롭다운은 열 때마다 실행 중인 보이는 프로그램 목록을 갱신한다.
- 프로그램 칩에는 사용자가 입력한 Title이 표시된다. 긴 제목은 줄임표와 tooltip을 사용한다. Edit/Remove 컨텍스트 메뉴를 제공한다. 선택은 보여줄 기록만 바꾸며 모든 등록된 프로그램의 포커스 측정은 계속된다.
- 시간 패널과 HOURS/MINUTES/SECONDS labels는 Timer와 같은 위치·크기다. Record의 시간은 읽기 전용이며 00:00:00부터 증가한다. 하단에는 Reset만 표시하고 선택한 기록만 초기화한다.
- 24시간 후 작은 1d 표시와 함께 시·분·초가 00:00:00으로 돌아간다. 7일은 1w, 8일은 1w 1d다. badge는 기존 Grid 안 좌측 상단의 overlay이고 새 행·열·margin을 만들지 않는다.
- Reset/Play/Pause는 24-unit canvas 안에서 실제 ink의 최대 직경을 Reset에 맞추고 20 DIP Viewbox로 표시한다. 세 glyph의 ink center를 버튼 center에 맞춘다. 테마 버튼은 36 × 36, glyph canvas 22 × 22다.
- 전경 HWND의 실행 파일 전체 경로를 대소문자 구분 없이 비교한다. 같은 프로그램의 여러 창이나 재실행은 같은 기록을 사용한다. 독립적인 실행 파일 경로의 프로그램을 5개까지 등록할 수 있다. 탭이나 표시할 칩은 측정 조건에 포함되지 않는다.
- 누적은 100ns tick 단위로 보관하고 표시에서만 초를 내린다. Windows awake clock을 사용하며 sleep/hibernation을 제외한다. 세션 잠금 시 WM_WTSSESSION_CHANGE 알림으로 포커스를 해제한다. 전경 프로그램은 50ms마다 확인한다. System.Threading.Timer 신호를 중복 없이 UI Dispatcher에 전달하고 모든 모델 변경과 화면 갱신은 UI thread에서 수행한다. 앱 종료 중에는 기록하지 않는다.
- EXE 옆 B01Timer.ini에 [Records] Count, [Record1] 이후 Id/Title/ExecutablePath/ElapsedTicks, [Settings] SelectedRecordId를 추가한다. 기존 Timer 항목과 migration은 보존한다. 활성 누적은 매초 atomic save하고 종료·Reset·등록 수정 시에도 저장한다.
