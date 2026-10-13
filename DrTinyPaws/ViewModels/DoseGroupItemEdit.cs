using CommunityToolkit.Mvvm.ComponentModel;
using DrTinyPaws.Models;

namespace DrTinyPaws.ViewModels;

public partial class DoseGroupItemEdit : ObservableObject
{
    public Medication Medication { get; }

    [ObservableProperty]
    private int _quantity = 1;

    [ObservableProperty]
    private bool _isIncluded;

    public DoseGroupItemEdit(Medication medication, int quantity = 1, bool isIncluded = false)
    {
        Medication = medication;
        _quantity = quantity;
        _isIncluded = isIncluded;
    }
}
