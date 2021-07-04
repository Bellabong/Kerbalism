﻿using Flee.PublicTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using KERBALISM.Planner;
using static KERBALISM.Planner.Planner;

namespace KERBALISM
{
	public class VesselDataShip : VesselDataBase
	{
		public static PartDataCollectionShip ShipParts { get; private set; } = new PartDataCollectionShip();

		private static VesselDataShip instance;

		public static VesselDataShip Instance
		{
			get
			{
				if (instance == null)
				{
					instance = new VesselDataShip();
					ShipParts.Clear();
				}
				return instance;
			}

			set => instance = value;
		}

		public VesselDataShip()
		{
			resHandler = new VesselResHandler(this, VesselResHandler.SimulationType.Planner);
			Synchronizer = new SynchronizerBase(this);
		}

		public void Start()
		{
			Synchronizer.Synchronize();

			foreach (PartData part in ShipParts)
			{
				foreach (ModuleHandler handler in part.modules)
				{
					handler.FirstSetup();
				}
			}

			// From now on, we assume that nobody will be altering part resources. Synchronize the resource sim state.
			resHandler.ForceHandlerSync();

			// Call OnStart() on every PartData, and every enabled ModuleHandler/KsmPartModule
			foreach (PartData part in Parts)
			{
				part.Start();
			}
		}

		#region BASE PROPERTIES IMPLEMENTATION

		public override string VesselName => EditorLogic.fetch?.ship == null ? "Unknown ShipConstruct" : $"{EditorLogic.fetch.ship.shipName} (Editor)";

		public override PartDataCollectionBase Parts => ShipParts;

		public override VesselResHandler ResHandler => resHandler; VesselResHandler resHandler;

		public override IConnectionInfo ConnectionInfo => connection;

		public override CelestialBody MainBody => body;

		public override double Altitude => altitude;

		public override double Latitude => latitude; public double latitude;

		public override double Longitude => longitude; public double longitude;

		public override double AngularVelocity => 0.0;

		public override int CrewCount => crewCount; public int crewCount;

		public override int CrewCapacity => crewCapacity; public int crewCapacity;

		public override bool EnvUnderwater => underwater; public bool underwater;

		public override bool EnvLanded => landed; public bool landed;

		public override double EnvStaticPressure => staticPressure; public double staticPressure;

		public override bool EnvInAtmosphere => atmosphere; public bool atmosphere;

		public override bool EnvInOxygenAtmosphere => oxygenAtmosphere; public bool oxygenAtmosphere;

		public override bool EnvInBreathableAtmosphere => breathable; public bool breathable;

		public override bool EnvZeroG => zerog; public bool zerog;

		public override double EnvTemperature => temperature; public double temperature;

		public override double EnvTempDiff => tempDiff; public double tempDiff;

		public override double EnvRadiation => radiation; public double radiation;

		public override double EnvRadiationHabitat => habitatRadiation; public double habitatRadiation;

		public override double EnvStormRadiation => 0.0;

		public override double EnvGammaTransparency => gammaTransparency; public double gammaTransparency;

		#endregion

		#region PLANNER FIELDS

		// environment
		public CelestialBody body;                            // target body
		public SunlightState sunlightState;
		public double altitude;                             // target altitude
		public double minHomeDistance;                      // min distance from KSC
		public double maxHomeDistance;                      // max distance from KSC
		public double orbitalPeriod;                        // length of orbit
		public double shadowPeriod;                         // length of orbit in shadow
		public double shadowTime;                           // proportion of orbit that is in shadow
		public double atmoFactor;                           // proportion of sun flux not absorbed by the atmosphere
		public double sunDist;                              // distance from the sun

		// radiation
		public double externRad;                            // environment radiation outside the heliopause
		public double heliopauseRad;                        // environment radiation inside the heliopause
		public double magnetopauseRad;                      // environment radiation inside the magnetopause
		public double innerRad;                             // environment radiation inside the inner belt
		public double outerRad;                             // environment radiation inside the outer belt
		public double surfaceRad;                           // environment radiation on the surface of the body
		public double stormRad;                             // environment radiation during a solar storm, inside the heliopause
		public double emitted;                              // amount of radiation emitted by components

		// crew
		public List<ProtoCrewMember> crew;                  // full information on all crew
		public bool crewEngineer;                           // true if an engineer is among the crew
		public bool crewScientist;                          // true if a scientist is among the crew
		public bool crewPilot;                              // true if a pilot is among the crew
		public uint crewEngineerMaxlevel;                   // experience level of top engineer on board
		public uint crewScientistMaxlevel;                  // experience level of top scientist on board
		public uint crewPilotMaxlevel;                      // experience level of top pilot on board

		// reliability
		public uint components;                             // number of components that can fail
		public double highQuality;                          // percentage of high quality components
		public double failureYear;                          // estimated failures per-year, averaged per-component
		public Dictionary<string, int> redundancy;          // number of components per redundancy group

		// comms
		private static CommHandlerEditor commHandler;
		public ConnectionInfoEditor connection = new ConnectionInfoEditor();

		#endregion

		#region PLANNER METHODS

		public void Analyze(List<Part> parts, CelestialBody body, Planner.Planner.Situation situation, SunlightState sunlight)
		{
			Synchronizer.Synchronize();
			AnalyzeEnvironment(body, situation, sunlight);
			AnalyzeCrew(parts);
			AnalyzeComms();
			ModuleDataUpdate();
			//AnalyzeRadiation(parts);
			AnalyzeReliability(parts);
		}

		private void AnalyzeEnvironment(CelestialBody body, Planner.Planner.Situation situation, SunlightState sunlight)
		{
			this.body = body;
			altitude = situation.Altitude(body);
			landed = altitude == 0.0;

			// Build a vessel position according the situation altitude and if the vessel should be on night/day side
			CelestialBody mainStarBody = Sim.GetParentStar(body);
			Vector3d vesselPosDirection = (mainStarBody.position - body.position).normalized;
			if (sunlight == SunlightState.Shadow)
				vesselPosDirection *= -1.0;

			Vector3d vesselPos = body.position + (vesselPosDirection * (body.Radius + altitude));
			body.GetLatLonAlt(vesselPos, out latitude, out longitude, out double unused);

			// Run the vessel sim
			SimVessel simVessel = new SimVessel();
			simVessel.UpdatePosition(this, vesselPos);
			SimStep step = new SimStep();
			step.Init(simVessel);
			step.Evaluate();
			ProcessSimStep(step);
			mainStar.direction = EditorDriver.editorFacility == EditorFacility.VAB ? new Vector3d(1.0, 1.0, 0.0).normalized : new Vector3d(0.0, 1.0, -1.0).normalized;
			atmoFactor = mainStar.directFlux / mainStar.directRawFlux;
			breathable = Sim.Breathable(body) && landed;
			temperature = Sim.VesselTemperature(irradianceTotal);
			tempDiff = Sim.TempDiff(temperature, body, landed);
			orbitalPeriod = Sim.OrbitalPeriod(body, altitude);
			shadowPeriod = Sim.ShadowPeriod(body, altitude);
			shadowTime = shadowPeriod / orbitalPeriod;
			zerog = !landed && (!body.atmosphere || body.atmosphereDepth < altitude);

			CelestialBody homeBody = FlightGlobals.GetHomeBody();
			CelestialBody parentPlanet = Sim.GetParentPlanet(body);

			if (body == homeBody)
			{
				minHomeDistance = maxHomeDistance = Math.Max(altitude, 500.0);
			}
			else if (parentPlanet == homeBody)
			{
				minHomeDistance = Sim.Periapsis(body);
				maxHomeDistance = Sim.Apoapsis(body);
			}
			else if (Sim.IsStar(body))
			{
				minHomeDistance = Math.Abs(altitude - Sim.Periapsis(homeBody));
				maxHomeDistance = altitude + Sim.Apoapsis(homeBody);
			}
			else
			{
				minHomeDistance = Math.Abs(Sim.Periapsis(parentPlanet) - Sim.Periapsis(homeBody));
				maxHomeDistance = Sim.Apoapsis(parentPlanet) + Sim.Apoapsis(homeBody);
			}

			RadiationBody rb = Radiation.Info(body);
			RadiationBody sun_rb = Radiation.Info(mainStarBody); // TODO Kopernicus support: not sure if/how this work with multiple suns/stars
			gammaTransparency = Sim.GammaTransparency(body, 0.0);

			// add gamma radiation emitted by body and its sun
			var gamma_radiation = Radiation.DistanceRadiation(rb.radiation_r0, altitude);

			var b = body;
			while (b != null && b.orbit != null && b != mainStarBody)
			{
				if (b == b.referenceBody) break;
				var dist = b.orbit.semiMajorAxis;
				b = b.referenceBody;

				gamma_radiation += Radiation.DistanceRadiation(Radiation.Info(b).radiation_r0, dist);
			}

			externRad = Settings.ExternRadiation;
			heliopauseRad = gamma_radiation + externRad + sun_rb.radiation_pause;
			magnetopauseRad = gamma_radiation + heliopauseRad + rb.radiation_pause;
			innerRad = gamma_radiation + magnetopauseRad + rb.radiation_inner;
			outerRad = gamma_radiation + magnetopauseRad + rb.radiation_outer;
			surfaceRad = magnetopauseRad * gammaTransparency + rb.radiation_surface;
			stormRad = heliopauseRad + PreferencesRadiation.Instance.StormRadiation * (MainStarSunlightFactor > 0.0 ? 1.0 : 0.0);
		}

		private void AnalyzeCrew(List<Part> parts)
		{
			// get number of kerbals assigned to the vessel in the editor
			// note: crew manifest is not reset after root part is deleted
			VesselCrewManifest manifest = KSP.UI.CrewAssignmentDialog.Instance.GetManifest();
			crew = manifest.GetAllCrew(false).FindAll(k => k != null);
			crewCount = crew.Count;
			crewEngineer = crew.Find(k => k.trait == "Engineer") != null;
			crewScientist = crew.Find(k => k.trait == "Scientist") != null;
			crewPilot = crew.Find(k => k.trait == "Pilot") != null;

			crewEngineerMaxlevel = 0;
			crewScientistMaxlevel = 0;
			crewPilotMaxlevel = 0;
			foreach (ProtoCrewMember c in crew)
			{
				switch (c.trait)
				{
					case "Engineer":
						crewEngineerMaxlevel = Math.Max(crewEngineerMaxlevel, (uint)c.experienceLevel);
						break;
					case "Scientist":
						crewScientistMaxlevel = Math.Max(crewScientistMaxlevel, (uint)c.experienceLevel);
						break;
					case "Pilot":
						crewPilotMaxlevel = Math.Max(crewPilotMaxlevel, (uint)c.experienceLevel);
						break;
				}
			}

			// scan the parts
			crewCapacity = 0;
			foreach (Part p in parts)
			{
				// accumulate crew capacity
				crewCapacity += p.CrewCapacity;
			}

			// if the user press SHIFT, the planner consider the vessel crewed at full capacity
			if (Input.GetKey(KeyCode.LeftShift))
				crewCount = crewCapacity;
		}

		private void AnalyzeComms()
		{
			if (commHandler == null)
				commHandler = CommHandlerEditor.GetHandler();

			if (commHandler == null)
				return;

			commHandler.Update(connection, minHomeDistance, maxHomeDistance);
		}

		//private void AnalyzeRadiation(List<Part> parts)
		//{
		//	// scan the parts
		//	emitted = 0.0;
		//	foreach (Part p in parts)
		//	{
		//		// for each module
		//		foreach (PartModule m in p.Modules)
		//		{
		//			// skip disabled modules
		//			if (!m.isEnabled)
		//				continue;

		//			// accumulate emitter radiation
		//			if (m.moduleName == "Emitter")
		//			{
		//				Emitter emitter = m as Emitter;
		//				emitter.Recalculate();

		//				if (emitter.running)
		//				{
		//					if (emitter.radiation > 0) emitted += emitter.radiation * emitter.radiation_impact;
		//					else emitted += emitter.radiation;
		//				}
		//			}
		//		}
		//	}
		//}


		private void AnalyzeReliability(List<Part> parts)
		{
			// reset data
			highQuality = 0.0;
			components = 0;
			failureYear = 0.0;
			redundancy = new Dictionary<string, int>();

			// scan the parts
			foreach (Part p in parts)
			{
				// for each module
				foreach (PartModule m in p.Modules)
				{
					// skip disabled modules
					if (!m.isEnabled)
						continue;

					// malfunctions
					if (m.moduleName == "Reliability")
					{
						Reliability reliability = m as Reliability;

						// calculate mtbf
						double mtbf = reliability.mtbf * (reliability.quality ? Settings.QualityScale : 1.0);
						if (mtbf <= 0) continue;

						// accumulate failures/y
						failureYear += Settings.ConfigsSecondsInYear / mtbf;

						// accumulate high quality percentage
						highQuality += reliability.quality ? 1.0 : 0.0;

						// accumulate number of components
						++components;

						// compile redundancy data
						if (reliability.redundancy.Length > 0)
						{
							int count = 0;
							if (redundancy.TryGetValue(reliability.redundancy, out count))
							{
								redundancy[reliability.redundancy] = count + 1;
							}
							else
							{
								redundancy.Add(reliability.redundancy, 1);
							}
						}

					}
				}
			}

			// calculate high quality percentage
			highQuality /= Math.Max(components, 1u);
		}

		#endregion
	}
}