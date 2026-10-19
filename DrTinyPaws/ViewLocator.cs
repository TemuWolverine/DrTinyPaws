using Avalonia.Controls;
using Avalonia.Controls.Templates;
using DrTinyPaws.ViewModels;
using DrTinyPaws.Views;

namespace DrTinyPaws;

/// <summary>
/// Given a view model, returns the corresponding view.
/// Implemented with direct strongly-typed mapping for Native AOT and trimming compatibility.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param) => param switch
    {
        DashboardViewModel => new DashboardView(),
        TakeMedicationViewModel => new TakeMedicationView(),
        MedicationManagementViewModel => new MedicationManagementView(),
        MedicationEditViewModel => new MedicationEditView(),
        DoseGroupManagementViewModel => new DoseGroupManagementView(),
        SettingsViewModel => new SettingsView(),
        _ => param != null ? new TextBlock { Text = "Not Found: " + param.GetType().Name } : null
    };

    public bool Match(object? data) => data is ViewModelBase;
}