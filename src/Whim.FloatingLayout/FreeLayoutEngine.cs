using System.Collections.Generic;

namespace Whim.FloatingLayout;

/// <summary>
/// Layout engine that lays out all windows as free-floating.
/// This layout will be soon renamed FloatingLayoutEngine.
/// </summary>
public class FreeLayoutEngine : ILayoutEngine
{
	private readonly IContext _context;
	private readonly FloatingManager<FreeLayoutEngine> _floatingManager;

	/// <inheritdoc/>
	public string Name { get; init; } = "Free";

	/// <inheritdoc/>
	public int Count => _floatingManager.Count;

	/// <inheritdoc/>
	public LayoutEngineIdentity Identity { get; }

	/// <summary>
	/// Creates a new instance of the <see cref="FreeLayoutEngine"/> class.
	/// </summary>
	/// <param name="context">The identity of the layout engine.</param>
	/// <param name="identity">The context of the layout engine.</param>
	public FreeLayoutEngine(IContext context, LayoutEngineIdentity identity)
	{
		Identity = identity;
		_context = context;
		_floatingManager = new FloatingManager<FreeLayoutEngine>(_context, (floatingManager) => new FreeLayoutEngine(this, floatingManager));
	}

	private FreeLayoutEngine(FreeLayoutEngine layoutEngine, FloatingManager<FreeLayoutEngine> floatingManager)
	{
		Name = layoutEngine.Name;
		Identity = layoutEngine.Identity;
		_context = layoutEngine._context;
		_floatingManager = floatingManager;
	}

	/// <inheritdoc/>
	public ILayoutEngine AddWindow(IWindow window)
	{
		Logger.Debug($"Adding window {window} to layout engine {Name}");

		(FreeLayoutEngine newEngine, bool error) = _floatingManager.AddWindow(this, window);
		return newEngine;
	}

	/// <inheritdoc/>
	public ILayoutEngine RemoveWindow(IWindow window)
	{
		Logger.Debug($"Removing window {window} from layout engine {Name}");

		(FreeLayoutEngine newEngine, bool error) = _floatingManager.RemoveWindow(this, window);
		return newEngine;
	}

	/// <inheritdoc/>
	public bool ContainsWindow(IWindow window)
	{
		Logger.Debug($"Checking if layout engine {Name} contains window {window}");
		return _floatingManager.ContainsWindow(window);
	}

	/// <inheritdoc/>
	public IWindow? GetFirstWindow() => _floatingManager.GetFirstWindow();

	/// <inheritdoc/>
	public IEnumerable<IWindowState> DoLayout(IRectangle<int> rectangle, IMonitor monitor)
	{
		Logger.Debug($"Doing layout for engine {Name}");

		return _floatingManager.DoLayout(monitor);
	}

	/// <inheritdoc/>
	public ILayoutEngine PerformCustomAction<T>(LayoutEngineCustomAction<T> action) => this;

	/// <inheritdoc/>
	public ILayoutEngine MoveWindowToPoint(IWindow window, IPoint<double> point)
	{
		(FreeLayoutEngine newEngine, bool error) = _floatingManager.UpdateWindowRectangle(this, window);
		return newEngine;
	}

	/// <inheritdoc/>
	public ILayoutEngine MoveWindowEdgesInDirection(Direction edges, IPoint<double> deltas, IWindow window)
	{
		(FreeLayoutEngine newEngine, bool error) = _floatingManager.UpdateWindowRectangle(this, window);
		return newEngine;
	}

	/// <inheritdoc/>
	public ILayoutEngine MinimizeWindowStart(IWindow window) => this;

	/// <inheritdoc/>
	public ILayoutEngine MinimizeWindowEnd(IWindow window) => this;

	/// <inheritdoc/>
	public ILayoutEngine FocusWindowInDirection(Direction direction, IWindow window) => this;

	/// <inheritdoc/>
	public ILayoutEngine SwapWindowInDirection(Direction direction, IWindow window) => this;
}
