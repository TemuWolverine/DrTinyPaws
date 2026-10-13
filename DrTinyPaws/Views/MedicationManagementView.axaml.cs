using Avalonia.ReactiveUI;
using DrTinyPaws.ViewModels;

namespace DrTinyPaws.Views;

public partial class MedicationManagementView : ReactiveUserControl<MedicationManagementViewModel>
{
    public MedicationManagementView()
    {
        InitializeComponent();
    }
}
