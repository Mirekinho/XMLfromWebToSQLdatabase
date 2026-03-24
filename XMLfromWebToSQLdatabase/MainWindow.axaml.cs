using Avalonia.Controls;
using System;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using XMLfromWebToSQLdatabase.Services;
using XMLfromWebToSQLdatabase.ViewModels;

namespace XMLfromWebToSQLdatabase
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Create the XML import service and view model, then assign as DataContext
            /* Use a cross-platform application data folder instead of the current directory
             - Windows 7/10/11: C:\Users\<USERNAME>\AppData\Local\XMLfromWebToSQLdatabase
             - Linux: /home/<USERNAME>/.local/share/XMLfromWebToSQLdatabase
             - macOS: /Users/<USERNAME>/Library/Application Support/XMLfromWebToSQLdatabase */
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(appData, "XMLfromWebToSQLdatabase");
            Directory.CreateDirectory(appFolder);
            var databaseFile = Path.Combine(appFolder, "xml_downloads.db");
            var xmlImportService = new XmlImportService(new HttpClient(), databaseFile);
            DataContext = new MainWindowViewModel(xmlImportService);

            // Clean up view model resources (like timers) when the window closes
            this.Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            List<string>? logEntriesToWrite = null;
            // If the DataContext is the view model, copy its log entries before disposing
            if (DataContext is ViewModels.MainWindowViewModel vm)
            {
                logEntriesToWrite = new List<string>(vm.LogEntries);
                logEntriesToWrite.Add($"Application closed at {DateTime.Now:yyyy-MM-dd HH:mm:ss}.");
            }

            if (DataContext is IDisposable d)
            {
                d.Dispose();
            }

            // Write the copied log entries to a timestamped file in the application data folder
            try
            {
                if (logEntriesToWrite != null && logEntriesToWrite.Count > 0)
                {
                    var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    var appFolder = Path.Combine(appData, "XMLfromWebToSQLdatabase");
                    Directory.CreateDirectory(appFolder);

                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                    var logFile = Path.Combine(appFolder, $"log_{timestamp}.txt");
                    File.WriteAllLines(logFile, logEntriesToWrite);
                }
            }
            catch (Exception ex)
            {
                // FOR DEVELEOPMENT
                //System.Diagnostics.Debug.WriteLine($"Failed writing shutdown log: {ex}");
            }
        }
    }
}
