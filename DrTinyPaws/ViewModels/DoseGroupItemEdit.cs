using ReactiveUI;
using ReactiveUI.SourceGenerators;
using DrTinyPaws.Models;

namespace DrTinyPaws.ViewModels;

public partial class DoseGroupItemEdit : ReactiveObject
{
    public Medication Medication { get; }

    [Reactive]
    public partial int Quantity { get; set; }

    [Reactive]
    public partial bool IsIncluded { get; set; }

    public DoseGroupItemEdit(Medication medication, int quantity = 1, bool isIncluded = false)
    {
        Medication = medication;
        Quantity = quantity;
        IsIncluded = isIncluded;
    }
}
