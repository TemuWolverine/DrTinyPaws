using System;

namespace DrTinyPaws.Models;

public class DoseGroupItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DoseGroupId { get; set; } = string.Empty;
    public string MedicationId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;

    // Auxiliary display properties
    public string MedicationName { get; set; } = string.Empty;
    public string MedicationColorHex { get; set; } = string.Empty;
    public string? MedicationDosage { get; set; }
}
