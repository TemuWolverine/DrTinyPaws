# DrTinyPaws 💊

A cross-platform medication intake tracking application built with **.NET 10** and **Avalonia UI**, targeting **Windows** and **Android**.

---

## Features

### 1. 📊 Weekly Intake Dashboard (Top ~1/3rd, ~200px)
- **7-Day Stacked Bar Graph**: Each day of the week (Monday through Sunday) is represented by a bar.
- **Color Blocks**: Within each bar, every medication is stacked with its distinct custom color block (e.g. Panadeine in Blue, Anti-histamine in Red, Vitamin C in Amber).
- **Week Navigation**: Jump between weeks using `◀ Prev`, `Next ▶`, or return instantly to `Today`.
- **Live Tooltips & Legends**: Interactive hover and legend powered by **LiveCharts2**.

### 2. 📋 Chronological Intake Feed (Bottom ~2/3rds)
- Scrolling list showing all logged intakes, sorted with the **most recent at the top**.
- Displays medication name, dosage badge, intake count (e.g. `× 2`), relative/absolute timestamp (e.g. `Today, 08:15 AM`), notes, and an undo/delete action.

### 3. 💊 Defining Medications
- **Name**: Required (e.g., *Panadeine*, *Amoxicillin*).
- **Dosage**: Optional (e.g., *500mg*, *10ml*, *1 tablet*).
- **Colour**: Color palette swatches and custom hex code input.
- **Schedule**: Optional recurrence (e.g., *Once daily (Morning)*, *Twice daily*, *Every 4 to 6 hours*, *As needed*).
- **Reminder**: Optional time of day (e.g., *08:00* or *21:30*).
- **Instructions / Notes**: Optional guidelines.

### 4. ⚡ Logging Intakes & Grouped Doses ("Presets")
- **Quick Logging**: Select any active medication and adjust quantities with `[-]` and `[+]` steppers.
- **Time Selection**: Defaults to `Now`, but can be edited for past doses.
- **Grouped Doses (Shortcuts)**:
  - Configure presets such as "Morning Routine" (e.g., 3× Tablet A + 2× Tablet B).
  - Recall the preset with 1 click in the intake dialog to prefill medications and counts.
  - Or use the **Quick Presets Ribbon** on the main dashboard for instant 1-click recording.
  - Easily save any current selection as a new group shortcut directly from the intake form.

### 5. 🔄 Local-First & Multi-Device Sync (Syncthing / Cloud Compatible)
- Uses an embedded **SQLite** database (`drtinypaws.db`) with GUID primary keys to guarantee conflict-free multi-device synchronization.
- **Configurable Location**: In `Settings`, you can point DrTinyPaws to any directory synced by **Syncthing**, **Nextcloud**, **Google Drive**, or **OneDrive**.
- **Real-Time Change Detection**: Uses a background `FileSystemWatcher` to detect when an external sync program updates the database file, automatically reloading the dashboard without restarting.

### 6. 🔔 Native Notifications
- **Windows**: Dispatches native Windows Toast notifications when medication reminder times arrive.
- **Android**: Uses Android `NotificationCompat` and `NotificationChannel` with high-priority reminder alerts.

---

## Solution Structure

```
DrTinyPaws/
├── DrTinyPaws/                  # Core shared cross-platform library (.NET 10)
│   ├── Converters/            # Avalonia value converters (HexToBrush, DateTimeFormat)
│   ├── Models/                # Medication, MedicationIntake, DoseGroup, DoseGroupItem
│   ├── Services/              # IDataStore, SqliteDataStore, INotificationService
│   ├── ViewModels/            # MainViewModel, WeeklyChartViewModel, TakeMedicationViewModel, etc.
│   └── Views/                 # MainView.axaml, WeeklyChartView.axaml
├── DrTinyPaws.Desktop/          # Windows Desktop launcher (.NET 10)
│   ├── Services/              # WindowsNotificationService (PowerShell WinRT toasts)
│   └── Program.cs
├── DrTinyPaws.Android/          # Android launcher (.NET 10 - net10.0-android)
│   ├── AndroidNotificationService.cs
│   └── MainActivity.cs
└── DrTinyPaws.Tests/            # xUnit automated test suite (.NET 10)
```

---

## Running the Application

### Windows Desktop
```bash
dotnet run --project DrTinyPaws.Desktop/DrTinyPaws.Desktop.csproj
```

### Android (Emulator or USB Debugging)
```bash
dotnet build DrTinyPaws.Android/DrTinyPaws.Android.csproj -t:Run
```

### Running Automated Tests
```bash
dotnet test DrTinyPaws.slnx
```
