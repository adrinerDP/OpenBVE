﻿using TrainManager.Car;
using TrainManager.SafetySystems;

namespace OpenBve
{
	/// <summary>The TrainManager is the root class containing functions to load and manage trains within the simulation world.</summary>
	public partial class TrainManager
	{
		internal struct TrainSpecs
		{
			internal double CurrentAverageAcceleration;
			internal double CurrentAirPressure;
			internal double CurrentAirDensity;
			internal double CurrentAirTemperature;

			internal DefaultSafetySystems DefaultSafetySystems;
			internal bool HasConstSpeed;
			internal bool CurrentConstSpeed;
			
			internal DoorMode DoorOpenMode;
			internal DoorMode DoorCloseMode;
			
			internal bool DoorClosureAttempted;
		}
	}
}
