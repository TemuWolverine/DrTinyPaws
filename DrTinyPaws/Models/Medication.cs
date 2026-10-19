using System;

namespace DrTinyPaws.Models;

public class Medication
{
    public string ColorHex { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? Dosage { get; set; }
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public bool IsActive { get; set; } = true;
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? ReminderTime { get; set; }
    public string? Schedule { get; set; }
}