# 망각곡선 학습 플래너 (Study Planner)

> **Forgetting Curve-based Study Planner for Korean University Students**
> 잊어버리기 전에 알려주는 똑똑한 학습 도우미

한국 대학생을 위한 **망각곡선 기반 학습 플래너** 데스크탑 애플리케이션입니다.
C# 프로그래밍 강의 기말 대체 과제로 제작되었습니다.

---

## 📌 프로젝트 개요

대학생은 한 학기에 평균 4~6과목을 동시에 수강하며, 중간고사·기말고사·과제를 병행합니다.
기존 학습 관리 도구(노션, 구글 캘린더, 에브리타임 등)는 **단순 기록**에 그쳐,
"이 내용을 **언제 다시 복습해야 가장 효율적인지**"에 대한 과학적 근거를 제시하지 못합니다.

본 프로젝트는 1885년 헤르만 에빙하우스(Hermann Ebbinghaus)의 **망각곡선(Forgetting Curve)** 이론과
**SuperMemo SM-2 알고리즘**을 적용하여, 사용자의 자가 평가에 따라 최적의 복습 시점을 자동으로 계산합니다.

---

## 🆚 기존 도구와의 차별점

| 구분 | SuperMemo / Anki | 노션 / 구글캘린더 | 에브리타임 | **본 프로젝트** |
|------|:---:|:---:|:---:|:---:|
| 망각곡선 자동 적용 | ✅ | ❌ | ❌ | ✅ |
| 한국어 인터페이스 | △ | ✅ | ✅ | ✅ |
| 대학 시험(중간/기말) 특화 | ❌ | ❌ | △ | ✅ |
| 시험 D-Day 역산 | ❌ | ❌ | ❌ | ✅ |
| 오프라인 동작 | △ | ❌ | ❌ | ✅ |
| 자가 평가 기반 추천 | ✅ | ❌ | ❌ | ✅ |

- **SuperMemo / Anki**: 영어 단어 암기용 플래시카드 → 한국 대학 시험(서술형·풀이형)에 부적합
- **노션 / 구글캘린더**: 모든 복습 시점을 사용자가 수동 입력해야 함
- **본 프로젝트**: 망각곡선 자동 적용 + 한국 대학 학사 일정 특화 + 100% 오프라인(개인정보 외부 전송 없음)

---

## ✨ 주요 기능 (7개 탭 + 부가 기능)

### 7개 화면

| 탭 | 단축키 | 주요 기능 |
|----|--------|-----------|
| 🏠 대시보드 | Ctrl+1 | 요약 카드 4개 + 차트 2개 (과목별 분포 / 14일 일정) |
| 📖 학습 주제 | Ctrl+2 | 등록·수정·삭제, 검색/필터, 정렬, 과목별 색상 띠 |
| ⏰ 오늘의 복습 | Ctrl+3 | SM-2 자가평가 0~5점 → 다음 복습일 자동 계산 |
| 📅 시험 D-Day | Ctrl+4 | 시험 등록 + D-Day + 시험 대비 일정 자동 분산 배치 |
| ⏱️ 포모도로 | Ctrl+5 | 집중 25분 / 휴식 5분 / 긴 휴식 15분 + 누적 통계 |
| 🗓️ 캘린더 | Ctrl+6 | 월간 7×6 그리드, 학습/복습/시험 마커 |
| 📊 통계 | Ctrl+7 | 카드 4 + 차트 3 + 약점 단원 Top 5 |

### 부가 기능

- 🌙 **다크모드 토글** (Ctrl+D) — 설정 영속화
- 🔔 **자동 알림** — 매일 정해진 시각에 트레이 토스트
- 📥📤 **데이터 백업/복원** — JSON 파일 (Ctrl+B / Ctrl+I)
- 🎨 **과목별 자동 색상 코딩** — 모든 표·차트에 일관 적용
- 🔥 **연속 학습 일수 (Streak)** — 동기부여 카드
- ⌨️ **키보드 단축키 11개+** — 마우스 없이 핵심 작업 가능
- ⚙️ **설정 다이얼로그** (Ctrl+,) — 알림 시각 등

### 키보드 단축키 (전체 목록)

| 단축키 | 동작 |
|--------|------|
| Ctrl+1~7 | 탭 전환 |
| Ctrl+N | 학습 주제 추가 (포커스) |
| Ctrl+E | 시험 추가 |
| Ctrl+F | 검색 박스 |
| Ctrl+D | 다크모드 토글 |
| Ctrl+B / Ctrl+I | 백업 / 가져오기 |
| Ctrl+, | 설정 다이얼로그 |
| F5 | 새로고침 |
| F2 / 더블클릭 | 행 편집 |
| Delete | 선택 행 삭제 |
| Enter | 폼 제출 |
| Esc | 다이얼로그 취소 |

---

## 🛠️ 기술 스택

| 항목 | 사용 기술 |
|------|-----------|
| 언어 | C# |
| 프레임워크 | .NET 8.0 (LTS) |
| UI | WPF (XAML) |
| 데이터베이스 | SQLite (Entity Framework Core 8.0.11) |
| 차트 | LiveCharts2 (LiveChartsCore.SkiaSharpView.WPF) |
| UI 테마 | MaterialDesignThemes |
| 개발 환경 | Visual Studio 2022 Community |
| 핵심 알고리즘 | SuperMemo SM-2 |

---

## 🚀 빌드 및 실행

### 요구 사항
- Windows 10/11
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 (".NET 데스크톱 개발" 워크로드)

### 실행 방법
```bash
# 저장소 클론
git clone <저장소 주소>
cd StudyPlanner

# 패키지 복원 및 실행
dotnet restore
dotnet run --project StudyPlanner
```
또는 `StudyPlanner.sln`을 Visual Studio 2022에서 열고 `Ctrl + F5`로 실행합니다.

### 시연용 데이터 채우기 (선택)

빈 상태로 보기엔 차트/통계가 비어있어요. 풍성한 데이터로 한 번에 채우려면:

1. 앱 실행 → 대시보드 우하단 📥 **가져오기** 클릭 (또는 Ctrl+I)
2. `docs/demo_backup.json` 선택
3. "예 (교체)" 선택 → 학습 주제 12개 + 시험 4개 즉시 로드
4. 모든 탭(대시보드/캘린더/통계)에 데이터 채워짐

---

## 📂 프로젝트 구조

```
StudyPlanner/
├── StudyPlanner.sln
├── StudyPlanner/
│   ├── Models/             # 데이터 모델 (StudyTopic, Exam, CalendarDay)
│   ├── Data/               # EF Core DbContext (SQLite)
│   ├── Services/           # SM-2, 백업, 설정, 테마, 색상, 트레이, 포모도로
│   ├── Converters/         # XAML 바인딩 변환기 (SubjectColorConverter)
│   ├── Dialogs/            # 편집/설정 모달 (Topic/Exam/Settings)
│   ├── App.xaml            # 앱 진입점 + MaterialDesign 테마 등록
│   └── MainWindow.xaml     # 메인 윈도우 (TabControl 7개 탭)
├── docs/
│   ├── REPORT_UPDATED.md          # 수행 계획서 (최종본)
│   ├── NOTION_REQUIREMENTS.md     # 노션 요구사항 정리
│   ├── PRESENTATION_OUTLINE.md    # 발표 슬라이드 구성안
│   └── demo_backup.json           # 시연용 백업 데이터
└── README.md
```

---

## 📅 개발 일정 (실제 진행)

| 단계 | 작업 |
|------|------|
| 1주차 | WPF UI 설계, SQLite + EF Core, 학습 주제 등록 |
| 2주차 | SM-2 알고리즘, 자가평가, 시험 D-Day 역산 |
| 3주차 ① | 대시보드, 차트, 트레이 알림 |
| 3주차 ② | MaterialDesign 전체 적용 |
| 확장 | 삭제/편집, 검색/필터, 백업, 다크모드, 통계 탭, 자동 알림 |
| UX Tier 1 | 단축키, 빈 상태, 카드 클릭, 연속 학습 일수 |
| 기능 Tier 2~3 | 과목별 색상, 포모도로 타이머, 월간 캘린더 |

## 📑 문서

- 📋 **수행 계획서**: [`docs/REPORT_UPDATED.md`](docs/REPORT_UPDATED.md)
- 📒 **요구사항 정리**: [`docs/NOTION_REQUIREMENTS.md`](docs/NOTION_REQUIREMENTS.md)
- 🎬 **발표 슬라이드 구성**: [`docs/PRESENTATION_OUTLINE.md`](docs/PRESENTATION_OUTLINE.md)
- 💾 **시연용 데이터**: [`docs/demo_backup.json`](docs/demo_backup.json)

## 🖼️ 스크린샷

> 발표/제출 시 추가 예정

- 대시보드: (스크린샷 위치)
- 캘린더: (스크린샷 위치)
- 통계: (스크린샷 위치)
- 다크모드: (스크린샷 위치)

---

## 📚 참고 문헌

- Ebbinghaus, H. (1885). *Über das Gedächtnis: Untersuchungen zur experimentalen Psychologie.*
- Cepeda, N. J., et al. (2006). Distributed practice in verbal recall tasks: A review and quantitative synthesis. *Psychological Bulletin, 132(3).*
- Dunlosky, J., et al. (2013). Improving Students' Learning With Effective Learning Techniques. *Psychological Science in the Public Interest, 14(1).*
- Wozniak, P. A. (1990). *Optimization of learning.* (SuperMemo SM-2 알고리즘)
- Murre, J. M. J., & Dros, J. (2015). Replication and Analysis of Ebbinghaus' Forgetting Curve. *PLoS ONE, 10(7).*

---

*본 프로젝트는 학습/교육 목적으로 제작되었습니다.*
