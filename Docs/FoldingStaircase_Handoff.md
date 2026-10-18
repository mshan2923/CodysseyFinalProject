# CatGameRoom 자동 접이식 계단 인수인계

## 작업 범위

- 대상 씬: `Assets/Scenes/CatGameRoom.unity`
- 기존 `L Staircase`를 백업한 뒤 `Folding Staircase`로 교체했습니다.
- 계단 이외의 1·2층 가구, 소품, 벽, 바닥, 커튼과 게임 시스템은 이동하거나 삭제하지 않았습니다.

## 구현 구조

- `Wall Mount and Storage Frame`
- `Upper Folding Pivot` / `Lower Folding Pivot`
- 상·하단 접근 감지 트리거
- 계단 전체 점유 및 접힘 경로 감지 트리거
- 이동용 경사 Collider 2개
- 상·하단 Landing

고양이가 1층 또는 2층 접근 영역에 들어오면 계단이 전개됩니다. 고양이가 계단 점유 영역에 있으면 접힘이 금지되고, 이용 후 3초 동안 접근·점유가 없으면 1.4초에 걸쳐 수납됩니다. 전개 또는 수납 중에는 이동용 Collider를 비활성화하며, 완전히 전개된 뒤에만 통행 Collider가 활성화됩니다.

## 주요 파일

- `Assets/Scripts/Environment/FoldingStaircaseController.cs`
- `Assets/Scripts/Environment/FoldingStaircaseSensor.cs`
- `Assets/StaircaseBackups/L Staircase_PreFolding_20261018.prefab`
- `Assets/Scenes/CatGameRoom_PreFoldingStaircase_20261018.unity`

## 검증 결과

- Unity 6.3 LTS에서 실제 프로젝트 스크립트 컴파일 및 에셋 임포트 성공
- 검증용 프로젝트에서 계단 외 오브젝트 785개의 Transform 및 활성 상태가 동일함을 확인
- 이동 경사각 `44.50°`로 고양이 `CharacterController`의 `45°` 제한 이내
- 전개 상태 경로의 네 지점에서 Physics Raycast가 계단 이동 Collider에 닿는 것을 확인
- 1층 가구 충돌 후보를 피해 계단 하단을 서쪽으로 이동하고, 하단 Landing도 동일한 X 좌표에 정렬
- 씬 내 기존 `L Staircase` 0개, 새 `Folding Staircase` 1개 확인

## 이어서 확인할 항목

Unity Play Mode에서 실제 고양이 캐릭터로 다음 항목을 최종 체감 테스트해 주세요.

1. 1층 접근 시 전개 및 1층→2층 이동
2. 2층 접근 시 전개 및 2층→1층 이동
3. 고양이가 계단 위에 있을 때 접히지 않는지 확인
4. 계단에서 완전히 벗어난 뒤 3초 후 안전하게 수납되는지 확인
5. 애니메이션 속도와 대기 시간이 플레이 감각에 맞는지 확인

필요하면 `FoldingStaircaseController`의 `transitionSeconds`와 `autoCloseDelay`만 조정하면 됩니다.
