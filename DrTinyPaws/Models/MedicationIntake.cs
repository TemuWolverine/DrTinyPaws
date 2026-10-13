using System;

namespace DrTinyPaws.Models;

public class MedicationIntake
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MedicationId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public DateTime TakenAt { get; set; } = DateTime.Now;
    public string? Notes { get; set; }
    
    // Auxiliary fields populated when loading for display
    public string MedicationName { get; set; } = string.Empty;
    public string MedicationColorHex { get; set; } = string.Empty;
    public string? MedicationDosage { get; set; }
}
