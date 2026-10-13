using Avalonia.ReactiveUI;
using DrTinyPaws.ViewModels;

namespace DrTinyPaws.Views;

public partial class MedicationEditView : ReactiveUserControl<MedicationEditViewModel>
{
    public MedicationEditView()
    {
        InitializeComponent();
    }
}
