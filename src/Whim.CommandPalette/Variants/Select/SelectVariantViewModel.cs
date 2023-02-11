using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Windows.System;

namespace Whim.CommandPalette;

internal class SelectVariantViewModel : IVariantViewModel
{
	private readonly ICommandPaletteWindowViewModel _commandPaletteWindowViewModel;

	private bool _allowMultiSelect;

	private SelectVariantConfig? _activationConfig;

	/// <summary>
	/// The rows which are currently unused and can be reused for new matches.
	/// Keeping these around avoids the need to create new rows every time the palette is shown.
	/// </summary>
	internal readonly List<IVariantRowControl<SelectOption>> _unusedRows = new();

	/// <summary>
	/// The current commands from which the matches shown in <see cref="SelectRows"/> are drawn.
	/// </summary>
	internal readonly List<SelectVariantRowModel> _allItems = new();

	/// <summary>
	/// Factory to create select rows to make it possible to use xunit.
	/// It turns out it's annoying to test the Windows App SDK with xunit.
	/// </summary>
	private readonly Func<
		IVariantRowModel<SelectOption>,
		SelectVariantConfig,
		IVariantRowControl<SelectOption>
	> _selectRowFactory;

	public readonly ObservableCollection<IVariantRowControl<SelectOption>> SelectRows = new();

	/// <summary>
	/// The height of the row.
	/// </summary>
	public required double RowHeight { get; init; }

	public bool ShowSaveButton => true;

	private Visibility _selectRowsItemsWrapperVisibility = Visibility.Visible;
	public Visibility SelectRowsItemsWrapperVisibility
	{
		get => _selectRowsItemsWrapperVisibility;
		set
		{
			if (SelectRowsItemsWrapperVisibility != value)
			{
				_selectRowsItemsWrapperVisibility = value;
				OnPropertyChanged(nameof(SelectRowsItemsWrapperVisibility));
			}
		}
	}

	private Visibility _selectRowsControlVisibility = Visibility.Visible;
	public Visibility SelectRowsItemsVisibility
	{
		get => _selectRowsControlVisibility;
		set
		{
			if (SelectRowsItemsVisibility != value)
			{
				_selectRowsControlVisibility = value;
				OnPropertyChanged(nameof(SelectRowsItemsVisibility));
			}
		}
	}

	private Visibility _noMatchingOptionsTextBlockVisibility = Visibility.Collapsed;
	public Visibility NoMatchingOptionsTextBlockVisibility
	{
		get => _noMatchingOptionsTextBlockVisibility;
		set
		{
			if (NoMatchingOptionsTextBlockVisibility != value)
			{
				_noMatchingOptionsTextBlockVisibility = value;
				OnPropertyChanged(nameof(NoMatchingOptionsTextBlockVisibility));
			}
		}
	}

	private int _selectedIndex;
	public int SelectedIndex
	{
		get => _selectedIndex;
		set
		{
			if (SelectedIndex != value)
			{
				_selectedIndex = value;
				OnPropertyChanged(nameof(SelectedIndex));
			}
		}
	}

	public event PropertyChangedEventHandler? PropertyChanged;

	public event EventHandler<EventArgs>? ScrollIntoViewRequested;

	public SelectVariantViewModel(
		ICommandPaletteWindowViewModel commandPaletteWindowViewModel,
		Func<IVariantRowModel<SelectOption>, SelectVariantConfig, IVariantRowControl<SelectOption>> selectRowFactory
	)
	{
		_commandPaletteWindowViewModel = commandPaletteWindowViewModel;
		_selectRowFactory = selectRowFactory;
	}

	public void Activate(BaseVariantConfig activationConfig)
	{
		if (activationConfig is SelectVariantConfig selectVariantConfig)
		{
			_activationConfig = selectVariantConfig;
			_allowMultiSelect = selectVariantConfig.AllowMultiSelect;
			PopulateItems(selectVariantConfig.Options);
			Update();
		}
		else
		{
			Logger.Error("Invalid activation config type");
		}
	}

	public void Update()
	{
		if (_activationConfig == null)
		{
			return;
		}

		int matchesCount = LoadSelectMatches(_commandPaletteWindowViewModel.Text, _activationConfig);

		SelectRowsItemsWrapperVisibility = Visibility.Visible;
		if (matchesCount == 0)
		{
			NoMatchingOptionsTextBlockVisibility = Visibility.Visible;
			SelectRowsItemsVisibility = Visibility.Collapsed;
		}
		else
		{
			NoMatchingOptionsTextBlockVisibility = Visibility.Collapsed;
			SelectRowsItemsVisibility = Visibility.Visible;
		}

		RemoveUnusedRows(matchesCount);
		Logger.Verbose($"Command palette match count: {SelectRows.Count}");
		SelectedIndex = SelectRows.Count > 0 ? 0 : -1;
	}

	public void OnKeyDown(VirtualKey key)
	{
		if (SelectRows.Count == 0)
		{
			return;
		}

		bool scrollIntoView = false;

		switch (key)
		{
			case VirtualKey.Down:
				// Go down the command palette's list.
				SelectedIndex = (SelectedIndex + 1).Mod(SelectRows.Count);
				scrollIntoView = true;
				break;

			case VirtualKey.Up:
				// Go up the command palette's list.
				SelectedIndex = (SelectedIndex - 1).Mod(SelectRows.Count);
				scrollIntoView = true;
				break;

			case VirtualKey.Enter:
				UpdateSelectedItem();
				break;

			default:
				break;
		}

		if (scrollIntoView)
		{
			ScrollIntoViewRequested?.Invoke(this, EventArgs.Empty);
		}
	}

	public void UpdateSelectedItem()
	{
		if (_activationConfig == null)
		{
			return;
		}

		IVariantRowModel<SelectOption> selectedItem = SelectRows[SelectedIndex].Model;
		SelectOption selectedData = selectedItem.Data;

		if (_allowMultiSelect)
		{
			selectedData.IsSelected = !selectedData.IsSelected;
		}
		else
		{
			foreach (SelectVariantRowModel variantItem in _allItems)
			{
				variantItem.Data.IsSelected = false;
			}

			selectedData.IsSelected = true;
		}

		_activationConfig.Matcher.OnMatchExecuted(selectedItem);
		_commandPaletteWindowViewModel.RequestFocusTextBox();
	}

	public void VariantRow_OnClick(IVariantRowControl<SelectOption> variantRow)
	{
		if (_activationConfig == null)
		{
			return;
		}

		int idx = SelectRows.IndexOf(variantRow);
		if (idx >= 0)
		{
			SelectedIndex = idx;
			UpdateSelectedItem();
		}
	}

	/// <summary>
	/// Populate <see cref="_allItems"/> with all the current commands.
	/// </summary>
	internal void PopulateItems(IEnumerable<SelectOption> items)
	{
		Logger.Debug($"Populating the current list of all commands.");

		int idx = 0;
		foreach (SelectOption selectOption in items)
		{
			if (idx < _allItems.Count)
			{
				if (_allItems[idx].Data != selectOption)
				{
					_allItems[idx] = new SelectVariantRowModel(selectOption);
				}
			}
			else
			{
				_allItems.Add(new SelectVariantRowModel(selectOption));
			}

			idx++;
		}

		for (; idx < _allItems.Count; idx++)
		{
			_allItems.RemoveAt(_allItems.Count - 1);
		}
	}

	/// <summary>
	/// Load the matches into the command palette rows.
	/// </summary>
	/// <param name="query">The query text string.</param>
	/// <param name="activationConfig"></param>
	/// <returns>The number of processed matches.</returns>
	internal int LoadSelectMatches(string query, SelectVariantConfig activationConfig)
	{
		int matchesCount = 0;

		foreach (IVariantRowModel<SelectOption> item in activationConfig.Matcher.Match(query, _allItems))
		{
			Logger.Verbose($"Matched {item.Title}");
			if (matchesCount < SelectRows.Count)
			{
				// Update the existing row.
				SelectRows[matchesCount].Update(item);
			}
			else if (_unusedRows.Count > 0)
			{
				// Restoring the unused row.
				IVariantRowControl<SelectOption> row = _unusedRows[^1];
				row.Update(item);

				SelectRows.Add(row);
				_unusedRows.RemoveAt(_unusedRows.Count - 1);
			}
			else
			{
				// Add a new row.
				IVariantRowControl<SelectOption> row = _selectRowFactory(item, activationConfig);
				SelectRows.Add(row);
				row.Initialize();
			}
			matchesCount++;

			Logger.Verbose($"Finished updating {item.Title}");
		}

		return matchesCount;
	}

	/// <summary>
	/// If there are more items than we have space for, remove the last ones.
	/// </summary>
	/// <param name="usedRowsCount">The currently used rows.</param>
	private void RemoveUnusedRows(int usedRowsCount)
	{
		int count = SelectRows.Count;
		for (; usedRowsCount < count; usedRowsCount++)
		{
			_unusedRows.Add(SelectRows[^1]);
			SelectRows.RemoveAt(SelectRows.Count - 1);
		}
	}

	public void Hide() { }

	public void Save() => _activationConfig?.Callback(_allItems.Select(x => x.Data));

	public double GetViewMaxHeight() => SelectRows.Count * RowHeight;

	protected virtual void OnPropertyChanged(string propertyName)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
