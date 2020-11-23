﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KERBALISM
{
	/// <summary>
	/// Handler for a single "real" resource on a vessel. Expose vessel-wide information about amounts, rates and brokers (consumers/producers).
	/// Responsible for synchronization between the resource simulator and the actual resources present on each part. 
	/// </summary>
	public class VesselKSPResource : VesselResource
	{
		private int stockId;
		private PartResourceDefinition stockDefinition;

		/// <summary> Associated resource name</summary>
		public override string Name => name;
		private string name;

		/// <summary> Shortcut to the resource definition "displayName" </summary>
		public override string Title => stockDefinition.displayName;

		/// <summary> Shortcut to the resource definition "isVisible" </summary>
		public override bool Visible => stockDefinition.isVisible;

		/// <summary> Amount of resource</summary>
		public override double Amount => resourceWrapper.amount;

		/// <summary> Storage capacity of resource</summary>
		public override double Capacity => resourceWrapper.capacity;

		public override bool NeedUpdate => availabilityFactor != 0.0 || deferred != 0.0 || Capacity != 0.0 || resourceBrokers.Count != 0;

		/// <summary> Shortcut to the resource definition "abbreviation" </summary>
		public string Abbreviation => stockDefinition.abbreviation;

		/// <summary> Shortcut to the resource definition "density" </summary>
		public float Density => stockDefinition.density;

		/// <summary> Shortcut to the resource definition "unitCost" </summary>
		public float UnitCost => stockDefinition.unitCost;

		/// <summary>Ctor</summary>
		public VesselKSPResource(string name, int id, PartResourceWrapperCollection resourceWrapper)
		{
			this.stockId = id;
			this.name = name;
			this.resourceWrapper = resourceWrapper;
			resourceBrokers = new List<ResourceBrokerRate>();
			brokersResourceAmounts = new Dictionary<ResourceBroker, double>();
			stockDefinition = PartResourceLibrary.Instance.resourceDefinitions[stockId];

			Init();
		}

		public void SetWrapper(PartResourceWrapperCollection resourceWrapper)
		{
			this.resourceWrapper = resourceWrapper;
		}

		/// <summary>synchronize resources from cache to vessel</summary>
		/// <remarks>
		/// this function will also sync from vessel to cache so you can always use the
		/// VesselResource properties to get information about resources
		/// </remarks>
		public override bool ExecuteAndSyncToParts(VesselDataBase vd, double elapsed_s)
		{
			// detect flow state changes
			bool flowStateChanged = resourceWrapper.capacity - resourceWrapper.oldCapacity > 1e-05;

			base.ExecuteAndSyncToParts(vd, elapsed_s);

			// if incoherent producers are detected, do not allow high timewarp speed
			// - can be disabled in settings
			// - ignore incoherent consumers (no negative consequences for player)
			// - ignore flow state changes (avoid issue with process controllers and other things that alter resource capacities)
			return Settings.EnforceCoherency && TimeWarp.CurrentRate > 1000.0 && unknownBrokersRate > 0.0 && !flowStateChanged;
		}
	}
}
