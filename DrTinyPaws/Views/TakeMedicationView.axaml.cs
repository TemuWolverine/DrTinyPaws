using Avalonia.ReactiveUI;
using DrTinyPaws.ViewModels;

namespace DrTinyPaws.Views;

public partial class TakeMedicationView : ReactiveUserControl<TakeMedicationViewModel>
{
    public TakeMedicationView()
    {
        InitializeComponent();
    }
}
