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
