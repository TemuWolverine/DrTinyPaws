using System;
using System.Diagnostics;
using System.Threading.Tasks;
using DrTinyPaws.Services;

namespace DrTinyPaws.Desktop.Services;

public class WindowsNotificationService : DefaultNotificationService
{
    public override Task SendNotificationAsync(string title, string message)
    {
        base.SendNotificationAsync(title, message);

        Task.Run(() =>
        {
            try
            {
                // Escape quotes for PowerShell
                var safeTitle = title.Replace("\"", "`\"");
                var safeMessage = message.Replace("\"", "`\"");

                // Native Windows Toast via PowerShell script
                var psScript = $@"
                    [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
                    [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
                    $template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
                    $textNodes = $template.GetElementsByTagName('text')
                    $textNodes.Item(0).AppendChild($template.CreateTextNode('{safeTitle}')) | Out-Null
                    $textNodes.Item(1).AppendChild($template.CreateTextNode('{safeMessage}')) | Out-Null
                    $toast = [Windows.UI.Notifications.ToastNotification]::new($template)
                    [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('DrTinyPaws').Show($toast)
                ";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{psScript}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(startInfo);
                proc?.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing Windows toast: {ex.Message}");
            }
        });

        return Task.CompletedTask;
    }
}
