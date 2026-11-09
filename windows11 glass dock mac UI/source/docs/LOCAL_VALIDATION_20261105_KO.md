# 2026-11-05 사용자 PC 로컬 검토 및 검증

현재 프로젝트 버전은 `src/Dock/Dock.csproj` 기준 **0.2.1**이다. 이번 작업은 사용자 Windows PC의 열린 프로젝트에서 직접 수행했다. 이후 변경도 이 소스에 직접 적용하며 패치 ZIP을 기본 전달 방식으로 사용하지 않는다. 이전 문서의 Linux 환경 및 ZIP 적용 설명은 과거 작업 기록이다.

## 실행 불가 보고 후 재확인

사용자가 이전에 실행 후 화면에 독이 없다고 보고했다. Windows 응용 프로그램 로그에서 `src/Dock/bin/Debug/net8.0-windows/GlassDock.exe`의 .NET 8 런타임 시작 오류를 확인했다. 해당 이벤트는 Debug 실행본에 대한 것이다. 독립 실행본 `artifacts/win-x64/GlassDock.exe`의 실행 실패 증거로 볼 수 없다.

독립 실행본의 진단 로그를 `%LOCALAPPDATA%\GlassDock\startup.log`에 기록하도록 추가했다. 2026-11-05 재실행 결과, 단일 실행 잠금을 새로 얻었고, 트레이 아이콘 및 독 창을 만들었다. `Show()`와 `ContentRendered`까지 완료했고 `visible=True`였다. HWND 존재·가시 상태도 확인했다. 실제 창 사각형은 `(759, 8)`에서 `(1160, 108)`로 401×100 픽셀이며 화면 위쪽에 놓였다. `MainWindowHandle=0`은 `ShowInTaskbar=false`인 독 설정과 일치하므로 그 값만으로 실패라고 판단할 수 없다.

중복 실행 때 조용히 종료하던 동작을 바꿔, 다른 인스턴스가 잠금을 소유하면 숨겨진 알림 영역 아이콘을 확인하라는 안내 창을 보여준다. 현재 진단 실행본은 PID 25264로 실행 중이다. Windows 창 가시 상태는 OS API로 확인했지만 화면 캡처로 실제 렌더링을 육안 대조하지는 못했다.

## 독 표면 색 보정

사용자가 실행을 확인한 후 독 표면이 흰색에 가까운 회색으로 보인다고 보고해 색을 보정했다. 승인 v14의 `#dce6f077` 푸른 회색 반투명 표면을 기준으로, 배경 효과가 실패해도 거의 불투명한 흰색으로 바뀌던 경로를 제거했다. 기본 그라데이션은 반투명 푸른 회색에서 연보라색으로 이어지며, 배경 투명도와 테두리 흰색 농도도 낮췄다.

Release 빌드 및 win-x64 self-contained publish가 성공했다. PID 8000에서 Windows 11 Desktop Acrylic 지원 및 적용이 모두 `True`, 독 창의 `visible=True`, 401×100 크기를 확인했다. 사용자는 색상이 더 진해졌다고 피드백했고, 그라데이션을 더 옅게 조정했다.

독 표면의 50% 투명도 요청에 따라 모든 배경 지점의 ARGB 알파를 128(50% 불투명도)로 맞췄다. 마지막 색상 정지점은 푸른 회색을 유지하면서 연보라색 채도를 낮췄다. 아이콘과 글자는 계속 완전한 불투명도로 그린다. Release 빌드와 self-contained publish에 성공했고, 새 PID 26704가 단일 실행 잠금을 얻어 창 표시·ContentRendered까지 정상 도달했다. 실행 파일은 `artifacts/win-x64/GlassDock.exe`다.

사용자가 배경색 제거를 요청해 후속 보정했다. 상자의 배경 브러시를 완전 투명으로 바꾸고 Windows Acrylic 시스템 배경을 끄며, 테두리만 아주 옅게 남겼다. WPF per-pixel 투명 레이어를 사용하고 아이콘 콘텐츠는 불투명하게 둔다. Release 빌드·publish가 성공했고 PID 23972에서 창 표시와 `ContentRendered`를 확인했다. 새 실행 파일은 같은 `artifacts/win-x64/GlassDock.exe` 경로에 있다.

앱 선택에 따라 아이콘이 이동한다는 보고를 받아 정렬을 수정했다. 원인은 창 목록이 전경 창을 우선 열거하고 독이 그 순서를 갱신마다 사용하던 점이다. 앱이 처음 발견됐을 때 순서를 설정에 저장하고 후속 새로 고침은 이 순서를 사용한다. 드래그로 사용자가 정한 순서도 동일 설정으로 유지한다. Release 빌드와 publish 성공 후 PID 23572로 다시 실행했으며, 현재 실제 앱 경로 세 개가 순서대로 설정 파일에 저장된 것을 확인했다.

AI 패널을 열었을 때만 독 배경을 불투명 흰색으로 채우고, 접으면 완전 투명 배경을 복원하도록 수정했다. 시작 때는 AI 패널이 접힌 상태라 독 배경이 계속 투명하다.

AI 패널 모션이 끊겨 보인다는 보고에 따라 패널 높이 애니메이션을 제거했다. 기존에는 매 프레임 `SizeToContent` 레이아웃과 독 위치 계산이 반복됐다. 새 전환은 한 번만 레이아웃·배치한 뒤 패널 투명도/ScaleTransform과 흰 배경의 불투명도를 보간한다. (이전 설정: 전개 420ms, 접기 360ms, Cubic Ease Out.)

사용자가 빠른 느낌보다 부드러운 전환을 원해 Sine Ease In/Out으로 변경하고, 미세 확대 폭을 8%로 조정했다. 전개를 700ms, 접기를 600ms로 늘렸다. Release 빌드 및 self-contained publish 성공.

## 검토

- 구현 브리프, 0.2 UI 기록, 최신 0.2.1 전체 창 보기 수정 기록, Windows 검증 절차 및 AI 검토 문서를 읽었다.
- Copilot 수정본 `ClickPolicy.cs`: 빈 앱 이름과 null 창 목록 방어, 앱 이름 Trim 및 대소문자 무시 비교를 확인했다. 클릭 연속 상태는 호출부의 4초 제한과 문맥 변경 시 Reset에 의존한다. 이번에는 이 파일을 변경하지 않았다.
- Copilot 수정본 `OverviewLayout.cs`: 유한한 양수 크기 검사, 타일 간격과 제목 영역 확보, 썸네일 비율 보존을 확인했다. 배치가 불가능할 만큼 좁은 영역에서는 빈 결과를 반환한다. 이번에는 이 파일을 변경하지 않았다. 이때의 UI 처리와 많은 창에서의 사용성은 후속 실기기 확인 대상이다.
- `tests/Program.cs`의 레이아웃 검사 수 출력이 실제 실행한 검사보다 2개 적었다. 빈 공간·NaN 크기·음수 개수 검사를 각각 집계해 30개로 바로잡았다. 검사 조건과 제품 동작은 변경하지 않았다.
- 프로젝트 루트에 `.git`이 없고 Git 조회는 상위 경로 접근 오류로 실패했다. 커밋 기반 변경 이력 및 Copilot 수정 전 원본과의 정확한 diff는 확인하지 못했다. 로컬 백업에는 두 핵심 파일의 이전 사본이 없다.

## 실행 결과

환경: Windows x64, Windows PowerShell 5.1, 사용 SDK 10.0.403. 프로젝트 대상은 .NET 8이며 .NET 8 SDK와 Windows Desktop 런타임도 설치되어 있다.

```powershell
dotnet run --project tests/PolicyTests.csproj -c Release
dotnet build src/Dock/Dock.csproj -c Release
dotnet publish src/Dock/Dock.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/win-x64
```

- 테스트: 클릭 정책 **15개**, 전체 창 배치·비율 **30개** 통과.
- WPF Release 빌드: 성공, 경고 **0개**, 오류 **0개**.
- win-x64 self-contained 단일 파일 publish: 성공, 종료 코드 0. `artifacts/win-x64/GlassDock.exe` 제품 버전 **0.2.1**, 크기 161,790,038바이트.
- `build.ps1` 직접 실행은 PC의 실행 정책으로 차단됐다. 실행 정책을 변경하지 않고 위 명령을 직접 실행했다.
- 최초 publish는 제한된 네트워크에서 NuGet 복원 실패(NU1301). 권한 승인 후 재실행해 복원 및 publish를 완료했다.

실행 파일을 띄워 UI, 실제 창 조작, DWM, 가상 데스크톱, 혼합 DPI 또는 성능을 검증한 결과는 아니다. 기존 `WINDOWS_VALIDATION_KO.md`의 실기기 확인 항목은 계속 남아 있다.

## AI 목표 유지

목표는 기존 구독 서비스의 실제 기존 대화를 독 안에서 이어가는 것이다. 현재 코드는 서비스 이름 등록과 로컬 초안 저장만 제공하며, 대화 선택·새 대화·전송은 비활성화되어 있다. 실제 연결 성공을 표시하지 않고 API 과금 방식으로 전환하지 않았다. 이번 검토에서 서비스별 공식 연동 가능성을 새로 조사하거나 로그인·전송을 검증하지 않았다.

## 2026-11-05 AI 바로가기 변경

AI 채팅 패널 및 독 내 입력 영역을 제거했다. AI 아이콘은 기본적으로 ChatGPT 앱을 열며, 우클릭 메뉴에서 ChatGPT/Claude를 선택하거나 사용자 지정 `.exe`를 설정한다. 선택은 `%LOCALAPPDATA%\GlassDock\settings.json`의 `SelectedService`와 `AiLaunchPath`에 저장한다. Windows 시작 메뉴 앱 ID를 통해 앱 실행을 요청한다. 앱이 없을 때는 웹으로 대신 열지 않는다. 대화 연동이나 API 요청은 하지 않는다.
## 2026-11-05 Glassmorphism 표면

독의 완전 투명 배경을 푸른 흰색 반투명 수직 그라데이션으로 교체했다. 테두리 광택, 상단 반사 하이라이트, 소프트 섀도우를 더했다. Release 빌드와 win-x64 self-contained 게시를 완료했고 실행 프로세스를 확인했다.
DWM Acrylic(Desktop Acrylic, DWMWA_SYSTEMBACKDROP_TYPE=TRANSIENTWINDOW) 적용을 요청했고 이 PC에서 HRESULT 0(S_OK)을 확인했다. 앱은 반투명 WPF 그라데이션, 이중 유리 테두리, 상단 스펙큘러 하이라이트, 소프트 섀도우를 함께 사용한다.

## 2026-11-05 네이티브 유리 재질 점검

이전 구현은 `AllowsTransparency=true`인 레이어드 WPF 창 위에서 DWM Acrylic을 요청했으므로 요청 성공 코드만으로 실제 뒤 화면 블러가 표시된다고 볼 수 없었다. Windows 11에서는 `AllowsTransparency=false`, `WindowChrome`, 투명 WPF 렌더 타깃, `DwmExtendFrameIntoClientArea`, Desktop Acrylic을 사용하고 `SetWindowRgn`으로 독 캡슐의 네이티브 창 모서리를 둥글게 잘라 사각 Acrylic 누출을 막는다. DPI 변경과 콘텐츠 크기 변경 때만 region을 갱신한다.

배경값은 `GlassTheme.cs` 한 곳에 모았다. Acrylic 본체 색상은 최대 alpha 20/255로 낮게 두고, 가장자리 안쪽 음영·가변 밝기 외곽선·윗면/왼쪽/아랫면 반사광을 분리했다. 장식 Grid는 `IsHitTestVisible=false`이며 기존 콘텐츠를 위에 얹는다. 바깥 그림자는 아이콘까지 효과가 번지지 않도록 창 본체 `Effect`를 쓰지 않고 DWM의 창 그림자를 사용한다. Acrylic API 호출 HRESULT(frame/corner/material)는 모두 0이며, 현재 Windows 11 Pro build 26200에서 투명 효과 설정은 켜져 있다. DWM 호출 성공은 compositor가 재질 요청을 수락했다는 뜻이며 픽셀 단위 화면 검증과 같지는 않다.

검증: `dotnet run --project tests/PolicyTests.csproj -c Release` — 클릭 정책 15개, 개요 배치/비율 30개 통과. `dotnet build src/Dock/Dock.csproj -c Release` — 경고/오류 0. win-x64 self-contained 게시 성공, 앱 프로세스 실행 확인. CUA 창 목록이 GlassDock의 비활성/작업 표시줄 숨김 HWND를 반환하지 않아 어두운 창·흰 문서·색상 배경 위의 실제 화면 비교 및 마우스 상호작용은 이 세션에서 관찰하지 못했다.