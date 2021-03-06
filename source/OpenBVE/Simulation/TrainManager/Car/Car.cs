using System;
using LibRender2;
using LibRender2.Camera;
using LibRender2.Cameras;
using LibRender2.Trains;
using TrainManager.BrakeSystems;
using OpenBveApi.Graphics;
using OpenBveApi.Math;
using OpenBveApi.Objects;
using OpenBveApi.Routes;
using OpenBveApi.Runtime;
using OpenBveApi.Trains;
using OpenBveApi.World;
using SoundManager;
using TrainManager.Car;
using TrainManager.Motor;

namespace OpenBve
{
	public partial class TrainManager
	{
		/// <summary>The base class containing the properties of a train car</summary>
		internal partial class Car : AbstractCar
		{
			/// <summary>The front bogie</summary>
			internal Bogie FrontBogie;
			/// <summary>The rear bogie</summary>
			internal Bogie RearBogie;
			/// <summary>The horns attached to this car</summary>
			internal Horn[] Horns;
			/// <summary>The doors for this car</summary>
			internal readonly Door[] Doors;
			/// <summary>The car brake for this car</summary>
			internal CarBrake CarBrake;
			/// <summary>The car sections (objects) attached to the car</summary>
			internal CarSection[] CarSections;
			/// <summary>The index of the current car section</summary>
			internal int CurrentCarSection;
			/// <summary>The driver's eye position within the car</summary>
			internal Vector3 Driver;
			/// <summary>The current yaw of the driver's eyes</summary>
			internal double DriverYaw;
			/// <summary>The current pitch of the driver's eyes</summary>
			internal double DriverPitch;
			internal CarSpecs Specs;
			internal CarSounds Sounds;
			/// <summary>Whether currently visible from the in-game camera location</summary>
			internal bool CurrentlyVisible;
			/// <summary>Whether currently derailed</summary>
			internal bool Derailed;
			/// <summary>Whether currently toppled over</summary>
			internal bool Topples;
			/// <summary>The coupler between cars</summary>
			internal Coupler Coupler;

			internal Windscreen Windscreen;
			internal double BeaconReceiverPosition;
			internal TrackFollower BeaconReceiver;
			/// <summary>Whether loading sway is enabled for this car</summary>
			internal bool EnableLoadingSway = true;
			/// <summary>A reference to the base train</summary>
			internal readonly Train baseTrain;

			/// <summary>Stores the camera restriction mode for the interior view of this car</summary>
			internal CameraRestrictionMode CameraRestrictionMode = CameraRestrictionMode.NotSpecified;

			internal CameraRestriction CameraRestriction;
			/// <summary>Stores the camera interior camera alignment for this car</summary>
			internal CameraAlignment InteriorCamera;

			internal bool HasInteriorView = false;
			
			internal Car(Train train, int index, double CoefficientOfFriction, double CoefficientOfRollingResistance, double AerodynamicDragCoefficient)
			{
				baseTrain = train;
				Index = index;
				CarSections = new CarSection[] { };
				FrontAxle = new Axle(Program.CurrentHost, train, this, CoefficientOfFriction, CoefficientOfRollingResistance, AerodynamicDragCoefficient);
				RearAxle = new Axle(Program.CurrentHost, train, this, CoefficientOfFriction, CoefficientOfRollingResistance, AerodynamicDragCoefficient);
				BeaconReceiver = new TrackFollower(Program.CurrentHost, train);
				FrontBogie = new Bogie(train, this);
				RearBogie = new Bogie(train, this);
				Doors = new Door[2];
				Doors[0].Width = 1000.0;
				Doors[0].MaxTolerance = 0.0;
				Doors[1].Width = 1000.0;
				Doors[1].MaxTolerance = 0.0;
			}

			internal Car(Train train, int index)
			{
				baseTrain = train;
				Index = index;
				CarSections = new CarSection[] { };
				FrontAxle = new Axle(Program.CurrentHost, train, this);
				RearAxle = new Axle(Program.CurrentHost, train, this);
				BeaconReceiver = new TrackFollower(Program.CurrentHost, train);
				FrontBogie = new Bogie(train, this);
				RearBogie = new Bogie(train, this);
				Doors = new Door[2];
				Doors[0].Width = 1000.0;
				Doors[0].MaxTolerance = 0.0;
				Doors[1].Width = 1000.0;
				Doors[1].MaxTolerance = 0.0;
			}

			/// <summary>Moves the car</summary>
			/// <param name="Delta">The delta to move</param>
			internal void Move(double Delta)
			{
				if (baseTrain.State != TrainState.Disposed)
				{
					FrontAxle.Follower.UpdateRelative(Delta, true, true);
					FrontBogie.FrontAxle.Follower.UpdateRelative(Delta, true, true);
					FrontBogie.RearAxle.Follower.UpdateRelative(Delta, true, true);
					if (baseTrain.State != TrainState.Disposed)
					{
						RearAxle.Follower.UpdateRelative(Delta, true, true);
						RearBogie.FrontAxle.Follower.UpdateRelative(Delta, true, true);
						RearBogie.RearAxle.Follower.UpdateRelative(Delta, true, true);
						if (baseTrain.State != TrainState.Disposed)
						{
							BeaconReceiver.UpdateRelative(Delta, true, true);
						}
					}
				}
			}

			/// <summary>Call this method to update all track followers attached to the car</summary>
			/// <param name="NewTrackPosition">The track position change</param>
			/// <param name="UpdateWorldCoordinates">Whether to update the world co-ordinates</param>
			/// <param name="AddTrackInaccurary">Whether to add track innaccuarcy</param>
			internal void UpdateTrackFollowers(double NewTrackPosition, bool UpdateWorldCoordinates, bool AddTrackInaccurary)
			{
				//Car axles
				FrontAxle.Follower.UpdateRelative(NewTrackPosition, UpdateWorldCoordinates, AddTrackInaccurary);
				RearAxle.Follower.UpdateRelative(NewTrackPosition, UpdateWorldCoordinates, AddTrackInaccurary);
				//Front bogie axles
				FrontBogie.FrontAxle.Follower.UpdateRelative(NewTrackPosition, UpdateWorldCoordinates, AddTrackInaccurary);
				FrontBogie.RearAxle.Follower.UpdateRelative(NewTrackPosition, UpdateWorldCoordinates, AddTrackInaccurary);
				//Rear bogie axles

				RearBogie.FrontAxle.Follower.UpdateRelative(NewTrackPosition, UpdateWorldCoordinates, AddTrackInaccurary);
				RearBogie.RearAxle.Follower.UpdateRelative(NewTrackPosition, UpdateWorldCoordinates, AddTrackInaccurary);
			}

			/// <summary>Initializes the car</summary>
			internal void Initialize()
			{
				for (int i = 0; i < CarSections.Length; i++)
				{
					CarSections[i].Initialize(false);
				}
				for (int i = 0; i < FrontBogie.CarSections.Length; i++)
				{
					FrontBogie.CarSections[i].Initialize(false);
				}
				for (int i = 0; i < RearBogie.CarSections.Length; i++)
				{
					RearBogie.CarSections[i].Initialize(false);
				}
				Brightness.PreviousBrightness = 1.0f;
				Brightness.NextBrightness = 1.0f;
			}

			/// <summary>Synchronizes the car after a period of infrequent updates</summary>
			internal void Syncronize()
			{
				double s = 0.5 * (FrontAxle.Follower.TrackPosition + RearAxle.Follower.TrackPosition);
				double d = 0.5 * (FrontAxle.Follower.TrackPosition - RearAxle.Follower.TrackPosition);
				FrontAxle.Follower.UpdateAbsolute(s + d, false, false);
				RearAxle.Follower.UpdateAbsolute(s - d, false, false);
				double b = FrontAxle.Follower.TrackPosition - FrontAxle.Position + BeaconReceiverPosition;
				BeaconReceiver.UpdateAbsolute(b, false, false);
			}

			public override void CreateWorldCoordinates(Vector3 Car, out Vector3 Position, out Vector3 Direction)
			{
				Direction = FrontAxle.Follower.WorldPosition - RearAxle.Follower.WorldPosition;
				double t = Direction.NormSquared();
				if (t != 0.0)
				{
					t = 1.0 / Math.Sqrt(t);
					Direction *= t;
					double sx = Direction.Z * Up.Y - Direction.Y * Up.Z;
					double sy = Direction.X * Up.Z - Direction.Z * Up.X;
					double sz = Direction.Y * Up.X - Direction.X * Up.Y;
					double rx = 0.5 * (FrontAxle.Follower.WorldPosition.X + RearAxle.Follower.WorldPosition.X);
					double ry = 0.5 * (FrontAxle.Follower.WorldPosition.Y + RearAxle.Follower.WorldPosition.Y);
					double rz = 0.5 * (FrontAxle.Follower.WorldPosition.Z + RearAxle.Follower.WorldPosition.Z);
					Position.X = rx + sx * Car.X + Up.X * Car.Y + Direction.X * Car.Z;
					Position.Y = ry + sy * Car.X + Up.Y * Car.Y + Direction.Y * Car.Z;
					Position.Z = rz + sz * Car.X + Up.Z * Car.Y + Direction.Z * Car.Z;
				}
				else
				{
					Position = FrontAxle.Follower.WorldPosition;
					Direction = Vector3.Down;
				}
			}

			public override double TrackPosition
			{
				get
				{
					return FrontAxle.Follower.TrackPosition;
				}
				
			}

			/// <summary>Backing property for the index of the car within the train</summary>
			public override int Index
			{
				get;
			}

			public override void Reverse()
			{
				// reverse axle positions
				double temp = FrontAxle.Position;
				FrontAxle.Position = -RearAxle.Position;
				RearAxle.Position = -temp;
				int idxToReverse = HasInteriorView ? 1 : 0;
				if (CarSections != null && CarSections.Length > 0)
				{
					foreach (var carSection in CarSections[idxToReverse].Groups[0].Elements)
					{
						for (int h = 0; h < carSection.States.Length; h++)
						{
							carSection.States[h].Prototype.ApplyScale(-1.0, 1.0, -1.0);
							Matrix4D t = carSection.States[h].Translation;
							t.Row3.X *= -1.0f;
							t.Row3.Z *= -1.0f;
							carSection.States[h].Translation = t;
						}

						carSection.TranslateXDirection.X *= -1.0;
						carSection.TranslateXDirection.Z *= -1.0;
						carSection.TranslateYDirection.X *= -1.0;
						carSection.TranslateYDirection.Z *= -1.0;
						carSection.TranslateZDirection.X *= -1.0;
						carSection.TranslateZDirection.Z *= -1.0;
					}
				}

				Bogie b = RearBogie;
				RearBogie = FrontBogie;
				FrontBogie = b;
				FrontBogie.Reverse();
				RearBogie.Reverse();
				FrontBogie.FrontAxle.Follower.UpdateAbsolute(FrontAxle.Position + FrontBogie.FrontAxle.Position, true, false);
				FrontBogie.RearAxle.Follower.UpdateAbsolute(FrontAxle.Position + FrontBogie.RearAxle.Position, true, false);

				RearBogie.FrontAxle.Follower.UpdateAbsolute(RearAxle.Position + RearBogie.FrontAxle.Position, true, false);
				RearBogie.RearAxle.Follower.UpdateAbsolute(RearAxle.Position + RearBogie.RearAxle.Position, true, false);
				
			}

			public override void OpenDoors(bool Left, bool Right)
			{
				bool sl = false, sr = false;
				if (Left & !Doors[0].AnticipatedOpen & (baseTrain.SafetySystems.DoorInterlockState == DoorInterlockStates.Left | baseTrain.SafetySystems.DoorInterlockState == DoorInterlockStates.Unlocked))
				{
					Doors[0].AnticipatedOpen = true;
					sl = true;
				}
				if (Right & !Doors[1].AnticipatedOpen & (baseTrain.SafetySystems.DoorInterlockState == DoorInterlockStates.Right | baseTrain.SafetySystems.DoorInterlockState == DoorInterlockStates.Unlocked))
				{
					Doors[1].AnticipatedOpen = true;
					sr = true;
				}
				if (sl)
				{
					SoundBuffer buffer = Doors[0].OpenSound.Buffer;
					if (buffer != null)
					{
						OpenBveApi.Math.Vector3 pos = Doors[0].OpenSound.Position;
						Program.Sounds.PlaySound(buffer, Specs.DoorOpenPitch, 1.0, pos, this, false);
					}
					for (int i = 0; i < Doors.Length; i++)
					{
						if (Doors[i].Direction == -1)
						{
							Doors[i].DoorLockDuration = 0.0;
						}
					}
				}
				if (sr)
				{
					SoundBuffer buffer = Doors[1].OpenSound.Buffer;
					if (buffer != null)
					{
						OpenBveApi.Math.Vector3 pos = Doors[1].OpenSound.Position;
						Program.Sounds.PlaySound(buffer, Specs.DoorOpenPitch, 1.0, pos, this, false);
					}
					for (int i = 0; i < Doors.Length; i++)
					{
						if (Doors[i].Direction == 1)
						{
							Doors[i].DoorLockDuration = 0.0;
						}
					}
				}
				for (int i = 0; i < Doors.Length; i++)
				{
					if (Doors[i].AnticipatedOpen)
					{
						Doors[i].NextReopenTime = 0.0;
						Doors[i].ReopenCounter++;
					}
				}
			}

			/// <summary>Returns the combination of door states what encountered at the specified car in a train.</summary>
			/// <param name="Left">Whether to include left doors.</param>
			/// <param name="Right">Whether to include right doors.</param>
			/// <returns>A bit mask combining encountered door states.</returns>
			internal TrainDoorState GetDoorsState(bool Left, bool Right)
			{
				bool opened = false, closed = false, mixed = false;
				for (int i = 0; i < Doors.Length; i++)
				{
					if (Left & Doors[i].Direction == -1 | Right & Doors[i].Direction == 1)
					{
						if (Doors[i].State == 0.0)
						{
							closed = true;
						}
						else if (Doors[i].State == 1.0)
						{
							opened = true;
						}
						else
						{
							mixed = true;
						}
					}
				}
				TrainDoorState Result = TrainDoorState.None;
				if (opened) Result |= TrainDoorState.Opened;
				if (closed) Result |= TrainDoorState.Closed;
				if (mixed) Result |= TrainDoorState.Mixed;
				if (opened & !closed & !mixed) Result |= TrainDoorState.AllOpened;
				if (!opened & closed & !mixed) Result |= TrainDoorState.AllClosed;
				if (!opened & !closed & mixed) Result |= TrainDoorState.AllMixed;
				return Result;
			}

			internal void UpdateRunSounds(double TimeElapsed)
			{
				if (Sounds.Run == null || Sounds.Run.Length == 0)
				{
					return;
				}
				const double factor = 0.04; // 90 km/h -> m/s -> 1/x
				double speed = Math.Abs(CurrentSpeed);
				if (Derailed)
				{
					speed = 0.0;
				}
				double pitch = speed * factor;
				double basegain;
				if (CurrentSpeed == 0.0)
				{
					if (Index != 0)
					{
						Sounds.RunNextReasynchronizationPosition = baseTrain.Cars[0].FrontAxle.Follower.TrackPosition;
					}
				}
				else if (Sounds.RunNextReasynchronizationPosition == double.MaxValue & FrontAxle.RunIndex >= 0)
				{
					double distance = Math.Abs(FrontAxle.Follower.TrackPosition - Program.Renderer.CameraTrackFollower.TrackPosition);
					const double minDistance = 150.0;
					const double maxDistance = 750.0;
					if (distance > minDistance)
					{
						if (FrontAxle.RunIndex < Sounds.Run.Length)
						{
							SoundBuffer buffer = Sounds.Run[FrontAxle.RunIndex].Buffer;
							if (buffer != null)
							{
								double duration = Program.Sounds.GetDuration(buffer);
								if (duration > 0.0)
								{
									double offset = distance > maxDistance ? 25.0 : 300.0;
									Sounds.RunNextReasynchronizationPosition = duration * Math.Ceiling((baseTrain.Cars[0].FrontAxle.Follower.TrackPosition + offset) / duration);
								}
							}
						}
					}
				}
				if (FrontAxle.Follower.TrackPosition >= Sounds.RunNextReasynchronizationPosition)
				{
					Sounds.RunNextReasynchronizationPosition = double.MaxValue;
					basegain = 0.0;
				}
				else
				{
					basegain = speed < 2.77777777777778 ? 0.36 * speed : 1.0;
				}
				for (int j = 0; j < Sounds.Run.Length; j++)
				{
					if (j == FrontAxle.RunIndex | j == RearAxle.RunIndex)
					{
						Sounds.RunVolume[j] += 3.0 * TimeElapsed;
						if (Sounds.RunVolume[j] > 1.0) Sounds.RunVolume[j] = 1.0;
					}
					else
					{
						Sounds.RunVolume[j] -= 3.0 * TimeElapsed;
						if (Sounds.RunVolume[j] < 0.0) Sounds.RunVolume[j] = 0.0;
					}
					double gain = basegain * Sounds.RunVolume[j];
					if (Program.Sounds.IsPlaying(Sounds.Run[j].Source))
					{
						if (pitch > 0.01 & gain > 0.001)
						{
							Sounds.Run[j].Source.Pitch = pitch;
							Sounds.Run[j].Source.Volume = gain;
						}
						else
						{
							Program.Sounds.StopSound(Sounds.Run[j]);
						}
					}
					else if (pitch > 0.02 & gain > 0.01)
					{
						SoundBuffer buffer = Sounds.Run[j].Buffer;
						if (buffer != null)
						{
							Sounds.Run[j].Source = Program.Sounds.PlaySound(buffer, pitch, gain, Sounds.Run[j].Position, this, true);
						}
					}
				}
			}

			internal void UpdateMotorSounds(double TimeElapsed)
			{
				if (!this.Specs.IsMotorCar)
				{
					return;
				}
				double speed = Math.Abs(Specs.CurrentPerceivedSpeed);
				int idx = (int)Math.Round(speed * Sounds.Motor.SpeedConversionFactor);
				int odir = Sounds.Motor.CurrentAccelerationDirection;
				int ndir = Math.Sign(Specs.CurrentAccelerationOutput);
				for (int h = 0; h < 2; h++)
				{
					int j = h == 0 ? BVEMotorSound.MotorP1 : BVEMotorSound.MotorP2;
					int k = h == 0 ? BVEMotorSound.MotorB1 : BVEMotorSound.MotorB2;
					if (odir > 0 & ndir <= 0)
					{
						if (j < Sounds.Motor.Tables.Length)
						{
							Program.Sounds.StopSound(Sounds.Motor.Tables[j].Source);
							Sounds.Motor.Tables[j].Source = null;
							Sounds.Motor.Tables[j].Buffer = null;
						}
					}
					else if (odir < 0 & ndir >= 0)
					{
						if (k < Sounds.Motor.Tables.Length)
						{
							Program.Sounds.StopSound(Sounds.Motor.Tables[k].Source);
							Sounds.Motor.Tables[k].Source = null;
							Sounds.Motor.Tables[k].Buffer = null;
						}
					}
					if (ndir != 0)
					{
						if (ndir < 0) j = k;
						if (j < Sounds.Motor.Tables.Length)
						{
							int idx2 = idx;
							if (idx2 >= Sounds.Motor.Tables[j].Entries.Length)
							{
								idx2 = Sounds.Motor.Tables[j].Entries.Length - 1;
							}
							if (idx2 >= 0)
							{
								SoundBuffer obuf = Sounds.Motor.Tables[j].Buffer;
								SoundBuffer nbuf = Sounds.Motor.Tables[j].Entries[idx2].Buffer;
								double pitch = Sounds.Motor.Tables[j].Entries[idx2].Pitch;
								double gain = Sounds.Motor.Tables[j].Entries[idx2].Gain;
								if (ndir == 1)
								{
									// power
									double max = Specs.AccelerationCurveMaximum;
									if (max != 0.0)
									{
										double cur = Specs.CurrentAccelerationOutput;
										if (cur < 0.0) cur = 0.0;
										gain *= Math.Pow(cur / max, 0.25);
									}
								}
								else if (ndir == -1)
								{
									// brake
									double max = CarBrake.DecelerationAtServiceMaximumPressure(baseTrain.Handles.Brake.Actual, CurrentSpeed);
									if (max != 0.0)
									{
										double cur = -Specs.CurrentAccelerationOutput;
										if (cur < 0.0) cur = 0.0;
										gain *= Math.Pow(cur / max, 0.25);
									}
								}

								if (obuf != nbuf)
								{
									Program.Sounds.StopSound(Sounds.Motor.Tables[j].Source);
									if (nbuf != null)
									{
										Sounds.Motor.Tables[j].Source = Program.Sounds.PlaySound(nbuf, pitch, gain, Sounds.Motor.Position, this, true);
										Sounds.Motor.Tables[j].Buffer = nbuf;
									}
									else
									{
										Sounds.Motor.Tables[j].Source = null;
										Sounds.Motor.Tables[j].Buffer = null;
									}
								}
								else if (nbuf != null)
								{
									if (Sounds.Motor.Tables[j].Source != null)
									{
										Sounds.Motor.Tables[j].Source.Pitch = pitch;
										Sounds.Motor.Tables[j].Source.Volume = gain;
									}
								}
								else
								{
									Program.Sounds.StopSound(Sounds.Motor.Tables[j].Source);
									Sounds.Motor.Tables[j].Source = null;
									Sounds.Motor.Tables[j].Buffer = null;
								}
							}
							else
							{
								Program.Sounds.StopSound(Sounds.Motor.Tables[j].Source);
								Sounds.Motor.Tables[j].Source = null;
								Sounds.Motor.Tables[j].Buffer = null;
							}
						}
					}
				}
				Sounds.Motor.CurrentAccelerationDirection = ndir;
			}

			/// <summary>Loads Car Sections (Exterior objects etc.) for this car</summary>
			/// <param name="currentObject">The object to add to the car sections array</param>
			/// <param name="visibleFromInterior">Wether this is visible from the interior of other cars</param>
			internal void LoadCarSections(UnifiedObject currentObject, bool visibleFromInterior)
			{
				int j = CarSections.Length;
				Array.Resize(ref CarSections, j + 1);
				CarSections[j] = new CarSection(Program.Renderer, ObjectType.Dynamic);
				CarSections[j].VisibleFromInterior = visibleFromInterior;
				if (currentObject is StaticObject)
				{
					StaticObject s = (StaticObject)currentObject;
					CarSections[j].Groups[0].Elements = new AnimatedObject[1];
					CarSections[j].Groups[0].Elements[0] = new AnimatedObject(Program.CurrentHost)
					{
						States = new[] {new ObjectState(s)},
						CurrentState = 0
					};
					Program.CurrentHost.CreateDynamicObject(ref CarSections[j].Groups[0].Elements[0].internalObject);
				}
				else if (currentObject is AnimatedObjectCollection)
				{
					AnimatedObjectCollection a = (AnimatedObjectCollection)currentObject;
					CarSections[j].Groups[0].Elements = new AnimatedObject[a.Objects.Length];
					for (int h = 0; h < a.Objects.Length; h++)
					{
						CarSections[j].Groups[0].Elements[h] = a.Objects[h].Clone();
						Program.CurrentHost.CreateDynamicObject(ref CarSections[j].Groups[0].Elements[h].internalObject);
					}
				}
			}

			/// <summary>Changes the currently visible car section</summary>
			/// <param name="newCarSection">The type of new car section to display</param>
			/// <param name="trainVisible">Whether the train is visible</param>
			internal void ChangeCarSection(CarSectionType newCarSection, bool trainVisible = false)
			{
				if (trainVisible)
				{
					if (CurrentCarSection != -1 && CarSections[CurrentCarSection].VisibleFromInterior)
					{
						return;
					}
				}
				for (int i = 0; i < CarSections.Length; i++)
				{
					for (int j = 0; j < CarSections[i].Groups.Length; j++)
					{
						for (int k = 0; k < CarSections[i].Groups[j].Elements.Length; k++)
						{
							Program.CurrentHost.HideObject(CarSections[i].Groups[j].Elements[k].internalObject);
						}
					}
				}
				switch (newCarSection)
				{
					case CarSectionType.NotVisible:
						this.CurrentCarSection = -1;
						break;
					case CarSectionType.Interior:
						if (this.HasInteriorView && this.CarSections.Length > 0)
						{
							this.CurrentCarSection = 0;
							this.CarSections[0].Initialize(false);
							CarSections[0].Show();
							break;
						}
						this.CurrentCarSection = -1;
						break;
					case CarSectionType.Exterior:
						if (this.HasInteriorView && this.CarSections.Length > 1)
						{
							this.CurrentCarSection = 1;
							this.CarSections[1].Initialize(false);
							CarSections[1].Show();
							break;
						}
						else if(!this.HasInteriorView && this.CarSections.Length > 0)
						{
							this.CurrentCarSection = 0;
							this.CarSections[0].Initialize(false);
							CarSections[0].Show();
							break;
						}
						this.CurrentCarSection = -1;
						break;
				}
				//When changing car section, do not apply damping
				//This stops objects from spinning if the last position before they were hidden is different
				baseTrain.Cars[Index].UpdateObjects(0.0, true, false);
			}
			
			/// <summary>Updates the currently displayed objects for this car</summary>
			/// <param name="TimeElapsed">The time elapsed</param>
			/// <param name="ForceUpdate">Whether this is a forced update</param>
			/// <param name="EnableDamping">Whether damping is applied during this update (Skipped on transitions between camera views etc.)</param>
			internal void UpdateObjects(double TimeElapsed, bool ForceUpdate, bool EnableDamping)
			{
				// calculate positions and directions for section element update

				Vector3 d = new Vector3(FrontAxle.Follower.WorldPosition - RearAxle.Follower.WorldPosition);
				Vector3 u, s;
				double t = d.NormSquared();
				if (t != 0.0)
				{
					t = 1.0 / Math.Sqrt(t);
					d *= t;
					u = new Vector3(Up);
					s.X = d.Z * u.Y - d.Y * u.Z;
					s.Y = d.X * u.Z - d.Z * u.X;
					s.Z = d.Y * u.X - d.X * u.Y;
				}
				else
				{
					u = Vector3.Down;
					s = Vector3.Right;
				}
				Vector3 p = new Vector3(0.5 * (FrontAxle.Follower.WorldPosition + RearAxle.Follower.WorldPosition));
				p -= d * (0.5 * (FrontAxle.Position + RearAxle.Position));
				// determine visibility
				Vector3 cd = new Vector3(p - Program.Renderer.Camera.AbsolutePosition);
				double dist = cd.NormSquared();
				double bid = Interface.CurrentOptions.ViewingDistance + Length;
				CurrentlyVisible = dist < bid * bid;
				// Updates the brightness value
				byte dnb;
				{
					float b = (float)(Brightness.NextTrackPosition - Brightness.PreviousTrackPosition);

					//1.0f represents a route brightness value of 255
					//0.0f represents a route brightness value of 0

					if (b != 0.0f)
					{
						b = (float)(FrontAxle.Follower.TrackPosition - Brightness.PreviousTrackPosition) / b;
						if (b < 0.0f) b = 0.0f;
						if (b > 1.0f) b = 1.0f;
						b = Brightness.PreviousBrightness * (1.0f - b) + Brightness.NextBrightness * b;
					}
					else
					{
						b = Brightness.PreviousBrightness;
					}
					//Calculate the cab brightness
					double ccb = Math.Round(255.0 * (double)(1.0 - b));
					//DNB then must equal the smaller of the cab brightness value & the dynamic brightness value
					dnb = (byte)Math.Min(Program.Renderer.Lighting.DynamicCabBrightness, ccb);
				}
				// update current section
				int cs = CurrentCarSection;
				if (cs >= 0 && cs < CarSections.Length)
				{
					if (CarSections[cs].Groups.Length > 0)
					{
						for (int i = 0; i < CarSections[cs].Groups[0].Elements.Length; i++)
						{
							UpdateCarSectionElement(cs, 0, i, p, d, u, s, CurrentlyVisible, TimeElapsed, ForceUpdate, EnableDamping);

							// brightness change
							if (CarSections[cs].Groups[0].Elements[i].internalObject != null)
							{
								for (int j = 0; j < CarSections[cs].Groups[0].Elements[i].internalObject.Prototype.Mesh.Materials.Length; j++)
								{
									CarSections[cs].Groups[0].Elements[i].internalObject.Prototype.Mesh.Materials[j].DaytimeNighttimeBlend = dnb;
								}
							}
						}
					}

					int add = CarSections[cs].CurrentAdditionalGroup + 1;
					if (add < CarSections[cs].Groups.Length)
					{
						for (int i = 0; i < CarSections[cs].Groups[add].Elements.Length; i++)
						{
							UpdateCarSectionElement(cs, add, i, p, d, u, s, CurrentlyVisible, TimeElapsed, ForceUpdate, EnableDamping);

							// brightness change
							if (CarSections[cs].Groups[add].Elements[i].internalObject != null)
							{
								for (int j = 0; j < CarSections[cs].Groups[add].Elements[i].internalObject.Prototype.Mesh.Materials.Length; j++)
								{
									CarSections[cs].Groups[add].Elements[i].internalObject.Prototype.Mesh.Materials[j].DaytimeNighttimeBlend = dnb;
								}
							}
						}

						if (CarSections[cs].Groups[add].TouchElements != null)
						{
							for (int i = 0; i < CarSections[cs].Groups[add].TouchElements.Length; i++)
							{
								UpdateCarSectionTouchElement(cs, add, i, p, d, u, s, false, TimeElapsed, ForceUpdate, EnableDamping);
							}
						}
					}
				}
				//Update camera restriction
				
				CameraRestriction.AbsoluteBottomLeft = new Vector3(CameraRestriction.BottomLeft);
				CameraRestriction.AbsoluteBottomLeft += Driver;
				CameraRestriction.AbsoluteBottomLeft.Rotate(new Transformation(d, u, s));
				CameraRestriction.AbsoluteBottomLeft.Translate(p);

				CameraRestriction.AbsoluteTopRight = new Vector3(CameraRestriction.TopRight);
				CameraRestriction.AbsoluteTopRight += Driver;
				CameraRestriction.AbsoluteTopRight.Rotate(new Transformation(d, u, s));
				CameraRestriction.AbsoluteTopRight.Translate(p);

				
			}

			/// <summary>Updates the given car section element</summary>
			/// <param name="SectionIndex">The car section</param>
			/// <param name="GroupIndex">The group within the car section</param>
			/// <param name="ElementIndex">The element within the group</param>
			/// <param name="Position"></param>
			/// <param name="Direction"></param>
			/// <param name="Up"></param>
			/// <param name="Side"></param>
			/// <param name="Show"></param>
			/// <param name="TimeElapsed"></param>
			/// <param name="ForceUpdate"></param>
			/// <param name="EnableDamping"></param>
			private void UpdateCarSectionElement(int SectionIndex, int GroupIndex, int ElementIndex, Vector3 Position, Vector3 Direction, Vector3 Up, Vector3 Side, bool Show, double TimeElapsed, bool ForceUpdate, bool EnableDamping)
			{
				Vector3 p;
				if (CarSections[SectionIndex].Groups[GroupIndex].Type == ObjectType.Overlay & (Program.Renderer.Camera.CurrentRestriction != CameraRestrictionMode.NotAvailable && Program.Renderer.Camera.CurrentRestriction != CameraRestrictionMode.Restricted3D))
				{
					p = new Vector3(Driver.X, Driver.Y, Driver.Z);
				}
				else
				{
					p = Position;
				}
				double timeDelta;
				bool updatefunctions;
				if (CarSections[SectionIndex].Groups[GroupIndex].Elements[ElementIndex].RefreshRate != 0.0)
				{
					if (CarSections[SectionIndex].Groups[GroupIndex].Elements[ElementIndex].SecondsSinceLastUpdate >= CarSections[SectionIndex].Groups[GroupIndex].Elements[ElementIndex].RefreshRate)
					{
						timeDelta = CarSections[SectionIndex].Groups[GroupIndex].Elements[ElementIndex].SecondsSinceLastUpdate;
						CarSections[SectionIndex].Groups[GroupIndex].Elements[ElementIndex].SecondsSinceLastUpdate = TimeElapsed;
						updatefunctions = true;
					}
					else
					{
						timeDelta = TimeElapsed;
						CarSections[SectionIndex].Groups[GroupIndex].Elements[ElementIndex].SecondsSinceLastUpdate += TimeElapsed;
						updatefunctions = false;
					}
				}
				else
				{
					timeDelta = CarSections[SectionIndex].Groups[GroupIndex].Elements[ElementIndex].SecondsSinceLastUpdate;
					CarSections[SectionIndex].Groups[GroupIndex].Elements[ElementIndex].SecondsSinceLastUpdate = TimeElapsed;
					updatefunctions = true;
				}
				if (ForceUpdate)
				{
					updatefunctions = true;
				}
				CarSections[SectionIndex].Groups[GroupIndex].Elements[ElementIndex].Update(true, baseTrain, Index, CurrentCarSection, FrontAxle.Follower.TrackPosition - FrontAxle.Position, p, Direction, Up, Side, updatefunctions, Show, timeDelta, EnableDamping, false, CarSections[SectionIndex].Groups[GroupIndex].Type == ObjectType.Overlay ? Program.Renderer.Camera : null);
				if (!Program.Renderer.ForceLegacyOpenGL && CarSections[SectionIndex].Groups[GroupIndex].Elements[ElementIndex].UpdateVAO)
				{
					VAOExtensions.CreateVAO(ref CarSections[SectionIndex].Groups[GroupIndex].Elements[ElementIndex].internalObject.Prototype.Mesh, true, Program.Renderer.DefaultShader.VertexLayout, Program.Renderer);
				}
			}

			private void UpdateCarSectionTouchElement(int SectionIndex, int GroupIndex, int ElementIndex, Vector3 Position, Vector3 Direction, Vector3 Up, Vector3 Side, bool Show, double TimeElapsed, bool ForceUpdate, bool EnableDamping)
			{
				Vector3 p;
				if (CarSections[SectionIndex].Groups[GroupIndex].Type == ObjectType.Overlay & (Program.Renderer.Camera.CurrentRestriction != CameraRestrictionMode.NotAvailable && Program.Renderer.Camera.CurrentRestriction != CameraRestrictionMode.Restricted3D))
				{
					p = new Vector3(Driver.X, Driver.Y, Driver.Z);
				}
				else
				{
					p = Position;
				}
				double timeDelta;
				bool updatefunctions;
				if (CarSections[SectionIndex].Groups[GroupIndex].TouchElements[ElementIndex].Element.RefreshRate != 0.0)
				{
					if (CarSections[SectionIndex].Groups[GroupIndex].TouchElements[ElementIndex].Element.SecondsSinceLastUpdate >= CarSections[SectionIndex].Groups[GroupIndex].TouchElements[ElementIndex].Element.RefreshRate)
					{
						timeDelta = CarSections[SectionIndex].Groups[GroupIndex].TouchElements[ElementIndex].Element.SecondsSinceLastUpdate;
						CarSections[SectionIndex].Groups[GroupIndex].TouchElements[ElementIndex].Element.SecondsSinceLastUpdate = TimeElapsed;
						updatefunctions = true;
					}
					else
					{
						timeDelta = TimeElapsed;
						CarSections[SectionIndex].Groups[GroupIndex].TouchElements[ElementIndex].Element.SecondsSinceLastUpdate += TimeElapsed;
						updatefunctions = false;
					}
				}
				else
				{
					timeDelta = CarSections[SectionIndex].Groups[GroupIndex].TouchElements[ElementIndex].Element.SecondsSinceLastUpdate;
					CarSections[SectionIndex].Groups[GroupIndex].TouchElements[ElementIndex].Element.SecondsSinceLastUpdate = TimeElapsed;
					updatefunctions = true;
				}
				if (ForceUpdate)
				{
					updatefunctions = true;
				}
				CarSections[SectionIndex].Groups[GroupIndex].TouchElements[ElementIndex].Element.Update(true, baseTrain, Index, CurrentCarSection, FrontAxle.Follower.TrackPosition - FrontAxle.Position, p, Direction, Up, Side, updatefunctions, Show, timeDelta, EnableDamping, true, CarSections[SectionIndex].Groups[GroupIndex].Type == ObjectType.Overlay ? Program.Renderer.Camera : null);
				if (!Program.Renderer.ForceLegacyOpenGL && CarSections[SectionIndex].Groups[GroupIndex].TouchElements[ElementIndex].Element.UpdateVAO)
				{
					VAOExtensions.CreateVAO(ref CarSections[SectionIndex].Groups[GroupIndex].TouchElements[ElementIndex].Element.internalObject.Prototype.Mesh, true, Program.Renderer.DefaultShader.VertexLayout, Program.Renderer);
				}
			}

			internal void UpdateTopplingCantAndSpring(double TimeElapsed)
			{
				// get direction, up and side vectors
				Vector3 d = new Vector3(FrontAxle.Follower.WorldPosition - RearAxle.Follower.WorldPosition);
				Vector3 s;
				{
					double t = 1.0 / d.Norm();
					d *= t;
					t = 1.0 / Math.Sqrt(d.X * d.X + d.Z * d.Z);
					double ex = d.X * t;
					double ez = d.Z * t;
					s = new Vector3(ez, 0.0, -ex);
					Up = Vector3.Cross(d, s);
				}
				// cant and radius
				double c;
				{
					double ca = FrontAxle.Follower.CurveCant;
					double cb = RearAxle.Follower.CurveCant;
					c = Math.Tan(0.5 * (Math.Atan(ca) + Math.Atan(cb)));
				}
				double r, rs;
				if (FrontAxle.Follower.CurveRadius != 0.0 & RearAxle.Follower.CurveRadius != 0.0)
				{
					r = Math.Sqrt(Math.Abs(FrontAxle.Follower.CurveRadius * RearAxle.Follower.CurveRadius));
					rs = (double)Math.Sign(FrontAxle.Follower.CurveRadius + RearAxle.Follower.CurveRadius);
				}
				else if (FrontAxle.Follower.CurveRadius != 0.0)
				{
					r = Math.Abs(FrontAxle.Follower.CurveRadius);
					rs = (double)Math.Sign(FrontAxle.Follower.CurveRadius);
				}
				else if (RearAxle.Follower.CurveRadius != 0.0)
				{
					r = Math.Abs(RearAxle.Follower.CurveRadius);
					rs = (double)Math.Sign(RearAxle.Follower.CurveRadius);
				}
				else
				{
					r = 0.0;
					rs = 0.0;
				}
				// roll due to shaking
				{

					double a0 = Specs.CurrentRollDueToShakingAngle;
					double a1;
					if (Specs.CurrentRollShakeDirection != 0.0)
					{
						const double c0 = 0.03;
						const double c1 = 0.15;
						a1 = c1 * Math.Atan(c0 * Specs.CurrentRollShakeDirection);
						double dr = 0.5 + Specs.CurrentRollShakeDirection * Specs.CurrentRollShakeDirection;
						if (Specs.CurrentRollShakeDirection < 0.0)
						{
							Specs.CurrentRollShakeDirection += dr * TimeElapsed;
							if (Specs.CurrentRollShakeDirection > 0.0) Specs.CurrentRollShakeDirection = 0.0;
						}
						else
						{
							Specs.CurrentRollShakeDirection -= dr * TimeElapsed;
							if (Specs.CurrentRollShakeDirection < 0.0) Specs.CurrentRollShakeDirection = 0.0;
						}
					}
					else
					{
						a1 = 0.0;
					}
					double SpringAcceleration;
					if (!Derailed)
					{
						SpringAcceleration = 15.0 * Math.Abs(a1 - a0);
					}
					else
					{
						SpringAcceleration = 1.5 * Math.Abs(a1 - a0);
					}
					double SpringDeceleration = 0.25 * SpringAcceleration;
					Specs.CurrentRollDueToShakingAngularSpeed += (double)Math.Sign(a1 - a0) * SpringAcceleration * TimeElapsed;
					double x = (double)Math.Sign(Specs.CurrentRollDueToShakingAngularSpeed) * SpringDeceleration * TimeElapsed;
					if (Math.Abs(x) < Math.Abs(Specs.CurrentRollDueToShakingAngularSpeed))
					{
						Specs.CurrentRollDueToShakingAngularSpeed -= x;
					}
					else
					{
						Specs.CurrentRollDueToShakingAngularSpeed = 0.0;
					}
					a0 += Specs.CurrentRollDueToShakingAngularSpeed * TimeElapsed;
					Specs.CurrentRollDueToShakingAngle = a0;
				}
				// roll due to cant (incorporates shaking)
				{
					double cantAngle = Math.Atan(c / Program.CurrentRoute.Tracks[FrontAxle.Follower.TrackIndex].RailGauge);
					Specs.CurrentRollDueToCantAngle = cantAngle + Specs.CurrentRollDueToShakingAngle;
				}
				// pitch due to acceleration
				{
					for (int i = 0; i < 3; i++)
					{
						double a, v, j;
						switch (i)
						{
							case 0:
								a = Specs.CurrentAcceleration;
								v = Specs.CurrentPitchDueToAccelerationFastValue;
								j = 1.8;
								break;
							case 1:
								a = Specs.CurrentPitchDueToAccelerationFastValue;
								v = Specs.CurrentPitchDueToAccelerationMediumValue;
								j = 1.2;
								break;
							default:
								a = Specs.CurrentPitchDueToAccelerationFastValue;
								v = Specs.CurrentPitchDueToAccelerationSlowValue;
								j = 1.0;
								break;
						}
						double da = a - v;
						if (da < 0.0)
						{
							v -= j * TimeElapsed;
							if (v < a) v = a;
						}
						else
						{
							v += j * TimeElapsed;
							if (v > a) v = a;
						}
						switch (i)
						{
							case 0:
								Specs.CurrentPitchDueToAccelerationFastValue = v;
								break;
							case 1:
								Specs.CurrentPitchDueToAccelerationMediumValue = v;
								break;
							default:
								Specs.CurrentPitchDueToAccelerationSlowValue = v;
								break;
						}
					}
					{
						double da = Specs.CurrentPitchDueToAccelerationSlowValue - Specs.CurrentPitchDueToAccelerationFastValue;
						Specs.CurrentPitchDueToAccelerationTargetAngle = 0.03 * Math.Atan(da);
					}
					{
						double a = 3.0 * (double)Math.Sign(Specs.CurrentPitchDueToAccelerationTargetAngle - Specs.CurrentPitchDueToAccelerationAngle);
						Specs.CurrentPitchDueToAccelerationAngularSpeed += a * TimeElapsed;
						double ds = Math.Abs(Specs.CurrentPitchDueToAccelerationTargetAngle - Specs.CurrentPitchDueToAccelerationAngle);
						if (Math.Abs(Specs.CurrentPitchDueToAccelerationAngularSpeed) > ds)
						{
							Specs.CurrentPitchDueToAccelerationAngularSpeed = ds * (double)Math.Sign(Specs.CurrentPitchDueToAccelerationAngularSpeed);
						}
						Specs.CurrentPitchDueToAccelerationAngle += Specs.CurrentPitchDueToAccelerationAngularSpeed * TimeElapsed;
					}
				}
				// derailment
				if (Interface.CurrentOptions.Derailments & !Derailed)
				{
					double a = Specs.CurrentRollDueToTopplingAngle + Specs.CurrentRollDueToCantAngle;
					double sa = (double)Math.Sign(a);
					if (a * sa > Specs.CriticalTopplingAngle)
					{
						baseTrain.Derail(Index, TimeElapsed);
					}
				}
				// toppling roll
				if (Interface.CurrentOptions.Toppling | Derailed)
				{
					double a = Specs.CurrentRollDueToTopplingAngle;
					double ab = Specs.CurrentRollDueToTopplingAngle + Specs.CurrentRollDueToCantAngle;
					double h = Specs.CenterOfGravityHeight;
					double sr = Math.Abs(CurrentSpeed);
					double rmax = 2.0 * h * sr * sr / (Program.CurrentRoute.Atmosphere.AccelerationDueToGravity * Program.CurrentRoute.Tracks[FrontAxle.Follower.TrackIndex].RailGauge);
					double ta;
					Topples = false;
					if (Derailed)
					{
						double sab = (double)Math.Sign(ab);
						ta = 0.5 * Math.PI * (sab == 0.0 ? Program.RandomNumberGenerator.NextDouble() < 0.5 ? -1.0 : 1.0 : sab);
					}
					else
					{
						if (r != 0.0)
						{
							if (r < rmax)
							{
								double s0 = Math.Sqrt(r * Program.CurrentRoute.Atmosphere.AccelerationDueToGravity * Program.CurrentRoute.Tracks[FrontAxle.Follower.TrackIndex].RailGauge / (2.0 * h));
								const double fac = 0.25; // arbitrary coefficient
								ta = -fac * (sr - s0) * rs;
								baseTrain.Topple(Index, TimeElapsed);
							}
							else
							{
								ta = 0.0;
							}
						}
						else
						{
							ta = 0.0;
						}
					}
					double td;
					if (Derailed)
					{
						td = Math.Abs(ab);
						if (td < 0.1) td = 0.1;
					}
					else
					{
						td = 1.0;
					}
					if (a > ta)
					{
						double da = a - ta;
						if (td > da) td = da;
						a -= td * TimeElapsed;
					}
					else if (a < ta)
					{
						double da = ta - a;
						if (td > da) td = da;
						a += td * TimeElapsed;
					}
					Specs.CurrentRollDueToTopplingAngle = a;
				}
				else
				{
					Specs.CurrentRollDueToTopplingAngle = 0.0;
				}
				// apply position due to cant/toppling
				{
					double a = Specs.CurrentRollDueToTopplingAngle + Specs.CurrentRollDueToCantAngle;
					double x = Math.Sign(a) * 0.5 * Program.CurrentRoute.Tracks[FrontAxle.Follower.TrackIndex].RailGauge * (1.0 - Math.Cos(a));
					double y = Math.Abs(0.5 * Program.CurrentRoute.Tracks[FrontAxle.Follower.TrackIndex].RailGauge * Math.Sin(a));
					Vector3 cc = new Vector3(s.X * x + Up.X * y, s.Y * x + Up.Y * y, s.Z * x + Up.Z * y);
					FrontAxle.Follower.WorldPosition += cc;
					RearAxle.Follower.WorldPosition += cc;
				}
				// apply rolling
				{
					double a = -Specs.CurrentRollDueToTopplingAngle - Specs.CurrentRollDueToCantAngle;
					double cosa = Math.Cos(a);
					double sina = Math.Sin(a);
					s.Rotate(d, cosa, sina);
					Up.Rotate(d, cosa, sina);
				}
				// apply pitching
				if (CurrentCarSection >= 0 && CarSections[CurrentCarSection].Groups[0].Type == ObjectType.Overlay)
				{
					double a = Specs.CurrentPitchDueToAccelerationAngle;
					double cosa = Math.Cos(a);
					double sina = Math.Sin(a);
					d.Rotate(s, cosa, sina);
					Up.Rotate(s, cosa, sina);
					Vector3 cc = new Vector3(0.5 * (FrontAxle.Follower.WorldPosition + RearAxle.Follower.WorldPosition));
					FrontAxle.Follower.WorldPosition -= cc;
					RearAxle.Follower.WorldPosition -= cc;
					FrontAxle.Follower.WorldPosition.Rotate(s, cosa, sina);
					RearAxle.Follower.WorldPosition.Rotate(s, cosa, sina);
					FrontAxle.Follower.WorldPosition += cc;
					RearAxle.Follower.WorldPosition += cc;
				}
				// spring sound
				{
					double a = Specs.CurrentRollDueToShakingAngle;
					double diff = a - Sounds.SpringPlayedAngle;
					const double angleTolerance = 0.001;
					if (diff < -angleTolerance)
					{
						SoundBuffer buffer = Sounds.SpringL.Buffer;
						if (buffer != null)
						{
							if (!Program.Sounds.IsPlaying(Sounds.SpringL.Source))
							{
								Sounds.SpringL.Source = Program.Sounds.PlaySound(buffer, 1.0, 1.0, Sounds.SpringL.Position, this, false);
							}
						}
						Sounds.SpringPlayedAngle = a;
					}
					else if (diff > angleTolerance)
					{
						SoundBuffer buffer = Sounds.SpringR.Buffer;
						if (buffer != null)
						{
							if (!Program.Sounds.IsPlaying(Sounds.SpringR.Source))
							{
								Sounds.SpringR.Source = Program.Sounds.PlaySound(buffer, 1.0, 1.0, Sounds.SpringR.Position, this, false);
							}
						}
						Sounds.SpringPlayedAngle = a;
					}
				}
				// flange sound
				if(Sounds.Flange != null && Sounds.Flange.Length != 0)
				{
					/*
					 * This determines the amount of flange noise as a result of the angle at which the
					 * line that forms between the axles hits the rail, i.e. the less perpendicular that
					 * line is to the rails, the more flange noise there will be.
					 * */
					Vector3 df = FrontAxle.Follower.WorldPosition - RearAxle.Follower.WorldPosition;
					df.Normalize();
					double b0 = df.X * RearAxle.Follower.WorldSide.X + df.Y * RearAxle.Follower.WorldSide.Y + df.Z * RearAxle.Follower.WorldSide.Z;
					double b1 = df.X * FrontAxle.Follower.WorldSide.X + df.Y * FrontAxle.Follower.WorldSide.Y + df.Z * FrontAxle.Follower.WorldSide.Z;
					double spd = Math.Abs(CurrentSpeed);
					double pitch = 0.5 + 0.04 * spd;
					double b2 = Math.Abs(b0) + Math.Abs(b1);
					double basegain = 0.5 * b2 * b2 * spd * spd;
					/*
					 * This determines additional flange noise as a result of the roll angle of the car
					 * compared to the roll angle of the rails, i.e. if the car bounces due to inaccuracies,
					 * there will be additional flange noise.
					 * */
					double cdti = Math.Abs(FrontAxle.Follower.CantDueToInaccuracy) + Math.Abs(RearAxle.Follower.CantDueToInaccuracy);
					basegain += 0.2 * spd * spd * cdti * cdti;
					/*
					 * This applies the settings.
					 * */
					if (basegain < 0.0) basegain = 0.0;
					if (basegain > 0.75) basegain = 0.75;
					if (pitch > Sounds.FlangePitch)
					{
						Sounds.FlangePitch += TimeElapsed;
						if (Sounds.FlangePitch > pitch) Sounds.FlangePitch = pitch;
					}
					else
					{
						Sounds.FlangePitch -= TimeElapsed;
						if (Sounds.FlangePitch < pitch) Sounds.FlangePitch = pitch;
					}
					pitch = Sounds.FlangePitch;
					for (int i = 0; i < Sounds.Flange.Length; i++)
					{
						if (i == this.FrontAxle.FlangeIndex | i == this.RearAxle.FlangeIndex)
						{
							Sounds.FlangeVolume[i] += TimeElapsed;
							if (Sounds.FlangeVolume[i] > 1.0) Sounds.FlangeVolume[i] = 1.0;
						}
						else
						{
							Sounds.FlangeVolume[i] -= TimeElapsed;
							if (Sounds.FlangeVolume[i] < 0.0) Sounds.FlangeVolume[i] = 0.0;
						}
						double gain = basegain * Sounds.FlangeVolume[i];
						if (Program.Sounds.IsPlaying(Sounds.Flange[i].Source))
						{
							if (pitch > 0.01 & gain > 0.0001)
							{
								Sounds.Flange[i].Source.Pitch = pitch;
								Sounds.Flange[i].Source.Volume = gain;
							}
							else
							{
								Sounds.Flange[i].Stop();
							}
						}
						else if (pitch > 0.02 & gain > 0.01)
						{
							SoundBuffer buffer = Sounds.Flange[i].Buffer;
							if (buffer != null)
							{
								Sounds.Flange[i].Source = Program.Sounds.PlaySound(buffer, pitch, gain, Sounds.Flange[i].Position, this, true);
							}
						}
					}
				}
			}

			/// <summary>Updates the position of the camera relative to this car</summary>
			internal void UpdateCamera()
			{
				Vector3 direction = new Vector3(FrontAxle.Follower.WorldPosition - RearAxle.Follower.WorldPosition);
				direction *= 1.0 / direction.Norm();
				double sx = direction.Z * Up.Y - direction.Y * Up.Z;
				double sy = direction.X * Up.Z - direction.Z * Up.X;
				double sz = direction.Y * Up.X - direction.X * Up.Y;
				double rx = 0.5 * (FrontAxle.Follower.WorldPosition.X + RearAxle.Follower.WorldPosition.X);
				double ry = 0.5 * (FrontAxle.Follower.WorldPosition.Y + RearAxle.Follower.WorldPosition.Y);
				double rz = 0.5 * (FrontAxle.Follower.WorldPosition.Z + RearAxle.Follower.WorldPosition.Z);
				Vector3 cameraPosition;
				Vector3 driverPosition = this.HasInteriorView ? Driver : this.baseTrain.Cars[this.baseTrain.DriverCar].Driver;
				cameraPosition.X = rx + sx * driverPosition.X + Up.X * driverPosition.Y + direction.X * driverPosition.Z;
				cameraPosition.Y = ry + sy * driverPosition.X + Up.Y * driverPosition.Y + direction.Y * driverPosition.Z;
				cameraPosition.Z = rz + sz * driverPosition.X + Up.Z * driverPosition.Y + direction.Z * driverPosition.Z;

				Program.Renderer.CameraTrackFollower.WorldPosition = cameraPosition;
				Program.Renderer.CameraTrackFollower.WorldDirection = direction;
				Program.Renderer.CameraTrackFollower.WorldUp = new Vector3(Up);
				Program.Renderer.CameraTrackFollower.WorldSide = new Vector3(sx, sy, sz);
				double f = (Driver.Z - RearAxle.Position) / (FrontAxle.Position - RearAxle.Position);
				double tp = (1.0 - f) * RearAxle.Follower.TrackPosition + f * FrontAxle.Follower.TrackPosition;
				Program.Renderer.CameraTrackFollower.UpdateAbsolute(tp, false, false);
			}

			internal void UpdateSpeed(double TimeElapsed, double DecelerationDueToMotor, double DecelerationDueToBrake, out double Speed)
			{

				double PowerRollingCouplerAcceleration;
				// rolling on an incline
				{
					double a = FrontAxle.Follower.WorldDirection.Y;
					double b = RearAxle.Follower.WorldDirection.Y;
					PowerRollingCouplerAcceleration =
						-0.5 * (a + b) * Program.CurrentRoute.Atmosphere.AccelerationDueToGravity;
				}
				// friction
				double FrictionBrakeAcceleration;
				{
					double v = Math.Abs(CurrentSpeed);
					double t = Index == 0 & CurrentSpeed >= 0.0 || Index == baseTrain.Cars.Length - 1 & CurrentSpeed <= 0.0 ? Specs.ExposedFrontalArea : Specs.UnexposedFrontalArea;
					double a = FrontAxle.GetResistance(v, t, baseTrain.Specs.CurrentAirDensity, Program.CurrentRoute.Atmosphere.AccelerationDueToGravity);
					double b = RearAxle.GetResistance(v, t, baseTrain.Specs.CurrentAirDensity, Program.CurrentRoute.Atmosphere.AccelerationDueToGravity);
					FrictionBrakeAcceleration = 0.5 * (a + b);
				}
				// power
				double wheelspin = 0.0;
				double wheelSlipAccelerationMotorFront = 0.0;
				double wheelSlipAccelerationMotorRear = 0.0;
				double wheelSlipAccelerationBrakeFront = 0.0;
				double wheelSlipAccelerationBrakeRear = 0.0;
				if (!Derailed)
				{
					wheelSlipAccelerationMotorFront = FrontAxle.CriticalWheelSlipAccelerationForElectricMotor(Program.CurrentRoute.Atmosphere.AccelerationDueToGravity);
					wheelSlipAccelerationMotorRear = RearAxle.CriticalWheelSlipAccelerationForElectricMotor(Program.CurrentRoute.Atmosphere.AccelerationDueToGravity);
					wheelSlipAccelerationBrakeFront = FrontAxle.CriticalWheelSlipAccelerationForFrictionBrake(Program.CurrentRoute.Atmosphere.AccelerationDueToGravity);
					wheelSlipAccelerationBrakeRear = RearAxle.CriticalWheelSlipAccelerationForFrictionBrake(Program.CurrentRoute.Atmosphere.AccelerationDueToGravity);
				}

				if (DecelerationDueToMotor == 0.0)
				{
					double a;
					if (Specs.IsMotorCar)
					{
						if (DecelerationDueToMotor == 0.0)
						{
							if (baseTrain.Handles.Reverser.Actual != 0 & baseTrain.Handles.Power.Actual > 0 &
							    !baseTrain.Handles.HoldBrake.Actual &
							    !baseTrain.Handles.EmergencyBrake.Actual)
							{
								// target acceleration
								if (baseTrain.Handles.Power.Actual - 1 < Specs.AccelerationCurves.Length)
								{
									// Load factor is a constant 1.0 for anything prior to BVE5
									// This will need to be changed when the relevant branch is merged in
									a = Specs.AccelerationCurves[baseTrain.Handles.Power.Actual - 1]
										.GetAccelerationOutput(
											(double) baseTrain.Handles.Reverser.Actual * CurrentSpeed,
											1.0);
								}
								else
								{
									a = 0.0;
								}

								// readhesion device
								if (a > Specs.ReAdhesionDevice.MaximumAccelerationOutput)
								{
									a = Specs.ReAdhesionDevice.MaximumAccelerationOutput;
								}

								// wheel slip
								if (a < wheelSlipAccelerationMotorFront)
								{
									FrontAxle.CurrentWheelSlip = false;
								}
								else
								{
									FrontAxle.CurrentWheelSlip = true;
									wheelspin += (double) baseTrain.Handles.Reverser.Actual * a * CurrentMass;
								}

								if (a < wheelSlipAccelerationMotorRear)
								{
									RearAxle.CurrentWheelSlip = false;
								}
								else
								{
									RearAxle.CurrentWheelSlip = true;
									wheelspin += (double) baseTrain.Handles.Reverser.Actual * a * CurrentMass;
								}

								// Update readhesion device
								this.Specs.ReAdhesionDevice.Update(a);
								// Update constant speed device

								this.Specs.ConstSpeed.Update(ref a, baseTrain.Specs.CurrentConstSpeed,
									baseTrain.Handles.Reverser.Actual);

								// finalize
								if (wheelspin != 0.0) a = 0.0;
							}
							else
							{
								a = 0.0;
								FrontAxle.CurrentWheelSlip = false;
								RearAxle.CurrentWheelSlip = false;
							}
						}
						else
						{
							a = 0.0;
							FrontAxle.CurrentWheelSlip = false;
							RearAxle.CurrentWheelSlip = false;
						}
					}
					else
					{
						a = 0.0;
						FrontAxle.CurrentWheelSlip = false;
						RearAxle.CurrentWheelSlip = false;
					}

					if (!Derailed)
					{
						if (Specs.CurrentAccelerationOutput < a)
						{
							if (Specs.CurrentAccelerationOutput < 0.0)
							{
								Specs.CurrentAccelerationOutput += Specs.JerkBrakeDown * TimeElapsed;
							}
							else
							{
								Specs.CurrentAccelerationOutput += Specs.JerkPowerUp * TimeElapsed;
							}

							if (Specs.CurrentAccelerationOutput > a)
							{
								Specs.CurrentAccelerationOutput = a;
							}
						}
						else
						{
							Specs.CurrentAccelerationOutput -= Specs.JerkPowerDown * TimeElapsed;
							if (Specs.CurrentAccelerationOutput < a)
							{
								Specs.CurrentAccelerationOutput = a;
							}
						}
					}
					else
					{
						Specs.CurrentAccelerationOutput = 0.0;
					}
				}

				// brake
				bool wheellock = wheelspin == 0.0 & Derailed;
				if (!Derailed & wheelspin == 0.0)
				{
					double a;
					// motor
					if (Specs.IsMotorCar & DecelerationDueToMotor != 0.0)
					{
						a = -DecelerationDueToMotor;
						if (Specs.CurrentAccelerationOutput > a)
						{
							if (Specs.CurrentAccelerationOutput > 0.0)
							{
								Specs.CurrentAccelerationOutput -= Specs.JerkPowerDown * TimeElapsed;
							}
							else
							{
								Specs.CurrentAccelerationOutput -= Specs.JerkBrakeUp * TimeElapsed;
							}

							if (Specs.CurrentAccelerationOutput < a)
							{
								Specs.CurrentAccelerationOutput = a;
							}
						}
						else
						{
							Specs.CurrentAccelerationOutput += Specs.JerkBrakeDown * TimeElapsed;
							if (Specs.CurrentAccelerationOutput > a)
							{
								Specs.CurrentAccelerationOutput = a;
							}
						}
					}

					// brake
					a = DecelerationDueToBrake;
					if (CurrentSpeed >= -0.01 & CurrentSpeed <= 0.01)
					{
						double rf = FrontAxle.Follower.WorldDirection.Y;
						double rr = RearAxle.Follower.WorldDirection.Y;
						double ra = Math.Abs(0.5 * (rf + rr) *
						                     Program.CurrentRoute.Atmosphere.AccelerationDueToGravity);
						if (a > ra) a = ra;
					}

					double factor = EmptyMass / CurrentMass;
					if (a >= wheelSlipAccelerationBrakeFront)
					{
						wheellock = true;
					}
					else
					{
						FrictionBrakeAcceleration += 0.5 * a * factor;
					}

					if (a >= wheelSlipAccelerationBrakeRear)
					{
						wheellock = true;
					}
					else
					{
						FrictionBrakeAcceleration += 0.5 * a * factor;
					}
				}
				else if (Derailed)
				{
					FrictionBrakeAcceleration += Train.CoefficientOfGroundFriction *
					                             Program.CurrentRoute.Atmosphere.AccelerationDueToGravity;
				}

				// motor
				if (baseTrain.Handles.Reverser.Actual != 0)
				{
					double factor = EmptyMass / CurrentMass;
					if (Specs.CurrentAccelerationOutput > 0.0)
					{
						PowerRollingCouplerAcceleration +=
							(double) baseTrain.Handles.Reverser.Actual * Specs.CurrentAccelerationOutput * factor;
					}
					else
					{
						double a = -Specs.CurrentAccelerationOutput;
						if (a >= wheelSlipAccelerationMotorFront)
						{
							FrontAxle.CurrentWheelSlip = true;
						}
						else if (!Derailed)
						{
							FrictionBrakeAcceleration += 0.5 * a * factor;
						}

						if (a >= wheelSlipAccelerationMotorRear)
						{
							RearAxle.CurrentWheelSlip = true;
						}
						else
						{
							FrictionBrakeAcceleration += 0.5 * a * factor;
						}
					}
				}
				else
				{
					Specs.CurrentAccelerationOutput = 0.0;
				}

				// perceived speed
				{
					double target;
					if (wheellock)
					{
						target = 0.0;
					}
					else if (wheelspin == 0.0)
					{
						target = CurrentSpeed;
					}
					else
					{
						target = CurrentSpeed + wheelspin / 2500.0;
					}

					double diff = target - Specs.CurrentPerceivedSpeed;
					double rate = (diff < 0.0 ? 5.0 : 1.0) * Program.CurrentRoute.Atmosphere.AccelerationDueToGravity *
					              TimeElapsed;
					rate *= 1.0 - 0.7 / (diff * diff + 1.0);
					double factor = rate * rate;
					factor = 1.0 - factor / (factor + 1000.0);
					rate *= factor;
					if (diff >= -rate & diff <= rate)
					{
						Specs.CurrentPerceivedSpeed = target;
					}
					else
					{
						Specs.CurrentPerceivedSpeed += rate * (double) Math.Sign(diff);
					}
				}
				// calculate new speed
				{
					int d = Math.Sign(CurrentSpeed);
					double a = PowerRollingCouplerAcceleration;
					double b = FrictionBrakeAcceleration;
					if (Math.Abs(a) < b)
					{
						if (Math.Sign(a) == d)
						{
							if (d == 0)
							{
								Speed = 0.0;
							}
							else
							{
								double c = (b - Math.Abs(a)) * TimeElapsed;
								if (Math.Abs(CurrentSpeed) > c)
								{
									Speed = CurrentSpeed - (double) d * c;
								}
								else
								{
									Speed = 0.0;
								}
							}
						}
						else
						{
							double c = (Math.Abs(a) + b) * TimeElapsed;
							if (Math.Abs(CurrentSpeed) > c)
							{
								Speed = CurrentSpeed - (double) d * c;
							}
							else
							{
								Speed = 0.0;
							}
						}
					}
					else
					{
						Speed = CurrentSpeed + (a - b * (double) d) * TimeElapsed;
					}
				}
			}
		}
	}
}
