using DrTinyPaws.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DrTinyPaws.Services;

public interface IDataStore
{
    event EventHandler? DataChanged;

    string DatabasePath { get; }

    Task DeleteDoseGroupAsync(string id);

    Task DeleteIntakeAsync(string id);

    Task DeleteMedicationAsync(string id);

    Task<DoseGroup?> GetDoseGroupByIdAsync(string id);

    // Dose Groups
    Task<List<DoseGroup>> GetDoseGroupsAsync();

    // Intakes
    Task<List<MedicationIntake>> GetIntakesForDateRangeAsync(DateTime startDate, DateTime endDate);

    Task<Medication?> GetMedicationByIdAsync(string id);

    // Medications
    Task<List<Medication>> GetMedicationsAsync(bool includeInactive = false);

    Task<List<Medication>> GetLastAddedMedicationsAsync(int count = 4);

    Task<List<MedicationIntake>> GetRecentIntakesAsync(int limit = 200);

    // Import
    Task<PillLoggerImportResult> ImportPillLoggerDatabaseAsync(string sourceDbPath, bool clearExistingData = true);

    Task InitializeAsync(bool seedSampleData = true);

    Task LogIntakeAsync(MedicationIntake intake);

    Task LogIntakesAsync(IEnumerable<MedicationIntake> intakes);

    Task SaveDoseGroupAsync(DoseGroup group);

    Task SaveMedicationAsync(Medication medication);

    Task SetDatabasePathAsync(string newPath, bool seedSampleData = true);
}