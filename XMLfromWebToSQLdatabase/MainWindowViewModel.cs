using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Avalonia.Threading;
using System.Windows.Input;

using XMLfromWebToSQLdatabase.Services;
namespace XMLfromWebToSQLdatabase.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly XmlImportService _xmlImportService;
    private readonly Timer _pollTimer;
    private DateTime? _nextRetrievalTime;

    private string _xmlUrl = "";
    private TimeSpan? _retrievalInterval = TimeSpan.FromHours(1);   // 1 hour default interval
    private string _nextRetrievalText = "N/A";

    public string XmlUrl
    {
        get => _xmlUrl;
        set
        {
            if (_xmlUrl != value)
            {
                _xmlUrl = value;
                OnPropertyChanged();
            }
        }
    }

    public TimeSpan? RetrievalInterval
    {
        get => _retrievalInterval;
        set
        {
            // Only allow positive intervals or null (disabled). Zero or negative intervals are not valid.
            if (_retrievalInterval != value && value != new TimeSpan(0))
            {
                _retrievalInterval = value;
                OnPropertyChanged();
                UpdateNextRetrievalText();
            }
        }
    }

    public string NextRetrievalText
    {
        get => _nextRetrievalText;
        set
        {
            if (_nextRetrievalText != value)
            {
                _nextRetrievalText = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<string> LogEntries { get; } = new();

    public ICommand StartRetrievalCommand { get; }

    public MainWindowViewModel(XmlImportService xmlImportService)
    {
        _xmlImportService = xmlImportService;

        /* When triggered from the UI "Immediate XML retrieval" button we do not want
         to update the scheduled next retrieval time. Pass false so the next
         retrieval time is only updated when the scheduled timer fires. */
        StartRetrievalCommand = new RelayCommand(async () => await StartRetrievalAsync(false));

        LogEntries.Add($"Application started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}.");

        /* Timer will be scheduled for the next retrieval
         time by ScheduleNextTimer. The callback dispatches the check to the UI thread
         and then reschedules itself. */
        _pollTimer = new Timer(async _ =>
        {
            try
            {
                await Dispatcher.UIThread.InvokeAsync(async () => await TriggerIfDueAsync());
            }
            // After handling, schedule the next run based on the (possibly updated) interval
            finally
            {
                ScheduleNextTimer();
            }
        }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        UpdateNextRetrievalText();
        ScheduleNextTimer();
    }

    private void UpdateNextRetrievalText()
    {
        // Back to default
        if (RetrievalInterval is null)
        {
            NextRetrievalText = "N/A";
            return;
        }

        _nextRetrievalTime = DateTime.Now.Add(RetrievalInterval.Value);
        NextRetrievalText = _nextRetrievalTime.Value.ToString("yyyy-MM-dd HH:mm");
    }

    private async Task StartRetrievalAsync(bool retrievalNeedsUpdate = true)
    {
        if (string.IsNullOrWhiteSpace(XmlUrl))
        {
            LogEntries.Add("Please enter a valid URL.");
            return;
        }

        LogEntries.Add($"XML retrieval started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        try
        {
            await _xmlImportService.ImportAsync(XmlUrl);
            LogEntries.Add($"XML retrieval successful at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        }
        catch (Exception ex)
        {
            LogEntries.Add($"XML retrieval failed: {ex.Message}");
        }
        finally
        {
            // Only update the scheduled next retrieval time when part of the run, not when triggered from the button
            if (retrievalNeedsUpdate)
            {
                // Recount the next retrieval time for the next run
                UpdateNextRetrievalText();
                ScheduleNextTimer();
            }
        }
    }

    private async Task TriggerIfDueAsync()
    {
        if (RetrievalInterval is null)
            return;

        if (!_nextRetrievalTime.HasValue)
        {
            UpdateNextRetrievalText();
            return;
        }

        if (DateTime.Now >= _nextRetrievalTime.Value)
        {
            // Run the retrieval on the UI thread (this method is already invoked on UI thread)
            await StartRetrievalAsync();
        }
    }

    private void ScheduleNextTimer()
    {
        if (RetrievalInterval is null)
            return;

        if (!_nextRetrievalTime.HasValue)
            UpdateNextRetrievalText();

        var due = _nextRetrievalTime!.Value - DateTime.Now;
        if (due < TimeSpan.Zero)
            due = TimeSpan.Zero;

        // Schedule timer for the computed due time
        _pollTimer.Change(due, Timeout.InfiniteTimeSpan);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // Dispose the timer to stop scheduled work when the view model is no longer used
    public void Dispose()
    {
        _pollTimer?.Dispose();
    }
}
