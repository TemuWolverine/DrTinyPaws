using Avalonia.Controls;
using ReactiveUI;
using DrTinyPaws.ViewModels;
using DrTinyPaws.Views;

namespace DrTinyPaws;

public class RxViewLocator : ReactiveUI.IViewLocator
{
    public IViewFor? ResolveView<T>(T? viewModel, string? contract = null) => viewModel switch
    {
        DashboardViewModel => new DashboardView(),
        TakeMedicationViewModel => new TakeMedicationView(),
        MedicationManagementViewModel => new MedicationManagementView(),
        MedicationEditViewModel => new MedicationEditView(),
        DoseGroupManagementViewModel => new DoseGroupManagementView(),
        SettingsViewModel => new SettingsView(),
        _ => null
    };
}
