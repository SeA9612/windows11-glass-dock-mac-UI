# Windows 휴지통과 앱·도구 혼합 배치

## 최신 정정 — 앱과 도구의 구역 유지

현재 실행본은 `artifacts/sectioned-recycle-dock-win-x64/GlassDock.exe`이다. 아래 본문은 구분선을 제거했던 이전 작업 기록이며, 구역에 대해서는 이 정정이 우선한다.

- `[앱 구역 │ 앱·전체 창·작업공간 구역 │ 휴지통]` 구조로 앱/도구 구분선을 복원했다. 앱은 양쪽 구역 모두에 배치할 수 있고, 오른쪽 도구 구역의 맨 앞/도구 사이/맨 뒤도 지원한다.
- `DockEntry.ToolBoundary()`는 `Kind=separator, Id=apps-tools`이다. 경계 앞/뒤로 앱이 어느 구역에 속하는지 저장한다. 구분선과 휴지통은 드래그 대상이 아니다.
- LayoutVersion 2에서 기존 버전 1의 항목 순서를 보존하고 첫 도구 앞에 경계를 추가한다. 신규 앱은 경계 앞의 왼쪽 구역에 추가한다. 오른쪽으로 옮겨 저장한 앱은 재실행 시에도 오른쪽에 남는다.
- 구분선 1 DIP와 추가 간격 8 DIP를 복원했으므로 이전 길이 계산에 9 DIP를 더한다. 프로그램 3개 기준 가로 284×52 / 세로 52×284 DIP이다.
- 순서 모델 포함 검증 63개와 실제 Windows 테스트 창 검증 255개 통과. 새 창에서 경계와 오른쪽 앱 배치를 육안 확인했다. 실제 OS 마우스 드래그는 앞서 기록한 화면 자동화 연결 제한으로 미확인이다.

## 이전 작업 기록

최신 소스는 현재 프로젝트 폴더, 실행본은 `artifacts/recycle-mixed-order-win-x64/GlassDock.exe`이다.

## 이동 제한의 원인과 변경

이전 코드는 앱 버튼에만 Drop 처리를 붙였으며 `settings.Order`에 앱 경로만 저장했다. 화면을 갱신할 때 앱들 뒤에 구분선과 전체 창/작업 보기 버튼을 항상 덧붙였다. 따라서 앱을 도구 사이에 저장하거나 도구 위/간격에 드롭할 수 없었다.

이제 하나의 이동 영역에 앱과 도구를 함께 배치한다. `DockEntry`는 `Kind=app/tool`과 `Id`를 갖는다. 앱 Id는 기존 정확한 실행 파일/AppsFolder 식별자, 도구 Id는 `overview`, `task-view`이다. 기존 앱·도구 사이 구분선은 제거하고 휴지통 앞의 구분선만 유지한다.

- 이동 영역 전체와 두 도구 버튼을 드롭 대상으로 등록했다. 아이콘 중심의 앞/뒤 절반과 사이 간격으로 삽입 위치를 계산하고, 파란 삽입 선을 표시한다.
- 앱을 전체 창 보기 앞, 두 도구 사이, 작업 보기 뒤, 휴지통 바로 앞에 둘 수 있다. 휴지통과 그 구분선 안쪽은 드롭 영역에서 제외한다.
- 드래그는 해당 앱에서 마우스를 누른 뒤 시스템 임계값(최소 6 DIP) 이상 움직였을 때만 시작한다. 드래그 후 클릭으로 앱이 실행되는 것을 방지한다.
- 드래그 동안 UI 갱신을 보류하고 버튼 인스턴스를 유지한다. 드롭/취소 후 최신 OS 목록을 반영한다. 파일 드롭 데이터는 받지 않으며 어떤 드롭 경로에도 삭제 호출이 없다.

## 저장 형식과 기존 설정

`LayoutVersion=1`, `LayoutOrder`를 추가했다. 처음 실행할 때 기존 `Order`의 앱 순서를 보존하고 두 도구를 뒤에 붙여 이관한다. 이후 혼합 순서는 `LayoutOrder`가 기준이고, 기존 `Order`에도 앱 순서를 따로 남겨 이전 정보와 호환한다.

닫힌 앱의 항목은 저장 목록에서 지우지 않으므로 다시 실행하면 도구 사이의 원래 위치로 돌아간다. 처음 보는 앱은 첫 도구 앞에 추가하며 기존 혼합 배치를 초기화하지 않는다. 드래그한 앱은 기존과 같이 고정한다. 고정 목록, 기존 설정 필드, 인식하지 못하는 추가 JSON 필드도 보존한다.

사용자 설정 이관 전 사본은 `artifacts/recycle-dock-validation/settings-before-migration.json`에 보관했다. 테스트는 별도 설정 객체/JSON 파일을 사용하며 사용자 설정을 저장하지 않았다.

## 실제 Windows 휴지통

- 휴지통은 이동 영역과 별도의 마지막 고정 구역이며 전체 유리 배경 안에 포함된다. 가로에서는 오른쪽 세로선 뒤, 세로에서는 아래쪽 가로선 뒤에 배치한다. 별도 장식 타일은 없다.
- 클릭/열기 메뉴는 `explorer.exe shell:RecycleBinFolder`로 실제 Windows 휴지통을 연다.
- `SHQueryRecycleBinW(NULL)`로 실제 상태를 약 2초마다 및 메뉴를 열 때 조회한다. 빈/찬 아이콘은 `SHGetStockIconInfo`의 SIID_RECYCLER/SIID_RECYCLERFULL과 `SHDefExtractIconW`로 요청 DPI에 맞춰 가져와 캐시한다. HICON은 DestroyIcon으로 해제한다.
- 조회 불가 시 물음표가 있는 중립 아이콘과 '상태 확인 불가'를 표시한다. 빈 상태나 조회 불가 상태에서는 비우기 메뉴를 비활성화한다.
- '휴지통 비우기' 메뉴를 직접 선택한 경우에만 상태를 다시 조회하고, 내용이 있을 때 `SHEmptyRecycleBinW(owner,NULL,0)`을 호출한다. 플래그 0을 사용해 Windows의 삭제 확인/진행 UI를 생략하지 않는다. 시작, 클릭 열기, 앱 드래그에서는 이 API를 호출하지 않는다.
- 임의 폴더를 만들거나 파일을 직접 영구 삭제하는 방식은 사용하지 않는다. 이번 검증에서도 실제 휴지통 비우기는 실행하지 않았다.

## 크기

아이콘은 32 DIP, 간격 8, 실행 점 3, 점과 아이콘 간격 3이다. 손잡이 12+간격 4, 바깥 길이 방향 여백 9씩/두께 방향 7씩을 유지했다.

M을 프로그램 N개+도구 2개+휴지통 1개, 즉 N+3으로 계산한다. 길이는 `18+16+32M+8(M−1)+9`이며 마지막 9는 구분선 1+추가 간격 8이다. 두께는 52 DIP. 프로그램 3개일 때 가로 275×52, 세로 52×275 DIP이다. 프로그램 0개일 때도 휴지통 구분선은 유지된다.

## 수정 파일

- `src/Dock.Core/DockOrder.cs`: 혼합 순서, 기존 순서 이관, 닫힌 앱의 위치 유지, 이동 검증.
- `src/Dock/Settings.cs`: 버전과 혼합 항목 저장, 추가 JSON 설정 보존.
- `src/Dock/DockWindow.cs`: 공통 드롭 처리, 삽입 표시, 고정 휴지통 구역, 상태/메뉴 갱신.
- `src/Dock/WindowsRecycleBin.cs`: 실제 Windows 상태 조회·아이콘·열기·확인 후 비우기 API.
- `tests/Program.cs`: 순서 모델 검증 15개 추가.
- `artifacts/dock-appearance-smoke/Program.cs`: 실제 WPF 창과 라우팅된 드래그 이벤트, 설정 재생성, 삭제 호출 경계 검증.

## 검증 결과와 한계

- Release 빌드 및 self-contained 게시 성공.
- 실행 검증 246개, 기존 클릭 정책 15개/배치 30개/새 순서 모델 15개, 실시간 미리보기 57개 통과: 합계 363개.
- 실제 Windows 테스트 창에서 네 방향 크기와 휴지통 마지막 배치, 혼합 배치, 네이티브 입력 영역, 실제 창 최소화/복원을 확인했다. 테스트 창 캡처도 육안 확인했다.
- 실제 WPF DragOver/Drop 이벤트를 전달하여 간격 삽입, 표시 선, 도구 사이 이동, 드래그 중 OS 목록 변경 보류와 종료 후 반영을 확인했다. 설정 JSON에서 새 독 창을 생성했을 때 도구 사이 배치가 유지됐다.
- 실제 Windows 휴지통 상태 조회와 빈/찬 시스템 아이콘 추출에 성공했다. 실제 휴지통 폴더 열기 호출도 성공했으나 Windows 화면 자동화 native pipe 연결 실패로 열린 탐색기 창을 별도 캡처 확인하지 못했다.
- 빈/찬/조회 불가 상태와 비우기 메뉴 조건, 열기·드래그가 비우기를 호출하지 않는 점은 테스트 대역으로 확인했다. 실제 비우기 및 Windows 확인 창 조작은 실행하지 않았고 사용자 문서·실제 휴지통 내용은 변경하지 않았다.
- Windows 화면 자동화 연결이 없어 실제 마우스로 누르고 끌어 놓는 전체 OS 동선은 미확인이다. 위 라우팅 이벤트 테스트와 구분한다. 물리 화면 배율 변경 검증도 이번 범위에서는 하지 않았다.
- 증거 파일: `artifacts/recycle-dock-validation/appearance-tests.log`, `dock-*-screen.png`, `dock-restarted-screen.png`, `restart-settings.json`.

공식 API 참고: [SHEmptyRecycleBinW](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shemptyrecyclebinw), [Windows stock icon IDs](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/ne-shellapi-shstockiconid), [WPF drag and drop](https://learn.microsoft.com/en-us/dotnet/framework/wpf/advanced/drag-and-drop-overview).
