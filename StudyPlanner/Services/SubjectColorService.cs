using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using SkiaSharp;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace StudyPlanner.Services
{
    // 과목명 → 색상 결정 (하이브리드 방식)
    // 1. 우선순위: 사용자가 지정한 색상 (subject_colors.json)
    // 2. 차선순위: 과목명 해시 기반 자동 색상 (Palette)
    public static class SubjectColorService
    {
        private const string ConfigFileName = "subject_colors.json";

        private static readonly string[] Palette = new[]
        {
            "#5C6BC0", "#26A69A", "#EC407A", "#FFA726", "#42A5F5", "#66BB6A",
            "#AB47BC", "#FF7043", "#26C6DA", "#9CCC65", "#7E57C2", "#FFCA28",
        };

        // 메모리 캐시: 과목명 -> Hex색상
        private static Dictionary<string, string>? customColors;

        private static void LoadCustomColors()
        {
            if (customColors != null) return;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            if (!File.Exists(path))
            {
                customColors = new Dictionary<string, string>();
                return;
            }
            try
            {
                customColors = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))
                               ?? new Dictionary<string, string>();
            }
            catch { customColors = new Dictionary<string, string>(); }
        }

        // 특정 과목에 원하는 색상을 강제 지정 (영속화)
        public static void SetCustomColor(string subject, string hexColor)
        {
            LoadCustomColors();
            if (string.IsNullOrEmpty(subject)) return;
            customColors![subject] = hexColor;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            File.WriteAllText(path, JsonSerializer.Serialize(customColors, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static string GetHex(string? subject)
        {
            if (string.IsNullOrEmpty(subject)) return "#9E9E9E";
            
            LoadCustomColors();
            if (customColors!.TryGetValue(subject, out string? hex)) return hex;

            int hash = 0;
            foreach (char c in subject) hash = hash * 31 + c;
            int idx = Math.Abs(hash) % Palette.Length;
            return Palette[idx];
        }

        public static SolidColorBrush GetBrush(string? subject)
        {
            var color = (Color)ColorConverter.ConvertFromString(GetHex(subject));
            return new SolidColorBrush(color);
        }

        public static SKColor GetSkColor(string? subject)
        {
            var hex = GetHex(subject).TrimStart('#');
            return SKColor.Parse(hex);
        }
    }
}
