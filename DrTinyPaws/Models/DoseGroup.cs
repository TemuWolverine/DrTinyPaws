using System;
using System.Collections.Generic;

namespace DrTinyPaws.Models;

public class DoseGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty; // e.g. "Morning Dose"
    public string? Description { get; set; }
    public string? ColorHex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<DoseGroupItem> Items { get; set; } = [];
}
