# 0.1.181

## 한국어

- 도시마다 설계도를 최대 256개까지 저장할 수 있습니다. 기존 설계도는 도시를 열 때 자동으로 변환되며, 다음 도시 저장부터 압축 형식으로 보관됩니다. 설계도가 많은 도시에서 목록을 불러오고 지도 위 단지를 선택하는 부담도 줄였습니다.
- 판상형 단지 안에서 가까운 두 T자 교차로를 합칠 때 가지도로가 비스듬해지는 경우를 줄였습니다. 사선이나 곡선을 보존해야 하는 곳은 기존 연결을 유지합니다. 탑상형 단지에서는 곡선 도로와 가지도로의 연결을 개선했습니다.
- 입구를 연결하면서 차선이나 구획이 다시 만들어질 때 삭제 보호가 건설을 막는 경우를 줄였습니다. 기존 건물이나 대체되지 않는 도로 구간은 계속 보호합니다.

## English

- Save up to 256 plans per city. Existing plans convert automatically when you open the city and are stored in compressed form the next time you save. Cities with many plans also do less work when loading the plan list or selecting a compound on the map.
- In slab compounds, nearby T-junctions can now form a right-angle crossroads without skewing the side streets. Existing connections are kept where a diagonal or curve needs to stay in place. Branch roads also connect more reliably to curves in tower compounds.
- Connecting an entrance is less likely to be blocked when the game rebuilds lanes or zoning cells. Existing buildings and road segments that would be lost still have deletion protection.

# 0.1.176

## 한국어

- 대형 부지의 배치 미리보기를 더 빨리 계산합니다. 420 × 300m 판상형 부지에 크기가 다른 건물 5종을 선택한 비교 시험에서, 입구가 1개일 때 28.6초에서 1.4초로, 2개일 때 약 49~50초에서 2.0~2.6초로 줄었습니다. 직전 공개 버전(0.1.166)과 게임 밖에서 비교한 결과이며, 실제 대기 시간은 부지와 PC에 따라 달라집니다.
- 크기가 다른 건물을 함께 선택하면 다양한 크기를 배치하기 쉬워졌습니다. 깊게 패인 빌라 부지에서 내부 도로가 겹치는 문제도 줄였습니다.
- 미리보기에서 구역 꼭짓점을 끌어 옮겨 보세요. 놓으면 바뀐 경계로 다시 계산합니다.
- 유형을 잘못 골랐다면 빈 유형 탭으로 바꾸세요. 같은 부지에서 이어갈 수 있고, 원래 탭으로 돌아가면 이전 계획이 복원됩니다.

## English

- Large sites now take less time to plan. In an offline comparison with version 0.1.166, a 420 × 300 m slab site with five building sizes took 1.4 seconds instead of 28.6 with one entrance, and 2.0–2.6 seconds instead of 49–50 with two. In-game wait times depend on the site and your PC.
- Plans make better use of mixed building sizes. Internal roads are also less likely to overlap on villa sites with deep indentations.
- Drag a boundary corner in the preview and release it to recalculate the layout.
- Picked the wrong housing type? Switch to an empty type tab to reuse the site. Return to the original tab to pick up your previous plan.

# 0.1.166

## 한국어

- 판상형·탑상형·빌라 모두 구역 지정 → 입구 지정 → 배치 미리보기 순서로 진행합니다. 마지막 입력을 취소하거나 다시 적용할 수 있고, 다음 단계에 필요한 건물이나 입구가 빠졌다면 그 자리에서 보완할 수 있습니다.
- 기본 패널을 줄여 지도를 더 넓게 볼 수 있습니다. ‘넓게 보기’에서는 왼쪽에서 배치 조건을 조절하면서 오른쪽에서 건물 에셋을 찾고 선택할 수 있습니다.
- 동간 간격과 도로 이격 거리 등 주요 설정을 바로 조절할 수 있습니다. 세부 조절의 슬라이더에 마우스를 올리면 기본값과 변경 예시를 그림으로 비교할 수 있습니다.
- 단지를 삭제한 뒤 이전 단지의 입구 지정 단계나 경고가 남는 문제를 수정했습니다.
- 빌라 진입로가 첫 교차로를 지나 되꺾이면서 단지 안 도로와 겹치는 경우를 줄였습니다.

## English

- Slab, tower and villa plans now follow the same steps: draw the boundary, place entrances, then preview the layout. You can undo or redo your last input. If an entrance or building selection is missing, you can add it from the current step.
- The compact panel leaves more of the map visible. In wide view, you can adjust the layout on the left while browsing and selecting buildings on the right.
- Common settings such as building spacing and road setbacks are within reach. Hover over a slider in the detailed settings to see how changing its value affects the layout.
- Fixed the panel returning to entrance placement or showing an old warning after a compound was deleted.
- Villa entrance roads are less likely to double back and overlap an internal street after reaching their first junction.

# 0.1.139

## 한국어
- 여러 계획에 저장된 도형이 합쳐서 48,000개를 넘어도, 작업 중인 계획을 저장하고 건설 미리보기로 넘어갈 수 있습니다. 계획 하나가 48,000개를 넘으면 구역을 나누도록 안내합니다.
- 저장 공간을 정리하고 싶다면 저장 버튼 위의 ‘미사용 계획 정리’를 누르세요. 정리할 수 있는 미건설 계획을 큰 것부터 보여줍니다. 현재 초안을 유지한 채 지울 계획을 직접 고르고 확인할 수 있습니다.

## English
- You can now save your plan and open the construction preview even when your plans contain more than 48,000 shapes in total. If a single plan exceeds that limit, the mod suggests splitting it into smaller areas.
- To free up space, select “Review unused plans” above Save. Eligible unbuilt plans appear largest first. Choose and confirm which plan to remove while keeping your current draft open.

# 0.1.138

## 한국어
- 단지 유형 탭에서 현재 선택한 유형에 파란 밑줄을 표시합니다. 옵션 스위치는 켜면 파란색, 끄면 회색으로 표시해 상태를 쉽게 확인할 수 있습니다.
- 배치 설정의 슬라이더와 숫자 입력칸을 가지런히 맞춰 여러 값을 비교하고 조절하기 쉬워졌습니다.
- 탑상형 주차장 검색어가 게임 언어에 맞게 표시됩니다. 설정해 둔 주차장 에셋이 설치되어 있지 않으면 다른 에셋을 선택할 수 있도록 선택 버튼을 표시합니다.

## English
- A blue underline marks the selected compound type. Option switches are blue when on and gray when off.
- Sliders and number fields now line up across layout settings, making values easier to compare and adjust.
- Tower parking search now starts with a term in your game language. If your saved parking asset is missing, the picker prompts you to choose another.

# 0.1.133

## 한국어
- 건설이나 주차장 배치가 막혔을 때, 공유 버튼으로 보내는 문제 보고에 원인 확인에 필요한 정보를 보강했습니다.
- 저장 한도 초과 보고에 이전 건설 정보가 섞이던 문제를 수정했습니다.

## English
- Improved reports sent through the Share button to help investigate blocked construction and parking layouts.
- Fixed storage-limit reports including unrelated information from an earlier construction attempt.

# 0.1.132

## 한국어
- 탑상형 단지에서 선택한 여러 건물 에셋이 더 고르게 배치됩니다.
- 일부 탑상형 단지에서 내부 도로를 연결하지 못하던 문제를 개선했습니다.
- 계획도를 만들기 어려울 때 조정할 항목을 더 알기 쉽게 안내합니다.

## English
- Tower compounds now use a better mix of selected building assets.
- Improved internal road planning for tower layouts that previously failed to connect.
- Clearer guidance when a layout cannot be generated.

# 0.1.131

## 한국어
- 탑상형 아파트와 빌라 단지 제작을 활성화했습니다. 빌라의 기본 중앙도로는 양방향 2차로 Small Road입니다.
- CS KR Underground Parking Lot (167777)을 필수 모드로 추가하고 탑상형 기본 지하주차장 입구로 지정했습니다. 저장 계획의 주차장 선택 복원을 보완했습니다.
- 빌라 필지 테두리와 세 유형의 이름 라벨 렌더링 부하를 줄이고 라벨 크기를 통일했습니다. 건설 검증의 후보 검색과 프레임별 작업 처리를 최적화했습니다.
- 넓은 도로의 짧은 중복 가지 처리, 빌라 접속 방향 및 기존 도로가 보존되는 분할·재생성 판정을 보완했습니다. 실제 도로 손실과 게임 오류는 계속 차단합니다.
- 단지 전체 삭제에서 주변 시설을 보호하며 단지 구성물을 정리하고, 저장 계획과 일치하는 고립된 AP 내부 도로의 정리를 보완했습니다.
- 빌라·탑상형을 포함한 선택적 진단 공유를 보강했습니다. 자동 전송 없이 사용자 동의 후 제한된 요약만 전송하며, 공개 패키지의 개발용 상세 성능 계측은 꺼져 있습니다.

## English
- Enabled tower apartment and villa / low-rise compound planning. Villa central roads default to the standard two-way, two-lane Small Road.
- Added CS KR Underground Parking Lot (167777) as a required asset and the default tower parking entrance. Improved restoration of saved parking selections.
- Reduced villa lot-outline and shared label rendering work, unified label sizes, and optimized construction candidate searches and per-frame preparation work.
- Improved redundant short branches on wide roads, villa connection directions, and recognition of native splits/rebuilds that preserve existing roads. Road loss and native errors remain blocked.
- Improved confirmed compound demolition and cleanup of isolated AP internal roads matching the saved plan while protecting surrounding facilities.
- Expanded optional diagnostic sharing for villa and tower plans. Only a limited summary is sent after consent; there are no automatic submissions. Developer performance instrumentation is disabled in this public package.

# 0.1.118

## 한국어
- 세이브를 다시 불러올 때 저장 계획이 늘어나는 원인을 수정했습니다. 이미 저장된 중복 기록은 보존합니다.
- 입구 하나를 지정할 때 도로 시작부 때문에 배치가 실패하던 문제를 수정했습니다.
- 단지 삭제가 차단된 이유를 확인하기 쉽도록 진단을 보강했습니다.

## English
- Fixed a cause of saved plans accumulating on reload. Existing duplicate records are preserved.
- Fixed single-entrance layouts failing at the road start.
- Added clearer diagnostics for blocked compound demolition.

# 0.1.85

## 한국어
- 일부 정상적인 도로 병합이 건설 차단으로 처리되는 경우를 개선했습니다.
- 도로 배치·곡선·주차 연결 실패의 진단 정보를 보강했습니다.

## English
- Improved handling of valid road merges that could block construction.
- Improved diagnostic details for road placement, curves and parking connections.

# 0.1.78

## 한국어
- 평상시 상세 로그 기록을 줄였습니다. 오류와 주요 작업 결과는 계속 기록됩니다.
- 문제 확인이 필요할 때만 상세 디버그 로그를 켤 수 있습니다. 게임을 다시 시작하면 꺼집니다.

## English
- Reduced routine detailed logging while keeping errors and key operation results.
- Detailed debug logs can be enabled for troubleshooting and turn off on game restart.

# 0.1.77

## 한국어
- 자동 계산 ON/OFF와 도로 검색·썸네일·출처 필터를 추가했습니다.
- 도로 너비 표시와 선택 순서를 개선하고, 도로 선택 UI 오류·번역 누락을 수정했습니다.
- 주차장 추가는 기본 OFF로 변경하고, 건설 미리보기 버튼을 넓혔습니다.
- 오류 진단과 필수 도로팩 구독·활성화 안내를 보강했습니다.

## English
- Added an auto-calculation toggle and road search, thumbnails and source filters.
- Improved road width display and selection order; fixed road picker errors and missing translations.
- Parking infill now starts OFF, with more room for the construction preview button.
- Improved error diagnostics and guidance for subscribing to and enabling the required road pack.

# 0.1.70

0.1.70: Open-space parking infill with 2xTwoway approaches and 1xTwoway inner aisles; confirmed compound replacement; edge vertex insertion; unsaved-close confirmation; UI layout, number formatting and animation improvements. / 유휴부지 주차 보충·단지 재배치·변 위 점 추가·닫기 확인·UI 개선.

Build and targeted layout/ownership/serialization checks passed. In-game verification of these changes is pending.
