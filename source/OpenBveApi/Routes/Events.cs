﻿using OpenBveApi.Trains;

namespace OpenBveApi.Routes
{
	/// <summary>The general event is the abstract event type which all events must inherit from</summary>
	public abstract class GeneralEvent<T, TT> where T : AbstractTrain where TT : AbstractCar
	{
		/// <summary>The delta track position that this event is placed at</summary>
		public double TrackPositionDelta;
		/// <summary>Whether this event should trigger once for the specified train, or multiple times</summary>
		public bool DontTriggerAnymore;

		/// <summary>The unconditional event trigger function</summary>
		/// <param name="currentTime">The in-game time in seconds since minight</param>
		/// <param name="Direction">The direction:
		///  1 - Forwards
		/// -1 - Reverse</param>
		/// <param name="TriggerType">The trigger type (Car axle, camera follower etc.)</param>
		/// <param name="Train">The train, or a null reference</param>
		/// <param name="Car">The car, or a null reference</param>
		public abstract void Trigger(double currentTime, int Direction, EventTriggerType TriggerType, T Train, TT Car);

		/// <summary>This method is called to attempt to trigger an event</summary>
		/// <param name="currentTime">The in-game time in seconds since midnight</param>
		/// <param name="Direction">The direction:
		///  1 - Forwards
		/// -1 - Reverse</param>
		/// <param name="TriggerType">The trigger type (Car axle, camera follower etc.)</param>
		/// <param name="Train">The train, or a null reference</param>
		/// <param name="Car">The car, or a null reference</param>
		public void TryTrigger(double currentTime, int Direction, EventTriggerType TriggerType, T Train, TT Car)
		{
			if (!DontTriggerAnymore)
			{
				Trigger(currentTime, Direction, TriggerType, Train, Car);
			}
		}
	}
}
