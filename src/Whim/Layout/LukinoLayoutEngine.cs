using System.Linq;

namespace Whim;

public record LukinoLayoutEngine : ILayoutEngine
{
	private readonly IContext _context;

	private readonly ImmutableDictionary<IWindow, IRectangle<double>> _dict;

	public string Name { get; init; } = "Lukino";

	public LayoutEngineIdentity Identity { get; }

	public int Count => _dict.Count;

	public LukinoLayoutEngine(IContext context, LayoutEngineIdentity identity)
	{
		Identity = identity;
		_context = context;
		_dict = ImmutableDictionary<IWindow, IRectangle<double>>.Empty;
	}

	private LukinoLayoutEngine(LukinoLayoutEngine layoutEngine, ImmutableDictionary<IWindow, IRectangle<double>> dict)
	{
		Name = layoutEngine.Name;
		Identity = layoutEngine.Identity;
		_context = layoutEngine._context;
		_dict = dict;
	}

	public ILayoutEngine AddWindow(IWindow window)
	{
		Logger.Debug($"Adding window {window} to layout engine {Name}");

		if (_dict.ContainsKey(window))
		{
			Logger.Debug($"Window {window} already exists in layout engine {Name}");
			return this;
		}

		return UpdateWindowRectangle(window);
	}

	public ILayoutEngine RemoveWindow(IWindow window)
	{
		Logger.Debug($"Removing window {window} from layout engine {Name}");

		ImmutableDictionary<IWindow, IRectangle<double>> newDict = _dict.Remove(window);

		return new LukinoLayoutEngine(this, newDict);
	}

	public bool ContainsWindow(IWindow window)
	{
		Logger.Debug($"Checking if layout engine {Name} contains window {window}");
		return _dict.ContainsKey(window);
	}

	public IWindow? GetFirstWindow() => _dict.Keys.First();

	public ILayoutEngine MinimizeWindowEnd(IWindow window) => this;

	public ILayoutEngine MinimizeWindowStart(IWindow window) => this;

	public ILayoutEngine FocusWindowInDirection(Direction direction, IWindow window) => this;

	public ILayoutEngine SwapWindowInDirection(Direction direction, IWindow window) => this;

	public ILayoutEngine MoveWindowEdgesInDirection(Direction edges, IPoint<double> deltas, IWindow window)
	{
		return UpdateWindowRectangle(window);
	}

	public IEnumerable<IWindowState> DoLayout(IRectangle<int> rectangle, IMonitor monitor)
	{
		Logger.Debug($"Performing a focus layout");

		foreach ((IWindow window, IRectangle<double> loc) in _dict)
		{
			yield return new WindowState()
			{
				Window = window,
				Rectangle = rectangle.ToMonitor(loc),
				WindowSize = WindowSize.Normal
			};
		}
	}

	public ILayoutEngine MoveWindowToPoint(IWindow window, IPoint<double> point)
	{
		return UpdateWindowRectangle(window);
	}

	public ILayoutEngine PerformCustomAction<T>(LayoutEngineCustomAction<T> action) =>
		throw new NotImplementedException();

	private LukinoLayoutEngine UpdateWindowRectangle(IWindow window)
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
			return this;
		}

		IMonitor newMonitor = _context.MonitorManager.GetMonitorAtPoint(newActualRectangle);
		IRectangle<double> newUnitSquareRectangle = newMonitor.WorkingArea.NormalizeRectangle(newActualRectangle);
		if (newUnitSquareRectangle.Equals(oldRectangle))
		{
			Logger.Debug($"Rectangle for window {window} has not changed");
			return this;
		}

		ImmutableDictionary<IWindow, IRectangle<double>> newDict = _dict.SetItem(window, newUnitSquareRectangle);

		return new LukinoLayoutEngine(this, newDict);
	}
}
