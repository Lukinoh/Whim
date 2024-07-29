using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Whim.FloatingLayout;

/// <summary>
/// A proxy layout engine to allow windows to be free-floating.
/// </summary>
internal record FloatingLayoutEngine : BaseProxyLayoutEngine
{
	private readonly IContext _context;
	private readonly IInternalFloatingLayoutPlugin _plugin;
	private readonly FloatingManager<FloatingLayoutEngine> _floatingManager;

	/// <inheritdoc />
	public override int Count => InnerLayoutEngine.Count + _floatingManager.Count;

	/// <summary>
	/// Creates a new instance of the proxy layout engine <see cref="FloatingLayoutEngine"/>.
	/// </summary>
	/// <param name="context"></param>
	/// <param name="plugin"></param>
	/// <param name="innerLayoutEngine"></param>
	public FloatingLayoutEngine(IContext context, IInternalFloatingLayoutPlugin plugin, ILayoutEngine innerLayoutEngine)
		: base(innerLayoutEngine)
	{
		_context = context;
		_plugin = plugin;
		_floatingManager = new FloatingManager<FloatingLayoutEngine>(context, (floatingManager, window) =>
		{
			ILayoutEngine newInner = innerLayoutEngine.RemoveWindow(window);
			return new FloatingLayoutEngine(this, newInner, floatingManager);
		});
	}

	private FloatingLayoutEngine(FloatingLayoutEngine oldEngine, ILayoutEngine newInnerLayoutEngine)
		: base(newInnerLayoutEngine)
	{
		_context = oldEngine._context;
		_plugin = oldEngine._plugin;
		_floatingManager = oldEngine._floatingManager;
	}

	private FloatingLayoutEngine(
		FloatingLayoutEngine oldEngine,
		ILayoutEngine newInnerLayoutEngine,
		FloatingManager<FloatingLayoutEngine> floatingManager
	)
		: this(oldEngine, newInnerLayoutEngine)
	{
		_floatingManager = floatingManager;
	}

	/// <summary>
	/// Returns a new instance of <see cref="FloatingLayoutEngine"/> with the given inner layout engine,
	/// if the inner layout engine has changed, or the <paramref name="gcWindow"/> was floating.
	/// </summary>
	/// <param name="newInnerLayoutEngine">The new inner layout engine.</param>
	/// <param name="gcWindow">
	/// The <see cref="IWindow"/> which triggered the update. If a window has triggered an inner
	/// layout engine update, the window is no longer floating (apart from that one case where we
	/// couldn't get the window's rectangle).
	/// </param>
	/// <returns></returns>
	private FloatingLayoutEngine UpdateInner(ILayoutEngine newInnerLayoutEngine, IWindow? gcWindow)
	{
		(FloatingLayoutEngine newEngine, bool _) =
			gcWindow != null ? _floatingManager.RemoveWindow(this, gcWindow) : (this, false);

		return InnerLayoutEngine == newInnerLayoutEngine && this == newEngine
			? this
			: new FloatingLayoutEngine(newEngine, newInnerLayoutEngine);
	}

	/// <inheritdoc />
	public override ILayoutEngine AddWindow(IWindow window)
	{
		Logger.Error("DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD");
		// If the window is already tracked by this layout engine, or is a new floating window,
		// update the rectangle and return.
		if (IsWindowFloating(window))
		{
			(FloatingLayoutEngine newEngine, bool error) = _floatingManager.AddWindow(this, window);
			if (!error)
			{
				return newEngine;
			}
		}

		return UpdateInner(InnerLayoutEngine.AddWindow(window), window);
	}

	/// <inheritdoc />
	public override ILayoutEngine RemoveWindow(IWindow window)
	{
		bool isFloating = IsWindowFloating(window);

		// If tracked by this layout engine, remove it.
		// Otherwise, pass to the inner layout engine.
		if (_floatingManager.ContainsWindow(window))
		{
			_plugin.MarkWindowAsDockedInLayoutEngine(window, InnerLayoutEngine.Identity);

			// If the window was not supposed to be floating, remove it from the inner layout engine.
			if (isFloating)
			{
				(FloatingLayoutEngine newEngine, bool error) = _floatingManager.RemoveWindow(this, window);
				return newEngine;
			}
		}

		return UpdateInner(InnerLayoutEngine.RemoveWindow(window), window);
	}

	/// <inheritdoc />
	public override ILayoutEngine MoveWindowToPoint(IWindow window, IPoint<double> point)
	{
		// If the window is floating, update the rectangle and return.
		if (IsWindowFloating(window))
		{
			(FloatingLayoutEngine newEngine, bool error) = _floatingManager.UpdateWindowRectangle(this, window);
			if (!error)
			{
				ILayoutEngine newInnerLayoutEngine = InnerLayoutEngine.RemoveWindow(window);
				return new FloatingLayoutEngine(newEngine, newInnerLayoutEngine);
			}
		}

		return UpdateInner(InnerLayoutEngine.MoveWindowToPoint(window, point), window);
	}
	

	/// <inheritdoc />
	public override ILayoutEngine MoveWindowEdgesInDirection(Direction edge, IPoint<double> deltas, IWindow window)
	{
		// If the window is floating, update the rectangle and return.
		if (IsWindowFloating(window))
		{
			(FloatingLayoutEngine newEngine, bool error) = _floatingManager.UpdateWindowRectangle(this, window);
			if (!error && newEngine != this)
			{
				ILayoutEngine newInnerLayoutEngine = InnerLayoutEngine.RemoveWindow(window);
				return new FloatingLayoutEngine(newEngine, newInnerLayoutEngine);
			}
		}

		return UpdateInner(InnerLayoutEngine.MoveWindowEdgesInDirection(edge, deltas, window), window);
	}

	private bool IsWindowFloating(IWindow? window) =>
		window != null && _plugin.FloatingWindows.TryGetValue(window, out ISet<LayoutEngineIdentity>? layoutEngines)
		&& layoutEngines.Contains(InnerLayoutEngine.Identity);

	/// <inheritdoc />
	public override IEnumerable<IWindowState> DoLayout(IRectangle<int> rectangle, IMonitor monitor)
	{
		// Iterate over all windows in the floating manager.
		foreach (IWindowState windowState in _floatingManager.DoLayout(monitor))
		{
			yield return windowState;
		}

		// Iterate over all windows in the inner layout engine.
		foreach (IWindowState windowState in InnerLayoutEngine.DoLayout(rectangle, monitor))
		{
			yield return windowState;
		}
	}

	/// <inheritdoc />
	public override IWindow? GetFirstWindow()
	{
		return InnerLayoutEngine.GetFirstWindow() ?? _floatingManager.GetFirstWindow();
	}

	/// <inheritdoc />
	public override ILayoutEngine FocusWindowInDirection(Direction direction, IWindow window)
	{
		if (IsWindowFloating(window))
		{
			// At this stage, we don't have a way to get the window in a child layout engine at
			// a given point.
			// As a workaround, we just focus the first window.
			InnerLayoutEngine.GetFirstWindow()?.Focus();
			return this;
		}

		return UpdateInner(InnerLayoutEngine.FocusWindowInDirection(direction, window), window);
	}

	/// <inheritdoc />
	public override ILayoutEngine SwapWindowInDirection(Direction direction, IWindow window)
	{
		if (IsWindowFloating(window))
		{
			// At this stage, we don't have a way to get the window in a child layout engine at
			// a given point.
			// For now, we do nothing.
			return this;
		}

		return UpdateInner(InnerLayoutEngine.SwapWindowInDirection(direction, window), window);
	}

	/// <inheritdoc />
	public override bool ContainsWindow(IWindow window) =>
		_floatingManager.ContainsWindow(window) || InnerLayoutEngine.ContainsWindow(window);

	// TODO: Fix those function ?
	/// <inheritdoc />
	public override ILayoutEngine MinimizeWindowStart(IWindow window) =>
		UpdateInner(InnerLayoutEngine.MinimizeWindowStart(window), window);

	/// <inheritdoc />
	public override ILayoutEngine MinimizeWindowEnd(IWindow window) =>
		UpdateInner(InnerLayoutEngine.MinimizeWindowEnd(window), window);

	/// <inheritdoc />
	public override ILayoutEngine PerformCustomAction<T>(LayoutEngineCustomAction<T> action)
	{
		if (IsWindowFloating(action.Window))
		{
			// At this stage, we don't have a way to get the window in a child layout engine at
			// a given point.
			// For now, we do nothing.
			return this;
		}

		return UpdateInner(InnerLayoutEngine.PerformCustomAction(action), action.Window);
	}
}
