using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DrTinyPaws.Models;

namespace DrTinyPaws.Services;

public interface IDataStore
{
    string DatabasePath { get; }
    event EventHandler? DataChanged;

    Task InitializeAsync(bool seedSampleData = true);
    Task SetDatabasePathAsync(string newPath, bool seedSampleData = true);

    // Medications
    Task<List<Medication>> GetMedicationsAsync(bool includeInactive = false);
    Task<Medication?> GetMedicationByIdAsync(string id);
    Task SaveMedicationAsync(Medication medication);
    Task DeleteMedicationAsync(string id);

    // Intakes
    Task<List<MedicationIntake>> GetIntakesForDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<List<MedicationIntake>> GetRecentIntakesAsync(int limit = 200);
    Task LogIntakeAsync(MedicationIntake intake);
    Task LogIntakesAsync(IEnumerable<MedicationIntake> intakes);
    Task DeleteIntakeAsync(string id);

    // Dose Groups
    Task<List<DoseGroup>> GetDoseGroupsAsync();
    Task<DoseGroup?> GetDoseGroupByIdAsync(string id);
    Task SaveDoseGroupAsync(DoseGroup group);
    Task DeleteDoseGroupAsync(string id);
}
