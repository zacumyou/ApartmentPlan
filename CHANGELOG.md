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
