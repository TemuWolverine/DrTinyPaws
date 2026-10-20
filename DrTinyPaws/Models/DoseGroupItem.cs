using System;

namespace DrTinyPaws.Models;

public class DoseGroupItem
{
    public string DoseGroupId { get; set; } = string.Empty;
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string MedicationColorHex { get; set; } = string.Empty;
    public string? MedicationDosage { get; set; }
    public string MedicationId { get; set; } = string.Empty;

    // Auxiliary display properties
    public string MedicationName { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}