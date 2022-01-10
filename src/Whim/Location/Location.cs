﻿namespace Whim;

public class Location : ILocation
{
	public int X { get; }

	public int Y { get; }

	public int Width { get; }

	public int Height { get; }

	public Location(int x, int y, int width, int height)
	{
		X = x;
		Y = y;
		Width = width;
		Height = height;
	}

	public bool IsPointInside(int x, int y) => IsPointInside(this, x, y);

	public override string ToString() => $"(X: {X}, Y: {Y}, Width: {Width}, Height: {Height})";

	public static bool IsPointInside(ILocation location, int x, int y) => location.X <= x
		&& location.Y <= y
		&& location.X + location.Width >= x
		&& location.Y + location.Height >= y;

	public static ILocation Add(ILocation a, ILocation b) => new Location(
		a.X + b.X,
		a.Y + b.Y,
		a.Width + b.Width,
		a.Height + b.Height);
}
