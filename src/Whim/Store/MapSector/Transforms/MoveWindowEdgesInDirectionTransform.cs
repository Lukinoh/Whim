namespace Whim;

/// <summary>
/// Moves the given <paramref name="WindowHandle"/> by the given <paramref name="PixelsDeltas"/>.
/// </summary>
/// <param name="Edges">The edges to change.</param>
/// <param name="PixelsDeltas">
/// The deltas (in pixels) to change the given <paramref name="Edges"/> by. When a value is
/// positive, then the edge will move in the direction of the <paramref name="Edges"/>.
/// The <paramref name="PixelsDeltas"/> are in the coordinate space of the monitors, not the
/// unit square.
/// </param>
/// <param name="WindowHandle">
/// The handle of the window to move. If not provided, the last focused window will be used.
/// </param>
/// <returns>Whether the window's edges were moved.</returns>
public record MoveWindowEdgesInDirectionTransform(
	Direction Edges,
	IPoint<int> PixelsDeltas,
	HWND WindowHandle = default
) : Transform
{
	internal override Result<Unit> Execute(IContext ctx, IInternalContext internalCtx, MutableRootSector rootSector)
	{
		Logger.Error($"PIXELS DELTA {PixelsDeltas}");

		HWND windowHandle = WindowHandle.OrLastFocusedWindow(ctx);
		if (windowHandle == default)
		{
			return Result.FromException<Unit>(StoreExceptions.NoValidWindow());
		}

		Result<IWindow> windowResult = ctx.Store.Pick(PickWindowByHandle(windowHandle));
		if (!windowResult.TryGet(out IWindow window))
		{
			return Result.FromException<Unit>(windowResult.Error!);
		}

		// New
		IRectangle<int>? newRectangle = ctx.NativeManager.DwmGetWindowRectangle(windowHandle);
		if (newRectangle == null)
		{
			// TOFIX
			return Result.FromException<Unit>(StoreExceptions.NoValidWindow());
		}

		IPoint<int> newRectanglePoint = new Point<int>(newRectangle.X, newRectangle.Y);
		Result<IMonitor> newMonitorResult = ctx.Store.Pick(PickMonitorAtPoint(newRectanglePoint));
		if (!newMonitorResult.TryGet(out IMonitor newMonitor))
		{
			return Result.FromException<Unit>(StoreExceptions.NoMonitorFoundAtPoint(newRectanglePoint));
		}

		Result<IWorkspace> newWorkspaceResult = ctx.Store.Pick(PickWorkspaceByMonitor(newMonitor.Handle));
		if (!newWorkspaceResult.TryGet(out IWorkspace newWorkspace))
		{
			return Result.FromException<Unit>(newWorkspaceResult.Error!);
		}

		Logger.Error($"New rectangle {newRectangle}");
		Logger.Error($"New Monitor {newMonitor}");

		// Old
		Result<IWorkspace> oldWorkspaceResult = ctx.Store.Pick(PickWorkspaceByWindow(windowHandle));
		if (!oldWorkspaceResult.TryGet(out IWorkspace oldWorkspace))
		{
			return Result.FromException<Unit>(oldWorkspaceResult.Error!);
		}

		// If the window is being moved to a different workspace, remove it from the current workspace.
		if (newWorkspace.Id != oldWorkspace.Id)
		{
			rootSector.MapSector.WindowWorkspaceMap = rootSector.MapSector.WindowWorkspaceMap.SetItem(
				WindowHandle,
				newWorkspace.Id
			);
			oldWorkspace.RemoveWindow(window: window);
			oldWorkspace.DoLayout();
		}

		// Normalize `PixelsDeltas` into the unit square.
		IPoint<double> normalized = newMonitor.WorkingArea.NormalizeDeltaPoint(PixelsDeltas);

		Logger.Debug($"Normalized point: {normalized}");
		newWorkspace.MoveWindowEdgesInDirection(Edges, normalized, window, deferLayout: false);

		Result<IWorkspace> workspaceResult = ctx.Store.Pick(Pickers.PickWorkspaceByWindow(window.Handle));
		if (workspaceResult.TryGet(out IWorkspace workspace))
		{
			Logger.Error($"WORKSPACE ID ${workspace}");
		}
		return Unit.Result;
	}
}
