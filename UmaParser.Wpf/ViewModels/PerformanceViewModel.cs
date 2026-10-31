using UmaParser.Analysis;
using UmaParser.Import;
using UmaParser.Mvvm;

namespace UmaParser.ViewModels;

/// <summary>One entry in the Performance sort box.</summary>
public sealed class PerformanceSortChoice
{
    public PerformanceSortChoice(PerformanceSortKey key, string label)
    {
        Key = key;
        Label = label;
    }

    public PerformanceSortKey Key { get; }
    public string Label { get; }

    public override string ToString() => Label;
}

/// <summary>
/// Performance tab state. A consistent roster fills <see cref="Chart"/> once per drop.
/// Changing the sort key reorders those rows and does not read the capture files again.
/// </summary>
public sealed class PerformanceViewModel : ObservableObject
{
    private PerformanceChart? _base;
    private PerformanceChart? _chart;
    private PerformanceSortChoice _selectedSort;
    private bool _ascending = true;
    private bool _showChart;
    private string _summary = "";
    private string _emptyMessage = PerformanceChartEngine.DropMessage;

    public PerformanceViewModel()
    {
        SortChoices =
        [
            new PerformanceSortChoice(PerformanceSortKey.Average, "Average"),
            new PerformanceSortChoice(PerformanceSortKey.Median, "Median"),
            new PerformanceSortChoice(PerformanceSortKey.Max, "Max"),
            new PerformanceSortChoice(PerformanceSortKey.P75, "P75"),
            new PerformanceSortChoice(PerformanceSortKey.P90, "P90"),
        ];
        _selectedSort = SortChoices[0];
        UseAscendingCommand = new RelayCommand(_ => SetAscending(true));
        UseDescendingCommand = new RelayCommand(_ => SetAscending(false));
    }

    public IReadOnlyList<PerformanceSortChoice> SortChoices { get; }

    public RelayCommand UseAscendingCommand { get; }
    public RelayCommand UseDescendingCommand { get; }

    public PerformanceSortChoice SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (value == null || !SetProperty(ref _selectedSort, value))
            {
                return;
            }

            Publish();
        }
    }

    public bool Ascending
    {
        get => _ascending;
        private set
        {
            if (SetProperty(ref _ascending, value))
            {
                OnPropertyChanged(nameof(Descending));
            }
        }
    }

    public bool Descending => !_ascending;

    public PerformanceChart? Chart
    {
        get => _chart;
        private set => SetProperty(ref _chart, value);
    }

    public bool ShowChart
    {
        get => _showChart;
        private set => SetProperty(ref _showChart, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string EmptyMessage
    {
        get => _emptyMessage;
        private set => SetProperty(ref _emptyMessage, value);
    }

    public void Apply(TeamTrialBatch batch)
    {
        _base = PerformanceChartEngine.Build(batch);
        Publish();
    }

    private void SetAscending(bool ascending)
    {
        if (_ascending == ascending)
        {
            return;
        }

        Ascending = ascending;
        Publish();
    }

    private void Publish()
    {
        if (_base is not { Available: true } || _base.Rows.Count == 0)
        {
            Chart = null;
            ShowChart = false;
            Summary = "";
            EmptyMessage = _base?.EmptyMessage ?? PerformanceChartEngine.DropMessage;
            return;
        }

        Chart = _base.Sorted(_selectedSort.Key, _ascending);
        ShowChart = true;
        EmptyMessage = "";
        Summary = PerformanceChartEngine.Describe(_chart!);
    }
}
