using System.Windows.Media;
using SkiaSharp;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace StudyPlanner.Services
{
    // 과목명 → 일관된 색상 결정 (해시 기반)
    // - 같은 과목명은 항상 같은 색으로 매핑됨 (별도 저장 불필요)
    // - Material Design 400 톤 팔레트 12색 사용 (라이트·다크 모드 둘 다 어울림)
    public static class SubjectColorService
    {
        private static readonly string[] Palette = new[]
        {
            "#5C6BC0",  // Indigo
            "#26A69A",  // Teal
            "#EC407A",  // Pink
            "#FFA726",  // Orange
            "#42A5F5",  // Blue
            "#66BB6A",  // Green
            "#AB47BC",  // Purple
            "#FF7043",  // Deep Orange
            "#26C6DA",  // Cyan
            "#9CCC65",  // Light Green
            "#7E57C2",  // Deep Purple
            "#FFCA28",  // Amber
        };

        // 과목명 → 팔레트 색상 16진수 문자열 (예: "#5C6BC0")
        public static string GetHex(string? subject)
        {
            if (string.IsNullOrEmpty(subject)) return "#9E9E9E";  // 기본 회색
            int hash = 0;
            foreach (char c in subject) hash = hash * 31 + c;
            int idx = Math.Abs(hash) % Palette.Length;
            return Palette[idx];
        }

        // WPF 바인딩용 SolidColorBrush
        public static SolidColorBrush GetBrush(string? subject)
        {
            var color = (Color)ColorConverter.ConvertFromString(GetHex(subject));
            return new SolidColorBrush(color);
        }

        // LiveCharts2(SkiaSharp)용 SKColor
        public static SKColor GetSkColor(string? subject)
        {
            var hex = GetHex(subject).TrimStart('#');
            return SKColor.Parse(hex);
        }
    }
}
