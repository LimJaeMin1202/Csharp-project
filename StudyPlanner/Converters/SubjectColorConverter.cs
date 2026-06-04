using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using StudyPlanner.Services;
using Brushes = System.Windows.Media.Brushes;

namespace StudyPlanner.Converters
{
    // XAML 바인딩에서 과목명을 색상 Brush로 변환
    // 사용: <Rectangle Fill="{Binding Subject, Converter={StaticResource SubjectColorConverter}}"/>
    public class SubjectColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string subject)
                return SubjectColorService.GetBrush(subject);
            return Brushes.Transparent;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
