using Xunit;
using StudyPlanner.Services;
using System.Windows.Media;

namespace StudyPlanner.Tests
{
    public class SubjectColorTests
    {
        [Theory]
        [InlineData("자료구조")]
        [InlineData("알고리즘")]
        [InlineData("운영체제")]
        [InlineData("")]
        [InlineData(null)]
        public void GetHex_Should_Be_Consistent_For_Same_Subject(string? subject)
        {
            // Act
            string hex1 = SubjectColorService.GetHex(subject);
            string hex2 = SubjectColorService.GetHex(subject);

            // Assert: 몇 번을 호출해도 동일한 과목 이름은 완벽히 똑같은 일관된 색상을 보장해야 함
            Assert.Equal(hex1, hex2);
            Assert.StartsWith("#", hex1);
            Assert.True(hex1.Length == 7);
        }

        [Fact]
        public void GetBrush_Should_Return_SolidColorBrush()
        {
            // Act
            var brush = SubjectColorService.GetBrush("자료구조");

            // Assert: WPF의 SolidColorBrush를 정상적으로 반환하는지 검증
            Assert.NotNull(brush);
            Assert.IsType<SolidColorBrush>(brush);
        }
    }
}
