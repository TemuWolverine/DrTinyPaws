using System;

namespace DrTinyPaws.Models;

public class Medication
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Dosage { get; set; }
    public string ColorHex { get; set; } = string.Empty;
    public string? Schedule { get; set; }
    public string? ReminderTime { get; set; } // Formatted as "HH:mm" e.g. "08:00"
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
