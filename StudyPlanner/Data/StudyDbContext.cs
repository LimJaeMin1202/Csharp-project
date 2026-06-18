using Microsoft.EntityFrameworkCore;
using StudyPlanner.Models;

namespace StudyPlanner.Data
{
    // EF Core 데이터베이스 컨텍스트
    // - StudyTopic 객체와 SQLite 파일(studyplanner.db)을 연결해주는 통로
    public class StudyDbContext : DbContext
    {
        // StudyTopics 테이블: StudyTopic 객체들의 집합
        // (이 프로퍼티가 곧 DB의 테이블 하나가 됨)
        public DbSet<StudyTopic> StudyTopics { get; set; }

        // Exams 테이블: 시험 정보 집합
        public DbSet<Exam> Exams { get; set; }

        // DB 연결 방법 설정
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // 실행 파일 기준(BaseDirectory) 절대 경로를 사용해 작업 디렉터리에 관계없이 일관된 파일 보장
            string dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "studyplanner.db");
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
}
