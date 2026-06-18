using System.IO;
using System.Text.Json;
using StudyPlanner.Data;
using StudyPlanner.Models;

namespace StudyPlanner.Services
{
    // JSON 파일로 학습 주제와 시험 데이터를 내보내거나 불러오는 백업 서비스
    // - 한 파일에 두 종류 데이터 + 메타정보(버전, 내보낸 시각)을 함께 저장
    // - System.Text.Json (.NET 내장) 사용 → 외부 의존성 없음
    public static class BackupService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            // 한글이 \uXXXX로 이스케이프되지 않고 그대로 저장되도록
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        // 백업 파일 구조 (직렬화/역직렬화 대상)
        public class BackupData
        {
            public string Version { get; set; } = "1.0";
            public DateTime ExportedAt { get; set; } = DateTime.Now;
            public List<StudyTopic> Topics { get; set; } = new();
            public List<Exam> Exams { get; set; } = new();
        }

        // 현재 DB의 모든 데이터를 JSON으로 직렬화해 파일에 저장
        // 반환: (학습주제 건수, 시험 건수)
        public static (int topicCount, int examCount) ExportToFile(string filePath)
        {
            BackupData data;
            using (var db = new StudyDbContext())
            {
                data = new BackupData
                {
                    Topics = db.StudyTopics.ToList(),
                    Exams = db.Exams.ToList()
                };
            }
            File.WriteAllText(filePath, JsonSerializer.Serialize(data, JsonOptions));
            return (data.Topics.Count, data.Exams.Count);
        }

        // JSON 파일을 읽어 DB에 추가
        // - replaceExisting=true : 기존 데이터 모두 삭제 후 가져오기 (이때 자동 롤백 백업 생성)
        // - replaceExisting=false: 기존 유지 + 추가
        // 반환: (추가된 학습주제 건수, 추가된 시험 건수)
        public static (int topicCount, int examCount) ImportFromFile(string filePath, bool replaceExisting)
        {
            // ── 데이터 교체 전, 현재 DB 파일 자동 안전 백업 (FIFO 5개 유지) ──
            if (replaceExisting)
            {
                CreateAutoRollbackBackup();
            }

            var json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<BackupData>(json, JsonOptions);
            if (data == null)
                throw new InvalidDataException("백업 파일 형식이 올바르지 않습니다.");

            using (var db = new StudyDbContext())
            {
                if (replaceExisting)
                {
                    db.StudyTopics.RemoveRange(db.StudyTopics);
                    db.Exams.RemoveRange(db.Exams);
                    db.SaveChanges();
                }

                // Id는 새로 발급되도록 초기화 (기존 DB와 PK 충돌 방지)
                foreach (var t in data.Topics) t.Id = 0;
                foreach (var x in data.Exams) x.Id = 0;

                db.StudyTopics.AddRange(data.Topics);
                db.Exams.AddRange(data.Exams);
                db.SaveChanges();
            }
            return (data.Topics.Count, data.Exams.Count);
        }

        // 현재 SQLite DB 파일을 별도 폴더에 백업하고 5개만 유지한다.
        private static void CreateAutoRollbackBackup()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string dbPath = Path.Combine(baseDir, "studyplanner.db");
                
                if (!File.Exists(dbPath)) return;

                string backupDir = Path.Combine(baseDir, "Backups");
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                // 타임스탬프 기반 파일명 생성
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupPath = Path.Combine(backupDir, $"studyplanner_auto_{timestamp}.db.bak");

                // 파일 복사
                File.Copy(dbPath, backupPath, true);

                // ── 5개 초과 시 오래된 파일 삭제 (FIFO) ──
                var backupFiles = new DirectoryInfo(backupDir)
                                    .GetFiles("studyplanner_auto_*.db.bak")
                                    .OrderBy(f => f.CreationTime)
                                    .ToList();

                while (backupFiles.Count > 5)
                {
                    backupFiles[0].Delete();
                    backupFiles.RemoveAt(0);
                }
            }
            catch
            {
                // 백업 실패가 메인 흐름(임포트)을 막지는 않도록 예외 무시
            }
        }
    }
}
