﻿using System.Collections.Immutable;

namespace Whim.FloatingLayout;

/// <summary>
/// Describe the result of the UpdateWindowRectangle
/// </summary>
public enum UpdateWindowStatus
{
	Error,
	NoChange,
	Updated,
}

/// <summary>
/// Provide methods for the floating engines
/// </summary>
public static class FloatingUtils
{
	/// <summary>
	/// Update the position of a <paramref name="window"/> from the <paramref name="dict"/>.
	/// </summary>
	/// <param name="context"></param>
	/// <param name="dict"></param>
	/// <param name="window"></param>
	/// <returns>A tuple with the maybe updated <paramref name="dict"/>, and its <param cref="UpdateWindowStatus"></param></returns>
	public static (
		ImmutableDictionary<IWindow, IRectangle<double>> dict,
		UpdateWindowStatus status
	) UpdateWindowRectangle(IContext context, ImmutableDictionary<IWindow, IRectangle<double>> dict, IWindow window)
	{
		// Try get the old rectangle.
		IRectangle<double>? oldRectangle = dict.TryGetValue(window, out IRectangle<double>? rectangle)
			? rectangle
			: null;

		// Since the window is floating, we update the rectangle, and return.
		IRectangle<int>? newActualRectangle = context.NativeManager.DwmGetWindowRectangle(window.Handle);
		if (newActualRectangle == null)
		{
			Logger.Error($"Could not obtain rectangle for floating window {window}");
			return (dict, UpdateWindowStatus.Error);
		}

		IMonitor newMonitor = context.MonitorManager.GetMonitorAtPoint(newActualRectangle);
		IRectangle<double> newUnitSquareRectangle = newMonitor.WorkingArea.NormalizeRectangle(newActualRectangle);
		if (newUnitSquareRectangle.Equals(oldRectangle))
		{
			Logger.Debug($"Rectangle for window {window} has not changed");
			return (dict, UpdateWindowStatus.NoChange);
		}

		ImmutableDictionary<IWindow, IRectangle<double>> newDict = dict.SetItem(window, newUnitSquareRectangle);

		return (newDict, UpdateWindowStatus.Updated);
	}
}
