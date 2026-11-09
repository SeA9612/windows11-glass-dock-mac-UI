# 미리보기 유리 배경 / 캡처 테두리 권한 보류

## 현재 적용 상태 — 사용자 선택 우선

사용자는 인증서 등록 여부 질문에 **'유리 배경만 먼저 적용'**을 선택했다. 따라서 현재 실행본은 `artifacts/glass-preview-win-x64/GlassDock.exe`이며 캡처 테두리 숨김 권한을 요청하지 않는다. 노란 Windows 캡처 알림은 유지한다. 관리자 인증서 등록이나 권한 패키지 설치는 추가 사용자 요청 전까지 진행하지 말 것.

## 유리 미리보기

- 아이콘 호버 미리보기의 보라색 불투명 배경을 제거했다. 독과 동일한 `GlassTheme.CreateLayers`, Acrylic tint/fallback, 반사광·경계 레이어와 네이티브 `HostBackdrop`을 사용한다.
- 각 카드 뒤에 별도의 클릭 통과 HWND를 둬 실제 뒤 화면을 Composition Gaussian blur로 보여준다. 그림자 반경 14 DIP, 깊이 5, 불투명도 .23 등 독과 같은 값을 재사용한다.
- 카드 모서리는 기존 16 DIP를 유지한다. 영상은 Windows Graphics Capture의 연속 프레임이며 정적 이미지로 대체하지 않는다. 영상에는 유리 블러를 적용하지 않는다.
- `HostBackdrop.Sync`에 카드 위치/크기를 전달하는 선택 인자를 추가했고 기존 독의 기본 동작은 유지했다. 미리보기의 재질 호스트는 독의 전역 호스트와 분리해 관리하며 위치·DPI·등장 모션에 동기화하고 닫을 때 해제한다.
- 유리 효과를 가리던 기본 버튼의 흰 호버 배경을 제거했다. 미리보기 이미지 주변 여백과 제목 영역이 같은 유리 표면을 공유한다.
- 현재 범위는 아이콘 호버 카드이며 기존 큰 수동 전체 창 보기 UI는 유지한다. 유리 색은 뒤에 있는 화면에 따라 달라지며, 독과 같은 재질 수치를 사용한다.

## 검증

Release self-contained 게시 성공. 미리보기 실행 검증 **67개** 통과: 실제 원본 Red→Blue 영상 변화, 뒤 배경 투과, 독과 같은 tint 색상, 16 DIP 클립, 네 방향 배치와 모션, 최소화/복원, 크기 변경, 여러 카드 선택과 자원 해제.

실제 테스트 창을 화면 픽셀로 검사하고 캡처를 육안 확인했다. 초록 배경이 제목과 모서리 주변으로 투과하는 것은 테스트용 배경이며 임의의 초록색을 앱에 넣은 것이 아니다. 실제 사용자 앱 조합과 모든 물리 DPI 배율을 확인한 것은 아니다.

- 로그: `artifacts/glass-preview-validation/glass-preview-tests.log`
- 캡처: `artifacts/rounded-preview-validation/rounded-card-screen.png`

## 테두리 없는 캡처 — 준비됐으나 적용 보류

- `CapturePermission.cs`: Windows 공식 `GraphicsCaptureAccess.RequestAccessAsync(Borderless)`를 요청하고 **Allowed일 때만** 세션에 `IsBorderRequired=false`를 적용한다. 거부/미지원/식별 정보 없음이면 테두리를 유지한다.
- `packaging/identity/AppxManifest.xml`: `uap11:Capability Name=graphicsCaptureWithoutBorder`, 외부 위치의 Win32 앱 식별 정보를 선언한다.
- `src/Dock/app.identity.manifest`: 패키지 식별 메타데이터. 기본 `app.manifest`에는 이 정보를 넣지 않았다.
- `-p:EnableBorderlessCaptureIdentity=true`로 명시적으로 빌드한 경우에만 식별 매니페스트와 시작 시 권한 요청을 활성화한다. 일반 빌드에서는 비활성화한다.
- 현재 로컬 Windows는 서명되지 않은 실행 앱 식별 패키지 등록을 0x80073D2B로 거부했다. 전용 로컬 인증서로 서명한 패키지도 신뢰 검증 0x800B0109로 설치되지 않았다. 이에 관리자 권한으로 LocalMachine/TrustedPeople 등록이 필요한 점을 설명했고 사용자는 보류를 선택했다.
- 준비된 서명 패키지는 `artifacts/borderless-glass-preview-win-x64/GlassDock.LocalPreview.identity.msix`다. 현재 실행본으로 안내하지 말 것. 로컬 서명 인증서 Subject는 `CN=GlassDock Local`, Thumbprint는 `9EE75578067D14E5C44279A5E1F643C1A4013D42`이다. 임시 PFX 개인 키는 서명 후 삭제했다.
- 최종 조회에서 해당 인증서는 현재 사용자/컴퓨터 TrustedPeople에 없고, `GlassDock.LocalPreview` 패키지도 등록되지 않은 상태다. 전역 캡처 알림, 개발자 모드와 보안 정책은 변경하지 않았다.
- 준비 도구: `tools/Build-CaptureIdentity.ps1`, `tools/SignIdentity`, `tools/Register-CaptureIdentity.ps1`. 인증서 등록/패키지 설치는 별도 승인 후 관리자 범위를 확인해 수행해야 한다.
- 기본 SDK 참조는 권한 API 컴파일을 위해 `net8.0-windows10.0.22000.0`으로 올렸고 WPF 실행 검증 프로젝트들도 맞췄다.

공식 참고: [IsBorderRequired 조건](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.graphicscapturesession.isborderrequired), [패키지 식별 정보](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps), [MSIX 인증서 신뢰 문제](https://learn.microsoft.com/en-us/windows/msix/msix-troubleshooting-guide).
