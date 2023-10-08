using NSubstitute;
using Whim.TestUtils;
using Xunit;

namespace Whim.LayoutPreview.Tests;

public class LayoutPreviewWindowTests
{
	#region ShouldContinue
	[Fact]
	public void ShouldContinue_DifferentLength()
	{
		// Given
		IWindowState[] prevWindowStates = Array.Empty<IWindowState>();
		int prevHoveredIndex = -1;
		IWindowState[] windowStates = new IWindowState[1];
		IPoint<int> cursorPoint = new Location<int>();

		// When
		bool shouldContinue = LayoutPreviewWindow.ShouldContinue(
			prevWindowStates,
			prevHoveredIndex,
			windowStates,
			cursorPoint
		);

		// Then
		Assert.True(shouldContinue);
	}

	[Fact]
	public void ShouldContinue_DifferentWindowState()
	{
		// Given
		IWindowState[] prevWindowStates = new IWindowState[]
		{
			new WindowState()
			{
				Window = Substitute.For<IWindow>(),
				Location = new Location<int>(),
				WindowSize = WindowSize.Normal
			},
			new WindowState()
			{
				Window = Substitute.For<IWindow>(),
				Location = new Location<int>(),
				WindowSize = WindowSize.Normal
			},
		};
		int prevHoveredIndex = -1;
		IWindowState[] windowStates = new IWindowState[]
		{
			prevWindowStates[0],
			new WindowState()
			{
				Window = Substitute.For<IWindow>(),
				Location = new Location<int>(),
				WindowSize = WindowSize.Maximized
			},
		};
		IPoint<int> cursorPoint = new Location<int>();

		// When
		bool shouldContinue = LayoutPreviewWindow.ShouldContinue(
			prevWindowStates,
			prevHoveredIndex,
			windowStates,
			cursorPoint
		);

		// Then
		Assert.True(shouldContinue);
	}

	[Fact]
	public void ShouldContinue_HoveredIndexChanged()
	{
		// Given
		Location<int> location = new() { Height = 100, Width = 100 };
		IWindowState[] prevWindowStates = new IWindowState[]
		{
			new WindowState()
			{
				Window = Substitute.For<IWindow>(),
				Location = location,
				WindowSize = WindowSize.Normal
			},
			new WindowState()
			{
				Window = Substitute.For<IWindow>(),
				Location = new Location<int>(),
				WindowSize = WindowSize.Normal
			},
		};
		int prevHoveredIndex = 0;
		IWindowState[] windowStates = new IWindowState[]
		{
			new WindowState()
			{
				Window = Substitute.For<IWindow>(),
				Location = location,
				WindowSize = WindowSize.Normal
			},
			new WindowState()
			{
				Window = Substitute.For<IWindow>(),
				Location = new Location<int>(),
				WindowSize = WindowSize.Normal
			},
		};
		IPoint<int> cursorPoint = new Location<int>() { X = 100, Y = 101 };

		// When
		bool shouldContinue = LayoutPreviewWindow.ShouldContinue(
			prevWindowStates,
			prevHoveredIndex,
			windowStates,
			cursorPoint
		);

		// Then
		Assert.True(shouldContinue);
	}

	[Fact]
	public void ShouldContinue_HoveredIndexNotChanged()
	{
		// Given
		Location<int> location = new() { Height = 100, Width = 100 };
		IWindowState[] prevWindowStates = new IWindowState[]
		{
			new WindowState()
			{
				Window = Substitute.For<IWindow>(),
				Location = location,
				WindowSize = WindowSize.Normal
			},
			new WindowState()
			{
				Window = Substitute.For<IWindow>(),
				Location = new Location<int>(),
				WindowSize = WindowSize.Normal
			},
		};
		int prevHoveredIndex = 0;
		IPoint<int> cursorPoint = new Location<int>() { X = 50, Y = 50 };

		// When
		bool shouldContinue = LayoutPreviewWindow.ShouldContinue(
			prevWindowStates,
			prevHoveredIndex,
			prevWindowStates,
			cursorPoint
		);

		// Then
		Assert.False(shouldContinue);
	}
	#endregion

	[Theory, AutoSubstituteData]
	public void Activate(IContext ctx, IWindow layoutWindow, IWindow movingWindow, IMonitor monitor)
	{
		// When
		LayoutPreviewWindow.Activate(ctx, layoutWindow, movingWindow, monitor);

		// Then
		ctx.NativeManager.Received(1).BeginDeferWindowPos(1);
	}
}