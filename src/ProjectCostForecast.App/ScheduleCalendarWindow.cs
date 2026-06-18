using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProjectCostForecast.App.Models;
using ProjectCostForecast.App.ViewModels;

namespace ProjectCostForecast.App;

public sealed class ScheduleCalendarWindow : Window
{
    private static readonly string[] DayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    private readonly MainWindowViewModel _viewModel;
    private readonly ListBox _calendarList;
    private readonly TextBox _nameTextBox;
    private readonly CheckBox[] _dayCheckBoxes = new CheckBox[7];
    private readonly ListBox _holidayList;
    private readonly ListBox _extraDayList;
    private readonly DatePicker _holidayPicker;
    private readonly DatePicker _extraDayPicker;
    private readonly CheckBox _defaultCheckBox;
    private readonly CheckBox _visibleCheckBox;
    private readonly TextBox _colorTextBox;
    private bool _suppressEdits;

    public ScheduleCalendarWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        Title = "Schedule calendars";
        Width = 760;
        Height = 560;
        MinWidth = 640;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.FromRgb(244, 246, 248));

        var root = new Grid { Margin = new Thickness(16) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Left: calendar list and add/remove
        _calendarList = new ListBox();
        _calendarList.SelectionChanged += (_, _) => LoadSelectedCalendar();
        var leftPanel = new DockPanel { Margin = new Thickness(0, 0, 14, 0), LastChildFill = true };
        var leftButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var addButton = new Button { Content = "Add", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0) };
        var removeButton = new Button { Content = "Delete", Padding = new Thickness(12, 4, 12, 4) };
        addButton.Click += (_, _) =>
        {
            var calendar = _viewModel.AddScheduleCalendar($"Calendar {_viewModel.ScheduleCalendars.Count + 1}");
            RefreshCalendarList(calendar);
        };
        removeButton.Click += (_, _) =>
        {
            if (_calendarList.SelectedItem is ScheduleCalendar calendar)
            {
                _viewModel.RemoveScheduleCalendar(calendar);
                RefreshCalendarList(_viewModel.ScheduleCalendars.FirstOrDefault());
            }
        };
        leftButtons.Children.Add(addButton);
        leftButtons.Children.Add(removeButton);
        DockPanel.SetDock(leftButtons, Dock.Bottom);
        leftPanel.Children.Add(leftButtons);

        leftPanel.Children.Add(_calendarList);
        root.Children.Add(leftPanel);

        // Right: detail editor
        var detail = new StackPanel();
        Grid.SetColumn(detail, 1);

        detail.Children.Add(new TextBlock { Text = "Calendar name", FontWeight = FontWeights.SemiBold });
        _nameTextBox = new TextBox { Margin = new Thickness(0, 4, 0, 10) };
        _nameTextBox.TextChanged += (_, _) =>
        {
            if (!_suppressEdits && SelectedCalendar is { } calendar)
            {
                calendar.Name = _nameTextBox.Text;
                RefreshCalendarListText();
                _viewModel.NotifyScheduleCalendarsChanged();
            }
        };
        detail.Children.Add(_nameTextBox);

        var displayPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        _visibleCheckBox = new CheckBox { Content = "Show calendar overlay on Gantt", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 18, 0) };
        _visibleCheckBox.Checked += (_, _) => SetCalendarVisibility(true);
        _visibleCheckBox.Unchecked += (_, _) => SetCalendarVisibility(false);
        displayPanel.Children.Add(_visibleCheckBox);
        displayPanel.Children.Add(new TextBlock { Text = "Colour", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        _colorTextBox = new TextBox { Width = 90, VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Hex colour, for example #3B82F6" };
        _colorTextBox.LostFocus += (_, _) => SaveCalendarColor();
        _colorTextBox.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                SaveCalendarColor();
            }
        };
        displayPanel.Children.Add(_colorTextBox);
        detail.Children.Add(displayPanel);

        _defaultCheckBox = new CheckBox { Content = "Default calendar for new activities", Margin = new Thickness(0, 0, 0, 10) };
        _defaultCheckBox.Checked += (_, _) =>
        {
            if (!_suppressEdits && SelectedCalendar is { } calendar)
            {
                _viewModel.ScheduleDataRef.DefaultCalendarId = calendar.Id;
                _viewModel.NotifyScheduleCalendarsChanged();
            }
        };
        detail.Children.Add(_defaultCheckBox);

        detail.Children.Add(new TextBlock { Text = "Working days", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        var daysPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        for (var i = 0; i < 7; i++)
        {
            var index = i;
            var checkBox = new CheckBox { Content = DayNames[i], Margin = new Thickness(0, 0, 14, 4) };
            checkBox.Checked += (_, _) => SetWorkingDay(index, true);
            checkBox.Unchecked += (_, _) => SetWorkingDay(index, false);
            _dayCheckBoxes[i] = checkBox;
            daysPanel.Children.Add(checkBox);
        }

        detail.Children.Add(daysPanel);

        var datesGrid = new Grid();
        datesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        datesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
        datesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _holidayPicker = new DatePicker();
        _holidayList = new ListBox { Height = 180, Margin = new Thickness(0, 6, 0, 0) };
        var holidayPanel = BuildDateListPanel(
            "Holidays / non-work days",
            _holidayPicker,
            _holidayList,
            addDate: date => EditDates(calendar => { if (!calendar.Holidays.Contains(date)) { calendar.Holidays.Add(date); calendar.Holidays.Sort(); } }),
            removeSelected: () => EditDates(calendar =>
            {
                if (_holidayList.SelectedItem is DateOnly date)
                {
                    calendar.Holidays.Remove(date);
                }
            }));
        datesGrid.Children.Add(holidayPanel);

        _extraDayPicker = new DatePicker();
        _extraDayList = new ListBox { Height = 180, Margin = new Thickness(0, 6, 0, 0) };
        var extraPanel = BuildDateListPanel(
            "Extra working days (overrides pattern)",
            _extraDayPicker,
            _extraDayList,
            addDate: date => EditDates(calendar => { if (!calendar.ExtraWorkDays.Contains(date)) { calendar.ExtraWorkDays.Add(date); calendar.ExtraWorkDays.Sort(); } }),
            removeSelected: () => EditDates(calendar =>
            {
                if (_extraDayList.SelectedItem is DateOnly date)
                {
                    calendar.ExtraWorkDays.Remove(date);
                }
            }));
        Grid.SetColumn(extraPanel, 2);
        datesGrid.Children.Add(extraPanel);
        detail.Children.Add(datesGrid);
        root.Children.Add(detail);

        var closeButton = new Button
        {
            Content = "Close",
            Padding = new Thickness(18, 6, 18, 6),
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        closeButton.Click += (_, _) => Close();
        Grid.SetRow(closeButton, 1);
        Grid.SetColumnSpan(closeButton, 2);
        root.Children.Add(closeButton);

        Content = root;
        RefreshCalendarList(_viewModel.ScheduleCalendars.FirstOrDefault());
    }

    private ScheduleCalendar? SelectedCalendar => _calendarList.SelectedItem as ScheduleCalendar;

    private static DockPanel BuildDateListPanel(string title, DatePicker picker, ListBox list, Action<DateOnly> addDate, Action removeSelected)
    {
        var panel = new DockPanel { LastChildFill = true };
        var header = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold };
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);

        var controls = new DockPanel { Margin = new Thickness(0, 6, 0, 0), LastChildFill = true };
        var addButton = new Button { Content = "Add", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(6, 0, 0, 0) };
        var removeButton = new Button { Content = "Remove", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(6, 0, 0, 0) };
        addButton.Click += (_, _) =>
        {
            if (picker.SelectedDate is { } date)
            {
                addDate(DateOnly.FromDateTime(date));
            }
        };
        removeButton.Click += (_, _) => removeSelected();
        DockPanel.SetDock(removeButton, Dock.Right);
        DockPanel.SetDock(addButton, Dock.Right);
        controls.Children.Add(removeButton);
        controls.Children.Add(addButton);
        controls.Children.Add(picker);
        DockPanel.SetDock(controls, Dock.Top);
        panel.Children.Add(controls);
        panel.Children.Add(list);
        return panel;
    }

    private void SetWorkingDay(int dayIndex, bool isWorking)
    {
        if (_suppressEdits || SelectedCalendar is not { } calendar)
        {
            return;
        }

        calendar.WorkingDays[dayIndex] = isWorking;
        _viewModel.NotifyScheduleCalendarsChanged();
    }

    private void SetCalendarVisibility(bool visible)
    {
        if (_suppressEdits || SelectedCalendar is not { } calendar)
        {
            return;
        }

        calendar.IsVisibleOnGantt = visible;
        _viewModel.NotifyScheduleCalendarsChanged();
    }

    private void SaveCalendarColor()
    {
        if (_suppressEdits || SelectedCalendar is not { } calendar)
        {
            return;
        }

        try
        {
            _ = (Color)ColorConverter.ConvertFromString(_colorTextBox.Text)!;
            calendar.ColorHex = _colorTextBox.Text.Trim();
            _colorTextBox.ClearValue(Control.BorderBrushProperty);
            _viewModel.NotifyScheduleCalendarsChanged();
        }
        catch
        {
            _colorTextBox.BorderBrush = Brushes.Red;
        }
    }

    private void EditDates(Action<ScheduleCalendar> edit)
    {
        if (SelectedCalendar is not { } calendar)
        {
            return;
        }

        edit(calendar);
        LoadSelectedCalendar();
        _viewModel.NotifyScheduleCalendarsChanged();
    }

    private void RefreshCalendarList(ScheduleCalendar? select)
    {
        _calendarList.ItemsSource = null;
        _calendarList.ItemsSource = _viewModel.ScheduleCalendars;
        _calendarList.DisplayMemberPath = nameof(ScheduleCalendar.Name);
        _calendarList.SelectedItem = select ?? _viewModel.ScheduleCalendars.FirstOrDefault();
    }

    private void RefreshCalendarListText()
    {
        var selected = _calendarList.SelectedItem;
        _calendarList.ItemsSource = null;
        _calendarList.ItemsSource = _viewModel.ScheduleCalendars;
        _calendarList.DisplayMemberPath = nameof(ScheduleCalendar.Name);
        _calendarList.SelectedItem = selected;
    }

    private void LoadSelectedCalendar()
    {
        if (SelectedCalendar is not { } calendar)
        {
            return;
        }

        _suppressEdits = true;
        try
        {
            _nameTextBox.Text = calendar.Name;
            _visibleCheckBox.IsChecked = calendar.IsVisibleOnGantt;
            _colorTextBox.Text = calendar.ColorHex;
            _defaultCheckBox.IsChecked = string.Equals(
                _viewModel.ScheduleDataRef.DefaultCalendarId,
                calendar.Id,
                StringComparison.OrdinalIgnoreCase);
            for (var i = 0; i < 7; i++)
            {
                _dayCheckBoxes[i].IsChecked = calendar.WorkingDays.Length > i && calendar.WorkingDays[i];
            }

            _holidayList.ItemsSource = calendar.Holidays.ToList();
            _extraDayList.ItemsSource = calendar.ExtraWorkDays.ToList();
        }
        finally
        {
            _suppressEdits = false;
        }
    }
}
