# 독 밀도와 실제 앱 아이콘 수정

이 문서는 휴지통 추가 전의 작업 기록이다. 현재 실행본과 최종 크기/혼합 순서는 [휴지통 및 혼합 순서 문서](RECYCLE_ORDER_20261105_KO.md)를 우선 참고한다.

현재 소스 작업 폴더는 `GlassDock-0.2-source/GlassDock-0.2-source`이며 실행본은 `artifacts/dense-native-icons-no-ai-win-x64/GlassDock.exe`이다. 이후 사용자 요청으로 전용 AI 바로가기 버튼과 관련 실행 메뉴/검색 코드를 제거했다. 실제 AI 앱은 일반 앱으로 고정해 사용한다. 기존 고정 목록은 유지한다.

## 크기 계산

모든 값은 화면 캡처 픽셀이 아닌 DIP이다. 프로그램/기능 아이콘 32, 아이콘 간격 8, 실행 점 3, 아이콘과 점 간격 3, 길이 방향 바깥 여백 9씩, 두께 방향 여백 7씩이다. 손잡이는 길이 12, 다음 아이콘과의 간격 4이며 아이콘과 같은 줄/열에 있다.

프로그램 수를 N, 기능 버튼(전체 창/작업 보기)을 포함한 아이콘 수를 M=N+2라 하면:

- 길이 = `2×9 + 12 + 4 + M×32 + (M−1)×8 + (N>0 ? 1+8 : 0)`.
- 두께 = `32 + 3 + 3 + 2×7 = 52`.
- 가로 독은 길이×52, 세로 독은 52×길이. 프로그램 3개면 235×52 또는 52×235 DIP이다. 전용 AI 버튼 제거로 같은 프로그램 수에서 길이가 40 DIP 줄었다.
- 앱 1개 추가/삭제 시 길이 변화는 40 DIP이며, 프로그램이 0개가 되면 구분선도 없어진다.
- WPF SizeToContent와 패널의 자연 측정을 사용한다. 이동 접힘 모션 중에만 뷰포트 크기를 고정하고, 완료 후 Auto로 돌린다. 호버 확대는 RenderTransform이라 평소 측정 크기를 늘리지 않는다.
- 전경 HWND 크기도 내용 측정값과 일치한다. 전경에 둥근 입력 영역을 적용하며, 별도 유리/그림자 HWND는 클릭 통과를 유지한다. 그림자 반경 14, 바깥 여유 20 DIP로 줄였다.

## 아이콘 조회

- `IShellItemImageFactory.GetImage(ICONONLY | BIGGERSIZEOK)`로 프로그램의 Shell 아이콘을 가져온다. 썸네일, 문서 화면, 브라우저 파비콘, 창 WM_GETICON을 사용하지 않는다. ICONBACKGROUND/CROPTOSQUARE/SCALEUP 플래그도 사용하지 않는다.
- 일반 `.exe`, 실행 파일을 가리키는 `.lnk`, 명시적인 AUMID를 가진 `shell:AppsFolder` 앱을 지원한다. 고정 앱은 창이 없어도 표시한다. `.lnk`는 정확한 실행 파일 또는 AUMID로 그룹을 연결하고 실행할 때 원래 바로가기를 사용해 인수를 보존한다.
- 실행 중 패키지 앱은 프로세스 AUMID를 조회한다. ApplicationFrameHost 창은 명시적인 창 AppUserModelID로 식별한다. 이름이 비슷하다는 이유로 다른 로고를 선택하지 않는다.
- 아이콘 조회는 전용 STA 스레드에서 수행하고, 소스 식별자+요청 픽셀 크기로 성공/실패를 캐시한다. DPI 변경 시 32 DIP에 필요한 32/40/48 픽셀 크기를 다시 요청한다.
- 반환 HBITMAP의 알파를 보존해 고정된 WPF Image에 표시하며, Stretch.Uniform으로 비율을 보존한다. HBITMAP은 DeleteObject, DC는 ReleaseDC, COM 참조는 FinalReleaseComObject로 해제한다. 새 HICON은 만들지 않는다.
- 아이콘과 버튼 인스턴스는 창 수/배율/순서 변경 때 유지한다. 배지·실행 점·이미지 소스만 갱신한다. 프로그램 아이콘에는 공통 장식 타일이 없으며 기능 버튼의 별도 심벌만 유지한다.
- 조회 실패는 중립 아이콘으로 표시하고 `%LOCALAPPDATA%/GlassDock/startup.log`에 `App icon FAILED: 소스; 요청 크기; 원인`으로 남긴다. 해당 버튼 접근성 도움말에도 실패 소스를 남긴다.

## 지원 한계

문서/URL/스크립트 바로가기, 실행 파일/AUMID를 확인할 수 없는 특수·광고형 바로가기는 앱 아이콘으로 간주하지 않는다. 새 고정 메뉴는 지원 가능한 exe/lnk만 받는다. AppUserModelID를 제공하지 않는 호스트형 앱은 실제 앱을 확정할 수 없어 호스트 로고 대신 중립 대체 아이콘을 사용한다. 보호된 프로세스의 경로/식별 정보가 읽히지 않는 경우는 기존 창 목록 수집 방식의 제한으로 남아 있다. Store 앱은 실행 중 아이콘의 고정 메뉴로 고정할 수 있으며 별도의 설치 앱 선택 창은 추가하지 않았다.

## 변경 파일

- `src/Dock/DockMetrics.cs`: DIP 수치와 계산식.
- `src/Dock/DockWindow.cs`: 한 줄/열 레이아웃, 실제 입력 영역, 고정 앱 유지, 아이콘 비동기 갱신, 현재 목록 기준 순서 변경.
- `src/Dock/DockArtwork.cs`: 원본 로고·점·배지를 보존하는 AppIconView, 작은 기능 아이콘.
- `src/Dock/ShellIconCache.cs`, `AppIdentity.cs`: Shell 아이콘과 정확한 앱/바로가기/패키지 식별.
- `src/Dock/App.xaml`: 버튼 크기/마진 기본값. `GlassTheme.cs`: 작은 독에 맞는 모서리와 그림자.
- `src/Dock/WindowCatalog.cs`: 패키지 앱 식별, 감시 중지/해제 처리.
- `src/Dock/LiveWindowCapture.cs`: 회귀 검증에서 발견한 마지막 프레임 누락 보완. 처리 중 변경 프레임을 보류하고 최신 프레임을 선택해 재처리한다.
- `src/Dock/AppearanceTestAccess.cs`, `artifacts/dock-appearance-smoke`: 실제 창/아이콘 실행 검증 지원. 테스트는 사용자 설정을 저장하지 않는다.

## 확인 결과와 범위

- Release 빌드 성공. 외형/아이콘 실행 검증 160개, 기존 클릭 정책 15개+배치 30개, 둥근 실시간 미리보기 57개 통과.
- 실제 설치된 Chrome, VS Code, 카카오톡, Discord, 탐색기, Firefox, 메모장의 Shell 로고·투명도를 확인했다. 실행하지 않은 설치 앱과 exe 바로가기, AppsFolder 패키지 아이콘도 확인했다.
- 실제 테스트 HWND에서 가로/세로 네 방향의 크기, 아이콘 추가/삭제, 네이티브 입력 영역, 그림자 클릭 통과 스타일을 검사하고 화면 캡처를 육안 확인했다.
- 테스트 창의 실제 최소화/복원을 버튼 이벤트로 확인했다. 고정/해제와 드롭 순서 변경 처리도 확인했다. 실제 마우스로 드래그를 끝까지 수행하는 동선, 모든 개별 프로그램의 활성화 조합, 자동 숨김의 실제 마우스 가장자리 진입은 이번 검증에서 재현하지 않았다.
- 100/125/150%에 대응하는 32/40/48px 원본 조회와 WPF 렌더링을 확인했다. Windows의 물리 화면 배율을 125/150%로 변경하여 전체 조작을 확인한 것은 아니다.
- 증거: `artifacts/dense-dock-validation/appearance-tests.log`, `preview-tests.log`, `icons-100.png`, `icons-125.png`, `icons-150.png`, `dock-*-screen.png`.
