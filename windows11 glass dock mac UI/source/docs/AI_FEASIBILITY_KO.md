## 2026-11-05 제품 동작 변경

AI 채팅 패널을 제거하고 AI 아이콘을 바로가기 런처로 전환했다. 기본 대상은 ChatGPT이며, 아이콘 우클릭 메뉴에서 Claude를 선택하거나 사용자 지정 `.exe` 앱을 고를 수 있다. ChatGPT/Claude는 Windows 시작 메뉴의 앱 등록 항목으로 실행한다. 앱을 찾지 못하면 웹으로 전환하지 않고 설치 또는 사용자 지정 실행 파일 지정을 안내한다. 기존 대화/로그인과 대화 재개는 해당 AI 앱이 담당하며 GlassDock은 대화 기록을 읽거나 요청을 전송하지 않는다. 별도 API 과금 경로도 추가하지 않았다.
# 기존 구독 대화 연동 검토

검토일: 2026-11-05. 공개 공식 문서 조회 결과이며 사용자 계정 로그인/전송 실험 결과는 아니다. 이 클라우드에 사용자 서비스 계정 세션은 없다. 현재 프로그램에 인증 SDK, 브라우저 로그인, HTTP AI 전송 구현을 포함하지 않았다.

## 기능별 판단

| 요구 | ChatGPT | Claude |
|---|---|---|
| 공식 서비스 자체의 사용자 로그인 | 가능 | 가능 |
| 이 개인 독에 구독 로그인 제공 | 참여 앱의 Sign in with ChatGPT 지원이 존재하나 이 독의 참여 자격·등록·권한 미확인 | 제3자 앱의 Claude.ai 로그인/구독 자격증명 중계는 공식 문서에서 제한 |
| 일반 개인 계정의 기존 대화 목록·본문 조회 | 검토 문서에서 해당 공식 권한을 확인하지 못함. 구독 로그인만으로 기록 접근 불가 | 개인 구독용 경로 확인 못함. Enterprise Compliance 조회는 조직 관리자용이며 개인 로그인 경로가 아님 |
| 기존 웹 대화 ID에 메시지 추가 | 공식 지원 경로 확인 못함 | 공식 지원 경로 확인 못함 |
| 독에서 서비스의 새 대화 생성 | 목표 구독/기록 경로 확인 못함 | 목표 구독/기록 경로 확인 못함 |
| 서비스 UI를 독 안에 WebView로 표시 | 로그인 허용·정책·기존 기록 접근·패널 내 정상 동작 모두 미확인, 실험 안 함 | 동일하게 미확인, 실험 안 함 |
| 로컬 서비스 선택 및 초안 유지 | 구현, 네트워크 전송 없음 | 구현, 네트워크 전송 없음 |

## OpenAI 공식 경로 재확인 — 2026-11-05

OpenAI Developers는 오픈소스 및 개인 로컬 앱을 위한 Sign in with ChatGPT 계획 사용 경로를 문서화했다. 사용자가 별도로 승인하면 OAuth PKCE로 연결하고, 적격 계정은 ChatGPT 구독에 포함된 사용량으로 앱의 Responses API 요청을 수행할 수 있다. 별도 API 키/결제는 요구하지 않지만, 사용량은 해당 ChatGPT 계획의 한도와 사용량에 반영되며 지원 자격·모델은 계정과 연결 승인에 따른다. 연결·사용량 권한은 사용자가 ChatGPT에서 관리한다. ([Quickstart](https://developers.openai.com/siwc/quickstart), [오픈소스 앱 통합 안내](https://developers.openai.com/cookbook/articles/sign-in-with-chatgpt))

이 기능은 **GlassDock 앱 전용 새 요청/대화**를 실행하는 방법이다. OpenAI 문서는 Sign in with ChatGPT나 `chatgpt.tokens.use.direct` 권한이 사용자의 기존 ChatGPT 대화에 접근하지 않는다고 명시한다. 따라서 이것을 기존 ChatGPT 대화를 이어 쓰는 기능으로 표시하면 안 된다. ([권한 설명](https://developers.openai.com/siwc/quickstart#understand-scopes), [등록 및 로그인](https://developers.openai.com/siwc/token-sharing-open-source/sign-in))

Codex App Server는 ChatGPT 계획 토큰을 사용하고 앱 전용 Codex 스레드를 시작·재개하는 별도 공식 선택지다. 여기서의 스레드 기록은 일반 ChatGPT 대화 기록이 아니다. ([Codex App Server](https://learn.chatgpt.com/docs/app-server), [ChatGPT 계획 사용 시 App Server 연결](https://developers.openai.com/siwc/token-sharing-open-source/codex-app-server))

결론: 현재 목표인 **독 안에서 실제 기존 ChatGPT 대화를 이어 쓰기**는 확인된 공식 경로로 구현할 수 없다. 공식 구독 사용량을 GlassDock 전용 새 대화에 쓰는 구현은 가능하지만, 이는 제품의 대화 흐름을 변경한다. 사용자가 그 변경을 허용하기 전까지 기존 대화 선택과 전송은 비활성 상태로 둔다. 세션 토큰·쿠키를 추출하거나 비공개 ChatGPT 웹 API를 호출하는 방식은 사용하지 않는다.

‘공식 경로 확인 못함’은 모든 미래 제품이나 개별 계약에서 절대 불가능하다는 뜻이 아니다. 현재 증거로 이 제품의 목표 흐름을 작동한다고 표시할 수 없다는 뜻이다.

## 근거와 해석

- [OpenAI: Using your ChatGPT plan in other apps and sites](https://help.openai.com/en/articles/20001542-using-your-chatgpt-plan-in-other-apps-and-sites): 참여 앱의 구독 사용 경로가 있지만 구독 사용만으로 ChatGPT 대화나 메모리 접근 권한을 주지 않는다. 따라서 ‘구독 연동은 모두 불가능’이라고 단정하지 않는다.
- [OpenAI: Managing billing](https://help.openai.com/en/articles/9039756-managing-billing-for-chatgpt-and-the-api-platform): API와 ChatGPT 결제는 구분된다. API 호출을 사용자의 기존 웹 대화로 간주하지 않는다. 일부 요금제의 API 크레딧 혜택도 기존 대화 ID 연동과는 별개다.
- [OpenAI: Codex App Server](https://learn.chatgpt.com/docs/app-server): 자체 UI에서 Codex 세션을 다루는 공식 후보는 있으나 일반 ChatGPT 웹 대화 기록과 동일하다고 볼 근거가 없다. 사용자 동의 없이 Codex 전용 대화로 제품을 변경하지 않았다.
- [Anthropic: Legal and compliance](https://code.claude.com/docs/en/legal-and-compliance): 제3자 개발자의 Claude.ai 로그인 제공, 구독 자격증명 요청 중계, 자격증명·세션 토큰 수집에 관한 제한이 명시되어 있다. 원본 Claude Code의 사용자 로그인 허용을 기존 Claude.ai 웹 대화 제어 권한으로 해석하지 않는다.
- [Anthropic: API overview](https://platform.claude.com/docs/en/api/overview): 모델 API 인증과 기능은 일반 웹 대화 제어가 아니다.
- [Anthropic: Compliance API setup](https://platform.claude.com/docs/en/manage-claude/compliance-api-access), [List chats](https://platform.claude.com/docs/en/api/http/compliance/apps/chats/list): 조직 관리자용 조회/삭제 기능은 개인 구독 채팅 전송 경로가 아니다.

## 현재 결정과 다음 검증

제품 목표는 ‘독 안에서 기존 대화 이어쓰기’ 그대로 유지한다. 프로그램의 AI/대화 선택 구조는 남기고 기존 대화, 새 대화, 전송은 비활성화한다. 서비스 추가는 이름 등록이며 로그인 성공이 아니다. 실제 대화 목록, 대화 ID, 응답을 생성하거나 연결한 것처럼 표시하지 않는다. 초안은 로컬 설정에 저장하며 서비스로 전송되지 않는다.

다음 단계는 공식 제공자에게 일반 개인 대화의 조회/추가 권한과 임베딩 지원 여부를 확인하거나 허용된 사용자 로그인 실험을 수행하는 것이다. WebView는 단지 화면 컨테이너이므로 그 자체로 인증·연동 가능성을 증명하지 않는다. 외부 앱 열기, Codex 전용 세션, API 기반 새 대화는 서로 다른 대안이며 승인 없이 적용하지 않는다. 토큰 추출, 세션 복사, 쿠키 가져오기, 비공개 엔드포인트 우회는 구현하지 않는다.
