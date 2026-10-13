using System.Threading.Tasks;

namespace DrTinyPaws.Services;

public record PillLoggerImportResult(
    bool Success,
    int MedicationsImported,
    int IntakesImported,
    int DoseGroupsImported,
    string? ErrorMessage = null
);

public interface IPillLoggerImporter
{
    Task<PillLoggerImportResult> ImportAsync(string pillLoggerDbPath, string targetDbPath, bool clearExistingData = true);
}
