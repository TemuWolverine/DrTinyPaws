using ReactiveUI;
using ReactiveUI.SourceGenerators;
using DrTinyPaws.Models;

namespace DrTinyPaws.ViewModels;

public partial class TakeMedicationEntryItem : ReactiveObject
{
    public Medication Medication { get; }

    [Reactive]
    public partial bool IsSelected { get; set; }

    [Reactive]
    public partial int Quantity { get; set; }

    public TakeMedicationEntryItem(Medication medication, int quantity = 1, bool isSelected = false)
    {
        Medication = medication;
        Quantity = quantity;
        IsSelected = isSelected;
    }

    [ReactiveCommand]
    public void Increment()
    {
        Quantity++;
        IsSelected = true;
    }

    [ReactiveCommand]
    public void Decrement()
    {
        if (Quantity > 1)
        {
            Quantity--;
        }
        else if (Quantity == 1)
        {
            Quantity = 0;
            IsSelected = false;
        }
    }
}
