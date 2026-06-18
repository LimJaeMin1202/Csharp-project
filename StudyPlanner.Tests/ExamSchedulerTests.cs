using System;
using System.Collections.Generic;
using Xunit;
using StudyPlanner.Models;
using StudyPlanner.Services;

namespace StudyPlanner.Tests
{
    public class ExamSchedulerTests
    {
        [Fact]
        public void DistributeReviewDates_Should_Set_To_Today_When_Exam_Is_Today_Or_Tomorrow()
        {
            // Arrange: 기준일(오늘) = 2026-06-19
            DateTime today = new DateTime(2026, 6, 19);
            
            // 시험일이 당일인 경우
            var examToday = new Exam { ExamDate = today, Subject = "컴퓨터네트워크" };
            
            var topics = new List<StudyTopic>
            {
                new StudyTopic { Subject = "컴퓨터네트워크", Unit = "1단원" },
                new StudyTopic { Subject = "컴퓨터네트워크", Unit = "2단원" }
            };

            // Act
            ExamSchedulerService.DistributeReviewDates(topics, examToday, today);

            // Assert: 모든 주제가 당일 복습 예정으로 설정됨
            Assert.All(topics, t => Assert.Equal(today, t.NextReviewDate));
        }

        [Fact]
        public void DistributeReviewDates_Should_Distribute_Backward_When_Exam_Is_In_Future()
        {
            // Arrange: 기준일 = 2026-06-19
            DateTime today = new DateTime(2026, 6, 19);
            
            // 시험일이 5일 뒤 (2026-06-24)인 경우 -> window = 5 - 1 = 4일 (6/20 ~ 6/23)
            var exam = new Exam { ExamDate = new DateTime(2026, 6, 24), Subject = "자료구조" };

            var topics = new List<StudyTopic>
            {
                new StudyTopic { Subject = "자료구조", Unit = "스택" },      // index 0 -> 1일 전 (6/23)
                new StudyTopic { Subject = "자료구조", Unit = "큐" },        // index 1 -> 2일 전 (6/22)
                new StudyTopic { Subject = "자료구조", Unit = "연결리스트" }, // index 2 -> 3일 전 (6/21)
                new StudyTopic { Subject = "자료구조", Unit = "이진트리" },   // index 3 -> 4일 전 (6/20)
                new StudyTopic { Subject = "자료구조", Unit = "해시테이블" }  // index 4 -> index % 4 + 1 = 1일 전 (6/23)으로 순환
            };

            // Act
            ExamSchedulerService.DistributeReviewDates(topics, exam, today);

            // Assert
            Assert.Equal(new DateTime(2026, 6, 23), topics[0].NextReviewDate);
            Assert.Equal(new DateTime(2026, 6, 22), topics[1].NextReviewDate);
            Assert.Equal(new DateTime(2026, 6, 21), topics[2].NextReviewDate);
            Assert.Equal(new DateTime(2026, 6, 20), topics[3].NextReviewDate);
            Assert.Equal(new DateTime(2026, 6, 23), topics[4].NextReviewDate); // 순환 배치 작동 확인
        }
    }
}
