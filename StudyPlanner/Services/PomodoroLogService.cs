using System.IO;
using System.Text.Json;

namespace StudyPlanner.Services
{
    // 포모도로 세션 기록 (JSON 파일 영속화)
    // - 매 집중 세션 완료 시 1건 추가
    // - 통계용: 오늘/이번주 총 집중 시간 계산
    public static class PomodoroLogService
    {
        private const string FileName = "pomodoro.json";

        public class Session
        {
            public DateTime CompletedAt { get; set; }
            public int Minutes { get; set; }
            public string? Subject { get; set; }   // 세션에 태깅한 과목 (없으면 null)
        }

        private static List<Session>? cached;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // 전체 세션 불러오기 (캐싱)
        public static List<Session> LoadAll()
        {
            if (cached != null) return cached;
            if (!File.Exists(FileName))
            {
                cached = new List<Session>();
                return cached;
            }
            try
            {
                cached = JsonSerializer.Deserialize<List<Session>>(File.ReadAllText(FileName))
                         ?? new List<Session>();
            }
            catch
            {
                cached = new List<Session>();
            }
            return cached;
        }

        // 세션 1건 추가 + 파일 저장
        public static void RecordSession(int minutes, string? subject = null)
        {
            var list = LoadAll();
            list.Add(new Session
            {
                CompletedAt = DateTime.Now,
                Minutes = minutes,
                Subject = subject
            });
            File.WriteAllText(FileName, JsonSerializer.Serialize(list, JsonOptions));
        }

        // 오늘 누적 (세션 수, 분)
        public static (int sessions, int minutes) TodayStats()
        {
            var today = DateTime.Today;
            var list = LoadAll().Where(s => s.CompletedAt.Date == today).ToList();
            return (list.Count, list.Sum(s => s.Minutes));
        }

        // 이번 주 (월요일 시작) 누적
        public static (int sessions, int minutes) ThisWeekStats()
        {
            var today = DateTime.Today;
            int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            var weekStart = today.AddDays(-daysSinceMonday);
            var list = LoadAll().Where(s => s.CompletedAt.Date >= weekStart).ToList();
            return (list.Count, list.Sum(s => s.Minutes));
        }
    }
}
