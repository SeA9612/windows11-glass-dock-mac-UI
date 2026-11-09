# GlassDock 다른 PC 작업 인수인계 — 2026-11-05

이 파일부터 읽어주세요. 이 압축은 현재 프로젝트 전체의 로컬 스냅샷입니다.
소스, 기존 문서/참고 HTML, 테스트, 빌드 도구, 기존 백업, 빌드 산출물과 최신 실행본을 포함합니다.
전체 채팅 원문을 내보낸 파일은 아니며, 아래에 작업 결정과 구현 상태를 정리했습니다.

## 최신 수정 — 미리보기 유리 배경만 적용 / 권한 등록 보류

- 최신 실행본: `artifacts/glass-preview-win-x64/GlassDock.exe`.
- 사용자 최종 선택은 **'유리 배경만 먼저 적용'**이다. 노란 캡처 테두리 숨김은 보류. 관리자 인증서 등록이나 패키지 설치를 자동으로 이어가지 말 것.
- 호버 미리보기는 독과 같은 실제 HostBackdrop 유리 재질·색상·반사광·테두리·그림자를 재사용한다. 각 카드 16 DIP 모서리와 실시간 영상, 위치/크기/등장 모션 유지. 독의 호스트와 분리해 생성·해제한다.
- 실행 검증 67개 및 실제 테스트 화면의 유리 투과·실시간 갱신 확인. 권한 비활성화 기본 빌드 게시 성공.
- 권한 코드와 서명 식별 패키지는 준비했지만 설치는 Windows 신뢰 검증 오류로 실패했고, 컴퓨터 인증서 등록 필요성을 설명한 후 사용자가 보류했다. 기본 빌드는 권한 창을 요청하지 않는다.
- 세부 변경/검증/보류 상태: `docs/GLASS_PREVIEW_20261105_KO.md`를 먼저 읽을 것.

## 이전 수정 — 앱 구역/도구 구역 구분선 유지

- 최신 실행본: `artifacts/sectioned-recycle-dock-win-x64/GlassDock.exe`.
- 사용자 정정에 따라 `[앱 구역 │ 앱도 배치 가능한 전체 창·작업공간 구역 │ 휴지통]` 구조로 수정. 앱/도구 구분선을 복원하고 저장 순서의 `separator:apps-tools` 경계로 관리한다. 앱은 경계 양쪽으로 이동할 수 있지만 경계 자체와 휴지통은 이동하지 않는다.
- LayoutVersion 2로 이관. 버전 1의 기존 앱/도구 순서는 보존하고 첫 도구 앞에 경계를 추가한다. 새로 발견한 앱은 왼쪽 구역에 추가. 기존 고정 앱/기타 설정 유지.
- 순서 모델 포함 검증 63개, Windows 테스트 창 실행 검증 255개 통과. 테스트 캡처에서 구분된 세 구역과 오른쪽 앱 배치 확인. 실제 OS 마우스 드래그는 앞서 기록된 화면 자동화 연결 제한으로 미확인.

## 이전 수정 — 실제 휴지통과 앱·도구 혼합 순서

- 최신 실행본: `artifacts/recycle-mixed-order-win-x64/GlassDock.exe`.
- 오른쪽/아래 마지막 고정 구역에 실제 Windows 휴지통과 구분선 추가. 실제 상태/시스템 빈·찬 아이콘, 열기, 명시적인 메뉴 선택 후 Windows 확인을 생략하지 않는 비우기. 드래그로 파일을 삭제하는 기능은 없음.
- 앱과 두 도구를 공통 이동 영역 및 `LayoutOrder` 데이터로 관리. 도구 앞/사이/뒤에 간격 드롭과 표시 선 제공. 휴지통 영역은 제외. 기존 Order/Pins/기타 설정을 보존해 LayoutVersion 1로 이관하고 닫힌 앱 위치도 유지.
- 검증 363개 통과. 실제 Windows 테스트 창/라우팅 이벤트/설정 새 창 생성 및 휴지통 조회·아이콘 확인. 실제 휴지통 열기 호출 성공. 화면 자동화 native pipe 연결 실패로 OS 마우스 드래그와 열린 탐색기 창은 미확인. 실제 비우기/삭제는 실행하지 않음.
- 사용자 설정 이관 전 사본: `artifacts/recycle-dock-validation/settings-before-migration.json`.
- 코드/원인/크기 계산/검증 한계: `docs/RECYCLE_ORDER_20261105_KO.md`를 먼저 읽을 것.

## 이전 수정 — 전용 AI 바로가기 버튼 제거

- 최신 실행본: `artifacts/dense-native-icons-no-ai-win-x64/GlassDock.exe`.
- 사용자는 AI 앱을 직접 고정해 사용하므로 별도의 별 모양 AI 실행 버튼, 전용 설정 메뉴와 앱 검색/실행 코드를 제거했다. 일반 앱으로 고정한 AI 아이콘과 설정의 고정 목록은 유지한다.
- 기능 버튼은 전체 창 보기/작업 보기 2개이며 계산식의 M=N+2로 변경. 동일한 앱 수에서 독 길이 40 DIP 감소. 기존 외형/아이콘 실행 검증 160개 재통과.

## 이전 수정 — 작은 독과 실제 프로그램 대표 아이콘

- 최신 실행본: `artifacts/dense-native-icons-win-x64/GlassDock.exe`. 아래 모든 실행본보다 최신 소스이며 기존 둥근 실시간 미리보기와 이동 모션도 포함한다.
- 32 DIP 아이콘, 8 간격, 3 실행 점, 길이 방향 여백 9/두께 방향 7. 손잡이를 같은 줄/열로 이동. 기본 독 두께 52 DIP, 앱 추가 시 길이 40 DIP 증가. 실제 입력 HWND와 둥근 영역도 맞추고 그림자는 클릭 통과 유지.
- 이름별 공통 그림/장식 타일을 없애고 Windows Shell의 실제 exe/lnk/AppsFolder 앱 아이콘을 사용. 전용 STA 추출, DPI별 캐시, 알파/비율 보존, 네이티브 자원 해제. 고정 앱은 창이 없어도 계속 표시.
- 외형/아이콘 실행 검증 160개, 기존 정책/배치 45개, 실시간 미리보기 57개 통과. 실제 테스트 창 캡처와 100/125/150% 대응 렌더링을 확인했으며 물리 화면 배율 변경/전체 마우스 동선 검증과 구분한다.
- 자세한 계산식, 수정 파일, 지원 범위, 실패 로그와 확인 한계: `docs/DENSE_DOCK_ICONS_20261105_KO.md`.

## 이전 수정 — 실제 영상까지 잘리는 16 DIP 라운드 카드

- 최신 실행본: `artifacts/rounded-preview-win-x64/GlassDock.exe`. 이전 compact-preview 및 그 이전 실행본보다 이후 버전임.
- `PreviewWindow.cs`: 영상과 제목을 동일한 `RectangleGeometry` 16 DIP 클립으로 묶고, 미리보기 대상 HWND에 카드별 둥근 네이티브 영역을 적용해 곡선 밖에서는 실제 뒤 배경이 보이게 함. 카드 크기/배치/색과 클릭 선택, 등장 모션, 자동 닫힘 유지. 제목이 곡선에 잘리지 않도록 안쪽 가로 여백 8 DIP. 원본 앱 HWND의 모양은 변경하지 않음.
- DWM 썸네일은 WPF Border/Clip의 영향을 받지 않으며 SetWindowRgn만 적용한 실제 픽셀 실험에서도 사각 영상이 곡선 밖에 남았음. 해당 방식은 채택하지 않음.
- `LiveWindowCapture.cs` 신규: 공식 Windows Graphics Capture의 연속 프레임을 Direct3D11/SoftwareBitmap을 통해 WPF WriteableBitmap에 갱신. 보이는 동안 최대 30fps, 한 프레임만 처리, 재사용 버퍼와 표시 크기에 맞춘 해상도 제한. 고정 이미지 또는 파일 캡처로 대체하지 않음. 캡처 세션/프레임풀/장치는 닫힐 때 해제함.
- 최소화로 원본 렌더링이 멈추면 마지막 프레임 또는 복원 안내를 표시하고 기존 ↗ 버튼으로 복원. 캡처 불가/보호된 창은 안내하며 정적 이미지를 실시간처럼 표시하지 않음. Windows가 원본 창에 캡처 표시를 할 수 있음.
- 빌드 성공. 실행 검증 57개 통과: 네 방향 모션과 배치, 실제 화면 Red→Blue 갱신, 네 모서리의 뒤 배경 픽셀, 제목 바닥/영상 상단 직선 유지, 최소화/복원, 원본 크기 변경 후 영상 갱신, 여러 카드의 개별 라운드 영역과 클릭 선택, 자원 해제.
- 실제 테스트 창의 캡처를 육안 확인함: `artifacts/rounded-preview-validation/rounded-card-screen.png`. 초록색은 투명하게 잘린 모서리 뒤의 테스트 배경이며 파란색은 살아 있는 원본 영상. 카카오톡 등 개별 앱 및 125%/150% DPI 실화면은 별도 검증 필요.

## 이전 수정 — 간결한 자동 미리보기 카드

- 최신 실행본: `artifacts/compact-preview-win-x64/GlassDock.exe`. 아래 모든 이전 실행본보다 이후 소스임.
- 자동 미리보기에서 상단 제목/창 개수/닫기 버튼, 하단 안내 문구, 상시 실시간 상태 문구, 이중 카드 여백 제거. 1개 창 기준 224×144 DIP에 화면+한 줄 제목으로 표시. 여러 창은 8 DIP 간격으로 배치하고 원본 비율을 보존함.
- 기존 실시간 DWM 연결, 300ms 등장 모션, 마우스 이탈 닫기, 클릭 창 선택, 켜기/끄기 설정 유지. 상태는 툴팁으로 확인할 수 있고 최소화된 원본에만 제목 옆 작은 ↗ 복원 버튼 제공. 전체 창 보기/수동 선택 화면은 유지함.
- 기존 미리보기 실행 검증 39개 통과: 실제 원본 Red→Blue 픽셀 변화, 네 방향 모션·배치, 자원 정리, 최소화·복원 동작 확인. self-contained 게시 성공.

## 이전 수정 — 미리보기 등장 모션과 실시간 상태

- 최신 실행본: `artifacts/live-preview-motion-win-x64/GlassDock.exe`. 아래 hover-preview/drag-motion/transfer-current 실행본보다 이후 버전임.
- 자동 미리보기 등장 시 독 방향에서 18 DIP를 300ms Sine EaseOut으로 이동. WPF 콘텐츠와 DWM 썸네일 불투명도를 함께 보간. 창 자체를 움직여 썸네일과 프레임 위치를 일치시키며 모션 타이머는 완료/종료 시 정리함.
- 기존 구현도 정적 이미지가 아닌 DWM 실시간 연결이었음. 750ms 타이머는 창 상태/썸네일 배치 점검 주기이며 영상 프레임 속도가 아님. 주기만 줄이는 방식으로 실시간 기능을 가장하지 않음.
- 상태 문구를 DWM 영상 밖의 캡션으로 옮겨 가려지지 않게 함. 최소화된 원본은 '최소화됨 · 마지막 화면'으로 표시하고 '실시간 보기'로 원본 창을 비활성 복원(SW_SHOWNOACTIVATE)할 수 있음. 마우스를 올리는 것만으로 원본 창을 복원하지 않음. 원본 앱 자체가 렌더링을 중단하거나 보호된 콘텐츠이면 실시간 갱신을 보장할 수 없음.
- `artifacts/hover-preview-smoke` 실행 검증 39개 통과. 네 방향 모션 시작/완료, 기존 상태/배치/자원 검증과 함께 테스트 원본 Red→Blue 변경 후 160ms 내 미리보기 실제 픽셀 변화 확인(썸네일 재등록 없이). 최소화 상태 표시, 복원 후 미리보기 유지 및 실시간 상태 복귀도 확인함. KakaoTalk 등 개별 앱의 실제 갱신은 별도 사용자 확인 필요.

## 이전 수정 — 마우스 올리기 미리보기

- 현재 실행본: `artifacts/hover-preview-win-x64/GlassDock.exe`. 아래 drag-motion/transfer-current 실행본보다 이후 소스이며 이동 모션도 포함함.
- 앱 아이콘에 300ms 머무르면 독 옆에 DWM 창 미리보기를 자동 표시. 창을 활성화하지 않고 유지하며, 아이콘과 미리보기 사이 이동을 위해 250ms 닫힘 유예를 둠. 미리보기 클릭 시 해당 창을 활성화함. 열린 창 없는 앱은 자동 미리보기를 표시하지 않음.
- 앱 아이콘 또는 독 손잡이/배경 우클릭 메뉴의 '마우스 미리보기 켜기/끄기'로 전체 자동 미리보기 설정 변경. `HoverPreviews`에 저장하며 기존 설정에 키가 없으면 기본 켜짐. 수동 '열린 창 미리보기'는 계속 사용 가능.
- `DockWindow.cs`, `PreviewWindow.cs`, `Settings.cs` 수정. 이동/아이콘 클릭/우클릭 시 자동 미리보기 정리, 마우스가 벗어나면 닫힘. 기존 전체 창 보기/수동 미리보기 동작 유지.
- Release 빌드 경고/오류 0, 기존 테스트 45개 통과, self-contained 게시 성공. `artifacts/hover-preview-smoke` 실행 검증 26개 통과: 기본/저장 설정, 네 방향 배치, 비활성 상태 유지, DWM 썸네일 등록/해제. 실제 마우스 진입/이탈 동선과 시각 품질은 사용자 확인 필요.

## 이관 후 수정 — 이동 손잡이와 접힘 모션

- 현재 작업 폴더: `GlassDock-0.2-source/GlassDock-0.2-source`. 비교 당시 full-handoff 폴더와 전체 1,223개 파일이 동일했으나 이후 수정은 이 폴더에만 적용함.
- `DockWindow.cs`: 손잡이 선택 영역을 32×16 DIP로 확대. 드래그 기준 거리를 넘으면 한 칸(콘텐츠 58×60 DIP)으로 280ms 동안 접고, 놓은 화면 가장자리에서 460ms 동안 펼침. Sine EaseInOut 사용.
- 마우스 캡처로 접히는 중에도 손잡이와 커서 위치를 맞추고, 이동/모션 중 자동 숨김과 아이콘 갱신을 보류. 네이티브 유리 배경도 크기 변경에 동기화함.
- 수정본 실행 경로: `artifacts/drag-motion-win-x64/GlassDock.exe`. 기존 `transfer-current-win-x64`는 수정 전 게시본임.
- .NET SDK 8.0.427로 Release 빌드 경고/오류 0, self-contained 단일 파일 게시 성공. 기존 정책 15개 및 배치/비율 30개 테스트 통과.
- 수정본 시작 및 ContentRendered 로그 확인. 화면 자동화 앱 접근 승인 시간 초과로 실제 드래그 모션과 여러 DPI에서의 시각 검증은 미완료.

## 새 PC에서 시작

1. ZIP 전체를 쓰기 가능한 폴더에 압축 해제합니다.
2. 최신 소스 기준 실행본: `artifacts/transfer-current-win-x64/GlassDock.exe`.
   Windows x64 self-contained 단일 파일 게시본이므로 실행에 별도 .NET 설치가 필요하지 않습니다.
   이 실행본은 빌드/게시 성공을 확인했지만, 게시 후 실제 화면 및 시작 검증은 아직 하지 않았습니다.
3. 기존 `artifacts/win-x64/GlassDock.exe`는 이전 게시본입니다. 최신 유리 구현과 다를 수 있어 혼동하지 마세요.
4. 소스 작업에는 Windows와 .NET SDK가 필요합니다. 현재 PC는 SDK 10.0.403을 사용했습니다.
   앱 대상은 `net8.0-windows10.0.19041.0`, WPF이며 네이티브 유리 지원 기준은 Windows 11입니다.
   첫 빌드 시 NuGet/Windows SDK 참조 복원에 인터넷 연결이 필요할 수 있습니다.
5. PowerShell을 압축 해제한 프로젝트 루트에서 열고 아래 명령을 실행합니다.

```powershell
dotnet run --project tests/PolicyTests.csproj -c Release
dotnet build src/Dock/Dock.csproj -c Release
dotnet publish src/Dock/Dock.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/win-x64
Start-Process -FilePath (Join-Path $PWD 'artifacts/win-x64/GlassDock.exe')
```

빌드할 경로의 실행본이 켜져 있으면 먼저 트레이 메뉴에서 GlassDock을 종료하세요.
`build.ps1`도 테스트와 게시를 수행합니다. 실행 정책 때문에 차단되면 위 명령을 직접 실행하면 됩니다.
새 PC의 SDK/네이티브 재질 지원은 따로 확인해야 합니다.

## 설정과 로컬 기록

`handoff-local-state`에 현재 PC의 settings.json, startup.log, errors.log 사본을 보관했습니다.
설정에는 독 방향, 고정 앱, 순서, 선택 AI 앱과 PC별 경로, 과거 초안 데이터가 포함될 수 있습니다.
자동으로 덮어쓰지 않습니다. 이전 설정을 이어받고 싶으면 GlassDock을 종료한 뒤
새 PC의 `%LOCALAPPDATA%/GlassDock/settings.json`을 먼저 백업하고 사본을 복사하세요.
모니터 이름과 앱 exe 경로는 새 PC에 맞게 다시 지정해야 할 수 있습니다.
ChatGPT 등의 로그인/앱 설치/구독은 이 ZIP으로 이전되지 않습니다.

## 지금까지의 사용자 결정

- 프로젝트 소스를 이 PC에서 직접 수정하고 테스트/빌드. 패치 ZIP만 주는 개발 방식은 사용하지 않음.
  이번 ZIP은 다른 PC로 작업 전체를 옮기기 위한 스냅샷 요청에 따른 것임.
- 실제 열린 앱과 창을 사용하는 Windows 독. 아이콘 크기/간격/실행 점/창 개수/순서 유지.
- 활성 창 변경이나 아이콘 클릭만으로 아이콘 순서가 바뀌지 않도록 안정 순서 저장.
  사용자가 드래그로 정한 순서는 저장.
- 초기 AI 패널에는 흰 배경과 부드러운 펼침 모션을 적용했으나, 이후 사용자 결정으로
  AI 대화 패널을 제거하고 선택한 AI 앱 바로가기 방식으로 바꿈.
- AI 아이콘은 ChatGPT/Claude 또는 사용자가 설정한 exe를 앱으로 실행. 웹 자동 대체하지 않음.
  기존 구독 서비스의 기존 대화를 원했고 별도 API 과금이나 새 대화로 대체하지 않음.
  현재 기능은 앱 바로가기이며, 독 내부 대화 연동이 된 것처럼 표시하면 안 됨.
- 최신 유리 요청은 바탕화면/HTML/생성 이미지가 아닌 실제 독 배경 재질 수정.
  아이콘은 유지하고 배경만 투명하게. 회색 플라스틱/고정 검정 채움은 피함.
  실제 뒤쪽 화면 블러와 얇은 유리 두께, 방향성 반사광, 좁은 내부 음영, 넓고 부드러운 외부 그림자.
  장식은 클릭/드래그/우클릭을 가로채지 않으며 아이콘 위에 블러나 반사광을 적용하지 않음.

## 최신 소스에 적용된 구현

- `src/Dock/GlassBackdrop.cs`: 투명 WPF 전경과 네이티브 배경 호스트를 분리. 실패 시 반투명 그라데이션 대체.
- `src/Dock/HostBackdrop.cs`: WPF 아래의 별도 비활성/클릭 통과 HWND에 Windows Composition HostBackdrop 배치.
  실제 뒤쪽 화면 소스와 네이티브 효과 그래프, 둥근 클립, 별도 배경 전용 Composition 그림자.
  배경 반복 캡처나 상시 반짝임 애니메이션 없음.
- `src/Dock/NativeBlurEffect.cs`: Direct2D GaussianBlur 효과를 Composition 효과 그래프에 연결하는
  WinRT/COM ABI 구현. 속성은 IPropertyValue 인터페이스로 반환하도록 수정됨.
  앞선 잘못된 ABI 구현에서는 시작 실패/네이티브 충돌이 있었으며 최신 수정 후 진단 실행은 시작됨.
  최신 게시 실행본에서도 안정성을 다시 확인해야 함.
- `src/Dock/GlassTheme.cs`: 색/알파/테두리/반사광/내부 빛/곡률/그림자 주요 값.
  NativeBackdropSigma는 앱이 소유한 Composition Gaussian 효과 값이며 DWM Acrylic의 블러 조절 값이 아님.
  일부 주석과 CreateAcrylicTint 명칭은 이전 구현의 이름이 남아 있음.
- `src/Dock/GlassEdgeLayer.cs`: 크기에 반응하는 도형/그라데이션으로 좁은 외곽/안쪽 경계,
  내부 음영과 빛 번짐, 상단 왼쪽/좌측/하단 반사광과 작은 반사점. 비히트테스트 장식.
- `src/Dock/DockWindow.cs`: 콘텐츠와 장식 분리, 배경 호스트 위치/크기/DPI/표시 상태 동기화.
  Border.Padding 안에 장식을 그려 별도 내부 캡슐처럼 보이던 문제를 수정하고
  같은 여백을 콘텐츠 Margin으로 이동하여 기존 아이콘 배치를 유지.
- `src/Dock/GlassMaterialReview.cs`: 환경변수 `GLASSDOCK_MATERIAL_REVIEW=1`일 때만 진단 배경 창과
  작업 표시줄 표시를 활성화. 키 1/2/3으로 어두운/흰색/색상 배경 변경.
  정상 실행에서는 이 변수를 설정하지 않음.
- `src/Dock/Dock.csproj`: Windows SDK WinRT Composition 참조를 위해 Windows 대상 TFM을 지정.
  WPF를 유지하며 Windows App SDK 추가 의존성은 넣지 않음.
- `src/Dock/Native.cs`: 네이티브 창 영역/위치 관련 선언 추가. 이전 실험의 미사용 선언이 일부 남을 수 있음.
- Copilot 수정 `src/Dock.Core/ClickPolicy.cs`, `OverviewLayout.cs`의 기존 동작을 유지.

## 검증 결과와 미완료 부분 — 중요

2026-11-05 이관 패키지 준비 시:
- 정책 테스트 15개 + 전체 창 배치/비율 테스트 30개 통과.
- 최신 소스의 win-x64 self-contained 게시 성공. 산출물 `artifacts/transfer-current-win-x64`.
- 로그: `artifacts/handoff-20261105/policy-tests.log`, `publish.log`.
- 기존 개발 실행본에서는 최신 COM 속성 수정 후 HostBackdrop 생성 및 ContentRendered 로그를 확인했음.
- 그 뒤 실제 화면 재확인을 진행하려던 중 사용자가 Escape로 화면 자동화를 중지하여 종료했음.
- 따라서 최신 효과 그래프 수정 후 배경 변경에 실시간 반응하는지, 블러/테두리/그림자가 목표에 맞는지
  최종 화면 검증이 끝나지 않았음. 빌드 성공을 디자인 완성이나 동작 검증으로 간주하지 말 것.
- 이전 직접 HostBackdropBrush만 SpriteVisual에 배치한 구현은 색상 배경 전환 후 흰 배경이 남는 현상이 있었음.
  현재는 효과 그래프 입력으로 연결했으나, 이 문제 해소 여부를 실제 화면에서 확인해야 함.
- 검은 창/흰 문서/컬러 배경, 세로/가로, 펼침·접힘, 클릭/드래그/우클릭/창 전환,
  실제 화면 배율 100%/125%/150% 확인이 남아 있음.
- HostBackdrop 생성 도중 실패할 때 부분 생성 리소스 정리, ABI 오류 경로와 유효하지 않은 인덱스 처리 등
  안정성 점검이 남아 있음. 구현 명칭/주석 정리 및 일부 장식 수치 중앙화도 후속 항목.
- `docs/LOCAL_VALIDATION_20261105_KO.md`, README, UI 문서에는 이전 DWM Acrylic/WindowChrome/SetWindowRgn
  구현 설명이 남아 있음. 최신 소스는 별도 Composition 호스트 구조이므로 그 설명을 현재 구조로 오해하지 말 것.

## 다음 AI에게 전달할 작업 지시

먼저 이 문서와 `docs/reference/CODEX_IMPLEMENTATION_BRIEF_KO.md`, 최근 docs와 실제 소스를 읽으세요.
이 문서의 최신 사용자 결정이 과거 브리프의 AI 대화 패널 요구보다 우선합니다.
사용자가 수정한 소스/설정/아이콘 배치를 보존하세요. 최신 구현은 빌드된 작업 중 상태이며
최종 화면/상호작용/여러 DPI 검증을 이어서 수행하고, 확인되지 않은 것을 작동/완료로 표시하지 마세요.
현재 PC의 사용자 폴더 바깥에 있는 상위 Git 저장소는 이 프로젝트 저장소가 아니며 포함하지 않았습니다.
프로젝트 안의 기존 `_GlassDock-backup-*`와 `artifacts/interop-inspect` 실험 파일도 원형 보존했습니다.
