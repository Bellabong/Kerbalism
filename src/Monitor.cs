﻿// ====================================================================================================================
// the vessel monitor
// ====================================================================================================================


using System;
using System.Collections.Generic;
using LibNoise.Unity.Operator;
using UnityEngine;


namespace KERBALISM {


public class Monitor
{
  // store last vessel clicked in the monitor ui, if any
  Guid last_clicked_id;

  // store vessel whose configs are being edited, if any
  Guid configured_id;

  // styles
  GUIStyle row_style;
  GUIStyle name_style;
  GUIStyle body_style;
  GUIStyle icon_style;
  GUIStyle config_style;

  // icons
  readonly Texture icon_battery_danger      = Lib.GetTexture("battery-red");
  readonly Texture icon_battery_warning     = Lib.GetTexture("battery-yellow");
  readonly Texture icon_battery_nominal     = Lib.GetTexture("battery-white");
  readonly Texture icon_supplies_danger     = Lib.GetTexture("box-red");
  readonly Texture icon_supplies_warning    = Lib.GetTexture("box-yellow");
  readonly Texture icon_supplies_nominal    = Lib.GetTexture("box-white");
  readonly Texture icon_malfunction_danger  = Lib.GetTexture("wrench-red");
  readonly Texture icon_malfunction_warning = Lib.GetTexture("wrench-yellow");
  readonly Texture icon_malfunction_nominal = Lib.GetTexture("wrench-white");
  readonly Texture icon_sun_shadow          = Lib.GetTexture("sun-black");
  readonly Texture icon_signal_none         = Lib.GetTexture("signal-red");
  readonly Texture icon_signal_relay        = Lib.GetTexture("signal-yellow");
  readonly Texture icon_signal_direct       = Lib.GetTexture("signal-white");
  readonly Texture icon_scrubber_danger     = Lib.GetTexture("recycle-red");
  readonly Texture icon_scrubber_warning    = Lib.GetTexture("recycle-yellow");
  readonly Texture icon_greenhouse_danger   = Lib.GetTexture("plant-red");
  readonly Texture icon_greenhouse_warning  = Lib.GetTexture("plant-yellow");
  readonly Texture icon_health_danger       = Lib.GetTexture("health-red");
  readonly Texture icon_health_warning      = Lib.GetTexture("health-yellow");
  readonly Texture icon_stress_danger       = Lib.GetTexture("brain-red");
  readonly Texture icon_stress_warning      = Lib.GetTexture("brain-yellow");
  readonly Texture icon_storm_danger        = Lib.GetTexture("storm-red");
  readonly Texture icon_storm_warning       = Lib.GetTexture("storm-yellow");
  readonly Texture icon_radiation_danger    = Lib.GetTexture("radiation-red");
  readonly Texture icon_radiation_warning   = Lib.GetTexture("radiation-yellow");
  readonly Texture icon_radiation_nominal   = Lib.GetTexture("radiation-white");
  readonly Texture icon_empty               = Lib.GetTexture("empty");
  readonly Texture icon_notes               = Lib.GetTexture("notes");
  readonly Texture[] icon_toggle            ={Lib.GetTexture("toggle-disabled"),
                                              Lib.GetTexture("toggle-enabled")};

  // ctor
  public Monitor()
  {
    // style for vessel row
    row_style = new GUIStyle();
    row_style.stretchWidth = true;
    row_style.fixedHeight = 16.0f; //< required for icon vertical alignment

    // style for vessel name
    name_style = new GUIStyle(HighLogic.Skin.label);
    name_style.richText = true;
    name_style.normal.textColor = Color.white;
    name_style.fixedWidth = 120.0f;
    name_style.stretchHeight = true;
    name_style.fontSize = 12;
    name_style.alignment = TextAnchor.MiddleLeft;

    // style for body name
    body_style = new GUIStyle(HighLogic.Skin.label);
    body_style.richText = true;
    body_style.normal.textColor = new Color(0.75f, 0.75f, 0.75f, 1.0f);
    body_style.fixedWidth = 36.0f;
    body_style.stretchHeight = true;
    body_style.fontSize = 8;
    body_style.alignment = TextAnchor.MiddleRight;

    // icon style
    icon_style = new GUIStyle();
    icon_style.alignment = TextAnchor.MiddleRight;

    // vessel config style
    config_style = new GUIStyle(HighLogic.Skin.label);
    config_style.normal.textColor = Color.white;
    config_style.padding = new RectOffset(0, 0, 0, 0);
    config_style.alignment = TextAnchor.MiddleLeft;
    config_style.imagePosition = ImagePosition.ImageLeft;
    config_style.fontSize = 9;
  }


  GUIContent indicator_ec(Vessel v)
  {
    // note: if there isn't ec capacity, show danger

    double amount = Lib.GetResourceAmount(v, "ElectricCharge");
    double capacity = Lib.GetResourceCapacity(v, "ElectricCharge");
    double level = capacity > 0.0 ? amount / capacity : 0.0;

    GUIContent state = new GUIContent();
    state.tooltip = "EC: " + (level * 100.0).ToString("F0") + "%";
    if (level <= Settings.ResourceDangerThreshold) state.image = icon_battery_danger;
    else if (level <= Settings.ResourceWarningThreshold) state.image = icon_battery_warning;
    else state.image = icon_battery_nominal;
    return state;
  }


  GUIContent indicator_supplies(Vessel v)
  {
    // note: on EVA, food is always ignored and oxygen is ignored if inside breathable atmosphere
    // note: if there isn't food/oxygen capacity, show nominal (eg: probe, eva on breathable atmosphere)

    double food_amount = Lib.GetResourceAmount(v, "Food");
    double food_capacity = Lib.GetResourceCapacity(v, "Food");
    double food_level = food_capacity > 0.0 ? food_amount / food_capacity : 0.0;
    double oxygen_amount = Lib.GetResourceAmount(v, "Oxygen");
    double oxygen_capacity = Lib.GetResourceCapacity(v, "Oxygen");
    double oxygen_level = oxygen_capacity > 0.0 ? oxygen_amount / oxygen_capacity : 0.0;
    double level = v.isEVA ? (LifeSupport.BreathableAtmosphere(v) ? 1.0 : oxygen_level) : Math.Min(food_level, oxygen_level);
    double capacity = food_capacity + oxygen_capacity;

    GUIContent state = new GUIContent();
    if (capacity > double.Epsilon)
    {
      if (level <= Settings.ResourceDangerThreshold) state.image = icon_supplies_danger;
      else if (level <= Settings.ResourceWarningThreshold) state.image = icon_supplies_warning;
      else state.image = icon_supplies_nominal;
      state.tooltip = v.isEVA ? "" : "Food: " + (food_level * 100.0).ToString("F0") + "%, ";
      state.tooltip += (v.isEVA && LifeSupport.BreathableAtmosphere(v)) ? "" : "Oxygen: " + (oxygen_level * 100.0).ToString("F0") + "%";
    }
    else state.image = icon_supplies_nominal;
    return state;
  }


  GUIContent indicator_reliability(Vessel v)
  {
    GUIContent state = new GUIContent();
    uint max_malfunctions = Malfunction.MaxMalfunction(v);
    if (max_malfunctions == 0)
    {
      state.image = icon_malfunction_nominal;
      state.tooltip = "No malfunctions";
    }
    else if (max_malfunctions == 1)
    {
      state.image = icon_malfunction_warning;
      state.tooltip = "Minor malfunctions";
    }
    else
    {
      state.image = icon_malfunction_danger;
      state.tooltip = "Major malfunctions";
    }
    double avg_quality = Malfunction.AverageQuality(v);
    if (avg_quality > 0.0) state.tooltip += "\n<i>Quality: " + Malfunction.QualityToString(avg_quality) + "</i>";
    return state;
  }


  GUIContent indicator_signal(Vessel v)
  {
    GUIContent state = new GUIContent();
    link_data ld = Signal.Link(v);
    switch(ld.status)
    {
      case link_status.direct_link:
        state.image = icon_signal_direct;
        state.tooltip = "Direct link";
        break;

      case link_status.indirect_link:
        state.image = icon_signal_relay;
        if (ld.path.Count == 1)
        {
          state.tooltip = "Signal relayed by <b>" + ld.path[ld.path.Count - 1] + "</b>";
        }
        else
        {
          state.tooltip = "Signal relayed by:";
          for(int i=ld.path.Count-1; i>0; --i) state.tooltip += "\n<b>" + ld.path[i] + "</b>";
        }
        break;

      case link_status.no_link:
        state.image = icon_signal_none;
        state.tooltip = !Signal.Blackout(v) ? "No signal" : "Blackout";
        break;

      case link_status.no_antenna:
        state.image = icon_signal_none;
        state.tooltip = "No antenna";
        break;
    }
    return state;
  }


  void problem_sunlight(vessel_info info, ref List<Texture> icons, ref List<string> tooltips)
  {
    if (!info.sunlight)
    {
      icons.Add(icon_sun_shadow);
      tooltips.Add("In shadow");
    }
  }


  void problem_scrubbers(Vessel v, ref List<Texture> icons, ref List<string> tooltips)
  {
    bool no_ec_left = Lib.GetResourceAmount(v, "ElectricCharge") <= double.Epsilon;
    List<Scrubber> scrubbers = Scrubber.GetScrubbers(v);
    bool scrubber_disabled = false;
    bool scrubber_nopower = false;
    foreach(Scrubber scrubber in scrubbers)
    {
      scrubber_disabled |= !scrubber.is_enabled;
      scrubber_nopower |= scrubber.is_enabled && no_ec_left;
    }
    if (scrubber_disabled)
    {
      icons.Add(icon_scrubber_warning);
      tooltips.Add("Scrubber disabled");
    }
    if (scrubber_nopower)
    {
      icons.Add(icon_scrubber_danger);
      tooltips.Add("Scrubber has no power");
    }
  }


  void problem_greenhouses(Vessel v, ref List<Texture> icons, ref List<string> tooltips)
  {
    List<Greenhouse> greenhouses = Greenhouse.GetGreenhouses(v);
    bool greenhouse_slowgrowth = false;
    bool greenhouse_nogrowth = false;
    foreach(Greenhouse greenhouse in greenhouses)
    {
      greenhouse_slowgrowth |= greenhouse.lighting <= 0.5;
      greenhouse_nogrowth |= greenhouse.lighting <= double.Epsilon;
    }
    if (greenhouse_nogrowth)
    {
      icons.Add(icon_greenhouse_danger);
      tooltips.Add("Greenhouse not growing");
    }
    else if (greenhouse_slowgrowth)
    {
      icons.Add(icon_greenhouse_warning);
      tooltips.Add("Greenhouse growing slowly");
    }
  }


  void problem_kerbals(List<ProtoCrewMember> crew, ref List<Texture> icons, ref List<string> tooltips)
  {
    UInt32 health_severity = 0;
    UInt32 stress_severity = 0;
    foreach(ProtoCrewMember c in crew)
    {
      // get kerbal data
      kerbal_data kd = DB.KerbalData(c.name);

      // skip disabled kerbals
      if (kd.disabled == 1) continue;

      // health
      if (kd.starved > Settings.StarvedDangerThreshold) { health_severity = Math.Max(health_severity, 2); tooltips.Add(c.name + " is starving"); }
      else if (kd.starved > Settings.StarvedWarningThreshold) { health_severity = Math.Max(health_severity, 1); tooltips.Add(c.name + " is hungry"); }
      if (kd.deprived > Settings.DeprivedDangerThreshold) { health_severity = Math.Max(health_severity, 2); tooltips.Add(c.name + " is suffocating"); }
      else if (kd.deprived > Settings.DeprivedWarningThreshold) { health_severity = Math.Max(health_severity, 1); tooltips.Add(c.name + " is gasping"); }
      if (kd.temperature < -Settings.TemperatureDangerThreshold) { health_severity = Math.Max(health_severity, 2); tooltips.Add(c.name + " is freezing"); }
      else if (kd.temperature < -Settings.TemperatureWarningThreshold) { health_severity = Math.Max(health_severity, 1); tooltips.Add(c.name + " feels cold"); }
      else if (kd.temperature > Settings.TemperatureDangerThreshold) { health_severity = Math.Max(health_severity, 2); tooltips.Add(c.name + " is burning"); }
      else if (kd.temperature > Settings.TemperatureWarningThreshold) { health_severity = Math.Max(health_severity, 1); tooltips.Add(c.name + " feels hot"); }

      // radiation
      if (kd.radiation > Settings.RadiationDangerThreshold) { health_severity = Math.Max(health_severity, 2); tooltips.Add(c.name + " exposed to extreme radiation"); }
      else if (kd.radiation > Settings.RadiationWarningThreshold) { health_severity = Math.Max(health_severity, 1); tooltips.Add(c.name + " exposed to intense radiation"); }


      // stress
      if (kd.stressed > Settings.StressedDangerThreshold) { stress_severity = Math.Max(stress_severity, 2); tooltips.Add(c.name + " mind is breaking"); }
      else if (kd.stressed > Settings.StressedWarningThreshold) { stress_severity = Math.Max(stress_severity, 1); tooltips.Add(c.name + " is stressed"); }
    }
    if (health_severity == 1) icons.Add(icon_health_warning);
    else if (health_severity == 2) icons.Add(icon_health_danger);
    if (stress_severity == 1) icons.Add(icon_stress_warning);
    else if (stress_severity == 2) icons.Add(icon_stress_danger);
  }


  void problem_radiation(vessel_info info, ref List<Texture> icons, ref List<string> tooltips)
  {
    string radiation_str = " (<i>" + (info.radiation * 60.0 * 60.0).ToString("F2") + " rad/h)</i>";
    if (info.belt_radiation > double.Epsilon)
    {
      icons.Add(icon_radiation_danger);
      tooltips.Add("Crossing radiation belt" + radiation_str);
    }
    else if (info.storm_radiation > double.Epsilon)
    {
      icons.Add(icon_radiation_danger);
      tooltips.Add("Exposed to solar storm" + radiation_str);
    }
    else if (info.cosmic_radiation > double.Epsilon)
    {
      icons.Add(icon_radiation_warning);
      tooltips.Add("Exposed to cosmic radiation" + radiation_str);
    }
  }


  void problem_storm(Vessel v, ref List<Texture> icons, ref List<string> tooltips)
  {
    if (Storm.Incoming(v.mainBody))
    {
      icons.Add(icon_storm_warning);
      tooltips.Add("Coronal mass ejection incoming <i>(" + Lib.HumanReadableDuration(Storm.TimeBeforeCME(v.mainBody)) + ")</i>");
    }
    if (Storm.InProgress(v.mainBody))
    {
      icons.Add(icon_storm_danger);
      tooltips.Add("Solar storm in progress <i>(" + Lib.HumanReadableDuration(Storm.TimeLeftCME(v.mainBody)) + ")</i>");
    }
  }


  // draw a vessel in the monitor
  // - return: 1 if vessel wasn't skipped
  uint render_vessel(Vessel v)
  {
    // avoid case when DB isn't ready for whatever reason
    if (!DB.Ready()) return 0;

    // skip invalid vessels
    if (!Lib.IsVessel(v)) return 0;

    // skip resque missions
    if (Lib.IsResqueMission(v)) return 0;

    // skip dead eva kerbals
    if (EVA.IsDead(v)) return 0;

    // get vessel info from cache
    vessel_info vi = Cache.VesselInfo(v);

    // get vessel data from the db
    vessel_data vd = DB.VesselData(v.id);

    // get vessel crew
    List<ProtoCrewMember> crew = v.loaded ? v.GetVesselCrew() : v.protoVessel.GetVesselCrew();

    // get vessel name
    string vessel_name = v.isEVA ? crew[0].name : v.vesselName;

    // get body name
    string body_name = v.mainBody.name.ToUpper();

    // store problems icons & tooltips
    List<Texture> problem_icons = new List<Texture>();
    List<string> problem_tooltips = new List<string>();

    // detect problems
    problem_sunlight(vi, ref problem_icons, ref problem_tooltips);
    problem_storm(v, ref problem_icons, ref problem_tooltips);
    if (crew.Count > 0)
    {
      problem_kerbals(crew, ref problem_icons, ref problem_tooltips);
      problem_radiation(vi, ref problem_icons, ref problem_tooltips);
      problem_scrubbers(v, ref problem_icons, ref problem_tooltips);
    }
    problem_greenhouses(v, ref problem_icons, ref problem_tooltips);




    // choose problem icon
    const UInt64 problem_icon_time = 3;
    Texture problem_icon = icon_empty;
    if (problem_icons.Count > 0)
    {
      UInt64 problem_index = (Convert.ToUInt64(Time.realtimeSinceStartup) / problem_icon_time) % (UInt64)(problem_icons.Count);
      problem_icon = problem_icons[(int)problem_index];
    }

    // generate problem tooltips
    string problem_tooltip = String.Join("\n", problem_tooltips.ToArray());

    // render vessel name & icons
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(new GUIContent("<b>" + Lib.Epsilon(vessel_name, 24) + "</b>", vessel_name.Length > 24 ? vessel_name : ""), name_style);
    GUILayout.Label(new GUIContent(Lib.Epsilon(body_name, 8), body_name.Length > 8 ? body_name : ""), body_style);
    GUILayout.Label(new GUIContent(problem_icon, problem_tooltip), icon_style);
    GUILayout.Label(indicator_ec(v), icon_style);
    GUILayout.Label(indicator_supplies(v), icon_style);
    GUILayout.Label(indicator_reliability(v), icon_style);
    GUILayout.Label(indicator_signal(v), icon_style);
    GUILayout.EndHorizontal();

    // remember last vessel clicked
    if (Lib.IsClicked()) last_clicked_id = v.id;

    // render vessel config
    if (configured_id == v.id) render_config(v);

    // spacing between vessels
    GUILayout.Space(10.0f);

    // signal that the vessel wasn't skipped for whatever reason
    return 1;
  }


  // draw vessel config
  void render_config(Vessel v)
  {
    // do nothing if db isn' ready
    if (!DB.Ready()) return;

    // get vessel data
    vessel_data vd = DB.VesselData(v.id);

    // draw the config
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(new GUIContent(" EC MESSAGES", icon_toggle[vd.cfg_ec]), config_style);
    if (Lib.IsClicked()) vd.cfg_ec = (vd.cfg_ec == 0 ? 1u : 0);
    GUILayout.EndHorizontal();
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(new GUIContent(" SUPPLY MESSAGES", icon_toggle[vd.cfg_supply]), config_style);
    if (Lib.IsClicked()) vd.cfg_supply = (vd.cfg_supply == 0 ? 1u : 0);
    GUILayout.EndHorizontal();
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(new GUIContent(" MALFUNCTIONS MESSAGES", icon_toggle[vd.cfg_malfunction]), config_style);
    if (Lib.IsClicked()) vd.cfg_malfunction = (vd.cfg_malfunction == 0 ? 1u : 0);
    GUILayout.EndHorizontal();
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(new GUIContent(" SIGNAL MESSAGES", icon_toggle[vd.cfg_signal]), config_style);
    if (Lib.IsClicked()) vd.cfg_signal = (vd.cfg_signal == 0 ? 1u : 0);
    GUILayout.EndHorizontal();
    GUILayout.BeginHorizontal(row_style);
    GUILayout.Label(new GUIContent(" NOTES", icon_notes), config_style);
    if (Lib.IsClicked()) Notepad.Toggle(v);
    GUILayout.EndHorizontal();
  }


  public float width()
  {
    return 300.0f;
  }


  public float height()
  {
    // guess vessel count
    uint count = 0;
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // skip invalid vessels
      if (!Lib.IsVessel(v)) continue;

      // skip resque missions
      if (Lib.IsResqueMission(v)) continue;

      // skip dead eva kerbals
      if (EVA.IsDead(v)) continue;

      ++ count;
    }
    count = Math.Max(1u, count); //< deal with no vessels case

    // calculate height
    return Math.Min(10.0f + (float)count * (16.0f + 10.0f) + (configured_id == Guid.Empty ? 0.0f : 80.0f), Screen.height * 0.5f);
  }


  public void render()
  {
    // reset last clicked vessel
    last_clicked_id = Guid.Empty;

    // forget edited vessel if it doesn't exist anymore
    if (FlightGlobals.Vessels.Find(k => k.id == configured_id) == null) configured_id = Guid.Empty;

    // store vessels rendered
    uint vessels_rendered = 0;

    // draw active vessel if any
    if (FlightGlobals.ActiveVessel != null)
    {
      vessels_rendered += render_vessel(FlightGlobals.ActiveVessel);
    }

    // for each vessel
    foreach(Vessel v in FlightGlobals.Vessels)
    {
      // skip active vessel
      if (v == FlightGlobals.ActiveVessel) continue;

      // draw the vessel
      vessels_rendered += render_vessel(v);
    }

    // if user clicked on a vessel
    if (last_clicked_id != Guid.Empty)
    {
      // if user clicked on configured vessel hide config, if user clicked on another vessel show its config
      configured_id = (last_clicked_id == configured_id ? Guid.Empty : last_clicked_id);
    }

    // no-vessels case
    if (vessels_rendered == 0)
    {
      GUILayout.BeginHorizontal(row_style);
      GUILayout.Label("<i>No vessels</i>", name_style);
      GUILayout.EndHorizontal();
      GUILayout.Space(10.0f);
    }
  }
}


} // KERBALISM