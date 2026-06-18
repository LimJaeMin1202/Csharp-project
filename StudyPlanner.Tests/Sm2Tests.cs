using System;
using Xunit;
using StudyPlanner.Models;
using StudyPlanner.Services;

namespace StudyPlanner.Tests
{
    public class Sm2Tests
    {
        [Fact]
        public void ApplyReview_Should_Initialize_To_1_Day_On_First_Success()
        {
            // Arrange
            var topic = new StudyTopic
            {
                RepetitionCount = 0,
                EaseFactor = 2.5,
                IntervalDays = 0,
                NextReviewDate = DateTime.Today
            };

            // Act: 4점 자가 평가 (성공)
            Sm2Service.ApplyReview(topic, 4);

            // Assert
            Assert.Equal(1, topic.RepetitionCount);
            Assert.Equal(1, topic.IntervalDays);
            Assert.Equal(DateTime.Today.AddDays(1), topic.NextReviewDate);
        }

        [Fact]
        public void ApplyReview_Should_Set_6_Days_On_Second_Success()
        {
            // Arrange
            var topic = new StudyTopic
            {
                RepetitionCount = 1,
                EaseFactor = 2.5,
                IntervalDays = 1,
                NextReviewDate = DateTime.Today
            };

            // Act: 5점 자가 평가 (완벽히 기억함)
            Sm2Service.ApplyReview(topic, 5);

            // Assert
            Assert.Equal(2, topic.RepetitionCount);
            Assert.Equal(6, topic.IntervalDays);
            Assert.Equal(DateTime.Today.AddDays(6), topic.NextReviewDate);
        }

        [Fact]
        public void ApplyReview_Should_Multiply_By_EaseFactor_On_Third_Success()
        {
            // Arrange
            var topic = new StudyTopic
            {
                RepetitionCount = 2,
                EaseFactor = 2.0,
                IntervalDays = 6,
                NextReviewDate = DateTime.Today
            };

            // Act: 4점 자가 평가 (성공, EF가 2.0이므로 6 * 2.0 = 12)
            Sm2Service.ApplyReview(topic, 4);

            // Assert
            Assert.Equal(3, topic.RepetitionCount);
            Assert.Equal(12, topic.IntervalDays);
            Assert.Equal(DateTime.Today.AddDays(12), topic.NextReviewDate);
        }

        [Fact]
        public void ApplyReview_Should_Reset_When_Review_Fails()
        {
            // Arrange
            var topic = new StudyTopic
            {
                RepetitionCount = 5,
                EaseFactor = 2.4,
                IntervalDays = 30,
                NextReviewDate = DateTime.Today
            };

            // Act: 2점 자가 평가 (기억 실패)
            Sm2Service.ApplyReview(topic, 2);

            // Assert
            Assert.Equal(0, topic.RepetitionCount); // 리셋
            Assert.Equal(1, topic.IntervalDays);     // 1일로 복귀
            Assert.Equal(DateTime.Today.AddDays(1), topic.NextReviewDate);
        }

        [Fact]
        public void ApplyReview_Should_Enforce_Minimum_EaseFactor_Of_1_3()
        {
            // Arrange
            var topic = new StudyTopic
            {
                RepetitionCount = 1,
                EaseFactor = 1.3, // 이미 하한선 근처
                IntervalDays = 1,
                NextReviewDate = DateTime.Today
            };

            // Act: 0점 자가 평가 (전혀 기억 안남 -> EaseFactor 대폭 감소 유도)
            Sm2Service.ApplyReview(topic, 0);

            // Assert
            Assert.True(topic.EaseFactor >= 1.3);
            Assert.Equal(1.3, topic.EaseFactor); // 하한선 1.3 보장 확인
        }
    }
}
