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
