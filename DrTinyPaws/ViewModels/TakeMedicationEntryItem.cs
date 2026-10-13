using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrTinyPaws.Models;

namespace DrTinyPaws.ViewModels;

public partial class TakeMedicationEntryItem : ObservableObject
{
    public Medication Medication { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _quantity = 1;

    public TakeMedicationEntryItem(Medication medication, int quantity = 1, bool isSelected = false)
    {
        Medication = medication;
        _quantity = quantity;
        _isSelected = isSelected;
    }

    [RelayCommand]
    public void Increment()
    {
        Quantity++;
        IsSelected = true;
    }

    [RelayCommand]
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
