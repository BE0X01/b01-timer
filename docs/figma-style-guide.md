# B01 Timer Figma 스타일 가이드

[Figma 파일 열기](https://www.figma.com/design/F3jN688KWp9JsFV7JSqPs7) · Project 팀 · 디자인 기준 커밋 `a39ebf1`

## 2026-10-07 앱 수정 이후의 차이

현재 EXE는 Pretendard 1.3.9 Regular/SemiBold를 내장하고 숫자도 Pretendard tabular로 표시한다. 버튼은 Idle/Hover만 사용하며 Pressed opacity, Focus stroke 및 preset Selected 배경은 제거했다. Disabled는 빈 Record Reset, 5개 등록 후 +, 0초 실행 불가를 위해 유지한다. Pause는 속이 채워진 두 막대, Reset은 실제 ink bounds를 중심으로 24px 캔버스에 배치한다. 완료음은 세 음 묶음을 두 번 재생한다.

아래 Figma 구성과 기존 ID는 초기 스냅샷을 설명한다. 현재 파일에는 이전 글꼴·Pressed/Focus/Selected variants가 남아 있으며 MCP 한도로 Figma에서 갱신하지 못했다. 현재 코드의 위 수정 지침이 우선이다. 이전 상태를 코드에 되돌리지 않는다. 이어 실행할 스크립트도 초기 스냅샷 기준이므로 최신 토큰과 버튼 계약에 맞춰 수정한 뒤 사용해야 한다.

## 완성한 범위

실제 WPF 소스에 사용된 컬러, typography, 벡터 아이콘, 버튼과 시간 입력의 상태를 편집 가능한 변수·스타일·컴포넌트로 생성했다. 화면 전체를 캡처한 이미지 레이어는 사용하지 않았다.

- 페이지 3개: 01 Foundations, 02 Components, 03 Interactions. 마지막 페이지는 아직 비어 있다.
- 변수 70개: 원시 컬러 26개 + semantic 컬러 26개(13역할 × Dark/Light) + geometry/opacity 18개.
- 텍스트 스타일 11개: 실제 앱 typography 9개 + 가이드용 2개.
- 실제 벡터 glyph 컴포넌트 10개: Reicon Play/Pause/Restart/Add/Sun/Moon, 창 Close/Minimize/Maximize, AppTimer.
- UI component sets 8개, variants 102개. glyph를 포함하면 Component 노드는 112개다.
- 원본 variant 86개에 Hover/Pressed 반응 102개를 설정했다. 실제 read-back은 인스턴스 상속분 포함 reaction nodes 172개였다.

| Component set | Variants | 실제 구현 대응 |
| --- | ---: | --- |
| Button/Text | 20 | Add, Save, Cancel · Accent/Neutral × 5 states × 2 themes |
| Button/Action | 20 | Play/Pause glyph swap + Reset · Accent/Neutral × 5 states × 2 themes |
| Button/Theme | 10 | Dark의 Sun, Light의 Moon · 5 states × 2 themes |
| Preset/Add | 10 | + · 5 states × 2 themes |
| Preset/Chip | 12 | Default/Hover/Pressed/Focus/Disabled/Selected × 2 themes |
| Tab | 10 | Timer 상태와 Record Disabled × 2 themes |
| Time/Field | 12 | Main/Dialog × Default/Hover/Focus × 2 themes |
| Menu/Item | 8 | Edit/Remove × Default/Hover × 2 themes |

5 states는 Default, Hover, Pressed, Focus, Disabled다. 버튼 문구는 Label property, 아이콘은 INSTANCE_SWAP property로 수정한다. Action의 Play glyph를 Pause glyph로 교체하면 실행 중 Pause 버튼을 표현할 수 있다. 원본 Reicon Add는 1.5px stroke이며 다른 glyph와 달리 Fill을 넣지 않는다.

## Figma 제한과 대체

Starter 팀에서 addMode가 `Limited to 1 modes only`로 실패하는 것을 확인했다. Dark/Light는 `B01/Color/Dark`, `B01/Color/Light` 별도 컬렉션에 연결했다. 두 컬렉션의 동일한 역할은 원본의 동일한 WPF Brush에 대응한다. 파일 모드를 바꿔 자동 전환하는 구조가 아니라 Theme variant가 각각 알맞은 컬렉션을 참조한다.

Figma의 available fonts에는 Segoe UI와 Consolas가 없었다. 초기 디자인 기준의 UI는 Inter, 숫자는 Roboto Mono로 표시했다. 당시 앱은 Segoe UI / Consolas였으며 현재 EXE는 Pretendard를 내장한다. 가이드 본문만 Noto Sans KR을 사용했다. font substitution은 text-style description과 JSON에 기록했다.

Figma opacity 변수는 percent 단위를 사용한다. WPF Pressed 0.8 → Figma 80, Disabled Neutral 0.45 → 45, Disabled Accent 0.4 → 40, Selection 0.5 → 50이다. `wpfValue`와 `value`를 구분해서 반영해야 한다. 변수의 WEB code syntax는 참조용 export alias이며 WPF에 실제 CSS 변수가 존재한다는 뜻이 아니다.

## 초기 Figma 스냅샷의 상태와 반응

- Hover: Neutral → HoverBrush, Accent → AccentHoverBrush.
- Pressed: Hover 배경 + 0.8 opacity.
- Focus: inset 2px, 2px focus stroke. Accent 버튼은 AccentForegroundBrush, 나머지는 AccentBrush.
- Disabled: Accent 0.4 / Neutral 0.45 opacity. Record는 DisabledBrush와 투명 배경.
- Selected preset: SelectedBrush 배경 + AccentBrush 글씨.
- Menu Highlighted: HoverBrush. Remove는 ErrorBrush.
- Time field Hover: RaisedBrush. Focus: AccentBrush 2px. Main 숫자는 60px, Dialog는 24px.

완료한 prototype은 개별 component의 `ON_HOVER → CHANGE_TO Hover`, `MOUSE_LEAVE → Default`, `MOUSE_DOWN → Pressed`, `MOUSE_UP → Hover`다. Focus, Disabled, Selected는 선택 가능한 명시적 상태이며 Windows 기능을 Figma에서 실행하는 것은 아니다.

## 아직 생성하지 못한 범위

Figma MCP의 Starter tool-call quota가 소진되어 use_figma 쓰기와 읽기가 모두 차단됐다. 현재 파일에는 아래 composed view 및 전체 flow prototype이 없다.

- Timer Ready / Running / Paused의 전체 창과 Dark/Light 화면 예시.
- Add/Edit dialog 및 context menu의 전체 창.
- 테마 전환, Start/Pause/Reset, Add/Edit/Remove 전체 흐름의 연결.
- 42×32 TitleButton 전용 상태 컴포넌트. 창 chrome의 실제 벡터 glyph는 완성했다.
- 시간 입력의 선택된 텍스트 highlight 전용 variant.

`docs/figma-complete-interactions.js`는 같은 파일의 03 Interactions 페이지에서 이어 만들도록 준비한 use_figma용 코드다. 제한으로 실행하지 않았으므로 테스트된 결과물로 취급하지 않는다. 먼저 최신 파일을 읽어 새로 생긴 사용자 작업이 없는지 확인하고, figma-use / figma-generate-library / figma-generate-design skill을 로드한 뒤 실행해야 한다. 실행 후 반환된 새 ID를 JSON에 추가하고 화면을 시각 검증한다. 준비된 prototype의 countdown/input/file storage는 정적인 예시이며, Add/Save/Remove는 overlay 닫힘과 별도 결과 snapshot으로 설명한다.

## 구현의 인터랙션 규칙

현재 Record는 활성 탭이며 등록한 프로그램의 전경 포커스 시간을 자동으로 누적한다. 표시할 칩이나 Timer/Record 탭을 전환해도 모든 등록 프로그램의 측정과 실행 중인 타이머는 계속된다. Record의 Reset은 선택한 기록만 0초로 만든다. 초기 Figma 스냅샷의 Disabled Record는 현재 구현에 적용하지 않는다.

테마 변경 및 preset dialog 열기 동안에도 실행 중 타이머는 계속 진행한다. 즐겨찾기를 선택하면 duration만 설정하고 Ready 상태가 되며 자동 시작하지 않는다. Timer의 Reset은 가장 최근에 사용자 입력이나 preset으로 설정한 시간으로 돌아가 Ready가 된다. Pause/Resume은 Reset 기준을 바꾸지 않는다.

시/분/초를 직접 누른 뒤 오른쪽부터 두 자리씩 입력한다. Hours 40은 40시간, Hours 8은 08시간이다. Minute 408은 04:08이며 기존 seconds를 보존한다. Seconds 123000은 12:30:00이다. 분/초 overflow는 정규화하고 최종 00:00:00–99:59:59 범위를 넘는 입력은 확정하지 않는다.

Add/Edit는 즐겨찾기 목록만 수정한다. Remove도 현재 timer 값이나 진행 상태를 바꾸지 않는다. 설정 파일은 실행 파일 옆의 `B01Timer.ini`다. 디자인 기준 커밋 뒤 저장 방식이 INI로 변경되었지만 이 가이드의 GUI 토큰/컴포넌트 디자인은 같다.

## 다음 수정의 소스 매핑

`docs/figma-design-map.json`에 실제 반환된 file key, page/frame IDs, 변수 ID/key, styles ID/key, component sets/variants ID/key, glyph ID/key 및 소스 파일 경로를 기록했다. 이름으로 추측하거나 새로운 ID를 만들지 않는다.

수정은 자동 동기화하지 않는다. 사용자가 Figma 변경 반영을 요청하면 현재 파일을 실제 읽고 JSON의 ID로 대응시킨다. 현재 코드와 비교해서 ThemeManager.cs, Styles.xaml, Icons.xaml, DurationEditor, MainWindow, PresetWindow, RecordWindow 및 Record 모델 중 해당 지점만 수정한다. Figma의 대체 폰트는 사용자가 앱 폰트 변경을 요청하지 않는 한 그대로 Windows에 적용하지 않는다. 코드 수정 뒤 core/appearance/Timer·Record UI QA와 실제 Dark/Light 화면 검증을 거친다.

## 시각 검증 근거

Foundations 1200×1761, Component states 1400×2502의 실제 Figma screenshot을 확인했다. 초기의 auto-layout 고정 높이, 버튼의 HUG width, glyph swap 기본값, opacity percent 문제를 수정한 뒤 잘림과 빈 요소가 없어졌고 컬러·폰트·아이콘·상태가 보이는 것을 확인했다. 이어 Cancel/Record 텍스트 override를 적용했다.

기존 screenshot URL을 로컬로 내려받으려 했지만 실행환경이 `Site Unavailable` HTML을 반환했다. 새 screenshot API는 호출하지 않았다. 짧은 수명의 기존 URL과 검증 메타데이터는 JSON의 visualEvidence에 보존했으며, 대화의 inline screenshot도 검증 근거다. 로컬 PNG 파일이 저장되었다고 주장하지 않는다.


## 2026-10-07 Record 구현 변경

현재 앱에는 Record 탭과 최대 5개의 프로그램 제목 칩, Title/Running program 드롭다운을 가진 등록 대화상자, 읽기 전용 누적 시간, Reset-only 하단 액션을 추가했다. day/week badge는 시간 패널 좌측 상단 overlay로 레이아웃을 바꾸지 않는다. Play/Pause는 Reset과 실제 ink 직경·중심을 맞춘 20 DIP canvas이고 Theme 버튼은 36 DIP, glyph canvas 22 DIP다. 이 변경은 source와 design-spec에 반영되어 있으며 Figma 원본 컴포넌트와 새 Record 화면에는 아직 반영하지 못했다. 기존 연결의 MCP quota 제한이 계속 적용되는 상태이고 원본을 업데이트했다고 주장하지 않는다.
