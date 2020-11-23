﻿using System;

namespace KERBALISM
{
	/// <summary>
	/// When extending this module in another plugin, make sure to call ModuleData.Init() from that plugin
	/// </summary>
	public abstract class KsmPartModule : PartModule
	{
		public const string VALUENAME_SHIPID = "dataShipId";
		public const string VALUENAME_FLIGHTID = "dataFlightId";

		[KSPField(isPersistant = true)]
		public int dataShipId = 0;

		[KSPField(isPersistant = true)]
		public int dataFlightId = 0;

		[KSPField] public string id = string.Empty;          // this is for identifying the module with B9PS

		public abstract ModuleData ModuleData { get; set; }

		public abstract Type ModuleDataType { get; }

		/// <summary>
		/// Override this method to add automation support for this part module
		/// </summary>
		/// <returns>IAutomationDevice with device module specific implementations for automation support</returns>
		public virtual AutomationAdapter[] CreateAutomationAdapter(KsmPartModule moduleOrPrefab, ModuleData moduleData)
		{
			return null;
		}
	}

	public abstract class KsmPartModule<TModule, TData> : KsmPartModule
		where TModule : KsmPartModule<TModule, TData>
		where TData : ModuleData<TModule, TData>
	{
		public TData moduleData;

		public override ModuleData ModuleData { get => moduleData; set => moduleData = (TData)value; }

		public override Type ModuleDataType => typeof(TData);

		public virtual void OnDestroy()
		{
			// clear loaded module reference to avoid memory leaks
			if (moduleData != null)
			{
				moduleData.loadedModule = null;
			}
		}
	}
}
