﻿namespace OpenBve
{
	internal static partial class World
	{
		/// <summary>Defines a background</summary>
		internal struct Background
		{
			internal Textures.Texture Texture;
			internal int Repetition;
			internal bool KeepAspectRatio;
			internal Background(Textures.Texture Texture, int Repetition, bool KeepAspectRatio)
			{
				this.Texture = Texture;
				this.Repetition = Repetition;
				this.KeepAspectRatio = KeepAspectRatio;
			}
		}

		/// <summary>The currently displayed background texture</summary>
		internal static Background CurrentBackground = new Background(null, 6, false);
		/// <summary>The new background texture (Currently fading in)</summary>
		internal static Background TargetBackground = new Background(null, 6, false);
		/// <summary>Defines the time in seconds taken for a new background to fade in</summary>
		internal const double TargetBackgroundDefaultCountdown = 0.8;
		/// <summary>The time remaining before the current background is at 100 % opacity</summary>
		internal static double TargetBackgroundCountdown;
	}
}