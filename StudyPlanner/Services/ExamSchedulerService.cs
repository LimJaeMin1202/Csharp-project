using System;
using System.Collections.Generic;
using StudyPlanner.Models;

namespace StudyPlanner.Services
{
    // 시험 대비 복습 일정 생성 서비스 (순수 알고리즘 격리)
    public static class ExamSchedulerService
    {
        // 선택한 시험의 일정에 맞춰 해당 과목의 학습 주제들의 NextReviewDate를 분산 배치한다.
        // - referenceDate: 기준일 (기본값 DateTime.Today, 테스트성 향상을 위해 매개변수화)
        public static void DistributeReviewDates(List<StudyTopic> topics, Exam exam, DateTime? referenceDate = null)
        {
            if (topics == null || topics.Count == 0 || exam == null) return;

            DateTime today = referenceDate ?? DateTime.Today;
            int daysUntil = (exam.ExamDate.Date - today.Date).Days;

            if (daysUntil <= 1)
            {
                // 시험이 오늘/내일/지났으면 → 전부 오늘 복습하도록 재배치
                foreach (var t in topics)
                {
                    t.NextReviewDate = today.Date;
                }
            }
            else
            {
                // 시험일에서 역산: 마지막 주제는 시험 하루 전, 그 앞은 이틀 전... 순으로 분산 배치
                // 남은 일수가 학습 주제 수보다 적으면 순환하여 고루 배치
                int window = daysUntil - 1;  // 오늘 다음날 ~ 시험 전날
                for (int i = 0; i < topics.Count; i++)
                {
                    int daysBeforeExam = (i % window) + 1; // 1 ~ window
                    topics[i].NextReviewDate = exam.ExamDate.Date.AddDays(-daysBeforeExam);
                }
            }
        }
    }
}
