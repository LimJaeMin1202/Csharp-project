namespace StudyPlanner.Models
{
    // 캘린더 한 칸을 표현하는 뷰모델 (DB 비저장, 화면 표시 전용)
    public class CalendarDay
    {
        public DateTime Date { get; set; }
        public int Day => Date.Day;
        public bool IsCurrentMonth { get; set; }   // 현재 보고 있는 달의 날인지
        public bool IsToday { get; set; }
        public bool IsWeekend { get; set; }

        public int StudyCount { get; set; }        // 이 날 학습 주제 등록 수
        public int ReviewCount { get; set; }       // 이 날 복습 예정 수
        public List<string> ExamSubjects { get; set; } = new();  // 이 날 시험 과목들

        // 표시용 헬퍼
        public bool HasStudy => StudyCount > 0;
        public bool HasReview => ReviewCount > 0;
        public bool HasExam => ExamSubjects.Count > 0;
        public string ExamText => ExamSubjects.Count > 0 ? "📝 " + string.Join(", ", ExamSubjects) : "";
        public string StudyText => StudyCount > 0 ? $"📚 {StudyCount}" : "";
        public string ReviewText => ReviewCount > 0 ? $"🔁 {ReviewCount}" : "";

        // ToolTip용 종합 텍스트
        public string TooltipText
        {
            get
            {
                var parts = new List<string> { Date.ToString("yyyy/MM/dd (ddd)") };
                if (HasStudy) parts.Add($"학습 등록: {StudyCount}건");
                if (HasReview) parts.Add($"복습 예정: {ReviewCount}건");
                if (HasExam) parts.Add($"시험: {string.Join(", ", ExamSubjects)}");
                return string.Join("\n", parts);
            }
        }
    }
}
