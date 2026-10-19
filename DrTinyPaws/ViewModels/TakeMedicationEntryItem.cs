using DrTinyPaws.Models;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace DrTinyPaws.ViewModels;

public partial class TakeMedicationEntryItem : ReactiveObject
{
    [Reactive] public partial bool IsSelected { get; set; }
    public Medication Medication { get; }
    [Reactive] public partial int Quantity { get; set; }

    public TakeMedicationEntryItem(Medication medication, int quantity = 1, bool isSelected = false)
    {
        Medication = medication;
        Quantity = quantity;
        IsSelected = isSelected;
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

    [ReactiveCommand]
    public void Increment()
    {
        Quantity++;
        IsSelected = true;
    }
}