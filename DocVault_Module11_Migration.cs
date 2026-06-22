using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// ============================================================================
// DOCVAULT MODULE 11: MIGRATION & VERSIONING
// ============================================================================
// Data migration, schema versioning, system upgrades

namespace DocVault.Core.Migration
{
    public class MigrationLog
    {
        public Guid Id { get; set; }
        public int MigrationVersion { get; set; }
        public string MigrationName { get; set; }
        public DateTime ExecutedAt { get; set; }
        public bool IsSuccessful { get; set; }
        public string ErrorMessage { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }

    public class SchemaVersion
    {
        public int VersionNumber { get; set; }
        public string Description { get; set; }
        public DateTime AppliedAt { get; set; }
        public List<string> Changes { get; set; } = new();
    }

    public class DataMigration
    {
        public Guid Id { get; set; }
        public string MigrationName { get; set; }
        public int RecordsMigrated { get; set; }
        public int RecordsFailed { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime CompletionTime { get; set; }
    }

    public interface IMigrationService
    {
        Task<bool> ExecuteMigrationAsync(int targetVersion);
        Task<int> GetCurrentSchemaVersionAsync();
        Task<List<MigrationLog>> GetMigrationHistoryAsync(int limit = 50);
        Task<bool> RollbackMigrationAsync(int fromVersion, int toVersion);
        Task<bool> ValidateMigrationAsync(int targetVersion);
    }

    public class MigrationService : IMigrationService
    {
        private readonly IMigrationRepository _repository;
        private readonly List<IMigrationRunner> _migrations;

        public MigrationService(IMigrationRepository repository, List<IMigrationRunner> migrations)
        {
            _repository = repository;
            _migrations = migrations;
        }

        public async Task<bool> ExecuteMigrationAsync(int targetVersion)
        {
            try
            {
                var currentVersion = await GetCurrentSchemaVersionAsync();
                if (currentVersion >= targetVersion) return true;

                for (int v = currentVersion + 1; v <= targetVersion; v++)
                {
                    var runner = _migrations.Find(m => m.TargetVersion == v);
                    if (runner == null) continue;

                    var log = await RunMigrationAsync(runner);
                    if (!log.IsSuccessful) return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<int> GetCurrentSchemaVersionAsync()
        {
            return await _repository.GetCurrentVersionAsync();
        }

        public async Task<List<MigrationLog>> GetMigrationHistoryAsync(int limit = 50)
        {
            return await _repository.GetMigrationHistoryAsync(limit);
        }

        public async Task<bool> RollbackMigrationAsync(int fromVersion, int toVersion)
        {
            if (toVersion >= fromVersion) return false;

            for (int v = fromVersion; v > toVersion; v--)
            {
                var runner = _migrations.Find(m => m.TargetVersion == v);
                if (runner == null) continue;

                var result = await runner.RollbackAsync();
                if (!result) return false;
            }

            return true;
        }

        public async Task<bool> ValidateMigrationAsync(int targetVersion)
        {
            var runner = _migrations.Find(m => m.TargetVersion == targetVersion);
            if (runner == null) return false;

            return await runner.ValidateAsync();
        }

        private async Task<MigrationLog> RunMigrationAsync(IMigrationRunner runner)
        {
            var startTime = DateTime.UtcNow;
            var log = new MigrationLog
            {
                Id = Guid.NewGuid(),
                MigrationVersion = runner.TargetVersion,
                MigrationName = runner.MigrationName,
                ExecutedAt = startTime
            };

            try
            {
                await runner.ExecuteAsync();
                log.IsSuccessful = true;
            }
            catch (Exception ex)
            {
                log.IsSuccessful = false;
                log.ErrorMessage = ex.Message;
            }

            log.ExecutionTime = DateTime.UtcNow - startTime;
            await _repository.LogMigrationAsync(log);
            return log;
        }
    }

    public interface IMigrationRunner
    {
        int TargetVersion { get; }
        string MigrationName { get; }
        Task ExecuteAsync();
        Task<bool> RollbackAsync();
        Task<bool> ValidateAsync();
    }

    public interface IMigrationRepository
    {
        Task<int> GetCurrentVersionAsync();
        Task<List<MigrationLog>> GetMigrationHistoryAsync(int limit);
        Task LogMigrationAsync(MigrationLog log);
        Task RecordVersionAsync(SchemaVersion version);
    }
}
