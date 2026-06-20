using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using StudyPlanner.Data;
using StudyPlanner.Dialogs;
using StudyPlanner.Models;
using StudyPlanner.Services;
// WinForms 참조로 인한 이름 충돌 방지 (WPF 쪽 명시)
using MessageBox = System.Windows.MessageBox;
using Button = System.Windows.Controls.Button;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using Keyboard = System.Windows.Input.Keyboard;
using TextBox = System.Windows.Controls.TextBox;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Brush = System.Windows.Media.Brush;
using BrushConverter = System.Windows.Media.BrushConverter;

namespace StudyPlanner
{
    /// <summary>
    /// 메인 화면 — 학습 주제 등록/조회 + 오늘의 복습(SM-2 자가 평가)
    /// </summary>
    public partial class MainWindow : Window
    {
        // 자가 평가 대상으로 선택된 학습 주제의 Id
        private int? selectedTopicId;

        // 트레이 아이콘 & 알림 서비스
        private TrayNotificationService? trayService;

        // 자동 알림 체크 타이머 (1분마다)
        private DispatcherTimer? notificationTimer;

        // 검색 입력 debounce 타이머 (타이핑 멈춘 후 250ms 뒤 필터 적용)
        private DispatcherTimer? topicSearchDebounce;
        private DispatcherTimer? examSearchDebounce;

        // 포모도로 타이머 상태
        private DispatcherTimer? pomodoroTimer;
        private int pomodoroRemainingSeconds = 25 * 60;
        private int pomodoroSelectedMinutes = 25;
        private string pomodoroMode = "집중";   // "집중" / "짧은 휴식" / "긴 휴식"
        private bool pomodoroRunning = false;

        // 캘린더가 현재 보고 있는 달의 1일
        private DateTime calendarViewMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        // 학습 주제 / 시험 캐시 (DB에서 한 번 읽고 필터링은 메모리 상에서 수행)
        private List<StudyTopic> allTopics = new();
        private List<Exam> allExams = new();

        // "전체" 옵션 (필터 ComboBox용)
        private const string FilterAllSubjects = "전체";

        public MainWindow()
        {
            InitializeComponent();

            // 저장된 설정 불러와서 테마 즉시 적용 (앱 껐다 켜도 모드 기억)
            var settings = SettingsService.Load();
            ThemeService.ApplyTheme(settings.DarkMode);
            UpdateThemeIcon(settings.DarkMode);

            // LiveCharts2 차트 텍스트 한글 깨짐 방지: 맑은 고딕을 전역 폰트로 지정
            LiveCharts.Configure(config =>
                config.HasGlobalSKTypeface(SKTypeface.FromFamilyName("Malgun Gothic")));

            // 프로그램 시작 시: DB 파일이 없으면 자동 생성
            using (var db = new StudyDbContext())
            {
                db.Database.EnsureCreated();
            }

            dpStudyDate.SelectedDate = DateTime.Today;          // 학습일 기본값 = 오늘
            dpExamDate.SelectedDate = DateTime.Today.AddDays(14); // 시험일 기본값 = 2주 뒤
            LoadTopics();        // 학습 주제 목록 불러오기
            LoadReviewList();    // 오늘 복습할 항목 불러오기
            LoadExams();         // 시험 목록 불러오기
            LoadDashboard();     // 대시보드 통계/차트 갱신
            LoadStatistics();    // 상세 통계 탭 갱신
            LoadPomodoroStats(); // 포모도로 누적 통계 표시
            PomodoroReset();     // 타이머 초기 상태 표시
            BuildCalendar();     // 월간 캘린더 초기 렌더링

            // 트레이 아이콘 등록 (앱 실행 중 작업표시줄 트레이에 표시됨)
            trayService = new TrayNotificationService(onOpenRequested: () =>
            {
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
            });

            // 시작 시 오늘 복습할 항목이 있으면 자동 알림
            ShowTodayReviewNotification();

            // 매일 정해진 시간에 자동 알림 보내는 타이머 시작 (1분 간격으로 체크)
            StartNotificationTimer();

            // 창 닫힐 때 트레이 아이콘 + 타이머 정리
            this.Closed += (s, e) =>
            {
                notificationTimer?.Stop();
                trayService?.Dispose();
            };
        }

        // 매일 정해진 시각에 한 번씩 트레이 알림을 보내는 백그라운드 타이머
        private void StartNotificationTimer()
        {
            notificationTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            notificationTimer.Tick += CheckScheduledNotification;
            notificationTimer.Start();
        }

        // 타이머 콜백 — 설정된 시간을 지났고 오늘 아직 알림 안 보냈으면 발송
        private void CheckScheduledNotification(object? sender, EventArgs e)
        {
            var settings = SettingsService.Load();
            if (!settings.DailyNotificationEnabled) return;

            // 오늘 이미 발송했으면 패스
            string todayStr = DateTime.Today.ToString("yyyy-MM-dd");
            if (settings.LastNotificationDate == todayStr) return;

            // 설정된 시각 파싱
            if (!TimeSpan.TryParse(settings.DailyNotificationTime, out var targetTime)) return;

            // 현재 시각이 설정 시각을 지났으면 발송
            if (DateTime.Now.TimeOfDay >= targetTime)
            {
                ShowTodayReviewNotification();
                settings.LastNotificationDate = todayStr;
                SettingsService.Save();
            }
        }

        // 오늘 복습할 항목 수를 트레이 알림으로 표시
        private void ShowTodayReviewNotification()
        {
            using (var db = new StudyDbContext())
            {
                DateTime today = DateTime.Today;
                int dueCount = db.StudyTopics.Count(t => t.NextReviewDate <= today);

                if (dueCount > 0)
                {
                    trayService?.ShowNotification(
                        "오늘의 복습",
                        $"오늘 복습할 항목이 {dueCount}건 있습니다.");
                }
                else
                {
                    trayService?.ShowNotification(
                        "오늘의 복습",
                        "오늘 복습할 항목이 없습니다. 새 주제를 학습해보세요!");
                }
            }
        }

        // [지금 알림 받기] 버튼 클릭 — 즉시 트레이 알림 표시 (시연/테스트용)
        private void btnNotify_Click(object sender, RoutedEventArgs e)
        {
            ShowTodayReviewNotification();
        }

        // DB에서 전체 학습 주제를 캐시에 적재 → 과목 필터 갱신 → 현재 필터 조건 적용
        private void LoadTopics()
        {
            using (var db = new StudyDbContext())
            {
                allTopics = db.StudyTopics
                              .OrderByDescending(t => t.StudyDate)
                              .ToList();
            }
            UpdateSubjectFilterItems();
            ApplyTopicFilter();
        }

        // 과목 필터 ComboBox에 "전체" + 현재 등록된 과목들을 채워넣음
        private void UpdateSubjectFilterItems()
        {
            var current = cmbSubjectFilter.SelectedItem as string;
            var subjects = new List<string> { FilterAllSubjects };
            subjects.AddRange(allTopics.Select(t => t.Subject).Distinct().OrderBy(s => s));
            cmbSubjectFilter.ItemsSource = subjects;
            // 기존 선택 유지 (목록에 없으면 "전체"로)
            cmbSubjectFilter.SelectedItem = (current != null && subjects.Contains(current))
                                            ? current
                                            : FilterAllSubjects;
        }

        // 현재 검색어 + 과목 선택을 캐시에 적용 → DataGrid에 표시
        private void ApplyTopicFilter()
        {
            // 초기화 중 호출 방지
            if (allTopics == null) return;

            string keyword = txtSearch?.Text?.Trim() ?? "";
            string subject = (cmbSubjectFilter?.SelectedItem as string) ?? FilterAllSubjects;

            IEnumerable<StudyTopic> result = allTopics;

            if (subject != FilterAllSubjects && !string.IsNullOrEmpty(subject))
                result = result.Where(t => t.Subject == subject);

            if (!string.IsNullOrEmpty(keyword))
                result = result.Where(t =>
                    (t.Subject?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (t.Unit?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (t.Memo?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));

            var list = result.ToList();
            dgTopics.ItemsSource = list;

            // 필터 결과 개수 표시 (필터가 걸려 있을 때만)
            bool filtered = subject != FilterAllSubjects || !string.IsNullOrEmpty(keyword);
            txtFilterResultCount.Text = filtered
                ? $"({list.Count} / {allTopics.Count})"
                : "";

            // 빈 상태 안내 (필터 후 결과 0이면 표시)
            emptyTopics.Visibility = list.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;
        }

        // 검색어 입력 변경 (debounce: 마지막 타이핑 후 250ms 뒤 필터 적용)
        private void txtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (topicSearchDebounce == null)
            {
                topicSearchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                topicSearchDebounce.Tick += (s, ev) =>
                {
                    topicSearchDebounce!.Stop();
                    ApplyTopicFilter();
                };
            }
            topicSearchDebounce.Stop();
            topicSearchDebounce.Start();
        }

        // 과목 필터 선택 변경
        private void cmbSubjectFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => ApplyTopicFilter();

        // 필터 초기화 버튼
        private void btnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Text = "";
            cmbSubjectFilter.SelectedItem = FilterAllSubjects;
        }

        // 오늘(또는 그 이전)이 복습 예정일인 항목만 불러와 표시 (오늘의 복습 탭)
        private void LoadReviewList()
        {
            using (var db = new StudyDbContext())
            {
                DateTime today = DateTime.Today;
                var dueList = db.StudyTopics
                                .Where(t => t.NextReviewDate <= today)
                                .OrderBy(t => t.NextReviewDate)
                                .ToList();

                dgReview.ItemsSource = dueList;
                txtReviewCount.Text = dueList.Count.ToString();

                emptyReview.Visibility = dueList.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // [등록] 버튼 클릭 — 새 학습 주제 저장
        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            // --- 입력 검증 ---
            if (string.IsNullOrWhiteSpace(txtSubject.Text))
            {
                MessageBox.Show("과목을 입력해주세요.", "알림",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(txtUnit.Text))
            {
                MessageBox.Show("단원/주제를 입력해주세요.", "알림",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime studyDate = dpStudyDate.SelectedDate ?? DateTime.Today;

            // --- 새 학습 주제 객체 생성 ---
            var topic = new StudyTopic
            {
                Subject = txtSubject.Text.Trim(),
                Unit = txtUnit.Text.Trim(),
                StudyDate = studyDate,
                Memo = txtMemo.Text.Trim(),
                NextReviewDate = studyDate   // 학습한 날 바로 첫 복습 대상 (이후 SM-2가 간격 계산)
            };

            using (var db = new StudyDbContext())
            {
                db.StudyTopics.Add(topic);
                db.SaveChanges();
            }

            // --- 입력 폼 초기화 + 목록 새로고침 ---
            txtSubject.Clear();
            txtUnit.Clear();
            txtMemo.Clear();
            dpStudyDate.SelectedDate = DateTime.Today;
            LoadTopics();
            LoadReviewList();
            LoadDashboard();
            LoadStatistics();
            BuildCalendar();
        }

        // 오늘의 복습 목록에서 항목을 선택했을 때
        private void dgReview_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgReview.SelectedItem is StudyTopic t)
            {
                selectedTopicId = t.Id;
                txtSelected.Text = $"[{t.Subject}] {t.Unit}\n" +
                                   $"현재 반복 {t.RepetitionCount}회 · 학습 난이도 {t.EaseFactor:F2}";
            }
            else
            {
                selectedTopicId = null;
                txtSelected.Text = "복습할 항목을 선택하세요.";
            }
        }

        // 자가 평가 점수 버튼(0~5) 클릭 — SM-2 알고리즘 적용
        private void btnRate_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTopicId == null)
            {
                MessageBox.Show("먼저 복습할 항목을 선택하세요.", "알림",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 버튼의 Tag에 담긴 점수(1~5) 읽기
            int inputQuality = int.Parse(((Button)sender).Tag.ToString()!);
            
            // SM-2 알고리즘 매핑: 1~5 -> 0, 1, 3, 4, 5 로 변환
            int quality = inputQuality switch
            {
                5 => 5,
                4 => 4,
                3 => 3,
                2 => 1,
                1 => 0,
                _ => 0
            };

            using (var db = new StudyDbContext())
            {
                // 선택된 주제를 DB에서 다시 찾아옴 (Id로 조회)
                var topic = db.StudyTopics.Find(selectedTopicId.Value);
                if (topic == null) return;

                // ★ SM-2 알고리즘 적용 → 다음 복습일/간격/EF 자동 계산
                Sm2Service.ApplyReview(topic, quality);
                db.SaveChanges();

                MessageBox.Show(
                    $"복습 완료!\n\n다음 복습일: {topic.NextReviewDate:yyyy-MM-dd}\n" +
                    $"복습 간격: {topic.IntervalDays}일\n학습 난이도: {topic.EaseFactor:F2}",
                    "SM-2 결과", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // 선택 해제 + 양쪽 목록 새로고침
            selectedTopicId = null;
            txtSelected.Text = "복습할 항목을 선택하세요.";
            LoadTopics();
            LoadReviewList();
            LoadDashboard();
            LoadStatistics();
            BuildCalendar();
        }

        // ===================== 시험 D-Day =====================

        // DB에서 전체 시험을 캐시에 적재 → 과목 필터 갱신 → 현재 필터 적용
        private void LoadExams()
        {
            using (var db = new StudyDbContext())
            {
                allExams = db.Exams.OrderBy(x => x.ExamDate).ToList();
            }
            UpdateExamSubjectFilterItems();
            ApplyExamFilter();
        }

        // 시험 과목 필터 ComboBox 갱신 ("전체" + 등록된 시험 과목들)
        private void UpdateExamSubjectFilterItems()
        {
            var current = cmbExamSubjectFilter.SelectedItem as string;
            var subjects = new List<string> { FilterAllSubjects };
            subjects.AddRange(allExams.Select(x => x.Subject).Distinct().OrderBy(s => s));
            cmbExamSubjectFilter.ItemsSource = subjects;
            cmbExamSubjectFilter.SelectedItem = (current != null && subjects.Contains(current))
                                                ? current
                                                : FilterAllSubjects;
        }

        // 시험 필터 적용 → DataGrid에 표시
        private void ApplyExamFilter()
        {
            if (allExams == null) return;

            string keyword = txtExamSearch?.Text?.Trim() ?? "";
            string subject = (cmbExamSubjectFilter?.SelectedItem as string) ?? FilterAllSubjects;

            IEnumerable<Exam> result = allExams;

            if (subject != FilterAllSubjects && !string.IsNullOrEmpty(subject))
                result = result.Where(x => x.Subject == subject);

            if (!string.IsNullOrEmpty(keyword))
                result = result.Where(x =>
                    (x.Subject?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (x.Name?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false));

            var list = result.ToList();
            dgExams.ItemsSource = list;

            bool filtered = subject != FilterAllSubjects || !string.IsNullOrEmpty(keyword);
            txtExamFilterResultCount.Text = filtered
                ? $"({list.Count} / {allExams.Count})"
                : "";

            emptyExams.Visibility = list.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;
        }

        // 시험 검색 (debounce)
        private void txtExamSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (examSearchDebounce == null)
            {
                examSearchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
                examSearchDebounce.Tick += (s, ev) =>
                {
                    examSearchDebounce!.Stop();
                    ApplyExamFilter();
                };
            }
            examSearchDebounce.Stop();
            examSearchDebounce.Start();
        }

        private void cmbExamSubjectFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => ApplyExamFilter();

        private void btnClearExamFilter_Click(object sender, RoutedEventArgs e)
        {
            txtExamSearch.Text = "";
            cmbExamSubjectFilter.SelectedItem = FilterAllSubjects;
        }

        // [시험 등록] 버튼 클릭
        private void btnAddExam_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtExamSubject.Text))
            {
                MessageBox.Show("과목을 입력해주세요.", "알림",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (dpExamDate.SelectedDate == null)
            {
                MessageBox.Show("시험일을 선택해주세요.", "알림",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var exam = new Exam
            {
                Subject = txtExamSubject.Text.Trim(),
                Name = string.IsNullOrWhiteSpace(txtExamName.Text) ? "시험" : txtExamName.Text.Trim(),
                ExamDate = dpExamDate.SelectedDate.Value
            };

            using (var db = new StudyDbContext())
            {
                db.Exams.Add(exam);
                db.SaveChanges();
            }

            txtExamSubject.Clear();
            txtExamName.Clear();
            dpExamDate.SelectedDate = DateTime.Today.AddDays(14);
            LoadExams();
            LoadDashboard();
            LoadStatistics();
            BuildCalendar();
        }

        // [시험 대비 복습 일정 생성] 버튼 클릭 — D-Day 역산
        // 선택한 시험의 과목에 속한 학습 주제들을, 시험일에서 거꾸로 하루씩 앞당겨 복습 예약한다.
        private void btnGenPlan_Click(object sender, RoutedEventArgs e)
        {
            if (dgExams.SelectedItem is not Exam exam)
            {
                MessageBox.Show("먼저 목록에서 시험을 선택하세요.", "알림",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (var db = new StudyDbContext())
            {
                // 해당 과목의 학습 주제들 가져오기
                var topics = db.StudyTopics
                               .Where(t => t.Subject == exam.Subject)
                               .ToList();

                if (topics.Count == 0)
                {
                    MessageBox.Show($"'{exam.Subject}' 과목의 학습 주제가 없습니다.\n먼저 학습 주제를 등록하세요.",
                                    "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // ★ 격리된 순수 스케줄러 서비스를 통해 복습 예정일 분산 계산 진행
                ExamSchedulerService.DistributeReviewDates(topics, exam);

                db.SaveChanges();
            }

            LoadTopics();
            LoadReviewList();
            LoadDashboard();
            LoadStatistics();
            MessageBox.Show(
                $"'{exam.Subject}' 시험({exam.DDayText}) 대비 복습 일정을 생성했습니다.\n" +
                $"학습 주제 탭에서 다음 복습일이 시험일 기준으로 재배치된 것을 확인하세요.",
                "일정 생성 완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ===================== 대시보드 =====================

        // 요약 카드와 차트(과목별 분포, 망각곡선)를 갱신한다.
        private void LoadDashboard()
        {
            using (var db = new StudyDbContext())
            {
                var topics = db.StudyTopics.ToList();
                var exams = db.Exams.ToList();
                var today = DateTime.Today;

                // ── 요약 카드 ──
                txtTotalTopics.Text = topics.Count.ToString();
                txtTodayReview.Text = topics.Count(t => t.NextReviewDate <= today).ToString();

                var nextExam = exams.Where(e => e.ExamDate >= today)
                                    .OrderBy(e => e.ExamDate)
                                    .FirstOrDefault();
                if (nextExam == null)
                {
                    txtNextExamDay.Text = "—";
                    txtNextExamLabel.Text = "등록된 시험 없음";
                }
                else
                {
                    int dday = (nextExam.ExamDate.Date - today).Days;
                    txtNextExamDay.Text = dday == 0 ? "D-DAY" : $"D-{dday}";
                    txtNextExamLabel.Text = nextExam.Subject;
                }

                // 연속 학습 일수 — 오늘부터 거꾸로 매일 학습 주제가 등록된 일수
                txtStreak.Text = CalculateStreak(topics).ToString();

                // ── 차트 1: 과목별 학습 주제 수 (막대) ──
                var bySubject = topics.GroupBy(t => t.Subject)
                                      .Select(g => new { Subject = g.Key, Count = g.Count() })
                                      .OrderByDescending(x => x.Count)
                                      .ToList();

                chartSubject.LegendPosition = LegendPosition.Hidden;
                chartSubject.TooltipPosition = TooltipPosition.Top;

                // 과목별로 별도 ColumnSeries 생성 → 각 막대를 그 과목의 색으로
                // (각 시리즈의 Values는 null로 채워진 자기 위치에만 값을 둠)
                var subjectSeries = new List<ISeries>();
                for (int i = 0; i < bySubject.Count; i++)
                {
                    var grp = bySubject[i];
                    var values = new double?[bySubject.Count];
                    values[i] = grp.Count;
                    subjectSeries.Add(new ColumnSeries<double?>
                    {
                        Values = values,
                        Name = grp.Subject,
                        Fill = new SolidColorPaint(SubjectColorService.GetSkColor(grp.Subject))
                    });
                }
                // 다크모드 여부에 따라 색상 결정 (다크: 흰색, 라이트: 어두운 회색)
                bool isDark = SettingsService.Load().DarkMode;
                var labelPaint = new SolidColorPaint(isDark ? SKColors.White : new SKColor(50, 50, 50));

                chartSubject.XAxes = new[]
                {
                    new Axis
                    {
                        Labels = bySubject.Select(x => x.Subject).ToArray(),
                        LabelsRotation = 0,
                        LabelsPaint = labelPaint
                    }
                };
                chartSubject.YAxes = new[]
                {
                    new Axis
                    {
                        MinLimit = 0,
                        MinStep = 1,
                        Labeler = v => $"{v:F0}개",
                        LabelsPaint = labelPaint
                    }
                };

                // (이하 중략: chartSchedule, chartMonthly, chartDayOfWeek, chartProgress 모두 동일 적용)

                // ── 차트 2: 향후 14일 복습 일정 ──
                // 본인 데이터 기반: NextReviewDate가 앞으로 N일 안에 있는 주제를 일별로 카운트
                int scheduleDays = 14;
                int[] reviewsByDay = new int[scheduleDays];
                foreach (var t in topics)
                {
                    int delta = (t.NextReviewDate.Date - today).Days;
                    if (delta >= 0 && delta < scheduleDays)
                        reviewsByDay[delta]++;
                }

                string[] dayLabels = Enumerable.Range(0, scheduleDays)
                    .Select(i =>
                    {
                        if (i == 0) return "오늘";
                        if (i == 1) return "내일";
                        // ※ "M/d"의 '/'는 .NET에서 문화권별 날짜 구분자(한국=- 등)로 해석됨
                        //    리터럴 슬래시를 원하면 '/' 처럼 따옴표로 감싸야 함
                        return today.AddDays(i).ToString("M'/'d");
                    })
                    .ToArray();

                chartSchedule.LegendPosition = LegendPosition.Hidden;
                chartSchedule.TooltipPosition = TooltipPosition.Top;

                chartSchedule.Series = new ISeries[]
                {
                    new ColumnSeries<int>
                    {
                        Values = reviewsByDay,
                        Fill = new SolidColorPaint(new SKColor(76, 175, 80))  // 초록 (복습 = 학습 활동)
                    }
                };

                chartSchedule.XAxes = new[]
                {
                    new Axis
                    {
                        Labels = dayLabels,
                        LabelsRotation = 0,
                        LabelsPaint = labelPaint
                    }
                };
                chartSchedule.YAxes = new[]
                {
                    new Axis
                    {
                        MinLimit = 0,
                        MinStep = 1,
                        Labeler = v => $"{v:F0}건",
                        LabelsPaint = labelPaint
                    }
                };
            }
        }

        // ===================== 학습 주제 삭제/편집 =====================

        // 학습 주제 행의 [휴지통] 버튼 클릭 → 확인 후 DB에서 삭제
        private void btnDeleteTopic_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is StudyTopic topic)
            {
                var result = MessageBox.Show(
                    $"'{topic.Subject} - {topic.Unit}' 주제를 정말 삭제하시겠습니까?",
                    "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;

                using (var db = new StudyDbContext())
                {
                    var tracked = db.StudyTopics.Find(topic.Id);
                    if (tracked != null)
                    {
                        db.StudyTopics.Remove(tracked);
                        db.SaveChanges();
                    }
                }

                LoadTopics();
                LoadReviewList();
                LoadDashboard();
                LoadStatistics();
            }
        }

        // 학습 주제 행 더블클릭 → 편집 다이얼로그 열기
        private void dgTopics_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // 헤더나 빈 공간 더블클릭은 무시
            if (dgTopics.SelectedItem is not StudyTopic selected) return;

            using (var db = new StudyDbContext())
            {
                var topic = db.StudyTopics.Find(selected.Id);
                if (topic == null) return;

                var dlg = new EditTopicDialog(topic) { Owner = this };
                if (dlg.ShowDialog() == true)
                {
                    if (dlg.DeleteRequested)
                    {
                        db.StudyTopics.Remove(topic);
                    }
                    db.SaveChanges();   // 다이얼로그가 topic 객체를 직접 수정함 (또는 삭제)
                    LoadTopics();
                    LoadReviewList();
                    LoadDashboard();
                    LoadStatistics();
                }
            }
        }

        // ===================== 시험 삭제/편집 =====================

        private void btnDeleteExam_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Exam exam)
            {
                var result = MessageBox.Show(
                    $"'{exam.Subject} - {exam.Name}' 시험을 정말 삭제하시겠습니까?",
                    "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;

                using (var db = new StudyDbContext())
                {
                    var tracked = db.Exams.Find(exam.Id);
                    if (tracked != null)
                    {
                        db.Exams.Remove(tracked);
                        db.SaveChanges();
                    }
                }

                LoadExams();
                LoadDashboard();
                LoadStatistics();
            }
        }

        private void dgExams_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgExams.SelectedItem is not Exam selected) return;

            using (var db = new StudyDbContext())
            {
                var exam = db.Exams.Find(selected.Id);
                if (exam == null) return;

                var dlg = new EditExamDialog(exam) { Owner = this };
                if (dlg.ShowDialog() == true)
                {
                    if (dlg.DeleteRequested)
                    {
                        db.Exams.Remove(exam);
                    }
                    db.SaveChanges();
                    LoadExams();
                    LoadDashboard();
                    LoadStatistics();
                }
            }
        }

        // ===================== 백업 / 복원 (JSON) =====================

        // [내보내기] 버튼 — 현재 DB를 JSON 파일로 저장
        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"학습플래너_백업_{DateTime.Now:yyyyMMdd_HHmmss}",
                DefaultExt = ".json",
                Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var (topicCount, examCount) = BackupService.ExportToFile(dlg.FileName);
                MessageBox.Show(
                    $"백업 완료!\n\n학습 주제 {topicCount}건, 시험 {examCount}건이 저장되었습니다.\n\n파일: {dlg.FileName}",
                    "내보내기 성공", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"내보내기 실패\n\n{ex.Message}", "오류",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===================== 다크모드 토글 =====================

        // [테마 전환] 버튼 클릭 → 라이트↔다크 토글 + 저장 + 차트 갱신
        private void btnTheme_Click(object sender, RoutedEventArgs e)
        {
            var settings = SettingsService.Load();
            settings.DarkMode = !settings.DarkMode;
            ThemeService.ApplyTheme(settings.DarkMode);
            SettingsService.Save();
            UpdateThemeIcon(settings.DarkMode);
            
            // 차트 갱신 (테마 변경 시 라벨 색상 즉시 반영)
            LoadDashboard();
            LoadStatistics();
        }

        // 현재 테마에 맞춰 토글 아이콘 변경
        // - 라이트모드일 때 → 달 모양 (클릭하면 다크로 전환)
        // - 다크모드일 때 → 해 모양 (클릭하면 라이트로 전환)
        private void UpdateThemeIcon(bool isDark)
        {
            iconTheme.Kind = isDark
                ? MaterialDesignThemes.Wpf.PackIconKind.WhiteBalanceSunny
                : MaterialDesignThemes.Wpf.PackIconKind.WeatherNight;
        }

        // [⚙️ 설정] 버튼 — 자동 알림 설정 다이얼로그 열기
        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SettingsDialog { Owner = this };
            dlg.ShowDialog();
            // 저장됐든 취소했든 별도 갱신 필요 없음 (타이머가 알아서 새 설정 사용)
        }

        // [가져오기] 버튼 — JSON 파일을 DB에 추가 또는 교체
        private void btnImport_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON 파일 (*.json)|*.json|모든 파일 (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            // 가져오기 방식 선택 (교체 vs 추가)
            var choice = MessageBox.Show(
                "기존 데이터를 어떻게 처리할까요?\n\n" +
                "[예]   기존 데이터를 모두 삭제하고 가져오기 (교체)\n" +
                "[아니오] 기존 데이터를 유지하고 추가하기 (병합)\n" +
                "[취소] 가져오기 중단",
                "가져오기 방식", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            if (choice == MessageBoxResult.Cancel) return;
            bool replaceExisting = (choice == MessageBoxResult.Yes);

            try
            {
                var (topicCount, examCount) = BackupService.ImportFromFile(dlg.FileName, replaceExisting);

                // 화면 전체 갱신
                LoadTopics();
                LoadReviewList();
                LoadExams();
                LoadDashboard();
                LoadStatistics();

                string mode = replaceExisting ? "교체" : "추가";
                string safetyMsg = replaceExisting 
                    ? "\n\n(안전 조치: 기존 DB는 실행 폴더의 Backups 폴더에 자동 백업되었습니다.)" 
                    : "";

                MessageBox.Show(
                    $"가져오기 완료! ({mode})\n\n학습 주제 {topicCount}건, 시험 {examCount}건이 반영되었습니다.{safetyMsg}",
                    "가져오기 성공", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"가져오기 실패\n\n{ex.Message}", "오류",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===================== 상세 통계 탭 =====================

        // 학습 주제 데이터를 분석해 통계 탭의 카드/차트/약점 Top 5를 갱신
        private void LoadStatistics()
        {
            using (var db = new StudyDbContext())
            {
                var topics = db.StudyTopics.ToList();
                var today = DateTime.Today;

                // ─── 요약 카드 4개 ───
                txtStatTotal.Text = topics.Count.ToString();
                txtStatReviewCount.Text = topics.Sum(t => t.RepetitionCount).ToString();
                txtStatUpcoming30.Text = topics.Count(t => t.NextReviewDate <= today.AddDays(30)).ToString();
                // 약점: 한 번 이상 복습한 주제 중 EF가 2.0 미만 (EF 기본값 2.5는 제외)
                txtStatWeak.Text = topics.Count(t => t.RepetitionCount > 0 && t.EaseFactor < 2.0).ToString();

                // ─── 차트 1: 월별 학습 추이 (최근 6개월) ───
                var months = Enumerable.Range(0, 6)
                    .Select(i => new DateTime(today.AddMonths(-5 + i).Year, today.AddMonths(-5 + i).Month, 1))
                    .ToArray();
                int[] monthCounts = months.Select(m =>
                    topics.Count(t => t.StudyDate.Year == m.Year && t.StudyDate.Month == m.Month)
                ).ToArray();
                string[] monthLabels = months.Select(m => $"{m.Month}월").ToArray();

                chartMonthly.LegendPosition = LegendPosition.Hidden;
                chartMonthly.TooltipPosition = TooltipPosition.Top;
                chartMonthly.Series = new ISeries[]
                {
                    new LineSeries<int>
                    {
                        Values = monthCounts,
                        Fill = null,
                        GeometrySize = 8,
                        LineSmoothness = 0.4,
                        Stroke = new SolidColorPaint(new SKColor(63, 81, 181)) { StrokeThickness = 3 },
                        GeometryStroke = new SolidColorPaint(new SKColor(63, 81, 181)) { StrokeThickness = 2 },
                        GeometryFill = new SolidColorPaint(SKColors.White)
                    }
                };
                // 다크모드에 따른 차트 텍스트 색상 결정
                bool isDark = SettingsService.Load().DarkMode;
                var labelPaint = new SolidColorPaint(isDark ? SKColors.White : new SKColor(33, 33, 33));

                chartMonthly.XAxes = new[]
                {
                    new Axis { Labels = monthLabels, LabelsRotation = 0, LabelsPaint = labelPaint }
                };
                chartMonthly.YAxes = new[]
                {
                    new Axis { MinLimit = 0, MinStep = 1, Labeler = v => $"{v:F0}개", LabelsPaint = labelPaint }
                };

                // ─── 차트 2: 요일별 학습 분포 ───
                // .NET DayOfWeek: 일=0, 월=1, ..., 토=6 → 월=0 시작으로 변환: (dow+6)%7
                string[] dayLabels = { "월", "화", "수", "목", "금", "토", "일" };
                int[] dayCounts = new int[7];
                foreach (var t in topics)
                {
                    int idx = ((int)t.StudyDate.DayOfWeek + 6) % 7;
                    dayCounts[idx]++;
                }

                chartDayOfWeek.LegendPosition = LegendPosition.Hidden;
                chartDayOfWeek.TooltipPosition = TooltipPosition.Top;
                chartDayOfWeek.Series = new ISeries[]
                {
                    new ColumnSeries<int>
                    {
                        Values = dayCounts,
                        Fill = new SolidColorPaint(new SKColor(38, 166, 154))  // teal
                    }
                };
                chartDayOfWeek.XAxes = new[]
                {
                    new Axis { Labels = dayLabels, LabelsRotation = 0, LabelsPaint = labelPaint }
                };
                chartDayOfWeek.YAxes = new[]
                {
                    new Axis { MinLimit = 0, MinStep = 1, Labeler = v => $"{v:F0}개", LabelsPaint = labelPaint }
                };

                // ─── 차트 3: 복습 진행도 분포 (0회, 1회, 2회, 3회, 4회+) ───
                string[] progressLabels = { "0회", "1회", "2회", "3회", "4회+" };
                int[] progressCounts = new[]
                {
                    topics.Count(t => t.RepetitionCount == 0),
                    topics.Count(t => t.RepetitionCount == 1),
                    topics.Count(t => t.RepetitionCount == 2),
                    topics.Count(t => t.RepetitionCount == 3),
                    topics.Count(t => t.RepetitionCount >= 4)
                };

                chartProgress.LegendPosition = LegendPosition.Hidden;
                chartProgress.TooltipPosition = TooltipPosition.Top;
                chartProgress.Series = new ISeries[]
                {
                    new ColumnSeries<int>
                    {
                        Values = progressCounts,
                        Fill = new SolidColorPaint(new SKColor(255, 167, 38))  // amber
                    }
                };
                chartProgress.XAxes = new[]
                {
                    new Axis { Labels = progressLabels, LabelsRotation = 0, LabelsPaint = labelPaint }
                };
                chartProgress.YAxes = new[]
                {
                    new Axis { MinLimit = 0, MinStep = 1, Labeler = v => $"{v:F0}개", LabelsPaint = labelPaint }
                };

                // ─── 약점 단원 Top 5 (한 번 이상 복습한 것 중 EF 낮은 순) ───
                var weakTop5 = topics
                    .Where(t => t.RepetitionCount > 0)
                    .OrderBy(t => t.EaseFactor)
                    .Take(5)
                    .ToList();
                dgWeakTopics.ItemsSource = weakTop5;
            }
        }

        // ===================== 키보드 단축키 (앱 수준) =====================

        // 전역 단축키 핸들러
        // Ctrl+1~5: 탭 전환  /  Ctrl+N: 학습주제 추가 포커스  /  Ctrl+E: 시험 추가 포커스
        // Ctrl+F: 학습주제 검색 포커스  /  Ctrl+D: 다크모드 토글
        // Ctrl+B: 백업 내보내기  /  Ctrl+I: 가져오기  /  Ctrl+,: 설정
        // F5: 새로고침
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if (ctrl)
            {
                switch (e.Key)
                {
                    case Key.D1: tabMain.SelectedIndex = 0; e.Handled = true; return;  // 대시보드
                    case Key.D2: tabMain.SelectedIndex = 1; e.Handled = true; return;  // 학습 주제
                    case Key.D3: tabMain.SelectedIndex = 2; e.Handled = true; return;  // 오늘의 복습
                    case Key.D4: tabMain.SelectedIndex = 3; e.Handled = true; return;  // 시험 D-Day
                    case Key.D5: tabMain.SelectedIndex = 4; e.Handled = true; return;  // 포모도로
                    case Key.D6: tabMain.SelectedIndex = 5; e.Handled = true; return;  // 캘린더
                    case Key.D7: tabMain.SelectedIndex = 6; e.Handled = true; return;  // 통계
                    case Key.N:
                        tabMain.SelectedIndex = 1;
                        Dispatcher.BeginInvoke(new Action(() => txtSubject.Focus()));
                        e.Handled = true; return;
                    case Key.E:
                        tabMain.SelectedIndex = 3;
                        Dispatcher.BeginInvoke(new Action(() => txtExamSubject.Focus()));
                        e.Handled = true; return;
                    case Key.F:
                        tabMain.SelectedIndex = 1;
                        Dispatcher.BeginInvoke(new Action(() => txtSearch.Focus()));
                        e.Handled = true; return;
                    case Key.D: btnTheme_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                    case Key.B: btnExport_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                    case Key.I: btnImport_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                    case Key.OemComma: btnSettings_Click(this, new RoutedEventArgs()); e.Handled = true; return;
                }
            }

            if (e.Key == Key.F5)
            {
                LoadTopics();
                LoadReviewList();
                LoadExams();
                LoadDashboard();
                LoadStatistics();
                e.Handled = true;
            }
        }

        // DataGrid 행에 포커스 두고 Delete 키 → 삭제 (학습 주제)
        private void dgTopics_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && dgTopics.SelectedItem is StudyTopic topic)
            {
                e.Handled = true;
                // 기존 삭제 핸들러 재사용 (Button.DataContext 패턴이라 가짜 버튼 생성)
                var fakeBtn = new Button { DataContext = topic };
                btnDeleteTopic_Click(fakeBtn, new RoutedEventArgs());
            }
            else if (e.Key == Key.F2 && dgTopics.SelectedItem is StudyTopic)
            {
                e.Handled = true;
                dgTopics_MouseDoubleClick(sender, null!);
            }
        }

        // DataGrid 행 + Delete/F2 (시험)
        private void dgExams_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && dgExams.SelectedItem is Exam exam)
            {
                e.Handled = true;
                var fakeBtn = new Button { DataContext = exam };
                btnDeleteExam_Click(fakeBtn, new RoutedEventArgs());
            }
            else if (e.Key == Key.F2 && dgExams.SelectedItem is Exam)
            {
                e.Handled = true;
                dgExams_MouseDoubleClick(sender, null!);
            }
        }

        // ===================== 폼 Enter 키로 제출 =====================

        // 학습 주제 입력 폼 — 어떤 필드에서든 Enter → 등록
        private void TopicForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !(sender is TextBox tb && tb.AcceptsReturn))
            {
                e.Handled = true;
                btnAdd_Click(this, new RoutedEventArgs());
            }
        }

        // 시험 입력 폼 — Enter → 시험 등록
        private void ExamForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                btnAddExam_Click(this, new RoutedEventArgs());
            }
        }

        // ===================== 연속 학습 일수 =====================

        // 오늘부터 거꾸로 매일 학습 주제가 등록되어 있는지 확인하여 연속 일수 반환
        // - 오늘 등록 X여도 어제까지 연속이면 streak는 어제까지의 일수
        private int CalculateStreak(List<StudyTopic> topics)
        {
            var dates = topics.Select(t => t.StudyDate.Date).ToHashSet();
            int streak = 0;
            var d = DateTime.Today;
            // 오늘이 비어있으면 어제부터 카운트 시작
            if (!dates.Contains(d)) d = d.AddDays(-1);
            while (dates.Contains(d))
            {
                streak++;
                d = d.AddDays(-1);
            }
            return streak;
        }

        // ===================== 대시보드 카드 클릭 → 탭 이동 =====================
        private void CardLearnTopics_Click(object sender, MouseButtonEventArgs e)  => tabMain.SelectedIndex = 1;
        private void CardTodayReview_Click(object sender, MouseButtonEventArgs e)  => tabMain.SelectedIndex = 2;
        private void CardNextExam_Click(object sender, MouseButtonEventArgs e)     => tabMain.SelectedIndex = 3;
        private void CardStreak_Click(object sender, MouseButtonEventArgs e)       => tabMain.SelectedIndex = 6;  // 통계 탭

        // ===================== 포모도로 타이머 =====================

        // 모드 선택 (집중 25 / 짧은 휴식 5 / 긴 휴식 15)
        private void btnPomodoroMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            int minutes = int.Parse(btn.Tag.ToString()!);
            pomodoroSelectedMinutes = minutes;
            pomodoroMode = minutes == 25 ? "집중"
                         : minutes == 5  ? "짧은 휴식"
                         : "긴 휴식";
            PomodoroReset();
        }

        // 시작 버튼
        private void btnPomodoroStart_Click(object sender, RoutedEventArgs e)
        {
            if (pomodoroTimer == null)
            {
                pomodoroTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                pomodoroTimer.Tick += PomodoroTick;
            }
            pomodoroTimer.Start();
            pomodoroRunning = true;
            btnPomodoroStart.IsEnabled = false;
            btnPomodoroPause.IsEnabled = true;
            txtPomodoroStatus.Text = $"⏳ {pomodoroMode} 진행 중...";
        }

        // 일시정지
        private void btnPomodoroPause_Click(object sender, RoutedEventArgs e)
        {
            pomodoroTimer?.Stop();
            pomodoroRunning = false;
            btnPomodoroStart.IsEnabled = true;
            btnPomodoroPause.IsEnabled = false;
            txtPomodoroStatus.Text = "⏸ 일시정지됨";
        }

        // 리셋 (현재 모드의 분으로 되돌림)
        private void btnPomodoroReset_Click(object sender, RoutedEventArgs e)
            => PomodoroReset();

        private void PomodoroReset()
        {
            pomodoroTimer?.Stop();
            pomodoroRunning = false;
            pomodoroRemainingSeconds = pomodoroSelectedMinutes * 60;
            btnPomodoroStart.IsEnabled = true;
            btnPomodoroPause.IsEnabled = false;
            txtPomodoroMode.Text = $"{pomodoroMode} 시간";

            // 모드별 색상 적용
            string color = pomodoroMode == "집중" ? "#5C6BC0"
                         : pomodoroMode == "짧은 휴식" ? "#26A69A"
                         : "#FFA726";
            var brush = (Brush)new BrushConverter().ConvertFromString(color)!;
            txtPomodoroMode.Foreground = brush;

            UpdatePomodoroDisplay();
            txtPomodoroStatus.Text = "시작 버튼을 눌러 시작하세요";
        }

        // 매초 호출 — 남은 시간 1초 차감, 0초 되면 세션 완료 처리
        private void PomodoroTick(object? sender, EventArgs e)
        {
            pomodoroRemainingSeconds--;
            UpdatePomodoroDisplay();

            if (pomodoroRemainingSeconds <= 0)
            {
                pomodoroTimer?.Stop();
                pomodoroRunning = false;

                // 집중 세션만 기록 (휴식은 기록하지 않음)
                if (pomodoroMode == "집중")
                {
                    PomodoroLogService.RecordSession(pomodoroSelectedMinutes);
                    trayService?.ShowNotification(
                        "🍅 집중 완료!",
                        $"{pomodoroSelectedMinutes}분 집중을 완료했습니다. 잠시 휴식하세요.");
                }
                else
                {
                    trayService?.ShowNotification(
                        "휴식 종료",
                        "다시 집중 모드로 돌아가볼까요?");
                }

                // 시스템 소리
                try { System.Media.SystemSounds.Asterisk.Play(); } catch { }

                txtPomodoroStatus.Text = "✅ 완료! 시작 버튼으로 다시 진행";
                btnPomodoroStart.IsEnabled = true;
                btnPomodoroPause.IsEnabled = false;
                pomodoroRemainingSeconds = pomodoroSelectedMinutes * 60;
                UpdatePomodoroDisplay();
                LoadPomodoroStats();
            }
        }

        private void UpdatePomodoroDisplay()
        {
            int min = pomodoroRemainingSeconds / 60;
            int sec = pomodoroRemainingSeconds % 60;
            txtPomodoroTime.Text = $"{min:D2}:{sec:D2}";
        }

        // 오늘·이번 주 누적 통계 갱신
        private void LoadPomodoroStats()
        {
            var (todaySessions, todayMinutes) = PomodoroLogService.TodayStats();
            var (weekSessions, weekMinutes) = PomodoroLogService.ThisWeekStats();
            txtTodaySessions.Text = todaySessions.ToString();
            txtTodayMinutes.Text = todayMinutes.ToString();
            txtWeekSessions.Text = weekSessions.ToString();
            txtWeekMinutes.Text = weekMinutes.ToString();
        }

        // ===================== 월간 캘린더 =====================

        // 현재 보고 있는 달의 42칸을 만들어 화면에 표시
        // - 시작은 월요일 (한국 캘린더 관례)
        // - 학습일/시험일/복습예정 마커 포함
        private void BuildCalendar()
        {
            if (calendarGrid == null) return;  // XAML 초기화 전 호출 방지

            // 타이틀
            txtCalendarTitle.Text = $"{calendarViewMonth.Year}년 {calendarViewMonth.Month}월";

            // 그 달의 1일이 무슨 요일인지 → 월요일 시작으로 정렬해서 시작 날짜 계산
            int firstDow = ((int)calendarViewMonth.DayOfWeek + 6) % 7;  // 월=0, 화=1, ..., 일=6
            DateTime gridStart = calendarViewMonth.AddDays(-firstDow);

            // DB에서 한 번에 끌어옴
            List<StudyTopic> topics;
            List<Exam> exams;
            using (var db = new StudyDbContext())
            {
                topics = db.StudyTopics.ToList();
                exams = db.Exams.ToList();
            }

            var today = DateTime.Today;
            var days = new List<CalendarDay>();
            for (int i = 0; i < 42; i++)
            {
                var d = gridStart.AddDays(i).Date;
                var dow = (int)d.DayOfWeek;
                var day = new CalendarDay
                {
                    Date = d,
                    IsCurrentMonth = d.Month == calendarViewMonth.Month,
                    IsToday = d == today,
                    IsWeekend = (dow == 0 || dow == 6),  // 일=0, 토=6
                    StudyCount = topics.Count(t => t.StudyDate.Date == d),
                    ReviewCount = topics.Count(t => t.NextReviewDate.Date == d),
                    ExamSubjects = exams.Where(x => x.ExamDate.Date == d).Select(x => x.Subject).ToList()
                };
                days.Add(day);
            }

            calendarGrid.ItemsSource = days;
        }

        private void btnCalendarPrev_Click(object sender, RoutedEventArgs e)
        {
            calendarViewMonth = calendarViewMonth.AddMonths(-1);
            BuildCalendar();
        }

        private void btnCalendarNext_Click(object sender, RoutedEventArgs e)
        {
            calendarViewMonth = calendarViewMonth.AddMonths(1);
            BuildCalendar();
        }

        private void btnCalendarToday_Click(object sender, RoutedEventArgs e)
        {
            calendarViewMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            BuildCalendar();
        }
    }
}
