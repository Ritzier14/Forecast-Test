using System.Windows;
using System.Windows.Media;
using ProjectCostForecast.App.Models;

namespace ProjectCostForecast.App;

/// <summary>
/// Presentation-only state used by the KPI cards. The persisted and calculated
/// summary DTOs remain in the Models namespace and expose only plain values.
/// </summary>
public sealed class KpiPill : ObservableModel
{
    private string _key = string.Empty;
    private string _name = string.Empty;
    private string _valueText = string.Empty;
    private string _subtext = string.Empty;
    private string _comparisonText = string.Empty;
    private string _comparisonDirection = string.Empty;
    private string _iconPath = string.Empty;
    private ImageSource? _iconSource;
    private Visibility _comparisonVisibility = Visibility.Collapsed;

    public int Id { get; init; }

    public string Key
    {
        get => _key;
        set => SetProperty(ref _key, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string ValueText
    {
        get => _valueText;
        set => SetProperty(ref _valueText, value);
    }

    public string Subtext
    {
        get => _subtext;
        set => SetProperty(ref _subtext, value);
    }

    public string ComparisonText
    {
        get => _comparisonText;
        set => SetProperty(ref _comparisonText, value);
    }

    public string ComparisonDirection
    {
        get => _comparisonDirection;
        set => SetProperty(ref _comparisonDirection, value);
    }

    public string IconPath
    {
        get => _iconPath;
        set => SetProperty(ref _iconPath, value);
    }

    public ImageSource? IconSource
    {
        get => _iconSource;
        set => SetProperty(ref _iconSource, value);
    }

    public Visibility ComparisonVisibility
    {
        get => _comparisonVisibility;
        set => SetProperty(ref _comparisonVisibility, value);
    }
}

/// <summary>
/// Presentation-backed workspace tab state. Its layout fields are persisted by
/// the app, while IconPreview is a WPF-only projection ignored by JSON.
/// </summary>
public sealed class WorkspaceViewTab : ObservableModel
{
    private string _name = string.Empty;
    private string _editName = string.Empty;
    private bool _isEditing;
    private string _contentKey = string.Empty;
    private string _iconKey = string.Empty;
    private string _iconColorHex = string.Empty;

    public string WorkspaceKey { get; init; } = string.Empty;

    public string ContentKey
    {
        get => _contentKey;
        set => SetProperty(ref _contentKey, value);
    }

    public string IconKey
    {
        get => _iconKey;
        set
        {
            if (SetProperty(ref _iconKey, value?.Trim() ?? string.Empty))
            {
                OnPropertyChanged(nameof(IconPreview));
            }
        }
    }

    public string IconColorHex
    {
        get => _iconColorHex;
        set
        {
            if (SetProperty(ref _iconColorHex, value?.Trim() ?? string.Empty))
            {
                OnPropertyChanged(nameof(IconPreview));
            }
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public ImageSource IconPreview => MainWindow.GetBuiltInImageSourceByPath(
        $"/Assets/Icons/png/{(string.IsNullOrWhiteSpace(IconKey) ? "ic_tab_forecast_16.png" : IconKey)}",
        IconColorHex);

    public List<string> HiddenColumnKeys { get; set; } = [];
    public List<WorkspaceColumnLayout> ColumnLayouts { get; set; } = [];
    public bool ShowZeroAsBlank { get; set; } = true;
    public bool GroupForecastLinesByTask { get; set; }
    public string ForecastGroupByKey { get; set; } = string.Empty;
    public bool ReportCanvasInitialized { get; set; }
    public string ReportCanvasPageSize { get; set; } = "A4";
    public string ReportCanvasOrientation { get; set; } = "Portrait";
    public List<ReportCanvasObjectLayout> ReportCanvasObjects { get; set; } = [];

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string RenameRestoreName { get; set; } = string.Empty;
    public string DefaultName { get; set; } = string.Empty;
    public bool IsNewlyCreated { get; set; }
}

/// <summary>
/// WPF column metadata used to materialize the forecast grid. It is kept out
/// of the persisted forecast and summary model candidates.
/// </summary>
public sealed class ForecastMonthColumnDefinition
{
    public string Key { get; init; } = string.Empty;
    public string YearLabel { get; init; } = string.Empty;
    public string PrimaryLabel { get; init; } = string.Empty;
    public string SecondaryLabel { get; init; } = string.Empty;
    public Brush PrimaryBackground { get; init; } = Brushes.White;
    public Brush SecondaryBackground { get; init; } = Brushes.White;
    public Brush ValueBackground { get; init; } = Brushes.White;
    public Brush ValueBorderBrush { get; init; } = BrushFactory.Frozen("#EEF3F8");
    public Brush ValueForeground { get; init; } = Brushes.Black;
    public Visibility LeftSolidSeparatorVisibility { get; init; } = Visibility.Collapsed;
    public Visibility RightSolidSeparatorVisibility { get; init; } = Visibility.Collapsed;
    public Visibility LeftDashedSeparatorVisibility { get; init; } = Visibility.Collapsed;
    public Visibility RightDashedSeparatorVisibility { get; init; } = Visibility.Collapsed;
    public bool IsEditable { get; init; } = true;
    public bool IsTotal { get; init; }
}
