using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Whim.FloatingLayout;

public delegate T NewLayout<T>(FloatingManager<T> floatingManager);

public class FloatingManager<T>
{
	private readonly IContext _context;
	private readonly NewLayout<T> _newLayoutCallback;
	private readonly ImmutableDictionary<IWindow, IRectangle<double>> _dict;
	
	public int Count => _dict.Count;

	
	public FloatingManager(IContext context, NewLayout<T> newLayoutCallback)
	{
		_context = context;
		_newLayoutCallback = newLayoutCallback;
		_dict = ImmutableDictionary<IWindow, IRectangle<double>>.Empty;
	}

	private FloatingManager(FloatingManager<T> floatingManager, ImmutableDictionary<IWindow, IRectangle<double>> dict)
	{
		_context = floatingManager._context;
		_newLayoutCallback = floatingManager._newLayoutCallback;
		_dict = dict;
	}
	
	public (T, bool error) AddWindow(T layoutEngine, IWindow window)
	{
		if (_dict.ContainsKey(window))
		{
			return (layoutEngine, false);
		}

		return UpdateWindowRectangle(layoutEngine, window);
	}

	public (T, bool error) RemoveWindow(T layoutEngine, IWindow window)
	{
		if (_dict.ContainsKey(window))
		{
			ImmutableDictionary<IWindow, IRectangle<double>> newDict = _dict.Remove(window);
			return (_newLayoutCallback(new FloatingManager<T>(this, newDict)), false);
		}

		return (layoutEngine, false);
	}
	
	public bool ContainsWindow(IWindow window)
	{
		return _dict.ContainsKey(window);
	}
	
	public IWindow? GetFirstWindow() => _dict.Keys.FirstOrDefault();

	public IEnumerable<IWindowState> DoLayout(IMonitor monitor)
	{
		foreach ((IWindow window, IRectangle<double> loc) in _dict)
		{
			yield return new WindowState()
			{
				Window = window,
				Rectangle = monitor.WorkingArea.ToMonitor(loc),
				WindowSize = window.IsMaximized
					? WindowSize.Maximized
					: window.IsMinimized
						? WindowSize.Minimized
						: WindowSize.Normal
			};
		}
	}
	
	public (T, bool error) UpdateWindowRectangle(T layoutEngine, IWindow window)
	{
		// Try get the old rectangle.
		IRectangle<double>? oldRectangle = _dict.TryGetValue(window, out IRectangle<double>? rectangle)
			? rectangle
			: null;

		// Since the window is floating, we update the rectangle, and return.
		IRectangle<int>? newActualRectangle = _context.NativeManager.DwmGetWindowRectangle(window.Handle);
		if (newActualRectangle == null)
		{
			Logger.Error($"Could not obtain rectangle for floating window {window}");
			return (layoutEngine, true);
		}

		IMonitor newMonitor = _context.MonitorManager.GetMonitorAtPoint(newActualRectangle);
		IRectangle<double> newUnitSquareRectangle = newMonitor.WorkingArea.NormalizeRectangle(newActualRectangle);
		if (newUnitSquareRectangle.Equals(oldRectangle))
		{
			Logger.Debug($"Rectangle for window {window} has not changed");
			return (layoutEngine, false);
		}

		ImmutableDictionary<IWindow, IRectangle<double>> newDict = _dict.SetItem(window, newUnitSquareRectangle);

		return (_newLayoutCallback(new FloatingManager<T>(this, newDict)), false);
	}
}
