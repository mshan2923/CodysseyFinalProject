# Codyssey

## 상황을 기억하는 AI NPC 프로젝트

Codyssey는 플레이어의 행동과 게임 월드에서 발생한 사건을 감지하고, 그 상황을 NPC의 다음 대사와 행동에 반영하는 Unity 기반 AI NPC 프로젝트입니다.

플레이어가 물건을 집거나 던지고, NPC를 바라보거나 충돌을 일으키면 게임은 해당 행동을 이벤트로 기록합니다. NPC는 현재 주변 상태와 최근 사건을 함께 전달받아 상황을 해석하고, 그 결과를 자연어 대사와 게임 행동으로 연결합니다.

이 프로젝트의 핵심은 단순히 LLM에게 대사를 생성시키는 것이 아닙니다.

> 플레이어 행동을 감지하고, 사건을 기억하고, 다음 판단에 반영하는 게임 시스템을 만드는 것

---

## 1. 프로젝트 목표

### 기존 NPC의 한계

일반적인 NPC는 미리 작성된 조건문이나 대사 목록을 기반으로 반응합니다.

```text
플레이어가 특정 오브젝트를 바라봄
  → 미리 정해진 대사 출력
```

이 방식은 예측 가능하고 안정적이지만, 플레이어가 어떤 행동을 했는지에 따라 반응이 이어지기 어렵습니다. 물건을 던져 NPC를 맞혔더라도 다음 대화에서 NPC가 아무 일도 없었던 것처럼 행동할 수 있습니다.

### Codyssey의 목표

Codyssey는 플레이어 행동을 다음 대화와 행동의 입력으로 사용합니다.

```text
플레이어 행동
  → 게임 이벤트 기록
  → 최근 상황과 관계 구성
  → LLM의 사건 해석
  → NPC의 대사와 행동
```

상황 기억은 NPC가 모든 과거를 무한히 저장한다는 의미가 아닙니다. 다음 판단에 필요한 최근 경험을 선택해 전달하고, 그 경험이 NPC의 반응에 영향을 주도록 만드는 구조입니다.

---

## 2. 게임 컨셉

플레이어는 실내 공간을 자유롭게 돌아다니며 오브젝트와 상호작용합니다. 고양이 NPC는 주변 상황을 인식하고 플레이어의 행동에 반응합니다.

### 핵심 플레이 경험

- 물건을 집고 놓거나 던질 수 있습니다.
- 플레이어의 시선과 주변 오브젝트를 센서가 감지합니다.
- 충돌, 피격, 점프, 물체 습득과 같은 사건을 기록합니다.
- 고양이 NPC는 최근 사건을 바탕으로 다음 대사를 생성합니다.
- 같은 공간에서도 플레이어의 행동에 따라 NPC의 반응이 달라질 수 있습니다.

### 예시 상황

```text
1. 플레이어가 상자를 집는다.
2. 상자를 고양이 근처에 던진다.
3. 상자와 고양이의 충돌이 World Event로 기록된다.
4. 다음 대화 요청에서 최근 충돌 사건이 Context에 포함된다.
5. 고양이가 놀라거나 경계하는 대사를 생성한다.
```

---

## 3. 핵심 설계 원칙

### Unity가 게임 규칙을 담당

충돌 여부, 체력 변화, 이동 가능 여부, 물체의 물리 동작은 Unity 게임 시스템이 판단합니다. LLM에게 게임 규칙 전체를 맡기지 않습니다.

### LLM은 상황을 해석

LLM은 Unity가 수집한 상태와 사건을 읽고, NPC 관점에서 의미를 해석합니다.

```text
Unity: 상자가 고양이와 충돌했다.
LLM: 고양이가 놀라거나 플레이어를 경계할 수 있다.
```

### Recognition과 Dialogue를 분리

LLM 요청을 두 단계로 나눕니다.

- `Recognition`: 어떤 대상과 사건이 관련되어 있는지 구조화합니다.
- `Dialogue`: 현재 Context와 플레이어 행동을 바탕으로 최종 대사를 생성합니다.

이렇게 분리하면 사건 데이터는 학습과 디버깅에 사용할 수 있고, 대사는 플레이어에게 자연스럽게 표시할 수 있습니다.

### 최신 사건을 대화 직전에 반영

대화 요청이 시작되기 전에 최신 World Context를 다시 구성합니다. 인식 요청이 진행되는 동안 발생한 충돌이나 플레이어 행동도 다음 대화에 포함될 수 있도록 하기 위한 구조입니다.

---

## 4. 시스템 구조

```text
┌──────────────┐
│ Player Action│
│ 이동·점프·잡기 │
│ 던지기·충돌   │
└──────┬───────┘
       │
       ▼
┌────────────────────┐
│ SensingWorldState   │
│ Active Targets      │
│ Vision Events       │
│ World Events        │
└────────┬───────────┘
         │ BuildContext()
         ▼
┌────────────────────┐
│ Unity Behavior      │
│ Recognition 요청    │
│ Dialogue 요청       │
└────────┬───────────┘
         │
         ▼
┌────────────────────┐
│ LLMService          │
│ Ollama              │
│ OpenAI              │
│ Anthropic           │
│ Fallback 처리       │
└────────┬───────────┘
         │
         ▼
┌────────────────────┐
│ NPC Response        │
│ Recognition         │
│ Dialogue            │
│ Animation / Action  │
└────────────────────┘
```

### 주요 흐름

1. 플레이어가 이동하거나 오브젝트와 상호작용합니다.
2. 게임 시스템이 물리·시선·행동 이벤트를 생성합니다.
3. `SensingWorldState`가 현재 대상과 최근 이벤트를 저장합니다.
4. Unity Behavior Graph가 Recognition 또는 Dialogue 요청을 실행합니다.
5. `LlmChatAction`이 필요한 Context를 구성합니다.
6. `LLMService`가 선택된 provider와 모델에 요청합니다.
7. 응답에서 Recognition 또는 Dialogue를 추출합니다.
8. NPC가 대사, 애니메이션, 이동 등으로 반응합니다.

---

## 5. 주요 시스템

### Sensing World

관련 파일:

```text
Assets/Samples/Sensing World/SensingWorldState.cs
Assets/Samples/Sensing World/SensingWorldEvents.cs
Assets/Samples/Sensing World/SensingWorldEventBusHost.cs
Assets/Samples/Sensing World/InteractionSensor.cs
```

주요 책임:

- 현재 감지 대상 관리
- 대상의 거리와 방향 정보 관리
- 최근 Vision Event 관리
- 최근 World Event 관리
- 플레이어가 들고 있는 오브젝트와 관계 관리
- LLM 요청에 사용할 Context 생성

Context는 카메라 이미지를 그대로 전달하는 방식이 아니라, 게임 센서가 수집한 구조화된 텍스트 정보로 구성합니다.

예시:

```text
[Current Sensor State]
고양이는 Player로부터 2.5m 떨어져 있다.

[Recent World Events]
Player가 상자를 Throw했다.
상자가 고양이와 Hit했다.
```

### Unity Behavior

관련 파일:

```text
Assets/Scripts/Action/SensingWorldAction.cs
Assets/Scripts/Action/LlmChatAction.cs
Assets/Scripts/Action/CallWorldEventAction.cs
```

Behavior Graph에서 NPC의 행동 흐름을 구성합니다.

- 센서 Context 요청
- LLM Recognition 요청
- 선택된 대상 파싱
- 월드 이벤트 저장
- LLM Dialogue 요청
- 대화 결과 출력

### LLMService

관련 파일:

```text
Assets/Scripts/LLMService.cs
```

`LLMService`는 Unity와 외부 또는 로컬 LLM provider 사이의 공통 호출 계층입니다.

지원 provider:

- Ollama
- OpenAI
- Anthropic

기본 provider가 실패하면 설정된 fallback provider 또는 fallback model로 재시도할 수 있습니다. Unity Behavior는 provider별 API 형식을 직접 알 필요 없이 공통 `Chat` 흐름을 사용할 수 있습니다.

### DialogueController

관련 파일:

```text
Assets/Scripts/DialogueController.cs
```

LLM이 생성한 Dialogue 결과를 게임 UI에 표시합니다. NPC 대사를 고정 문자열로 대체하지 않고, LLM 응답을 파싱해 표시하는 구조를 유지합니다.

### 물리와 상호작용

관련 파일:

```text
Assets/Scripts/Grab/GravityGrabber.cs
Assets/Scripts/Grab/GrabbableObject.cs
Assets/Scripts/PlayerController.cs
```

플레이어는 오브젝트를 집고, 놓고, 던질 수 있습니다. 이 동작은 단순한 물리 효과에 그치지 않고 Sensing World Event로 전달되어 NPC 반응의 입력이 됩니다.

### 전투와 체력

관련 파일:

```text
Assets/Scripts/Gameplay/Entities/GameEntity.cs
Assets/Scripts/Gameplay/Systems/CombatSystem.cs
Assets/Scripts/Gameplay/Systems/CombatEvents.cs
Assets/Scripts/Presentation/UI/HealthUI.cs
```

체력 감소와 전투 요청을 게임 이벤트 흐름에 연결합니다. 전투 결과는 UI와 VFX에 반영될 수 있으며, 필요할 경우 NPC Context의 사건으로도 확장할 수 있습니다.

### NavMesh와 Jump-Up Link

관련 파일:

```text
Assets/Editor/JumpUpLinkBakeProcessor.cs
Assets/Scripts/JumpUpLinkTraversal.cs
```

NavMeshAgent 기반 NPC가 높이 차이가 있는 공간을 이동할 수 있도록 Jump-Up Link를 생성하고, 링크를 통과할 때 점프 애니메이션과 이동을 연결합니다.

---

## 6. LLM과 머신러닝의 역할

### LLM

LLM은 현재 Context와 최근 사건을 읽고 자연어 의미를 해석합니다.

```text
입력:
최근 World Event: 상자가 고양이와 충돌함

출력:
Recognition: 고양이가 상자에 맞음
Dialogue: 방금 그 상자... 일부러 던진 거야?
```

### 머신러닝

게임에서 생성한 Recognition 데이터를 학습 데이터로 활용해 특정 게임 도메인에 맞는 사건 표현과 반응 패턴을 학습합니다.

현재 파인튜닝 작업은 `Assets/GemmaFineTune/`에서 관리합니다. 상세한 학습·변환 명령은 해당 폴더의 README를 참고합니다.

```text
게임 플레이
  → Context / Recognition 수집
  → 데이터셋 생성
  → Gemma 4 E2B QLoRA 학습
  → GGUF 변환
  → Ollama 모델 등록
  → Unity에서 사용
```

LLM은 상황을 해석하고, 머신러닝은 누적된 프로젝트 데이터에 모델을 적응시키는 역할을 담당합니다.

---

## 7. 실행 방법

### Unity 프로젝트 열기

1. Unity Hub에서 `CodysseyFinalProject` 폴더를 엽니다.
2. 프로젝트에 맞는 Unity Editor 버전으로 로드합니다.
3. 필요한 패키지가 정상적으로 설치되었는지 확인합니다.

주요 패키지:

- Universal Render Pipeline `17.3.0`
- Unity Behavior `1.0.16`
- AI Navigation `2.0.14`
- Input System `1.20.0`
- Timeline `1.8.13`
- UGUI `2.0.0`

### 권장 테스트 씬

```text
Assets/Scenes/CatGameRoom.unity
Assets/Samples/HouseSample Scene.unity
```

정확한 테스트 대상은 현재 작업 중인 Behavior Graph와 NPC 구성에 따라 달라질 수 있습니다.

### Play Mode 테스트 순서

1. 테스트 씬을 엽니다.
2. `LLMService`가 씬에 존재하는지 확인합니다.
3. Ollama 또는 선택한 provider가 실행 중인지 확인합니다.
4. Play Mode를 시작합니다.
5. 플레이어가 오브젝트를 집거나 던집니다.
6. NPC 주변에서 충돌이나 시선 이벤트를 발생시킵니다.
7. Unity Console에서 Sensing, Recognition, Dialogue 로그를 확인합니다.
8. 다음 대화가 방금 발생한 사건을 반영하는지 확인합니다.

---

## 8. Provider 설정

### Ollama

현재 기본 로컬 모델 이름은 다음과 같습니다.

```text
codyssey-cat-q4-test512:latest
```
[[GGUF 파일 다운로드 링크](https://mega.nz/file/mdME0QxB#U0BYvN0CaRiRxt3nLlZ4bq1O7CxTgOubd5QcukvV4PA)]

fallback 모델 예시는 다음과 같습니다.

```text
gemma4:e2b
```

Ollama는 로컬에서 실행할 수 있어 반복적인 Play Mode 테스트에 적합합니다.

### OpenAI와 Anthropic

API provider를 사용할 경우 프로젝트 루트의 `.env` 파일에 키를 설정합니다.

```text
OPENAI_API_KEY=your_key_here
ANTHROPIC_API_KEY=your_key_here
```

프로젝트에는 `.env.example` 파일이 있으며, 실제 `.env`는 직접 생성해야 합니다.

실제 API 키는 다음 위치에 저장하거나 커밋하지 않습니다.

- Git 저장소
- Unity Inspector
- 소스 코드
- 공개된 빌드 파일

배포 환경에서는 클라이언트에 API 키를 포함하지 않고 서버 proxy를 사용하는 것을 권장합니다.

---

## 9. 발표 및 시연 시나리오

프로젝트의 핵심을 보여주는 가장 짧은 시연 순서는 다음과 같습니다.

### 장면 1. 기억이 없는 상태

고양이 NPC가 플레이어를 처음 만난 상태에서 기본 대화를 실행합니다.

### 장면 2. 사건 발생

플레이어가 물건을 집어 고양이 근처에 던집니다. 물건과 고양이가 충돌하고, 해당 사건이 World Event로 기록됩니다.

### 장면 3. 기억이 반영된 대화

같은 NPC에게 다시 대화를 요청합니다. 고양이가 방금 발생한 충돌이나 플레이어의 행동을 반영하는지 확인합니다.

### 장면 4. 다른 행동과 비교

다른 오브젝트를 사용하거나 행동 순서를 바꾸고, NPC의 반응이 달라지는지 비교합니다.

시연에서 가장 중요한 확인 포인트는 API 요청이 성공했는지가 아니라, 플레이어 행동 직후의 다음 반응이 실제 사건을 기억하고 있는지입니다.

---

## 10. 현재 한계

### 장기 기억이 아닌 단기 기억 중심

현재 시스템은 최근 이벤트와 현재 상태를 중심으로 Context를 구성합니다. 오래된 사건이나 장기적인 관계를 자동으로 영구 보존하는 메모리 시스템은 아직 제한적입니다.

### 경량 모델의 복잡한 지시 이해 한계

Gemma E2B는 가볍고 빠른 추론에 적합하지만, 긴 지시와 많은 사건을 한 번에 전달하면 이해력이 떨어질 수 있습니다. 따라서 짧은 지시, 필요한 사건만 포함한 Context, Recognition과 Dialogue의 분리 전략을 사용합니다.

### 도메인 확장에 필요한 추가 학습

현재 모델과 데이터는 Codyssey의 고양이 NPC와 게임 이벤트에 맞춰져 있습니다. 교육, 돌봄, 로봇, 시뮬레이션 등 다른 분야에 적용하려면 해당 도메인의 사건과 반응을 담은 추가 데이터가 필요합니다.

---

## 11. 향후 확장

- 중요한 사건을 자동으로 선별하는 기억 우선순위 정책
- 오래된 사건을 요약하고 필요할 때 다시 불러오는 장기 기억
- 플레이어와 NPC 사이의 신뢰도와 관계 상태
- 감정 상태와 성격에 따른 반응 변화
- 새로운 도메인별 Recognition 데이터셋
- 여러 NPC가 같은 사건을 서로 다르게 해석하는 상호작용
- 서버 proxy 기반의 안전한 API provider 운영

핵심 방향은 기억을 많이 저장하는 것이 아니라, 다음 판단에 필요한 경험을 남기는 것입니다.

---

## 12. 프로젝트 폴더 안내

```text
Assets/
├─ Scripts/
│  ├─ LLMService.cs                 # LLM provider 호출과 fallback
│  ├─ DialogueController.cs         # NPC 대사 표시
│  ├─ Action/                       # Behavior Graph 커스텀 액션
│  ├─ Behavior/                     # 센서와 NPC 등록
│  ├─ Gameplay/                     # 전투와 엔티티 시스템
│  ├─ Grab/                         # 물리 기반 잡기와 던지기
│  └─ Presentation/                 # UI, 오디오, VFX
├─ Samples/
│  ├─ Cat/                          # 고양이 NPC 샘플
│  └─ Sensing World/                # 센서와 이벤트 시스템
├─ Scenes/                          # 프로젝트 테스트 씬
├─ Editor/                          # NavMesh 링크 생성 도구
├─ GemmaFineTune/                   # Gemma QLoRA 작업 폴더
├─ Model/                            # 캐릭터와 모델 리소스
└─ Readme.asset                     # Unity Editor용 프로젝트 안내
```

---

## 13. 개발 시 주의사항

- LLM 응답을 고정 대사로 대체하지 않습니다.
- 물리 판정과 게임 규칙은 Unity 시스템이 담당합니다.
- LLM은 이벤트 해석과 자연어 반응 생성에 사용합니다.
- 대화 요청 직전에 최신 Context를 다시 구성합니다.
- API 키와 `.env` 파일은 저장소에 커밋하지 않습니다.
- 코드가 컴파일되는 것만으로 기능이 완성되었다고 판단하지 않습니다.
- NPC의 다음 반응, 이벤트 소비, provider fallback은 Play Mode에서 확인합니다.
- NavMesh 링크 생성 기능은 Scene View에서 실제 배치를 확인합니다.

---

## License and Third-Party Assets

프로젝트에는 Unity 패키지와 외부 에셋이 포함되어 있습니다. 각 패키지와 에셋의 라이선스 및 사용 조건을 확인한 뒤 배포합니다.
