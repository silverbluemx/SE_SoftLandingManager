using Microsoft.NET.StringTools;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.GameSystems;
using Sandbox.Game.Gui;
using Sandbox.Gui;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SharpDX.Toolkit.Graphics;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Network;
using VRageMath;
using VRageRender;

namespace IngameScript
{
	partial class Program : MyGridProgram
	{
	

/*
-------------------------------------
SOFT LANDING MANAGER by silverbluemx
-------------------------------------

A script to automatically manage your thrusters to land safely on planets while
optimizing your fuel and energy use. Also scans the terrain and guides the ship
to a safe landing spot, avoiding obstacles and steep slopes.
Best for use with inverse square law gravity mods such as Real Orbits and high
speed limit mods such as 1000m/s speed mod.

Version 2.1 - 2025-12-14 - Ongoing

New features:
- Compatibility with vanilla SE gravity (set gravityExponent = 7; in the config)
- Mode 3 becomes "hover mode" : horizontal speed control (with thrusters and/or tilting)
using forward, backward, left, right input keys limited to a "safe speed" based on altitude
- Can use displays in cockpits. The display adapts to smaller screens.
- Prototech thrusters handled separately from Ion thrusters (efficiency loss in atmosphere is different)

Beta features:
- Added mode 4 for altitude/speed hold, acting as an autopilot. Works mostly but needs more refinement.
See GitHub for details

Fixes:
- Improved thruster configurations for languages other than English
- Improved triggering of liftoff and landing timers
- Command thrustersswitch now works correctly

Planet catalog:
- Added presets for all planets from Cauldron System by Major Jon and Mirathi System by Infinite

See the Steam Workshop page and README.md and technical.md files on GitHub for more information :
https://github.com/silverbluemx/SE_SoftLandingManager
Published script has been minified a little (tabs, line breaks, comments removed), see original code on GitHub

*/

// Settings

/// <summary>
/// Configure here the tags used for ship blocks.
/// </summary>
public class SLMShipConfiguration {

	public List<string> CTRLLER_NAME, RADAR_NAME, IGNORE_NAME, LCD_NAME, DEBUGLCD_NAME, LANDING_TIMER_NAME, LIFTOFF_TIMER_NAME, ON_TIMER_NAME, OFF_TIMER_NAME, SOUND_NAME;

	public SLMShipConfiguration()
	{
		// OPTIONAL : Include this tag in blocks that you want this script to ignore. For example, on an auxiliary drone.
		IGNORE_NAME = new List<string> { "SLMignore" };

		// RECOMMENDED : Reference controller (seat, cockpit, etc.) to use for the ship orientation.
		CTRLLER_NAME = new List<string> { "SLMref", "reference", "Reference" };

		// RECOMMENDED : Reference downward-facing camera(s) (up to 2) to use as a ground radar.
		RADAR_NAME = new List<string> { "SLMradar" };

		// OPTIONAL : Main display for the script (any number of them, or none at all)
		LCD_NAME = new List<string> { "SLMdisplay" };

		// OPTIONAL : Additional debug display for the script (any number of them, or none at all)
		DEBUGLCD_NAME = new List<string> { "SLMdebug" };

		// OPTIONAL : Timer blocks triggered a little before landing (ex : extend landing gear)
		LANDING_TIMER_NAME = new List<string> { "SLMlanding" };

		// OPTIONAL : Timer blocks triggered a little after liftoff landing (ex : retract landing gear)
		LIFTOFF_TIMER_NAME = new List<string> { "SLMliftoff" };

		// OPTIONAL : Timer blocks triggered when the SLM activates (ex : by the command "mode1")
		ON_TIMER_NAME = new List<string> { "SLMon" };

		// OPTIONAL : Timer blocks triggered when the SLM deactivates (ex : by the command "off", or at landing)
		OFF_TIMER_NAME = new List<string> { "SLMoff" };

		// OPTIONAL : Sound block used to warn of dangerous situations
		SOUND_NAME = new List<string> { "SLMsound" };
	}

}

/// <summary>
/// Configure here many settings for how the script behaves.
/// </summary>
public class SLMConfiguration {

	// Offset to the altitude value (ex : if the ship reads altitude 5m when landed, set it to 5)
	public readonly double altitudeOffset = 0;

	// Gravity exponent is 2 for Real Orbits, 7 for vanilla SE
	public readonly double gravityExponent = 2; 


	// Set any of these to false to disable the features by default
	// (they can always be switched on/off using commands when the script is running)
	public readonly bool autoLevel = true; 
	public readonly bool terrainAvoidGyro = true;
	public readonly bool terrainAvoidThrusters = true;


	// ---------------------------------------------------------------------------------
	// ---- There should be no reason to change the parameters below for normal use ----
	// ---------------------------------------------------------------------------------
	
	// ALTITUDE CORRECTION

	// Use this default value for the height of the ground above sea level
	public readonly double defaultASLmeters = 500;

	// SURFACE GRAVITY ESTIMATOR
	
	// Above the transition altitude, surface gravity comes from the gravity estimator
	// Below the transition altitude, local gravity value is used directly
	// In between, we use a linear interpolation between the two
	public readonly double gravTransitionHigh = 4000;
	public readonly double gravTransitionLow = 1000;

	// VERTICAL PID CONTROLLER
	// Coefficients of the PID controller used to control vertical speed
	// Input in m/s (vertical speed delta), output in thrust-to-weight ratio (thrust set point)
	public readonly double aiMax = 4;
	public readonly double aiMin = -0.1;
	public readonly double vertKp = 0.4;
	public readonly double vertKi = 0.05;
	public readonly double vertKd = 10;
	// Low-pass filter of the D component (0:no filtering, 1:values don't move!)
	public readonly double vertAdFilt=0.8; 
	public readonly double vertAdMax=0.5;



	// LANDING PROFILE THRUST SETTINGS

	// Margins applied to the ship LWR when computing the vertical speed target.
	// Used = (real LWR - offset) / LWRsafetyfactor
	public readonly double LWRoffset = 0.0;
	public readonly double LWRsafetyfactor = 1.1;
	// Lift-to-weight ratios and vertical acceleration (m/s² including gravity) limits :
	// profile computation will not use values higher than this.
	public readonly double LWRlimit = 5;
	public readonly double accel_limit = 30;

	// SPEED TARGET COMPUTATION AT HIGH ALTITUDE
	public readonly double vspeed_safe_limit = 500;
	public readonly double vspeed_default = 200;
	public readonly double LWR_mix_gnd_ratio = 0.7;

	// MODE 1 SETTINGS
	public readonly double elec_LWR_start = 1.3;
	public readonly double elec_LWR_sufficient = 2;
	public readonly double mode1_ion_alt_limit = 2000;
	public readonly double mode1_ion_speed = 115;
	public readonly double mode1_atmo_speed = 10;

	// SPEED TARGET FOR FINAL LANDING
	public readonly double transition_altitude = 20;
	public readonly double final_speed = 1.5;

	// PANIC/MARGINAL DETECTION
	public readonly double panicDelta = 5;
	public readonly double panicRatio = 10;
	public readonly double marginal_max = 10;
	public readonly double marginal_warn = 5;
	// H2 warning in %
	public readonly double h2_margin_warning = 5;

	// LANDING/LIFTOFF TIMERS TRIGGER ALTITUDE
	// The small difference acts as a hysteresis
	public readonly double landingTimerAltitude = 200;
	public readonly double liftoffTimerAltitude = 250;



	// RADAR CONFIGURATION
	// The camera used as a ground radar will limit itself to this range (in meters)
	public readonly double radarMaxRange = 2e5;



	// LEVELER AND TERRAIN AVOIDANCE SETTINGS

	// Leveler settings
	
	public readonly double maxAngle = 20;
	public readonly int smartDelayTime=20;
	public readonly double gyroResponsiveness=5;
	public readonly double gyroRpmScale=0.1;

	// Terrain Avoidance settings

	public readonly double horizKp = 0.5;
	public readonly double horizKi = 0.1;
	public readonly double horizKd = 0.1;
	public readonly double horizAiMax = 0.05;
	public readonly double speedScale = 20;
	public readonly double inertiaRatioSmall = 1e7;
	public readonly double inertiaRatioLarge = 6e8;



	// AUTOPILOT SETTINGS (for mode 4)

	public readonly double mode4InitialAlt = 50;
	public readonly double mode4InitialSpeed = 0;

	// Altitude PID controller 
	// Coefficients of the PID controller used to control altitude speed
	// Input in m (altitude delta), output in m/s (vertical speed set-point)
	public readonly double alt_aiMax = 0.5;
	public readonly double alt_aiMin = -0.1;
	public readonly double altKp = 0.3;
	public readonly double altKi = 0.1;
	public readonly double altKd = 0.5;
	public readonly double altAdFilt=0.8; 
	public readonly double altAdMax=0.5;

	// Pilot control settings settings
	public readonly double speedIncrement = 0.1;
	public readonly double maxSpeed = 100;
	public readonly int speedFilterLength=30;
	public readonly int altFilterLength=30;
	public readonly int safeSpeedFilterLength=10;

	// Safe speed settings
	public readonly double safeSpeedAltMin = 10;
	public readonly double safeSpeedAltMax = 400;
	public readonly double safeSpeedMin = 3;
	public readonly double safeSpeedMax = 200;

	// LOGGING
	public readonly bool ALLOW_LOGGING = false;
	public readonly int LOG_FACTOR = 2;
}

/// <summary>
/// Source of the vertical speed setpoint
/// </summary>
public enum SPSource {None,Profile,AltGravFormula,GravFormula,FinalSpeed,Hold,Unable}

/// <summary>
/// Source of the altitude value
/// </summary>
public enum AltSource {Undefined,Ground,Radar}

/// <summary>
/// Source of surface gravity estimate
/// </summary>
public enum GravSource {Undefined,Identified,Estimate}

/// <summary>
/// Gravity warning type
/// </summary>
public enum WarnType {Info,Good,Risk,Bad}

/// <summary>
/// Current terrain scan mode
/// </summary>
public enum ScanMode {NoRadar,SingleStandby,SingleNarrow,DoubleStandby,DoubleEarly,DoubleWide}

/// <summary>
/// Struct with planet data
/// </summary>
public struct Planet {
	public string shortname, name;
	public double atmo_density_sealevel, atmo_limit_altitude, hillparam, g_sealevel;
	public bool precise, set;

	public Planet(string shortname, string name, double atmo_density_sealevel, double atmo_limit_altitude, double hillparam, double g_sealevel, bool precise = true, bool set = true) {
		this.shortname = shortname.ToLower();
		this.name = name;
		this.atmo_density_sealevel = atmo_density_sealevel >= 0 ? atmo_density_sealevel : 0;
		this.atmo_limit_altitude = atmo_limit_altitude >= 0 ? atmo_limit_altitude : 0;
		this.hillparam = hillparam >= 0 ? hillparam : 0;
		this.g_sealevel = g_sealevel >= 0 ? g_sealevel : 0;
		this.precise = precise;
		this.set = set;
	}
}

/// <summary>
/// List of planets with their data. See tech doc Appendix A.
/// </summary>
public class PlanetCatalog {
	private List<Planet> Catalog;

	public PlanetCatalog() {

		// The "shortname" is used to identify the planet in the command line arguments and must be unique.

	Catalog = new List<Planet>
		{

		// The constructor for Planet takes the following :
		// shortname (what the command given to the PB must include),
		// name (nicer name displayed on the LCD),

		// Other data from the planet.sbc file:
		// atmo_density_sealevel	<Atmosphere><Density> xxx </Density></Atmosphere>
		// atmo_limit_altitude		<Atmosphere><LimitAltitude> xxx </LimitAltitude></Atmosphere>
		// hillparam				<HillParams Min = "whatever" Max =" xxxx "/>
		// g_sealevel				<SurfaceGravity> xxx </SurfaceGravity>

		// (optional) precise
		// (optional) set
		// The most generic planet, we don't know anything about it. It must always be in first position
		new Planet("unknown", "Unknown Planet", 1, 2, 0.1, 1, false, false),

		new Planet("dynvacuum", "Deduced Vacuum Planet", 0, 0, 0.1, 1, false, true),

		// We assume a moderately dense atmosphere but that doesn't go as high as Earthlike
		new Planet("dynatmo", "Deduced Atmo Planet", 0.8, 1, 0.05, 1, false, true),

		new Planet("vacuum", "Generic Vacuum Planet", 0, 0, 0.1, 1, false, true),
		// We assume a moderately dense atmosphere but that doesn't go as high as Earthlike
		new Planet("atmo", "Generic Atmo Planet", 0.8, 1, 0.05, 1, false, true),

		// Vanilla planets of space engineers. Values read directly from PlanetGeneratorDefinitions.sbc or Pertam.sbc or Triton.sbc
		new Planet("pertam", "Pertam", 1, 2, 0.025, 1.2),
		new Planet("triton", "Triton", 1, 0.47, 0.20, 1),
		new Planet("earth", "Earthlike", 1, 2, 0.12, 1),
		new Planet("alien", "Alien", 1.2, 2, 0.12, 1.1),
		new Planet("mars", "Mars (vanilla)", 1, 2, 0.12, 0.9),
		new Planet("moon", "Moon (vanilla)", 0, 1, 0.03, 0.25),

		// no value in .sbc file for atmo_density_sealevel and atmo_limit_altitude ?!
		new Planet("europa", "Europa", 0.5, 1, 0.06, 0.25),
		new Planet("titan", "Titan", 0.5, 1, 0.03, 0.25),

		// Below are additional planets from mods or custom planets that I like a lot

		// by Major Jon
		new Planet("komorebi", "Komorebi", 1.12, 2.4, 0.032, 1.14),
		new Planet("orlunda", "Orlunda", 0.89, 6, 0.01, 1.12),
		new Planet("trelan", "Trelan", 1, 1.2, 0.1285, 0.92),
		new Planet("teal", "Teal", 1, 2, 0.02, 1),
		new Planet("kimi", "Kimi", 0, 1, 0, 0.05),
		new Planet("qun", "Qun", 0, 1, 0.25, 0.42),
		new Planet("tohil", "Tohil", 0.5, 1, 0.03, 0.328),
		new Planet("satreus", "Satreus", 0.9, 1.5, 0.04, 0.95),

		new Planet("agni", "Agni", 1.8, 2.3, 0.022, 1.27),
		new Planet("cauldron", "Cauldron", 1, 3.5, 0.01, 1.58),
		// Kor (Cauldron System) is lower on the list to avoid collision with Valkor from Infinite
		new Planet("tellus", "Tellus", 1, 2.7, 0.06, 1),

		// by Elindis
		new Planet("pyke", "Pyke", 1.5, 2, 0.06, 1.42),
		new Planet("saprimentas", "Saprimentas", 1.5, 2, 0.07, 0.96),
		new Planet("aulden", "Aulden", 1.2, 2, 0.10, 0.82),
		new Planet("silona", "Silona", 0.85, 2, 0.03, 0.64),

		// by Infinite
		new Planet("argus", "Argus", 0.79, 2, 0.01, 1.45),
		new Planet("aridus", "Aridus", 1.3, 1, 0.1, 0.5),
		new Planet("microtech", "Microtech", 1, 0.5, 0.25, 1f),
		new Planet("hurston", "Hurston", 1, 1.9, 0.11, 1.1),
		new Planet("ignis", "Ignis", 0.85, 3, 0.005, 1.08),
		new Planet("tharsis", "Tharsis", 0.85, 3, 0.015, 0.75),
		new Planet("umbris", "Umbris", 0, 0, 0.05, 0.19),
		new Planet("valkor", "Valkor", 1, 0.3, 0.165, 1.05),
		new Planet("theros", "Theros", 1, 0.73, 0.1, 0.95),
		new Planet("thanatos", "Thanatos", 1.5, 2.8, 0.04, 1.4),
		new Planet("halcyon", "Halcyon", 0.85, 1.3, 0.3, 0.5),

		// from the Solar System Pack by Infinite
		new Planet("terra", "(Terra) Earth by Infinite", 2, 0.9, 0.02, 1),
		new Planet("luna", "Luna by Infinite", 0, 1, 0.07, 0.16),
		// because the script uses the first match, "mars" will still match the vanilla Mars
		new Planet("sspmars", "Mars by Infinite", 0.006, 2, 0.09, 0.38),
		new Planet("venus", "Venus by Infinite", 92, 2, 0.04, 0.9),
		new Planet("mercury", "Mercury by Infinite", 0, 1, 0.1, 0.37),
		new Planet("ceres", "Ceres by Infinite", 0, 0.5, 0.1, 0.05),
		new Planet("deimos", "Deimos by Infinite", 0, 0, 0.8, 0.05),
		new Planet("phobos", "Phobos by Infinite", 0, 0, 1, 0.05),

		new Planet("callisto", "Callisto by Infinite", 0, 0.5, 0.04, 0.12),
		new Planet("europa", "Europa by Infinite", 0, 0.5, 0.04, 0.13),
		new Planet("ganymede", "Ganymede by Infinite", 0, 0, 0.04, 0.14),
		new Planet("io", "Io by Infinite", 0, 0, 0.025, 0.18),

		new Planet("dione", "Dione by Infinite", 0, 0.5, 0.06, 0.05),
		new Planet("enceladus", "Enceladus by Infinite", 0, 0.5, 0.02, 0.05),
		new Planet("iapetus", "Iapetus by Infinite", 0, 0, 0.03, 0.05),
		new Planet("mimas", "Mimas by Infinite", 0, 0.5, 0.07, 0.05),
		new Planet("rhea", "Rhea by Infinite", 0, 0, 0.06, 0.05),
		new Planet("thetys", "Thetys by Infinite", 0, 0.5, 0.09, 0.05),
		new Planet("titan", "Titan by Infinite", 1.5, 3, 0.01, 0.14),

		new Planet("ariel", "Ariel by Infinite", 0, 0.5, 0.03, 0.05),
		new Planet("charon", "Charon by Infinite", 0, 0.5, 0.03, 0.05),
		new Planet("miranda", "Miranda by Infinite", 0, 0.5, 0.05, 0.08),
		new Planet("oberon", "Oberon by Infinite", 0, 0.5, 0.03, 0.05),
		new Planet("pluto", "Pluto by Infinite", 0.00001, 0, 0.03, 0.06),
		new Planet("titania", "Titania by Infinite", 0, 0.5, 0.03, 0.05),
		new Planet("triton", "Triton by Infinite", 0, 0.5, 0.03, 0.07),
		new Planet("umbriel", "Umbriel by Infinite", 0, 0.5, 0.03, 0.05),

		// Mirathi System also by Infinite
		new Planet("acheris", "Acheris", 1.5, 2, 0.0003, 1.36),
		new Planet("ares", "Ares", 0.85, 3, 0.025, 0.53),
		new Planet("euterpe", "Euterpe", 0.1, 2, 0.025, 0.19),
		new Planet("gaia", "Gaia", 1, 3, 0.03, 0.97),
		new Planet("nyxion", "Nyxion", 0, 0, 0.095, 0.22),
		new Planet("tartarus", "Tartarus", 1.1, 1.5, 0.08, 1.13),
		new Planet("tarvos", "Tarvos", 0.85, 3, 0.03, 0.75),
		new Planet("vulcanis", "Vulcanis", 0.85, 3, 0.065, 0.9),
		new Planet("zephyr", "Zephyr", 10, 3, 0.01, 3.24),
		new Planet("calliope", "Calliope", 1, 3, 0.03, 0.92),
		new Planet("calypso", "Calypso", 0.2, 3, 0.03, 0.63),
		new Planet("cryos", "Cryos", 0.1, 2, 0.065, 0.09),
		new Planet("erebus", "Erebus", 0, 0, 0.028, 0.32),

		// by Almirante Orlock
		new Planet("helghan", "Helghan", 1.2, 3.5, 0.01, 1.1),

		// by Fizzy
		new Planet("arcadia", "Arcadia", 1, 2, 0.04, 1.17),
		new Planet("sarilla", "Sarilla", 0, 0, 0.14, 0.74),
		new Planet("anteros", "Anteros", 1.10, 1.69, 0.07, 1.32),
		new Planet("chimera", "Chimera", 1.22, 1.5, 0.1, 1),
		new Planet("zira", "Zira", 0, 0, 0.14, 0.16),
		new Planet("celaeno", "Celaeno", 1.02, 6.5, 0.02, 0.93),
		new Planet("scylla", "Scylla", 0, 0, 0.01, 0.32),

		// by SlowpokeFarm
		new Planet("dustydesert", "Dusty Desert Planet", 1, 2, 0.12, 1),

		// Urdavis System by sam
		new Planet("gamadon", "Gamadon", 0.8, 2, 0.15, 0.72),
		new Planet("kuma", "Kuma", 1, 0.5, 0.1, 1),
		new Planet("mieliv", "Mieliv", 1, 0.5, 0.1, 1),
		new Planet("sario", "Sario", 0, 0, 0.30, 0.3),

		// by Major Jon again, but lower on the list
		// to avoid collision with valkor
		new Planet("kor", "Kor", 0, 0, 0.03, 0.74)
		};
	}

	/// <summary>
	/// Return the planet that matches a name given in the script command
	/// </summary>
	/// <param name="command">Input string containing the name</param>
	/// <param name="found">Output boolean, true if the planet if found</param>
	/// <returns>Planet (if not found, returns the "unknown" planet</returns>
	public Planet get_planet(string command, out bool found) {
		foreach (Planet candidate in Catalog) {
			if (command.ToLower().Contains(candidate.shortname)) {
				found = true;
				return candidate;
			}
		}
		found = false;
		return Catalog[0];
	}
}

/// <summary>
/// Class with all ship blocks needed by the script
/// </summary>
public class ShipBlocks {


	public ThrGroup lifters, fwdThr, rearThr, leftThr, rightThr;
	public List<IMyTextSurface> MainDisplays, DebugDisplays;
	public List<IMyParachute> parachutes;
	public IMyShipController ship_ctrller;
	public List<IMyTerminalBlock> landing_timers, liftoff_timers;
	public List<IMyTerminalBlock> on_timers, off_timers;
	public List<IMyGyro> gyros;
	public List<IMyLandingGear> gears;
	public List<IMyTerminalBlock> radars;
	public List<IMyTerminalBlock> soundblocks;
	public List<IMyGasTank> h2_tanks;

	public ShipBlocks() {
		// Only lists are initialized here, the rest is done in GetBlocks()
		MainDisplays = new List<IMyTextSurface>();
		DebugDisplays = new List<IMyTextSurface>();
		parachutes = new List<IMyParachute>();
		landing_timers = new List<IMyTerminalBlock>();
		liftoff_timers = new List<IMyTerminalBlock>();
		on_timers = new List<IMyTerminalBlock>();
		off_timers = new List<IMyTerminalBlock>();
		gyros = new List<IMyGyro>();
		gears = new List<IMyLandingGear>();
		radars = new List<IMyTerminalBlock>();
		soundblocks = new List<IMyTerminalBlock>();
		h2_tanks = new List<IMyGasTank>();
	}

}


	
LandingManager manager;
RunTimeCounter runtime;
Logger logger;
bool ranTick1=false;
bool ranTick10=false;
bool ranTick100=false;
bool logging = false;



public Program()
{
	// The constructor.

	Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10 | UpdateFrequency.Update100;
	
	SLMShipConfiguration shipconfig = new SLMShipConfiguration();
	ShipBlocks ship = GetBlocks(shipconfig);

	SLMConfiguration config = new SLMConfiguration();

	PlanetCatalog catalog = new PlanetCatalog();

	runtime = new RunTimeCounter(this);
	manager = new LandingManager(config, ship, catalog, runtime);

	logger = new Logger(manager.AllLogNames(), config.LOG_FACTOR, config.ALLOW_LOGGING);

}

public void Main(string arg, UpdateType updateSource)

{
	runtime.Count(ranTick1,ranTick10,ranTick100);

	// MANAGE ARGUMENTS AND REFRESH SOURCE
	if ((updateSource & (UpdateType.Trigger | UpdateType.Terminal)) != 0)
  	{
		if (arg == "off") {

			manager.ConfigureMode0();
			logging = false;
			logger.Clear();

		} else {
			
			// Mode switching

			if (arg.Contains("mode1")) {
				manager.ConfigureMode1();
				logging = true;

			} else if (arg.Contains("mode2")) {
				manager.ConfigureMode2();
				logging = true;

			} else if (arg.Contains("mode3")) {
				manager.ConfigureMode3();
				logging = true;

			} else if (arg.Contains("mode4")) {
				manager.ConfigureMode4();
			}

			// Change in autoleveler and terrain avoidance settings

			if (arg.Contains("angleoff")) manager.DisableAngle();
			else if (arg.Contains("angleon")) manager.EnableAngle();
			else if (arg.Contains("angleswitch")) manager.SwitchAngle();

			if (arg.Contains("thrustersoff")) manager.DisableThrust();
			else if (arg.Contains("thrusterson")) manager.EnableThrust();
			else if (arg.Contains("thrustersswitch")) manager.SwitchThrust();

			if (arg.Contains("leveloff")) manager.DisableLeveler();
			else if (arg.Contains("levelon")) manager.EnableLeveler();
			else if (arg.Contains("levelswitch")) manager.SwitchLeveler();

			// Change mode 4 altitude and speed setpoints

			if (arg.Contains("altup")) manager.Mode4IncreaseAltitude();
			else if (arg.Contains("altdown")) manager.Mode4DecreaseAltitude();

			if (arg.Contains("speedup")) manager.Mode4IncreaseSpeed();
			else if (arg.Contains("speeddown")) manager.Mode4DecreaseSpeed();

			if (arg.Contains("altswitch")) manager.Mode4AltSwitch();
			else if (arg.Contains("altgnd")) manager.Mode4AltGND();
			else if (arg.Contains("altsl")) manager.Mode4AltSL();

			if (arg.Contains("dumplog")) {
				logging = false;
				Me.CustomData = logger.Output();
			}

			if (arg.Contains("clearlog")) logger.Clear();

			manager.SetPlanet(arg);

		}
	}

	ranTick1=false;
	ranTick10=false;
	ranTick100=false;

	if ((updateSource & UpdateType.Update100) != 0) {
		ranTick100=true;
		manager.Tick100();
	}

	if ((updateSource & UpdateType.Update10) != 0) {
		ranTick10=true;
		manager.Tick10();
	}

	if ((updateSource & UpdateType.Update1) != 0) {
		ranTick1=true;
		manager.Tick1();

		if (logging) logger.Log(manager.AllLogValues());

	}
	
}


/// <summary>
/// Get all the blocks needed by the script and return them in a ShipBlocks object
/// </summary>
public ShipBlocks GetBlocks(SLMShipConfiguration config) {

	var s = new ShipBlocks();

	Echo ("SOFT LANDING MANAGER");

	// Filter function to find only blocks that are on the same grid as the script
	// and include none of the ignore patterns in their name
	Func<IMyTerminalBlock, bool> filter = b => {
		bool result=b.IsSameConstructAs(Me);
		foreach (string name in config.IGNORE_NAME) {
			if (b.CustomName.Contains(name)) result = false;
		}
		return result;
	};

	// Action to search for blocks based on their name from a list of strings.
	Action<List<IMyTerminalBlock>, List<string>, string, Func<IMyTerminalBlock, bool>> SearchBlocks = (blocksList, names, descr, filtr) => {
		List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
		foreach (string name in names) {
			GridTerminalSystem.SearchBlocksOfName(name, temp, filtr);
			blocksList.AddRange(temp);
		}
		Echo ("Found "+blocksList.Count+" "+descr);
	};

	// Action to seach for text surfaces in the named blocks and select the appropriate surface
	Action<List<IMyTextSurface>, List<string>, string, Func<IMyTerminalBlock, bool>> SearchSurfaces = (blocksList, prefixes, descr, filtr) => {

		List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
		SearchBlocks(temp, prefixes, "possible display(s)", filtr);

		foreach (IMyTerminalBlock pos in temp) {

			if (pos is IMyTextPanel) {

				// If its a simple text panel, add it directly
				blocksList.Add(pos as IMyTextSurface);

			} else if (pos is IMyTextSurfaceProvider) {

				// Otherwise, select the appropriate text surface based on its name
				IMyTextSurfaceProvider posta = (IMyTextSurfaceProvider)pos;
				if (posta.SurfaceCount >= 1 && posta.UseGenericLcd) {
					int N = Helpers.FindN(pos.CustomName, prefixes);
					if (N >=0) {
						blocksList.Add(posta.GetSurface(N));
					} else {
						blocksList.Add(posta.GetSurface(0));
					}
				}
			}
		}
		Echo ("Found "+blocksList.Count+" "+descr);
	};

	// Look for blocks based on the name
	
	SearchBlocks(s.radars, config.RADAR_NAME, "radars(s)", filter);
	SearchBlocks(s.landing_timers, config.LANDING_TIMER_NAME, "landing timer(s)", filter);
	SearchBlocks(s.liftoff_timers, config.LIFTOFF_TIMER_NAME, "liftoff timer(s)", filter);
	SearchBlocks(s.on_timers, config.ON_TIMER_NAME, "on timer(s)", filter);
	SearchBlocks(s.off_timers, config.OFF_TIMER_NAME, "off timer(s)", filter);
	SearchBlocks(s.soundblocks, config.SOUND_NAME, "sound block(s)", filter);

	// Find text surfaces

	SearchSurfaces(s.MainDisplays, config.LCD_NAME, "valid display(s)", filter);
	SearchSurfaces(s.DebugDisplays, config.DEBUGLCD_NAME, "valid debug display(s)", filter);


	// Look for blocks based on the type

	GridTerminalSystem.GetBlocksOfType(s.parachutes, filter);
	Echo ("Found "+s.parachutes.Count+" parachutes");

	GridTerminalSystem.GetBlocksOfType(s.gyros, filter);
	Echo ("Found "+s.gyros.Count+" gyros");

	GridTerminalSystem.GetBlocksOfType(s.gears, filter);
	Echo ("Found "+s.gears.Count+" landing gears");

	var all_tanks = new List<IMyGasTank >();
	GridTerminalSystem.GetBlocksOfType(all_tanks, filter);
	foreach (IMyGasTank tank in all_tanks) {
		if (tank.BlockDefinition.SubtypeName.Contains("Hydrogen")) {
			Echo ("Found h2 tank:"+tank.CustomName);
			s.h2_tanks.Add(tank);
		}
	}

	// Find a suitable ship controller.
	// Prefer the one that matches one of the configured names, otherwise select the first available one
	var named_ctrllers = new List<IMyTerminalBlock>();
	SearchBlocks(named_ctrllers, config.CTRLLER_NAME, "possible controller(s)", filter);

	if (named_ctrllers.Count >= 1) {
		s.ship_ctrller = named_ctrllers[0] as IMyShipController;
		Echo ("Using controller:" + s.ship_ctrller.CustomName);
	} else {
		var possible_controllers = new List<IMyShipController>();
		GridTerminalSystem.GetBlocksOfType(possible_controllers, b => b.CanControlShip);
		if (possible_controllers.Count == 0) {
			throw new Exception("Error: no suitable cockpit or remote control block.");
		} else {
			s.ship_ctrller = possible_controllers[0];
			Echo ("Using controller:" + s.ship_ctrller.CustomName);
		}
	}

	// Check radar orientation consistency
	foreach (IMyTerminalBlock radar in s.radars) {
		Matrix MatrixRadar, MatrixCockpit;
		radar.Orientation.GetMatrix(out MatrixRadar);
		s.ship_ctrller.Orientation.GetMatrix(out MatrixCockpit);

		if (MatrixRadar.Forward != MatrixCockpit.Down || MatrixRadar.Up != MatrixCockpit.Forward) {
			Echo ("Warning: radar "+radar.CustomName+" wrong orientation.");
		}
	}

	// Find thrusters based on orientation
	var fwd_thrusters = new List<IMyThrust>();
	var rear_thrusters = new List<IMyThrust>();
	var left_thrusters = new List<IMyThrust>();
	var right_thrusters = new List<IMyThrust>();
	var lifters = new List<IMyThrust>();

	var possible_thrusters = new List<IMyThrust>();

	GridTerminalSystem.GetBlocksOfType(possible_thrusters, filter);

	foreach (IMyThrust t in possible_thrusters) {

		Matrix MatrixCockpit,MatrixThrust;
		t.Orientation.GetMatrix(out MatrixThrust);
		s.ship_ctrller.Orientation.GetMatrix(out MatrixCockpit);

		if (MatrixThrust.Forward==MatrixCockpit.Down)
			lifters.Add(t);
		else if (MatrixThrust.Forward==MatrixCockpit.Backward)
			fwd_thrusters.Add(t);
		else if (MatrixThrust.Forward==MatrixCockpit.Forward)
			rear_thrusters.Add(t);
		else if (MatrixThrust.Forward==MatrixCockpit.Right)
			left_thrusters.Add(t);
		else if (MatrixThrust.Forward==MatrixCockpit.Left)
			right_thrusters.Add(t);
	}

	s.lifters = new ThrGroup(lifters);
	Echo ("Found "+s.lifters.Inventory()+" lifters");

	s.fwdThr = new ThrGroup(fwd_thrusters);
	Echo ("Found "+s.fwdThr.Inventory()+" fwd thr");

	s.rearThr = new ThrGroup(rear_thrusters);
	Echo ("Found "+s.rearThr.Inventory()+" rear thr");

	s.leftThr = new ThrGroup(left_thrusters);
	Echo ("Found "+s.leftThr.Inventory()+" left thr");

	s.rightThr = new ThrGroup(right_thrusters);
	Echo ("Found "+s.rightThr.Inventory()+" right thr");

	// Return the final object
	return s;
}

/// <summary>
/// Main class for the landing manager
/// </summary>
public class LandingManager {

	// TODO : too many attibutes used as global variables !
	// This should be refactored at some point !

	int mode = 0;
	public bool use_angle, use_horiz_thr,level;

	double observed_density = -1;

	double grav_now=0, shipWeight = 0;
	double gndAltitude = 0, slAltitude = 0, gnd_sl_offset=0;
	double radar_offset=0;

	double current_aLWR = 0,current_iLWR = 0,current_hLWR = 0;
	double aLWR_gnd=0, iLWR_gnd=0, hLWR_gnd=0;

	double current_LWRtarget = 0;
	double vertSpeedSP = 0, vertSpeed = 0, fwdSpeedSP = 0, fwdSpeed = 0, leftSpeedSP = 0, leftSpeed = 0;
	double vertSpeedDelta = 0, thrCommand = 0, lwrCommand = 0;
	bool panic = false;
	// The script should not go to off mode (mode 0) too soon after being activated.
	bool allowDisable = false;
	int marginal = 0;
	
	bool landing_timer_allowed = false, liftoff_timer_allowed = false;
	
	double gndGravExp;
	double maxGNow;
	bool blink;
	WarnType warnState = WarnType.Info;

	SPSource speedSPSrc=SPSource.None;
	AltSource altSrc=AltSource.Undefined;
	GravSource gravSrc=GravSource.Undefined;

	SLMConfiguration config;
	ShipBlocks ship;
	EarlySurfaceGravityEstimator estimator;
	ShipInfo shipinfo;
	LiftoffProfileBuilder profile;
	Planet planet;
	PlanetCatalog catalog;
	PIDController vert_PID;
	AutoLeveler leveler;
	GroundRadar radar;
	HorizontalThrusters horizThrusters;
	RunTimeCounter runTime;
	MovingAverage left_speed_tgt, fwd_speed_tgt, alt_filt;
	RateLimiter speed_tgt;
	AutoPilot autopilot;




	/// <summary>
	/// Constructor for the LandingManager class
	/// </summary>

	public LandingManager(SLMConfiguration conf, ShipBlocks ship_defined, PlanetCatalog catalog_input, RunTimeCounter runTime) {
		config = conf;
		ship = ship_defined;
		catalog = catalog_input;
		this.runTime = runTime;

		estimator = new EarlySurfaceGravityEstimator();
		shipinfo = new ShipInfo(ship, config);
		profile = new LiftoffProfileBuilder(config.gravityExponent);
		vert_PID = new PIDController(conf.vertKp, conf.vertKi, conf.vertKd, conf.aiMin, conf.aiMax, conf.vertAdFilt, config.vertAdMax);
		

		leveler = new AutoLeveler(ship.ship_ctrller, ship.gyros, Math.Min(config.maxAngle,shipinfo.MaxAngle()), config.smartDelayTime, config.gyroResponsiveness, config.gyroRpmScale);

		radar = new GroundRadar(ship.radars, config.radarMaxRange, config.speedScale);

		horizThrusters = new HorizontalThrusters(ship, config.smartDelayTime, config.horizKp, config.horizKi, config.horizKd, config.horizAiMax);

		left_speed_tgt = new MovingAverage(3);
		fwd_speed_tgt = new MovingAverage(3);
		alt_filt = new MovingAverage(3);
		speed_tgt = new RateLimiter(999, -0.1);
		autopilot = new AutoPilot(config);

		// These initial settings can be dynamically changed by the pilot
		use_angle = config.terrainAvoidGyro;
		use_horiz_thr = config.terrainAvoidThrusters;
		level = config.autoLevel;

		ConfigureMode0();
		SetUpLCDs();
		
	}

	// ------------------------------
	// PUBLIC METHODS
	// ------------------------------

	
	/// <summary>
	/// Switch to mode 0 (off or standby mode)
	/// </summary>
	public void ConfigureMode0() {
		mode = 0;
		ship.lifters.Disable();
		radar.DisableRadar();
		SetPlanet("unknown");
		TriggerOffTimers();
		profile.Invalidate();
		speedSPSrc = SPSource.None;
		altSrc = AltSource.Undefined;
		gravSrc = GravSource.Undefined;
		radar.mode = ScanMode.NoRadar;
		leveler.Disable();
		horizThrusters.Disable();
		estimator.Reset();
		InitializeTimers();
	}

	/// <summary>
	/// Switch to mode 1 : landing with hydrogen saving mode
	/// Some actions are skipped if we were in mode 2 before
	/// </summary>
	public void ConfigureMode1() {
		
		if (mode != 2 && mode != 1) {
			// Only if the SLM was off previously
			radar.StartRadar();
			TriggerOnTimers();
			profile.Invalidate();
			speed_tgt.Init(-config.vspeed_safe_limit);
		}

		mode = 1;
		ship.ship_ctrller.DampenersOverride = false;
		if (level) leveler.Enable();
		allowDisable = false;
		InitializeTimers();
	}

	/// <summary>
	/// Switch to mode 2 : landing with a quick profile
	/// Some actions are skipped in we were in mode 1 before
	/// </summary>
	public void ConfigureMode2() {
		
		if (mode != 2 && mode != 1) {
			// Only if the SLM was off previously
			radar.StartRadar();
			TriggerOnTimers();
			profile.Invalidate();
			speed_tgt.Init(-config.vspeed_safe_limit);
		}
		
		mode = 2;
		ship.ship_ctrller.DampenersOverride = false;
		if (level) leveler.Enable();
		allowDisable = false;
		InitializeTimers();
	}

	/// <summary>
	/// Switch to mode 3 : hover mode with horizontal speed control
	/// </summary>
	public void ConfigureMode3() {
		mode = 3;
		ship.ship_ctrller.DampenersOverride = true;
		if (level) leveler.Enable();
		speedSPSrc = SPSource.None;
		ship.lifters.Disable();
		allowDisable = false;
		radar.DisableRadar();
		InitializeTimers();
	}

	/// <summary>
	/// Switch to mode 4 : autopilot with altitude / forward speed hold
	/// </summary>
	public void ConfigureMode4() {
		mode = 4;
		ship.ship_ctrller.DampenersOverride = false;
		autopilot.mode4DesiredAltitude=config.mode4InitialAlt;
		autopilot.mode4DesiredSpeed=config.mode4InitialSpeed;
		speed_tgt.Init(0);
		if (level) leveler.Enable();
		speedSPSrc = SPSource.Hold;
		autopilot.Init();
		vert_PID.Reset();
		GearUnLock();
		allowDisable = false;
		radar.StartRadar();
		InitializeTimers();
	}

	public void SetPlanet(string name) {
		bool found;
		Planet tplanet = catalog.get_planet(name, out found);
		if (found) planet = tplanet;
	}

	// See tech doc §2.2
	public void Tick100() {
		
		estimator.UpdateEstimates(grav_now, slAltitude, planet.hillparam, config.gravityExponent);

		ManageSoundBlocks();
		shipinfo.UpdateMass();
		shipinfo.UpdateInertia();
		UpdateMaxGravitiesAndWarning();
		UpdatePlanetAtmo();
		ship.lifters.UpdateDensitySweep();
		allowDisable = true;
		
	}

	// See tech doc §2.2
	public void Tick10() {

		UpdateGrav();
		UpdateShipWeight();
		ship.lifters.UpdateThrust();
		horizThrusters.UpdateThrust();
		UpdateAvailableLWR();

		ComputeSurfaceGravityEstimate();
		
		UpdateLWRTarget();
		
		
		if ((mode == 1) || (mode == 2)) {

			radar.ScanForAltitude(90-leveler.pitch,90-leveler.roll);
			if (use_angle || use_horiz_thr)
				radar.ScanTerrain(90-leveler.pitch,90-leveler.roll);
			UpdateProfile();

		} else {

			UpdateAltitude();
			UpdateShipSpeeds();
		}

		if (mode == 3 || mode == 4) {
			// cos(40°) = 0.766
			const double COS40 = 0.766;
			autopilot.forward = COS40 * radar.ScanDir(40, 0, 90-leveler.pitch, 90-leveler.roll, (config.safeSpeedAltMax+10)/COS40);
			autopilot.UpdateSafeSpeed(gndAltitude);
			
		}

		UpdateDisplays();
		ManageTimers();
		ManagePanicParachutes();
		UpdateDebugDisplays();

		// Manage next mode transition

		// Disable the SLM if: no gravity or we've landed (detected from the altitude) or landing gear locked
		if (allowDisable && mode != 0 && (grav_now == 0 || gndAltitude < 2 || CheckGearLock()))
			ConfigureMode0();
		
	}


	// See tech doc §2.2
	public void Tick1() {

		if ((mode == 1) || (mode == 2)) {
				
			// Manage vertical speed
			radar.IncrementAltAge();
			UpdateAltitude();

			UpdateShipSpeeds();
			UpdateSpeedSetPoint();
			vertSpeedDelta = vertSpeedSP - vertSpeed;

			vert_PID.UpdatePIDController(vertSpeedDelta, config.aiMin, current_aLWR + current_iLWR + current_hLWR);
			ApplyThrustOverride(vert_PID.output);

			// Manage horizontal speed

			left_speed_tgt.AddValue(radar.RecommandLeftSpeed());
			leftSpeedSP = left_speed_tgt.Get();

			fwd_speed_tgt.AddValue(radar.RecommandFwdSpeed());
			fwdSpeedSP = fwd_speed_tgt.Get();
			
			
			if (level) {
				if (use_angle) {
					leveler.Tick(fwdSpeed, leftSpeed,fwdSpeedSP, leftSpeedSP);
				} else {
					leveler.Tick(fwdSpeed, leftSpeed,0,0);
				}
			}

			if (use_horiz_thr)
				horizThrusters.Tick(fwdSpeed, leftSpeed,fwdSpeedSP, leftSpeedSP, shipinfo.mass, use_angle,true);
		}

		if (mode==3) {
			UpdateShipSpeeds();

			autopilot.UpdateSpeedDirect(ship.ship_ctrller.MoveIndicator);

			fwdSpeedSP = autopilot.fwdSpeedSP;
			leftSpeedSP = autopilot.leftSpeedSP;
			leveler.Tick(fwdSpeed, leftSpeed, fwdSpeedSP, leftSpeedSP);

			horizThrusters.Tick(fwdSpeed, leftSpeed,fwdSpeedSP, leftSpeedSP, shipinfo.mass, use_angle, false);
			
		}

		// EXPERIMENTAL MODE 4
		if (mode == 4) {
			UpdateShipSpeeds();
			UpdateAltitude();

			autopilot.UpdateSpeedProgressive(ship.ship_ctrller.MoveIndicator);

			autopilot.UpdateVertSpeedSP(gndAltitude, slAltitude, leveler.pitch, leveler.roll);

			fwdSpeedSP = autopilot.fwdSpeedSP;
			leftSpeedSP = autopilot.leftSpeedSP;
			vertSpeedSP = autopilot.vertSpeedSP;

			leveler.Tick(fwdSpeed, leftSpeed,fwdSpeedSP, leftSpeedSP);

			vertSpeedDelta = vertSpeedSP - vertSpeed;
			vert_PID.UpdatePIDController(vertSpeedDelta, config.aiMin, current_aLWR + current_iLWR + current_hLWR);
			ApplyThrustOverride(vert_PID.output);

			if (use_horiz_thr)
				horizThrusters.Tick(fwdSpeed, leftSpeed,fwdSpeedSP, leftSpeedSP, shipinfo.mass, use_angle, true);
		}
	}


	// ------------------------------
	// PRIVATE METHODS WITH SIDE-EFFECTS INSIDE THE CLASS
	// (they update class attributes used as global variables but otherwise don't have an effect on the ship)
	// ------------------------------

	private void UpdateProfile() {
		
		if (estimator.best_confidence > 0.95) {
			// If we don't know the planet from a catalog, use the estimated surface gravity
			if (!planet.precise == false)
				planet.g_sealevel = Helpers.ms2_to_g(gndGravExp);

			// In any case, we need to use the estimated planet radius
			if (mode == 1)
				profile.Compute(config.transition_altitude+gnd_sl_offset, shipinfo, planet, estimator.best_est_radius, config.accel_limit, config.elec_LWR_sufficient, config.LWRsafetyfactor, config.vspeed_safe_limit, config.final_speed, ship.lifters);

			if (mode == 2)
				profile.Compute(config.transition_altitude+gnd_sl_offset, shipinfo, planet, estimator.best_est_radius, config.accel_limit, config.LWRlimit, config.LWRsafetyfactor, config.vspeed_safe_limit, config.final_speed, ship.lifters);
		}
	}

	private void UpdateShipWeight() {
		// Compute ship weight
		shipWeight = shipinfo.mass*grav_now;
	}

	private void UpdateGrav() {
		grav_now = ship.ship_ctrller.GetNaturalGravity().Length();
	}

	
	private void UpdateAvailableLWR() {

		current_aLWR=LWR(grav_now,shipinfo.mass,ship.lifters.eff_athrust);
		current_iLWR=LWR(grav_now,shipinfo.mass,ship.lifters.eff_ithrust);
		current_hLWR=LWR(grav_now,shipinfo.mass,ship.lifters.eff_hthrust);

		aLWR_gnd = LWR(gndGravExp, shipinfo.mass, ship.lifters.AtmoThrustForAtmoDensity(planet.atmo_density_sealevel));
		iLWR_gnd = LWR(gndGravExp, shipinfo.mass, ship.lifters.IonThrustForAtmoDensity(planet.atmo_density_sealevel)+ship.lifters.PrototechThrustForAtmoDensity(planet.atmo_density_sealevel));
		hLWR_gnd = LWR(gndGravExp, shipinfo.mass, ship.lifters.max_hthrust);
	}

	private void UpdateMaxGravitiesAndWarning() {

		maxGNow = (shipinfo.mass>0) ? Helpers.ms2_to_g(ship.lifters.AtmoThrustForAtmoDensity(planet.atmo_density_sealevel)+ship.lifters.IonThrustForAtmoDensity(planet.atmo_density_sealevel)+ship.lifters.PrototechThrustForAtmoDensity(planet.atmo_density_sealevel)+ship.lifters.max_hthrust)/(shipinfo.mass*config.LWRsafetyfactor*(1+config.LWRoffset)) : 0;

		warnState = (maxGNow < Helpers.ms2_to_g(gndGravExp)) ? WarnType.Bad : WarnType.Good;
	}


	private void UpdateSpeedSetPoint() {

		// See tech doc §3.1

		double temp_vspeed_sp;

		if (gndAltitude < GroundRadar.UNDEFINED_ALTITUDE) {

			if (gndAltitude > config.transition_altitude) {

				if (profile.IsValid()) {
					temp_vspeed_sp = - profile.InterpolateSpeed(slAltitude);
					speedSPSrc = SPSource.Profile;
				} else if (current_LWRtarget>1) {
					temp_vspeed_sp = - Math.Sqrt(2 * (gndAltitude-config.transition_altitude) * (current_LWRtarget-1)*gndGravExp) -config.final_speed;
					speedSPSrc = SPSource.AltGravFormula;
				} else {
					temp_vspeed_sp = 0;
					speedSPSrc = SPSource.Unable;
				}

			} else {
				temp_vspeed_sp = -config.final_speed;
				speedSPSrc = SPSource.FinalSpeed;				
			}

		} else {
			temp_vspeed_sp = -config.vspeed_default*(current_LWRtarget-1)/Helpers.ms2_to_g(grav_now);
			speedSPSrc = SPSource.GravFormula;
		}

		temp_vspeed_sp = Helpers.NotNan(temp_vspeed_sp);

		vertSpeedSP = Math.Max(temp_vspeed_sp, speed_tgt.Limit(temp_vspeed_sp));
		vertSpeedSP = Helpers.SatMinMax(vertSpeedSP,-config.vspeed_safe_limit,-config.final_speed);
	}

	private void UpdateShipSpeeds() {

		MyShipVelocities velocities = ship.ship_ctrller.GetShipVelocities();
		Vector3D normlinvel = Vector3D.Normalize(velocities.LinearVelocity);
		Vector3D normal_gravity = -Vector3D.Normalize(ship.ship_ctrller.GetNaturalGravity());
		vertSpeed = Helpers.NotNan(Vector3D.Dot(normlinvel, normal_gravity))* ship.ship_ctrller.GetShipSpeed();

		fwdSpeed = Helpers.NotNan(Vector3D.Dot(normlinvel, Vector3D.Cross(normal_gravity, ship.ship_ctrller.WorldMatrix.Right)) * ship.ship_ctrller.GetShipSpeed());
		leftSpeed = Helpers.NotNan(Vector3D.Dot(normlinvel, Vector3D.Cross(normal_gravity, ship.ship_ctrller.WorldMatrix.Forward)) * ship.ship_ctrller.GetShipSpeed());

	}

	private void UpdateAltitude() {

		// See tech doc §4.1

		double ctrller_alt_surf, radar_alt;

		bool ctrller_alt_surf_valid = ship.ship_ctrller.TryGetPlanetElevation(MyPlanetElevation.Surface, out ctrller_alt_surf);
		
		if (radar.exists && radar.valid) {

			radar_alt = radar.GetDistance();

			if (ctrller_alt_surf_valid) {
				if (radar.alt_age <= 1) 
					radar_offset = radar_alt-ctrller_alt_surf;
				alt_filt.AddValue(ctrller_alt_surf+radar_offset);
			} else {
				alt_filt.AddValue(radar_alt);
			}
			gndAltitude = alt_filt.Get();
			altSrc = AltSource.Radar;
		
		} else if (ctrller_alt_surf_valid) {

			gndAltitude = ctrller_alt_surf;
			altSrc = AltSource.Ground;

		} else {

			gndAltitude = GroundRadar.UNDEFINED_ALTITUDE;
			altSrc = AltSource.Undefined;
		}

		gndAltitude -= config.altitudeOffset;

		// Update the altitude offset from the ground to the sea level

		double ctrller_alt_sl;

		bool ctrller_alt_sl_valid = ship.ship_ctrller.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out ctrller_alt_sl);

		gnd_sl_offset = (ctrller_alt_sl_valid && ctrller_alt_surf_valid) ? ctrller_alt_sl - ctrller_alt_surf : config.defaultASLmeters;

		slAltitude = gndAltitude + gnd_sl_offset;

	}

	


	private void UpdateLWRTarget() {

		// Update the various LWR targets,for the current conditions as well as expected conditions on the planet surface
		
		double LWRtarget_here = ComputeLWRTarget(grav_now, mode, current_aLWR, current_iLWR, current_hLWR);
		
		double LWRtarget_gnd = ComputeLWRTarget(gndGravExp, mode, aLWR_gnd, iLWR_gnd, hLWR_gnd);

		current_LWRtarget = Helpers.Mix(LWRtarget_gnd,LWRtarget_here,config.LWR_mix_gnd_ratio);
	}

	private void UpdatePlanetAtmo() {

		// Estimate outside atmo density and update the planet object with the best guess unless a precise planet is defined

		double parachute_density = (ship.parachutes.Count > 0 && ship.parachutes[0].Atmosphere > 0.01) ? (double) ship.parachutes[0].Atmosphere : -1;
		double athrusters_density = (ship.lifters.max_athrust > 0 && ship.lifters.eff_athrust > 1) ? ship.lifters.eff_athrust/ship.lifters.max_athrust*0.7+0.3 : -1;
		double ithrusters_density = (ship.lifters.max_ithrust > 0 && ship.lifters.eff_ithrust > 1) ? (1-ship.lifters.eff_ithrust/ship.lifters.max_ithrust)/0.8 : -1;
		observed_density = Helpers.Max3(parachute_density, athrusters_density,ithrusters_density);

		bool found;
		// If disabled at high altitude, we need to set the planet to unknown
		if (mode == 0 && gndAltitude > 10000)
			planet = catalog.get_planet("unknown", out found);

		if (planet.precise == false) {

			if (planet.shortname == "unknown") planet.atmo_density_sealevel = ship.lifters.WorstDensity();

			if (planet.shortname == "atmo") planet.atmo_density_sealevel = Math.Max(planet.atmo_density_sealevel, ship.lifters.WorstDensity());

			if (observed_density > -1) {
				if (observed_density > 0.2 && gndAltitude < 10000) {
					planet = catalog.get_planet("dynatmo", out found);
					planet.atmo_density_sealevel = Math.Max(observed_density,ship.lifters.WorstDensity());
				}

				if (observed_density < 0.2 && gndAltitude < 1000) {
					planet = catalog.get_planet("dynvacuum", out found);
				}
			}
		}
	}

	// Estimates the surface gravity with what's available and return the value in m/s²
	private void ComputeSurfaceGravityEstimate() {

		if (planet.precise) {
			gravSrc = GravSource.Identified;
			gndGravExp = Helpers.g_to_ms2(planet.g_sealevel);

		} else {

			double weighted_ground_estimate = Helpers.Interpolate(0,1, grav_now, estimator.best_est_gravity, estimator.best_confidence);
			gravSrc = (estimator.best_confidence > 0.9) ? GravSource.Estimate : GravSource.Undefined;
			gndGravExp = Math.Max(grav_now, Helpers.Interpolate(config.gravTransitionLow, config.gravTransitionHigh, grav_now, weighted_ground_estimate, gndAltitude));
			planet.g_sealevel = Helpers.ms2_to_g(gndGravExp);
		}
	}

	public void EnableLeveler() {
		level = true;
		leveler.Enable();
	}

	public void DisableLeveler() {
		level = false;
		leveler.Disable();
	}

	public void SwitchLeveler() {
		level = !level;
		if (level) {
			leveler.Enable();
		} else {
			leveler.Disable();
		}
	}

	public void EnableThrust() {
		use_horiz_thr = true;
	}

	public void DisableThrust() {
		use_horiz_thr = false;
		horizThrusters.Disable();
	}

	public void SwitchThrust() {
		if (use_horiz_thr) DisableThrust();
		else EnableThrust();
	}

	public void EnableAngle() {
		use_angle = true;
	}

	public void DisableAngle() {
		use_angle = false;
	}

	public void SwitchAngle() {
		if (use_angle) DisableAngle();
		else EnableAngle();
	}

	public void Mode4IncreaseSpeed() {
		autopilot.mode4DesiredSpeed += 5;
	}

	public void Mode4DecreaseSpeed() {
		autopilot.mode4DesiredSpeed = Math.Max(autopilot.mode4DesiredSpeed - 5,0);
	}

	public void Mode4IncreaseAltitude() {
		autopilot.mode4DesiredAltitude += 10;
	}

	public void Mode4DecreaseAltitude() {
		autopilot.mode4DesiredAltitude = Math.Max(autopilot.mode4DesiredAltitude - 10,0);
	}

	public void Mode4AltSwitch() {
		if (autopilot.altitudeMode == AutoPilot.AltitudeMode.Ground) {
			Mode4AltSL();
		} else {
			Mode4AltGND();
		}
	}

	public void Mode4AltGND() {
		autopilot.altitudeMode = AutoPilot.AltitudeMode.Ground;
		autopilot.mode4DesiredAltitude = gndAltitude;
		autopilot.altitudeFilter.Set(gndAltitude);
	}

	public void Mode4AltSL() {
		autopilot.altitudeMode = AutoPilot.AltitudeMode.SeaLevel;
		autopilot.mode4DesiredAltitude = slAltitude;
		autopilot.altitudeFilter.Set(slAltitude);
	}


	// ------------------------------
	// PRIVATE METHODS WITH SIDE-EFFECTS ON THE SHIP
	// (they perfom actions on the ship blocks)
	// ------------------------------

	// Setup the LCDs (display type, font size etc.)
	private void SetUpLCDs() {

		foreach (IMyTextSurface d in ship.MainDisplays) {
			//d.Enabled = true;
			d.ContentType = ContentType.SCRIPT;
			d.Script = "None";
			d.ScriptBackgroundColor=VRageMath.Color.Black;
		}

		foreach (IMyTextSurface d in ship.DebugDisplays) {
			//d.Enabled = true;
			d.ContentType = ContentType.TEXT_AND_IMAGE;
			d.Font = "Monospace";
			d.FontColor = VRageMath.Color.White;
			d.FontSize = 0.45f;
		}

	}

	public void UpdateDebugDisplays() {

		foreach (IMyTextSurface d in ship.DebugDisplays) {

			// Compact debug info
			var sb = new StringBuilder();
			sb.AppendLine("-- SLM debug --");
			sb.AppendLine(runTime.RunTimeString());
			sb.AppendLine($"Density: {observed_density:0.00} (cat){planet.atmo_density_sealevel:0.00}");
			sb.AppendLine(shipinfo.DebugString());
			sb.AppendLine($"LWR tgt: {current_LWRtarget:0.00}");
			sb.AppendLine($"Alt: {slAltitude:0.0}, SL offset {gnd_sl_offset:000}m");
			sb.AppendLine(leveler.DebugString());
			sb.AppendLine(estimator.DebugString());
			sb.AppendLine(radar.AltitudeDebugString());
			sb.AppendLine(radar.TerrainDebugString());
			sb.AppendLine("[VERT PID] " + vert_PID.DebugString());
			sb.AppendLine(ship.lifters.DebugString());
			sb.AppendLine(horizThrusters.DebugString());
			sb.AppendLine(autopilot.DebugString());
			sb.AppendLine(profile.DebugString());
			d.WriteText(sb.ToString());
		}

	}

	// Update the main displays
	public void UpdateDisplays() {

		const float PLMARGIN = 5, PTMARGIN = 5, PBMARGIN = 35;
		const float HMAX = 20;
		VRageMath.Color GRAY = VRageMath.Color.Gray, WHITE = VRageMath.Color.White, RED = VRageMath.Color.Red, YELLOW = VRageMath.Color.Yellow, CYAN = VRageMath.Color.Cyan, GREEN = VRageMath.Color.Green, BLUE = VRageMath.Color.Blue;
		
		blink = !blink;

		foreach (IMyTextSurface d in ship.MainDisplays) {

			VRageMath.RectangleF view;

			float speed, speed_scale, alt_scale, xpos, ypos;

			view = new VRageMath.RectangleF(
				(d.TextureSize - d.SurfaceSize) / 2f,
				d.SurfaceSize
			);

			// Adapt drawing to screen size

			float width=d.SurfaceSize[0];
			float height=d.SurfaceSize[1];

			float LEFT_MARGIN, PLEFT, PRIGHT, HLEFT, HTOP, VLEFT, VTOP, TTOP, PTOP, PBOTTOM, HVER, ALEFT, ATOP, SLEFT, STOP, Tsize, HSIZE,THR_CUR_W,THR_MAX_W,THR_SW_W;
			bool showDetails = false;
			bool hcompact = false;

			Tsize = 1f;

			if (width > 400) {
				LEFT_MARGIN = 40;
				PLEFT = 150;
				PRIGHT = 30;
				HLEFT = 160;
				VLEFT = 40;
				THR_CUR_W = 20;
				THR_MAX_W = 5;
				THR_SW_W = 5;
				
				
			} else {
				LEFT_MARGIN = 5;
				PLEFT = 115;
				PRIGHT = 5;
				HLEFT = 115;
				VLEFT = 5;
				Tsize = 0.75f;
				THR_CUR_W = 20;
				THR_MAX_W = 5;
				THR_SW_W = 5;
			}

			

			if (height > 300) {
				TTOP = 20;
				PTOP = 70;
				PBOTTOM=height-112;
				HSIZE=40;
				HVER = PBOTTOM+HSIZE+10;
				VTOP = HTOP = height-100;
				showDetails = true;
				ALEFT = PLEFT+5;
				ATOP = (PTOP+PBOTTOM)/2-10;
				STOP = PBOTTOM-PBMARGIN-20;
				SLEFT = (PLEFT+width-PRIGHT)/2-20;
			} else {
				TTOP = 5;
				PTOP = 55;
				PBOTTOM=height-70;
				HSIZE=28;
				HVER = PBOTTOM+HSIZE+4;
				VTOP = HTOP = height-70;
				ALEFT = PLEFT+5;
				ATOP = (PTOP+PBOTTOM)/2-30;
				STOP = (PTOP+PBOTTOM)/2-30;
				SLEFT = (PLEFT+width-PRIGHT)/2-5;
				Tsize = 0.75f;
			}

			if (width < 300) {
				hcompact = true;
				Tsize = 0.7f;
				PLEFT = 80;
				HLEFT = 85;
				THR_CUR_W = 15;
				THR_MAX_W = 3;
				THR_SW_W = 3;
				Tsize = 0.65f;
				ALEFT = PLEFT+5;
				SLEFT = ALEFT+45;
				
			}

			// SPEED PROFILE

			// Thrust scaling in px/(m/s²)
			float THR_SCALE=(PBOTTOM-PTOP)/40; 

			// HHOR : horizontal position of the center of the horizontal speed display
			// HVER : vertical position
			float HHOR=width-PRIGHT-HSIZE;
			float HSCALE = HSIZE/HMAX;

			// Scale the profile display for various altitudes

			if (gndAltitude < 1600) {
				speed_scale = 300/(width-PLEFT-PRIGHT);
				alt_scale=2000/(PBOTTOM-PTOP);
			} else if (gndAltitude < 6400) {
				speed_scale = 400/(width-PLEFT-PRIGHT);
				alt_scale=8000/(PBOTTOM-PTOP);
			} else if (gndAltitude < 25600) {
				speed_scale = 550/(width-PLEFT-PRIGHT);
				alt_scale=32000/(PBOTTOM-PTOP);
			} else {
				speed_scale = 550/(width-PLEFT-PRIGHT);
				alt_scale=200000/(PBOTTOM-PTOP);
			}

			var frame = d.DrawFrame();

			if ((mode == 1 || mode == 2) && showDetails) {
				if (profile.IsValid() && profile.IsComputed()) {

					// Draw the profile with crosses
					for (int i=0; i<profile.alt_sl.Count()-1;i++) {
						speed = (float)Math.Min(profile.vert_speed[i],config.vspeed_safe_limit);
						ypos = PBOTTOM-70-((float)(profile.alt_sl[i]-gnd_sl_offset)/alt_scale);
						xpos = width-PRIGHT-20-speed/speed_scale;
						if (ypos >= PTOP && xpos >= PLEFT) {
							frame.Add(new MySprite()
							{
								Type = SpriteType.TEXT,
								Data = "+",
								Position = new Vector2(xpos, ypos) + view.Position,
								RotationOrScale = 1.5f ,
								Color = new VRageMath.Color((float)profile.hratio[i],(float)profile.aratio[i],(float)profile.iratio[i]),
								Alignment = TextAlignment.CENTER,
								FontId = "White"
							});
						}
					}

					// H2 margin

					double h2_capa = shipinfo.H2_capa_liters();

					if (h2_capa > 0 && ship.lifters.max_hthrust > 0) {
						double h2_to_use = profile.InterpolateH2Used(slAltitude)/h2_capa*100;
						double h2_stored = shipinfo.H2_stored_liters()/h2_capa*100;
						double h2_margin = h2_stored-h2_to_use;

						frame.Add(TextSprite(
							"H2 Margin : "+h2_margin.ToString("00")+"%",
							width-PRIGHT-200,
							PTOP+PTMARGIN,
							view,
							(h2_margin > config.h2_margin_warning) ? WHITE : RED,
							TextAlignment.LEFT));
					}

					if (panic) {
						frame.Add(new MySprite()
						{
							Type = SpriteType.TEXT,
							Data = "PANIC",
							Position = new Vector2(width/2,170) + view.Position,
							RotationOrScale = 2f ,
							Color = RED,
							FontId = "White"
						});
					}

					// Legend

					frame.Add(TextSprite(
						((width-PRIGHT-PLEFT)*speed_scale).ToString("000") + "m/s",
						PLEFT+PLMARGIN,
						PBOTTOM-PBMARGIN,
						view,
						GRAY,
						TextAlignment.LEFT));

					frame.Add(TextSprite(
						((PBOTTOM-PTOP)*alt_scale).ToString("000") + "m",
						PLEFT+PLMARGIN,
						PTOP+PTMARGIN,
						view,
						GRAY,
						TextAlignment.LEFT));

				} else if (profile.IsComputed()) {

					List<string> prof_warn = new List<string> {"Unable to compute", "valid landing profile"};

					for (int i=0;i < prof_warn.Count;i++) {
						frame.Add(TextSprite(
							prof_warn[i],
							(PLEFT+width-PRIGHT)/2,
							PTOP+i*20,
							view,
							VRageMath.Color.Orange,
							TextAlignment.CENTER));
					}

				} else {

					frame.Add(TextSprite(
						"No profile computed",
						(PLEFT+width-PRIGHT)/2,
						PTOP+25,
						view,
						WHITE,
						TextAlignment.CENTER));
				}
			} else if (showDetails) {
				List<string> strinfo = new List<string> {Helpers.Truncate(ship.ship_ctrller.CubeGrid.DisplayName,20), Math.Round(shipinfo.mass) + "kg"};
				for (int i=0;i < strinfo.Count;i++) {
					frame.Add(TextSprite(
						strinfo[i],
						PLEFT+5,
						PTOP+5+i*20,
						view,
						GRAY,
						TextAlignment.LEFT));
					}
			}

			// Yellow marker for the current altitude/speed

			ypos = PBOTTOM-70-(float)gndAltitude/alt_scale;
			xpos = width-PRIGHT-20+(float)vertSpeed/speed_scale;

			if (speedSPSrc == SPSource.Profile && ypos >= PTOP && xpos >= PLEFT) {
				frame.Add(new MySprite()
				{
					Type = SpriteType.TEXT,
					Data = "O",
					Position = new Vector2(xpos,ypos) + view.Position,
					RotationOrScale = 2f ,
					Color = YELLOW,
					Alignment = TextAlignment.CENTER,
					FontId = "White"
				});
			}

			// Speed indicators

			if ((mode == 1) || (mode == 2))
			frame.Add(TextSprite(
				Helpers.FormatCompact(-vertSpeed) + "m/s",
				SLEFT,
				STOP,
				view,
				YELLOW,
				TextAlignment.LEFT,
				Tsize));

			if ((mode == 1) || (mode == 2))
				frame.Add(TextSprite(
					Helpers.FormatCompact(-vertSpeedSP) + "m/s",
					SLEFT,
					STOP+20,
					view,
					CYAN,
					TextAlignment.LEFT,
					Tsize));

			// Altitude indicators

			string alt_txt = "";

			if (mode == 4 && autopilot.altitudeMode == AutoPilot.AltitudeMode.SeaLevel) {
				alt_txt = slAltitude.ToString("000") + "m (SL)";
			} else {
				alt_txt = gndAltitude < GroundRadar.UNDEFINED_ALTITUDE ? gndAltitude.ToString("000") + "m" : (radar.exists ? (radar.active ? "---" : "XXX") : "XXX");
			}

			frame.Add(TextSprite(
				alt_txt,
				ALEFT,
				ATOP,
				view,
				YELLOW,
				TextAlignment.LEFT,
				Tsize));

			if (mode == 4) {
				string alt_sp_text = autopilot.mode4DesiredAltitude.ToString("000") + "m";
				if (autopilot.altitudeMode == AutoPilot.AltitudeMode.SeaLevel)
					alt_sp_text += " (SL)";
				frame.Add(TextSprite(
					alt_sp_text,
					ALEFT,
					ATOP+20,
					view,
					CYAN,
					TextAlignment.LEFT,
					Tsize));
			}


			VRageMath.Color bColor;
			if (warnState == WarnType.Bad || speedSPSrc == SPSource.Unable) {
				bColor = RED;
			} else if (marginal >= config.marginal_warn) {
				bColor = VRageMath.Color.OrangeRed;
			} else {
				bColor = WHITE;
			}
			Helpers.Rectangle(frame,PLEFT,width-PRIGHT,PTOP,PBOTTOM, view, 2, bColor);

			// THRUST INDICATION

			// Helper method to draw thrust bars
			// list[0] = value A
			// list[1] = value B
			// list[2] = value C
			// list[3] = value D 
			// list[4] = x position
			// list[5] = width
			Action<List<double>, VRageMath.Color, VRageMath.Color, VRageMath.Color, VRageMath.Color> drawThrustBars = (list, colorA, colorB, colorC, colorD) =>
			{
				float Aref = (float)(list[0] / shipinfo.mass) * THR_SCALE;
				MySprite sA = MySprite.CreateSprite(
					"SquareSimple",
					new Vector2((float)list[4], PBOTTOM - Aref / 2) + view.Position,
					new Vector2((float)list[5], Aref)
				);
				sA.Color = colorA;
				frame.Add(sA);

				float Bref = (float)(list[1] / shipinfo.mass) * THR_SCALE;
				MySprite sB = MySprite.CreateSprite(
					"SquareSimple",
					new Vector2((float)list[4], PBOTTOM - Aref - Bref / 2) + view.Position,
					new Vector2((float)list[5], Bref)
				);
				sB.Color = colorB;
				frame.Add(sB);

				float Cref = (float)(list[2] / shipinfo.mass) * THR_SCALE;
				MySprite sC = MySprite.CreateSprite(
					"SquareSimple",
					new Vector2((float)list[4], PBOTTOM - Aref - Bref - Cref / 2) + view.Position,
					new Vector2((float)list[5], Cref)
				);
				sC.Color = colorC;
				frame.Add(sC);

				float Dref = (float)(list[3] / shipinfo.mass) * THR_SCALE;
				MySprite sD = MySprite.CreateSprite(
					"SquareSimple",
					new Vector2((float)list[4], PBOTTOM - Aref - Bref - Cref - Dref / 2) + view.Position,
					new Vector2((float)list[5], Dref)
				);
				sD.Color = colorD;
				frame.Add(sD);
			};

			// Draw the thrust bars for the current and max thrust
			List<List<double>> data = new List<List<double>>();
			data.Add(new List<double> { ship.lifters.current_athrust, ship.lifters.current_pthrust, ship.lifters.current_ithrust, ship.lifters.current_hthrust, LEFT_MARGIN + THR_CUR_W / 2 + 5, THR_CUR_W });
			data.Add(new List<double> { ship.lifters.eff_athrust, ship.lifters.eff_pthrust, ship.lifters.eff_ithrust, ship.lifters.eff_hthrust, LEFT_MARGIN + THR_CUR_W + THR_MAX_W / 2 + 10, THR_MAX_W });

			foreach (List<double> list in data)
				drawThrustBars(list, GREEN, VRageMath.Color.DarkBlue, BLUE, RED);

			// Draw the thrust bars for the density sweep (the order is not the same)
			double sweep_left = LEFT_MARGIN + THR_CUR_W + THR_MAX_W + 15;
			List<List<double>> data2 = new List<List<double>>();
			for (int i = 0; i < 11; i++)
			{
				data2.Add(new List<double> { ship.lifters.eff_hthrust, ship.lifters.pthrust_density[i], ship.lifters.ithrust_density[i], ship.lifters.athrust_density[i], sweep_left + THR_SW_W / 2 + i * THR_SW_W, THR_SW_W });
			}

			foreach (List<double> list in data2)
				drawThrustBars(list, RED, VRageMath.Color.DarkBlue, BLUE, GREEN);

			// Gravity estimate
			if (gravSrc != GravSource.Undefined) {
				float grav_x = (float)(sweep_left+THR_SW_W/2+Math.Min(planet.atmo_density_sealevel,1)*10*THR_SW_W);
				frame.Add(MySprite.CreateSprite
				(
					"SquareSimple",
					new Vector2(grav_x,PBOTTOM-(float)gndGravExp*THR_SCALE) + view.Position,
					new Vector2(10,10)
				));

				frame.Add(TextSprite(
					Helpers.ms2_to_g(gndGravExp).ToString("0.00")+"g",
					(float)sweep_left,
					PBOTTOM-(float)gndGravExp*THR_SCALE-50,
					view,
					warnState == WarnType.Bad ? RED : WHITE,
					TextAlignment.LEFT,
					Tsize));
			}

			// Gravity scale
			for (int i=0;i<5;i++) {
				frame.Add(MySprite.CreateSprite
				(
					"SquareSimple",
					new Vector2(LEFT_MARGIN,PBOTTOM-(float)Helpers.g_to_ms2(i)*THR_SCALE) + view.Position,
					new Vector2(5,5)
				));
			}

			// Bottom Line
			frame.Add(MySprite.CreateSprite
			(
				"SquareSimple",
				new Vector2(50+LEFT_MARGIN,PBOTTOM) + view.Position,
				new Vector2(100,2)
			));

			// HORIZONTAL SPEED DISPLAY

			// Draw a rectangle for the speed scale

			Helpers.Rectangle(frame,HHOR-HSIZE, HHOR+HSIZE, HVER+HSIZE, HVER-HSIZE, view, 2, WHITE);

			// Horiz speed tgt

			if ((mode == 1) || (mode == 2) || (mode == 3) || mode == 4) {
				var ssp=MySprite.CreateSprite
				(
					"SquareSimple",
					new Vector2(HHOR-(float)Helpers.SatMinMax(leftSpeedSP,-HMAX,HMAX)*HSCALE,
						        HVER-(float)Helpers.SatMinMax(fwdSpeedSP,-HMAX,HMAX)*HSCALE) + view.Position,
					new Vector2(12,12)
				);
				ssp.Color = CYAN;
				frame.Add(ssp);

				if ((mode == 3) || ((mode == 4) && !((Math.Abs(fwdSpeedSP) < Math.Abs(autopilot.mode4DesiredSpeed)) && blink))) {
					frame.Add(TextSprite(
						mode == 3 ? fwdSpeedSP.ToString("00") : autopilot.mode4DesiredSpeed.ToString("00"),
						HHOR,
						HVER+5,
						view,
						CYAN,
						TextAlignment.CENTER,
						Tsize));
				}
			}

			// Horiz speed now

			var snow=MySprite.CreateSprite
			(
				"SquareSimple",
				new Vector2(HHOR-(float)Helpers.SatMinMax(leveler.speedLeft,-HMAX,HMAX)*HSCALE,
							HVER-(float)Helpers.SatMinMax(leveler.speedFwd,-HMAX,HMAX)*HSCALE)
							 + view.Position,
				new Vector2(12,12)
			);
			snow.Color = YELLOW;
			frame.Add(snow);

			if (radar.obstruction)
				frame.Add(MySprite.CreateSprite
					(
						"Danger",
						new Vector2(HHOR,HVER) + view.Position,
						new Vector2(80,80)
					));

			// GENERAL

			// Top info

			List<string> top_str = new List<string> {"Soft Landing Manager", planet.name};

			for (int i=0;i < top_str.Count;i++) {
				frame.Add(TextSprite(
					top_str[i],
					width/2,
					TTOP+i*20,
					view,
					WHITE,
					TextAlignment.CENTER,
					Tsize));
			}
			
			
			// Vertical mode information
			string info;
			switch (speedSPSrc) {
				case SPSource.None: info="Disabled";break;
				case SPSource.Profile: info="Profile";break;
				case SPSource.AltGravFormula: info="Alt/grav";break;
				case SPSource.GravFormula: info="Gravity";break;
				case SPSource.FinalSpeed: info="Final";break;
				case SPSource.Unable: info="Unable";break;
				case SPSource.Hold: info="Alt Hold";break;
				default: info="Unknown";break;
			}

			List<string> ver_str = new List<string> {"VERTICAL", "Mode "+mode, info};

			for (int i=0;i < ver_str.Count;i++) {
				frame.Add(TextSprite(
					ver_str[i],
					VLEFT,
					VTOP+i*20,
					view,
					WHITE,
					TextAlignment.LEFT,
					Tsize));
			}

			// Horizontal mode information

			string str1 = "";
			if (use_angle == true && use_horiz_thr == true) {
				str1 = hcompact ? "G+T" : "Gyro("+leveler.MaxAngle().ToString("00")+"°)+thrust";
			} else if (use_angle == true && use_horiz_thr == false) {
				str1 = hcompact ? "G" : "Gyro("+leveler.MaxAngle().ToString("00")+"°) only";
			} else if (use_angle == false && use_horiz_thr == true) {
				str1 = hcompact ? "T" : "Thrusters only";
			} else {
				str1= hcompact ? "Off" : "Disabled";
			}


			string str2="";
			if (mode == 1 || mode == 2) {
				switch (radar.mode) {
					case ScanMode.DoubleStandby: str2 = hcompact ? "SBY(D)" : "Standby (D)";break;
					case ScanMode.SingleStandby: str2 = hcompact ? "SBY(S)" : "Standby (S)";break;
					case ScanMode.SingleNarrow: str2 = hcompact ? "Simple" : "Simple avoidance";break;
					case ScanMode.DoubleEarly: str2 = hcompact ? "Early" : "Early avoidance";break;
					case ScanMode.DoubleWide: str2 = hcompact ? "Wide" : "Wide avoidance";break;
					default:str2 = "No avoidance";break;
				}
			} else if (mode == 3) {
				str2= hcompact ? "Hover" : "Hover Mode";
			} else if (mode == 4) {
				str2= hcompact ? "Speed" : "Speed Hold";
			}

			List<string> hor_str = new List<string> {"HORIZ", str1,str2};

			for (int i=0;i < hor_str.Count;i++) {
				frame.Add(TextSprite(
					hor_str[i],
					HLEFT,
					HTOP+i*20,
					view,
					WHITE,
					TextAlignment.LEFT,
					Tsize));
			}

			frame.Dispose();
		}
	}

	private void ApplyThrustOverride(double PIDoutput) {

		// See tech doc §8

		// Compute the total thrust wanted
		lwrCommand = PIDoutput + 0.5+0.5*Math.Tanh(vertSpeedDelta+5);
		thrCommand = lwrCommand* shipWeight;

		// If the thrust wanted is higher than the total thrust available, we are in a marginal situation
		if ((thrCommand > ship.lifters.eff_total_thrust || vertSpeedDelta > 5) && marginal < config.marginal_max)  {
			marginal++;
		} else if (marginal > 0) {
			marginal--;
		}

		// Compute minimum thrust in mode 1
		double min_athrust = (mode==1) ?
			Helpers.Interpolate(-config.mode1_atmo_speed, -config.mode1_atmo_speed+5, shipWeight, 0, vertSpeed) : 0;
		
		double min_ithrust = (mode == 1 && gndAltitude > config.mode1_ion_alt_limit && vertSpeedDelta < 0) ?
 			Helpers.Interpolate(-config.mode1_ion_speed, -config.mode1_ion_speed+5, shipWeight, 0, vertSpeed) : 0;

		ship.lifters.ApplyThrust(thrCommand, min_athrust, min_ithrust);
	}

	private void ManagePanicParachutes() {

		if ((mode == 1 || mode == 2) && (vertSpeedDelta > gndAltitude/config.panicRatio + config.panicDelta)) {
			panic = true;
			foreach (IMyParachute parachute in ship.parachutes) {
				parachute.OpenDoor();
			}
		} else {
			panic = false;
		}
	}
	
	private void ManageSoundBlocks() {
		
		foreach (IMySoundBlock sound in ship.soundblocks) {
			
			if (panic) {
				sound.Enabled = true;
				sound.SelectedSound = "SoundBlockAlert2";
				sound.Play();
			} else if (warnState == WarnType.Bad || speedSPSrc == SPSource.Unable) {
				sound.Enabled = true;
				sound.SelectedSound = "SoundBlockAlert1";
				sound.Play();
			}
		}
	}

	/// <summary>
	///  Trigger the landing and liftoff timers depending on altitude.
	/// </summary>
	private void ManageTimers() {

		if (gndAltitude < config.landingTimerAltitude && landing_timer_allowed) {

			foreach (IMyTimerBlock timer in ship.landing_timers) 
				timer.Trigger();
			landing_timer_allowed = false;
			liftoff_timer_allowed = true;
		}

		// Make sure that the liftoff triggers altitude is higher than the landing altitude
		if (gndAltitude > Math.Max(config.liftoffTimerAltitude,config.landingTimerAltitude+1) && liftoff_timer_allowed) {

			foreach (IMyTimerBlock timer in ship.liftoff_timers)
				timer.Trigger();
			liftoff_timer_allowed = false;
			landing_timer_allowed = true;
		}
	}

	/// <summary>
	///  When the script is first started, arm either the landing of liftoff timer depending on altitude.
	/// </summary>
	private void InitializeTimers() {
		if (gndAltitude < config.landingTimerAltitude) {
			landing_timer_allowed = false;
			liftoff_timer_allowed = true;
		} else {
			landing_timer_allowed = true;
			liftoff_timer_allowed = false;
		}
	}

	private void TriggerOnTimers() {
		foreach (IMyTimerBlock timer in ship.on_timers)
			timer.Trigger();
	}

	private void TriggerOffTimers() {
		foreach (IMyTimerBlock timer in ship.off_timers)
			timer.Trigger();
	}

	// PRIVATE METHODS WITH NO SIDE-EFFECTS


	private double ComputeLWRTarget(double gravity, int mode, double aLWR, double iLWR, double hLWR) {

		double computedLWRtarget;

		if (gravity>0) {
			if (mode == 1) {
				computedLWRtarget = Helpers.Interpolate(config.elec_LWR_start, config.elec_LWR_sufficient,aLWR + iLWR + hLWR, aLWR + iLWR, aLWR + iLWR);
			} else {
				computedLWRtarget = aLWR + iLWR + hLWR;
			}
			
			// Compute final target : apply margins (ratio and offset), then limit to the max value
			return Math.Min((computedLWRtarget/config.LWRsafetyfactor) - config.LWRoffset,config.LWRlimit);
		} else {
			return 0;
		}
	}

	private double LWR(double gravity, double shipmass, double thrust) {
		return (gravity>0) ? thrust / (gravity*shipmass) : 0;
	}

	private bool CheckGearLock() {
		foreach (IMyLandingGear gear in ship.gears) {
			if (!gear.Closed && gear.IsWorking && gear.IsLocked )
				return true;
		}
		return false;
	}

	private void GearUnLock() {
		foreach (IMyLandingGear gear in ship.gears) {
			gear.Unlock();
		}
	}

	public List<string> LogNames() {
		return new List<string>{"mode","grav_now","vspeed","vspeed_sp","speed_sp_source","gnd_altitude","gnd_sl_offset","alt_source","vpid_p","vpid_i","vpid_d","PIDoutput","twr_wanted"};
	}

	public List<double> LogValues() {
		return new List<double>{mode,grav_now,vertSpeed,vertSpeedSP,(float)speedSPSrc,gndAltitude,gnd_sl_offset,(float)altSrc,vert_PID.ap,vert_PID.ai,vert_PID.ad,vert_PID.output,lwrCommand};
	}

	public List<string> AllLogNames() {
		List<string> names = new List<string>();
		names.AddRange(this.LogNames());
		names.AddRange(radar.LogNames());
		names.AddRange(horizThrusters.LogNames());

		return names;
	}

	public List<double> AllLogValues() {
		List<double> values = new List<double>();
		values.AddRange(this.LogValues());
		values.AddRange(radar.LogValues());
		values.AddRange(horizThrusters.LogValues());

		return values;
	}

	private MySprite TextSprite(string text, float x, float y, VRageMath.RectangleF view, VRageMath.Color color, TextAlignment align, float size = 1f) {
		return new MySprite()
			{
			Type = SpriteType.TEXT,
			Data = text,
			Position = new Vector2(x,y) + view.Position,
			RotationOrScale = size ,
			Color = color,
			FontId = "White",
			Alignment = align
			};
	}
	
}

/// <summary>
/// Estimates the surface gravity of a planet (for "Real Orbits" mod)
/// See tech doc §6
/// </summary>
public class EarlySurfaceGravityEstimator {

	public double current_est_radius=0, best_est_radius=0;
	public double current_est_gravity=0, best_est_gravity=0;
	public double current_confidence, best_confidence;

	double grav_prev = 0;
	double alt_sl_prev = 0;

	public void UpdateEstimates(double grav, double alt_sl, double hillparam, double exp) {

		double K, new_est_radius;

		if (grav_prev == 0 || alt_sl_prev == 0) {
			// First run, initalize and don't return anything
			grav_prev = grav;
			alt_sl_prev = alt_sl;
			new_est_radius = -1;

		} else if (grav != grav_prev && alt_sl != alt_sl_prev && grav > 0) {

			// Power method

			K = Math.Pow(grav_prev/grav,1/exp);

			if (K != 1) {
				new_est_radius = (K*alt_sl_prev-alt_sl)/(1-K);
			} else {
				// This should not happen
				new_est_radius = -2;
			}

			// Confidence in the estimation is based on how much it changes from one sample to the next
			current_confidence = Math.Pow(Math.Min(new_est_radius,current_est_radius)/Math.Max(new_est_radius,current_est_radius),2);

			if (new_est_radius < 0) current_confidence = 0;
			if (new_est_radius > 1e7) current_confidence = 0;

			current_est_radius = new_est_radius;

		} else {
			current_est_radius =  -3;
			current_confidence = 0;
		}

		if (alt_sl+current_est_radius > 0 && current_est_radius>0) {
			current_est_gravity = grav * Math.Pow((alt_sl+current_est_radius)/(current_est_radius*(1+hillparam)),exp);
		} else {
			current_est_gravity = -1;
			current_confidence = 0;
		}

		// Update best estimates if the current one is better

		current_confidence = Helpers.SatMinMax(current_confidence,0,1);

		if (current_confidence > best_confidence || current_confidence > 0.95) {
			best_confidence = current_confidence;
			best_est_radius = current_est_radius;
			best_est_gravity = current_est_gravity;
		}

		grav_prev = grav;
		alt_sl_prev = alt_sl;
	}

	public void Reset() {
		grav_prev = 0;
		alt_sl_prev = 0;
		best_confidence = 0;
	}

	public string DebugString() {

		string str = "[SURFACE GRAVITY ESTIMATOR]";
		
		str += "\nCurrent : R=:"+current_est_radius.ToString("000000")+"m, g="+ Helpers.ms2_to_g(current_est_gravity).ToString("0.00")+"g, c="+current_confidence.ToString("0.00");
		str += "\nBest    : R=:"+best_est_radius.ToString("000000")+"m, g="+Helpers.ms2_to_g(best_est_gravity).ToString("0.00")+"g, c="+best_confidence.ToString("0.00");
		
		return str;

	}
}

/// <summary>
/// Build a liftoff profile for a ship, based on its characteristics and the planet it is on, then used to control the ship during the landing phase by looking up the vertical speed according to altitude.
/// See tech doc §5
/// </summary>
public class LiftoffProfileBuilder {

	public double[] vert_speed = new double[NB_PTS];
	public double[] alt_sl = new double[NB_PTS];
	public double[] aratio = new double[NB_PTS];
	// Prototech and Ion are counted together
	// since they are used together in the same way
	// (they are both electric thrusters)
	// Prototech higher efficiency (30% in full atmo) is properly accounted for
	public double[] iratio = new double[NB_PTS];
	public double[] hratio = new double[NB_PTS];
	// H2 used in liters
	public double[] h2_used = new double[NB_PTS];	

	// Time step in seconds
	const double DT_START=0.5; 			
	// Number of time steps to compute
	const int NB_PTS=256;	
	// liter per second per N of thrust
	const double H2_FLOW_RATIO = 0.00081; 
	readonly double gravityExponent;
	double dt=DT_START;
	// A landing profile has two attribues :
	// - computed : if the profile has been computed or not
	// - valid    : if the computed profile concludes on a successfull liftoff
	bool valid = false;
	bool computed = false;

	public LiftoffProfileBuilder(double gravityExponent) {
		this.gravityExponent = gravityExponent;
	}

	// Compute atmospheric density at a set altitude above sea level, based on planet info and radius
	private double ComputeAtmoDensity(double alt_above_sl, Planet planet, double radius) {
		
		double atmo_alt = radius*planet.atmo_limit_altitude*planet.hillparam;

		if (alt_above_sl > atmo_alt) {
			return 0;
		} else if (alt_above_sl>=0) {
			return planet.atmo_density_sealevel * (1-alt_above_sl/atmo_alt);
		} else {
			return planet.atmo_density_sealevel;
		}
	}

	// Compute gravity value at a set altitude above sea level, based on planet info and radius
	private double ComputeGravity(double alt_above_sl, Planet planet, double radius) {

		double PlanetMaxRadius = radius * (1 + planet.hillparam);

		if (alt_above_sl >= (PlanetMaxRadius-radius)) {
			double raw = Helpers.g_to_ms2(planet.g_sealevel * Math.Pow(PlanetMaxRadius/(alt_above_sl+radius), gravityExponent));
			if (raw > Helpers.g_to_ms2(0.05)) {
				return raw;
			} else {
				return 0;
			}
		} else {
			return Helpers.g_to_ms2(planet.g_sealevel);
		}
	}

	// Build the altitude/speed profile by simulating a liftoff from a standstill at a specified altitude above sea level.
	public void Compute(double alt_start, ShipInfo shipinfo, Planet planet, double radius, double max_accel, double max_twr, double safetyfactor, double max_speed, double init_speed, ThrGroup lifters) {


		double t=0;
		bool temp_valid = true;

		double safety_inverse = 1/safetyfactor;

		vert_speed[0]=init_speed;
		alt_sl[0]=alt_start;
		aratio[0]=1;
		iratio[0]=1;
		hratio[0]=1;
		h2_used[0]=0;

		dt=DT_START;

		for (int i=1; i<NB_PTS; i++ ) {

			t = t+dt;
			// Gravity is in m/s²
			double gravity = ComputeGravity(alt_sl[i-1], planet, radius); 
			// Cache atmo density
			double atmoDensity = ComputeAtmoDensity(alt_sl[i - 1], planet, radius);  

			double thrust_max_accel = shipinfo.mass*Math.Min(max_accel,gravity+2*t);
			double thrust_max_twr = shipinfo.mass*gravity*max_twr;

			// Compute maximum thrust for electric thrusters

			// Since we're going down, it's safe to assume that whatever thrust the atmo thrusters
			// can provide right now, they can provide at least as much for the remainder of the descent
			double athrust = Math.Max(lifters.eff_athrust, lifters.AtmoThrustForAtmoDensity(atmoDensity)) * safety_inverse;

			athrust = Helpers.Min3(athrust, thrust_max_accel, thrust_max_twr);
			
			double ithrust = (lifters.IonThrustForAtmoDensity(atmoDensity)+lifters.PrototechThrustForAtmoDensity(atmoDensity)) * safety_inverse;
			ithrust = Math.Max(0,Helpers.Min3(ithrust, thrust_max_accel-athrust, thrust_max_twr-athrust));

			// Compute the hydrogen thrust so as not to exceed the limit
			double hthrust;
			if (vert_speed[i-1] >= max_speed) {
				hthrust=Math.Max(0,shipinfo.mass*gravity-athrust-ithrust);
			} else {
				hthrust=Math.Max(0,Helpers.Min3(lifters.max_hthrust, thrust_max_accel-athrust-ithrust, thrust_max_twr-athrust-ithrust)) * safety_inverse;
			}

			h2_used[i]=h2_used[i-1]+hthrust*H2_FLOW_RATIO*dt;

			double total_thrust=hthrust+athrust+ithrust;

			if (total_thrust > 0) {
				aratio[i]=athrust/total_thrust;
				iratio[i]=ithrust/total_thrust;
				hratio[i]=hthrust/total_thrust;
			} else {
				aratio[i]=0;
				iratio[i]=0;
				hratio[i]=0;
			}

			// Apply Newton formula, m/s², positive up
			double accel = total_thrust/shipinfo.mass - gravity;

			// Integrate acceleration to compute speed
			vert_speed[i]= Math.Min(accel * dt + vert_speed[i-1], max_speed);

			// If at any point, the vertical speed becomes negative, then it is a failed liftoff
			if (vert_speed[i] < 0) {
				temp_valid = false;
				break;
			}

			// Integrate speed to compute altitude above sea level
			alt_sl[i] = alt_sl[i-1] + vert_speed[i-1] * dt + 0.5 * accel * dt * dt;

			dt+=0.05;

		}
		computed = true;
		valid = temp_valid;
	}

	// Interpolates the altitude/speed profile to return the speed corresponding to the altitude given.
	// Uses linear interpolation, with binary search
	// If the altitude is above the final computed altitude, return the final computed speed.
	public double InterpolateSpeed(double alt) {
		return Interpolate(alt, ref vert_speed);
	}

	public double InterpolateH2Used(double alt) {
		return Interpolate(alt, ref h2_used);
	}

	private double Interpolate(double alt, ref double[] y) {

		int left = 0;
		int right = NB_PTS-1;
		int m=(left + right) / 2;

		if (!valid) return 0;

		// If we are currently below the starting altitude, return 0
		if (alt<=alt_sl[0]) return y[0];

		// If we are currently above the altitude of the last simulated point, return the speed corresponding to that
		if (alt >= alt_sl[NB_PTS-1]) return y[NB_PTS-1];

		// Binary search
		while (left <= right) {
			if (alt_sl[m] == alt) {
				break;
			} else if (alt_sl[m] > alt) {
				right = m-1;
			} else {
				left = m+1;
			}
			m=(left + right) / 2;
		}

		if (m+1>=NB_PTS) return y[NB_PTS-1];

		// Now m is the index such that alt_sl[m] is the highest value lower than alt
		return Helpers.Interpolate(alt_sl[m], alt_sl[m+1], y[m], y[m+1], alt);
		
	}

	public void Invalidate() {
		valid = false;
		computed = false;
	}

	public bool IsValid() => valid;
	public bool IsComputed() => computed;

	public double GetFinalSpeed() {
		if (valid & computed) {
			return vert_speed[NB_PTS-1];
		} else {
			return 0;
		}
	}

	public double GetFinalAlt() {
		if (valid & computed) {
			return alt_sl[NB_PTS-1];
		} else {
			return 0;
		}
	}

	public string DebugString() {
		
		string str = "[LITFOFF PROFILE]";

		str += "\nComputed:"+computed.ToString()+" Valid:"+valid.ToString();
		str += "\nFinal:"+vert_speed[NB_PTS-1].ToString("000.0")+"m/s , "+alt_sl[NB_PTS-1].ToString("000.0")+"m , "+(h2_used[NB_PTS-1]/1000).ToString("000.0")+"kL";
		str += "\nAlt(m) | speed (m/s) | a/i/h ratio";
		for (int i=0; i<10; i++) {
			str += "\n"+alt_sl[i].ToString("000.0") +"  | "+vert_speed[i].ToString("000.0") + "  | "+aratio[i].ToString("0.00")+" "+iratio[i].ToString("0.00")+" "+hratio[i].ToString("0.00");
		}

		return str;
	}
}

/// <summary>
/// Computes ship info, such as mass, inertia, H2 tank status, etc.
/// See tech doc §12.6
/// </summary>
public class ShipInfo {
	// kg
	public double mass;		
	// kg.m²
	public double inertia;	

	ShipBlocks ship;
	readonly SLMConfiguration config;

	public ShipInfo(ShipBlocks ship, SLMConfiguration config) {
		this.ship = ship;
		this.config = config;
		UpdateMass();
		UpdateInertia();
	}

	public void UpdateMass() {
		mass = ship.ship_ctrller.CalculateShipMass().TotalMass;
	}

	public void UpdateInertia() {
		Vector3I extend = ship.ship_ctrller.CubeGrid.Max - ship.ship_ctrller.CubeGrid.Min;
		Vector3D size = extend * ship.ship_ctrller.CubeGrid.GridSize;
		double inertia_x = mass * (size.Y*size.Y + size.Z*size.Z) / 12;
		double inertia_y = mass * (size.X*size.X + size.Z*size.Z) / 12;
		double inertia_z = mass * (size.X*size.X + size.Y*size.Y) / 12;

		inertia = Helpers.Max3(inertia_x, inertia_y, inertia_z);
	}

	public double MaxAngle() {
		return ship.gyros.Count / inertia * (ship.ship_ctrller.CubeGrid.GridSizeEnum == MyCubeSize.Small ? config.inertiaRatioSmall : config.inertiaRatioLarge);
	}

	public double H2_stored_liters() {
		double fill = 0;
		foreach (IMyGasTank tank in ship.h2_tanks) {
			fill += tank.FilledRatio * tank.Capacity;
		}
		return fill;
	}

	public double H2_capa_liters() {
		double capa = 0;
		foreach (IMyGasTank tank in ship.h2_tanks) {
			capa += tank.Capacity;
		}
		return capa;
	}

	public string DebugString() {
		return "[SHIP INFO]\n"+mass.ToString("000000")+"kg "+inertia.ToString("000000")+"kg.m² "+MaxAngle().ToString("00.00")+"° "+(H2_stored_liters()/1000).ToString("0"); 
	}
	
}

/// <summary>
/// PID controller with anti-windup and low-pass filtering of the D component
/// See tech doc §12.5
/// </summary>
public class PIDController {

	public double output = -1;
	
	public double ap, ai, ad;

	// PID coefficients (constant during execution, defined with the constructor)
	readonly double KP, KI, KD, AI_MIN_FIXED, AI_MAX_FIXED, AD_FILT, AD_MAX;

	// Private PID parameters
	double delta_prev, deriv_prev;

	public PIDController(double kp, double ki, double kd, double ai_min_fixed, double ai_max_fixed, double ad_filt, double ad_max) {
		KP=kp;
		KI=ki;
		KD=kd;
		AI_MIN_FIXED=ai_min_fixed;
		AI_MAX_FIXED=ai_max_fixed;
		AD_FILT=Helpers.SatMinMax(ad_filt,0,1);
		AD_MAX=ad_max;
	}


	public void UpdatePID(double delta) {
		UpdatePIDController(delta, AI_MIN_FIXED, AI_MAX_FIXED);
	}

	public void UpdatePIDController(double delta, double ai_min_dynamic, double ai_max_dynamic) {

		delta = Helpers.NotNan(delta);

		// P
		ap = delta * KP;

		// I
		ai = ai + delta*KI;

		// Saturate the integral component, first with fixed limits (that have priority), then with dynamic limits
		ai = Helpers.SatMinMax(ai, AI_MIN_FIXED, AI_MAX_FIXED);
		ai = Helpers.SatMinMax(ai, ai_min_dynamic, ai_max_dynamic);
		
		// D
		// Low-pass filtering of the derivative component
		double deriv = AD_FILT * deriv_prev + (1-AD_FILT) *(delta - delta_prev);
		deriv_prev = deriv;
		delta_prev = delta;
		ad = deriv*KD;
		ad = Helpers.SatMinMax(ad, -AD_MAX, AD_MAX);
		
		// PID output
		output = ap + ai + ad;

	}

	public void Reset() {
		deriv_prev=0;
		delta_prev=0;
		ap=0;
		ai=0;
		ad=0;
	}

	public string DebugString() {

		return "P: " +  Math.Round(ap,2).ToString("+0.00;-0.00") + " I:" + Math.Round(ai,2).ToString("+0.00;-0.00") +" D:"+Math.Round(ad,2).ToString("+0.00;-0.00");

	}
}

/// <summary>
/// Gyroscope controller to align the ship with a target orientation (from Flight Assist by Naosyth)
/// See tech doc §9
/// </summary>
public class GyroController 
{
	private bool gyroOverride;
	private double angle;
	
	readonly List<IMyGyro> gyros;
	readonly double gyroRpmScale;
	Vector3D reference, target;

	public GyroController(List<IMyGyro> gyros, double gyroRpmScale)
	{
		this.gyros = gyros;
		this.gyroRpmScale = gyroRpmScale;
	}

	public void SetGyroOverride(bool state)
	{
		gyroOverride = state;
		foreach (IMyGyro g in gyros) {
			
			if (!state) {
				g.Pitch = 0;
				g.Yaw = 0;
				g.Roll = 0;
			}
			g.GyroOverride = state;
		}
	}

	public void SetTargetOrientation(Vector3D setReference, Vector3D setTarget)
	{
		reference = setReference;
		target = setTarget;
	}

	public void Tick()
	{
		if (!gyroOverride) return;
		
		foreach (IMyGyro g in gyros) {

			// Translating the reference and target vectors to local space of the gyro
			Matrix localOrientation;
			g.Orientation.GetMatrix(out localOrientation);
			var localReference = Vector3D.Transform(reference, MatrixD.Transpose(localOrientation));
			var localTarget = Vector3D.Transform(target, MatrixD.Transpose(g.WorldMatrix.GetOrientation()));

			// Calculating the rotation needed to align the gyro with the target
			// using the cross product of the local reference and target vectors
			var axis = Vector3D.Cross(localReference, localTarget);
			angle = axis.Length();

			// Don't know what this does
			angle = Math.Atan2(angle, Math.Sqrt(Math.Max(0.0, 1.0 - angle * angle)));

			// If the angle is negative, the rotation is in the opposite direction
			if (Vector3D.Dot(localReference, localTarget) < 0)
				angle = Math.PI - angle;

			// Scaling the rotation : keep the axis of rotation, but scale the angle
			axis.Normalize();
			axis *= Math.Max(0.002, g.GetMaximum<float>("Roll") * MathHelper.RPMToRadiansPerSecond * (angle / Math.PI) * gyroRpmScale);

			// Applying the rotation to the gyroscope
			g.Pitch = (float)-axis.X;
			g.Yaw = (float)-axis.Y;
			g.Roll = (float)-axis.Z;
		}

	}
}

/// <summary>
/// Manages ship orientation to achieve a desired forward and lateral speed.
/// See tech doc §9
/// </summary>
public class AutoLeveler {

	private readonly int delay;
	private readonly double gyroResponsiveness;
	private readonly double maxAngle;
	private bool Enabled = false;
	private readonly IMyShipController cockpit;
	private readonly GyroController gyroController;
	private int timer;
	private double desiredPitch, desiredRoll;

	public double pitch, roll;
	public double speedFwd, speedLeft, desiredSpeedFwd, desiredSpeedLeft;

	public AutoLeveler(IMyShipController cockpit, List<IMyGyro> gyros, double maxAngle, int delay, double gyroResponsiveness, double gyroRpmScale) {
		this.cockpit = cockpit;
		this.gyroController = new GyroController(gyros, gyroRpmScale);
		this.maxAngle = maxAngle;
		this.delay = delay;
		this.gyroResponsiveness = gyroResponsiveness;
	}

	public void Enable() {
		Enabled = true;
		gyroController.SetGyroOverride(true);
	}

	public void Disable() {
		Enabled = false;
		gyroController.SetGyroOverride(false);
		speedFwd = 0;
		speedLeft = 0;
		desiredSpeedFwd = 0;
		desiredSpeedLeft = 0;
	}

	public void Tick(double speedFwd, double speedLeft, double desiredSpeedFwd, double desiredSpeedLeft) {
		
		this.speedFwd = speedFwd;
		this.speedLeft = speedLeft;
		this.desiredSpeedFwd = desiredSpeedFwd;
		this.desiredSpeedLeft = desiredSpeedLeft;

		if (Enabled) {

			Vector3D gravity = -Vector3D.Normalize(cockpit.GetNaturalGravity());
			pitch = Helpers.NotNan(Math.Acos(Vector3D.Dot(cockpit.WorldMatrix.Forward, gravity)) * Helpers.radToDeg);
			roll = Helpers.NotNan(Math.Acos(Vector3D.Dot(cockpit.WorldMatrix.Right, gravity)) * Helpers.radToDeg);

			// "smart delay" : if the pilot is actively trying to move the ship, don't auto-level
			// otherwise, auto-level after a short delay

			if (cockpit.RotationIndicator.Length() > 0.0f)  {

				desiredPitch = -(pitch - 90);
				desiredRoll = (roll - 90);
				gyroController.SetGyroOverride(false);
				timer = 0;

			} else if (timer > delay) {

				// After the delay, auto-level the ship

				// The desired pitch and roll are based on the ship's current velocity
				// An atan function is used to scale the desired pitch and roll to the max pitch and roll values

				gyroController.SetGyroOverride(true);
				desiredPitch = Math.Atan((speedFwd-desiredSpeedFwd) / gyroResponsiveness) / Helpers.halfPi * maxAngle;
				desiredRoll = Math.Atan((speedLeft-desiredSpeedLeft) / gyroResponsiveness) / Helpers.halfPi * maxAngle;

				Matrix cockpitOrientation;
				cockpit.Orientation.GetMatrix(out cockpitOrientation);
				var quatPitch = Quaternion.CreateFromAxisAngle(cockpitOrientation.Left, (float)(desiredPitch * Helpers.degToRad));
				var quatRoll = Quaternion.CreateFromAxisAngle(cockpitOrientation.Backward, (float)(desiredRoll * Helpers.degToRad));
				var reference = Vector3D.Transform(cockpitOrientation.Down, quatPitch * quatRoll);

				gyroController.SetTargetOrientation(reference, cockpit.GetNaturalGravity());

				gyroController.Tick();

			} else {
				timer++;
			}
		}
	}

	

	public string DebugString() {
		string str = "[AUTO LEVELER]";
		str += "\nFwd :" + speedFwd.ToString("000.0") + "(" + desiredSpeedFwd.ToString("000.0") + ")";
		str += "\nLeft:" + speedLeft.ToString("000.0") + "(" + desiredSpeedLeft.ToString("000.0") + ")";
		str += "\npitch:" + Math.Round(90-pitch,2)+" roll:" + Math.Round(roll-90,2) + "max:"+Math.Round(maxAngle,2);
		
		return str;
	}

	public double MaxAngle() {
		return maxAngle;
	}


}

/// <summary>
/// Handles the downward-facing cameras to scan for terrain slopes and altitude
/// See tech doc §7
/// </summary>
public class GroundRadar {

	public bool valid=false;
	public bool exists=false;
	public bool active=false;
	public bool obstruction = false;
	public ScanMode mode;
	public int alt_age=0;

	public const double UNDEFINED_ALTITUDE = 1e6;


	const double RANGE_MARGIN = 50;
	const double START_RANGE = 1000;
	const double MAX_TERRAIN_DISTANCE_SINGLE_RADAR = 180;
	const double MAX_TERRAIN_DISTANCE_DOUBLE_RADAR = 200;
	const double DOUBLE_RADAR_WIDE_SCAN_DISTANCE = 1000;
	const double DOUBLE_RADAR_INITIAL_SCAN_DISTANCE = 5000;

	const double MIN_SCAN_ANGLE = 2;
	const double MAX_SCAN_ANGLE = 30;
	const double GROUND_SCAN_HORIZ_LENGTH = 20;
	const int MAX_SCAN_PACER = 0;
	const double HORIZ_DEADZONE = 2;
	const double HORIZ_MAX_SPEED = 20;

	


	readonly double RADAR_MAX_RANGE;
	readonly double SPEED_SCALE;
	readonly double MAX_TERRAIN_DIST;
	readonly bool double_radar;

	double terrainScanRange, altScanRange;
	
	MyDetectedEntityInfo radar_return;
	
	double d_fwd, d_rear, d_left, d_right,d_fwd_wide, d_rear_wide, d_left_wide, d_right_wide, d_fwd_left, d_fwd_right, d_rear_left, d_rear_right;
	double angle=1, double_angle=1, diag_angle=1;
	double dz=HORIZ_DEADZONE;
	
	int scan_step = 0;
	int scan_pacer = 0;


	Vector3D hitpos;
	IMyCameraBlock altitudeRadar;
	IMyCameraBlock terrainRadar;

	public GroundRadar(List<IMyTerminalBlock> radars, double max_range, double speed_scale) {
		if (radars.Count == 0) {
			exists = false;
			mode = ScanMode.NoRadar;
		} else if (radars.Count == 1) {
			// If only one radar is available, use it for both altitude and terrain
			altitudeRadar = radars[0] as IMyCameraBlock;
			terrainRadar = radars[0] as IMyCameraBlock;
			MAX_TERRAIN_DIST = MAX_TERRAIN_DISTANCE_SINGLE_RADAR;
			double_radar = false;
			exists = true;
			mode = ScanMode.SingleStandby;
		} else {
			// If two or more radars are available, use the first one for altitude and the second one for terrain
			altitudeRadar = radars[0] as IMyCameraBlock;
			terrainRadar = radars[1] as IMyCameraBlock;
			MAX_TERRAIN_DIST = MAX_TERRAIN_DISTANCE_DOUBLE_RADAR;
			double_radar = true;
			exists = true;
			mode = ScanMode.DoubleStandby;
		}
		
		RADAR_MAX_RANGE = max_range;
		SPEED_SCALE = speed_scale;
	}

	public void DisableRadar() {
		if (!exists) return;

		altitudeRadar.EnableRaycast = false;
		terrainRadar.EnableRaycast = false;

		d_fwd = d_rear = d_left = d_right = MAX_TERRAIN_DIST;
		d_fwd_wide = d_rear_wide = d_left_wide = d_right_wide = MAX_TERRAIN_DIST;
		d_fwd_left = d_fwd_right = d_rear_left = d_rear_right = MAX_TERRAIN_DIST;

		valid = false;
		active = false;
	}

	public void StartRadar() {
		if (!exists) return;

		altScanRange = START_RANGE;
		altitudeRadar.EnableRaycast = true;
		terrainRadar.EnableRaycast = true;
		alt_age=0;

		d_fwd = d_rear = d_left = d_right = MAX_TERRAIN_DIST;
		d_fwd_wide = d_rear_wide = d_left_wide = d_right_wide = MAX_TERRAIN_DIST;
		d_fwd_left = d_fwd_right = d_rear_left = d_rear_right = MAX_TERRAIN_DIST;

		active = true;
	}

	/// <summary>
	/// Attempt to cast a ray to mesure ship altitude.
	/// If the ray doesn't hit either a planet, an asteroid or a large grid,
	/// then the scan range is increased for the next attempt (up to some limit).
	/// </summary>
	public void ScanForAltitude(double pitch, double roll) {
		if (!exists) return;

		if (altitudeRadar.CanScan(altScanRange)) {

			radar_return = altitudeRadar.Raycast(altScanRange,(float)-pitch,(float)-roll);

			if ((radar_return.Type ==  MyDetectedEntityType.Planet || radar_return.Type == MyDetectedEntityType.LargeGrid || radar_return.Type == MyDetectedEntityType.Asteroid) && radar_return.HitPosition.HasValue) {

				// If we have a return (either a planet, or a large grid (landing pad, silo)), adjust range
				valid = true;
				altScanRange = GetDistance()+RANGE_MARGIN;
				alt_age = 0;

			} else {

				// If we have no return, invalidate the previous return and increase the scan range
				valid = false;
				altScanRange = Math.Min(altScanRange*2,RADAR_MAX_RANGE);
			}
		}
	}

	public void IncrementAltAge() {
		alt_age++;
	}

	public double GetDistance() {
		if (!exists) return UNDEFINED_ALTITUDE;

		if (valid) {
			// Hitpos is updated when the radar has a return
			// Mypos is always updated
			hitpos = radar_return.HitPosition.Value; 
			Vector3D mypos = altitudeRadar.GetPosition(); 
			return VRageMath.Vector3D.Distance(hitpos,mypos);
		} else {
			return UNDEFINED_ALTITUDE;
		}
	}

	/// <summary>
	/// Perform one step of scanning the terrain below the ship. Each time this is called, the ideal
	/// radar scan distance is determined, and if it can scan, it will cast a pair of rays. The angle
	/// of the rays change at each call, to update the overall view of the ground below.
	/// </summary>
	public void ScanTerrain(double ship_pitch, double ship_roll) {
		
		// Define the scan mode
		if (!exists) {
			mode = ScanMode.NoRadar;
			return;
		}

		if (double_radar) {

			mode = ScanMode.DoubleStandby;
			terrainScanRange = Math.Min(GetDistance() * 1.2 + 20, DOUBLE_RADAR_INITIAL_SCAN_DISTANCE);
			if (valid && GetDistance() < terrainScanRange)
				mode = (GetDistance() < DOUBLE_RADAR_WIDE_SCAN_DISTANCE) ? ScanMode.DoubleWide : ScanMode.DoubleEarly;

		} else {

			mode = ScanMode.SingleStandby;
			terrainScanRange = MAX_TERRAIN_DIST;
			if (valid && GetDistance() < terrainScanRange) 
				mode = ScanMode.SingleNarrow;
		}

		double scan_angle_raw = Math.Atan(GROUND_SCAN_HORIZ_LENGTH/GetDistance())*Helpers.radToDeg;

		angle=Helpers.SatMinMax(scan_angle_raw,MIN_SCAN_ANGLE,MAX_SCAN_ANGLE-5);
		diag_angle=Helpers.SatMinMax(scan_angle_raw*1.414,MIN_SCAN_ANGLE,MAX_SCAN_ANGLE);
		double_angle=Helpers.SatMinMax(scan_angle_raw*2,MIN_SCAN_ANGLE,MAX_SCAN_ANGLE);

		// Perform the scan

		if (mode == ScanMode.SingleNarrow || mode == ScanMode.DoubleEarly || mode == ScanMode.DoubleWide) {

			if (terrainRadar.CanScan(2 * terrainScanRange) && scan_pacer >= MAX_SCAN_PACER) {
				ScanStep(ship_pitch, ship_roll);
				scan_pacer = 0;
			} else {
				scan_pacer++;
			}

		} else {
			d_fwd = d_rear = d_left = d_right = MAX_TERRAIN_DIST;
			d_fwd_wide = d_rear_wide = d_left_wide = d_right_wide = MAX_TERRAIN_DIST;
			d_fwd_left = d_fwd_right = d_rear_left = d_rear_right = MAX_TERRAIN_DIST;
		}
	}

	/// <summary>
	/// Perform one step of scanning the terrain in a double radar configuration
	/// </summary>
	private void ScanStep(double ship_pitch, double ship_roll) {
		switch (scan_step)
		{
			case 0:
			obstruction = false;
			obstruction = ScanPair(angle, 0, ship_pitch, ship_roll, terrainScanRange, out d_fwd, out d_rear);
			scan_step++;
			break;

			case 1:
			obstruction = ScanPair(0, -angle, ship_pitch, ship_roll, terrainScanRange, out d_left, out d_right);
			// If we only have one radar, the next scan will be back to step 1
			// otherwise continue with other steps. When in ScanMode.DoubleEarly mode,
			// the next steps do nothing, so the radar does the same steps as for a single
			// mode and then has a short pause.
			if (mode == ScanMode.SingleNarrow)
				scan_step=0;
			else
				scan_step++;
			break;

			case 2:
			if (mode == ScanMode.DoubleWide)
				obstruction = ScanPair(double_angle, 0, ship_pitch, ship_roll, terrainScanRange, out d_fwd_wide, out d_rear_wide);
			scan_step++;
			break;

			case 3:
			if (mode == ScanMode.DoubleWide)
				obstruction = ScanPair(0, -double_angle, ship_pitch, ship_roll, terrainScanRange, out d_left_wide, out d_right_wide);
			scan_step++;
			break;

			case 4:
			if (mode == ScanMode.DoubleWide)
				obstruction = ScanPair(diag_angle, -diag_angle, ship_pitch, ship_roll, terrainScanRange, out d_fwd_left, out d_rear_right);
			scan_step++;
			break;

			case 5:
			if (mode == ScanMode.DoubleWide)
				obstruction = ScanPair(diag_angle, diag_angle, ship_pitch, ship_roll, terrainScanRange, out d_fwd_right, out d_rear_left);
			scan_step = 0;
			break;
		}
	}

	/// <summary>
	/// Cast a ray to the specified pitch and yaw angles (relative to the vertical)
	/// and if the ray touches a planet or a large grid, return the distance.
	/// Otherwise return the max range.
	/// </summary>
	public double ScanDir(double scan_pitch, double scan_yaw, double ship_pitch, double ship_roll, double max_range) {

		// Define the scan mode
		if (!exists) {
			mode = ScanMode.NoRadar;
			return max_range+1;
		}

		if (terrainRadar.CanScan(max_range)) {
			
			MyDetectedEntityInfo temp_return = terrainRadar.Raycast(max_range, (float)(scan_pitch-ship_pitch), (float)(scan_yaw-ship_roll));

			if ((temp_return.Type ==  MyDetectedEntityType.Planet || temp_return.Type == MyDetectedEntityType.LargeGrid) && temp_return.HitPosition.HasValue) {
				return VRageMath.Vector3D.Distance(temp_return.HitPosition.Value , terrainRadar.GetPosition());
			} else if (temp_return.EntityId == terrainRadar.CubeGrid.EntityId) {
				return -1;
			} else {
				return max_range;
			}

		} else {
			return max_range+1;
		}
	}

	/// <summary>
	/// Cast a pair of rays to the specified pitch and yaw angles (relative to the vertical)
	/// symmetrical around the vertical, using information about the current ship pitch and roll angles.
	/// If both rays touch a planet or a large grid, return the distance for each ray, projected along the vertical axis,
	/// in the out values.
	/// Otherwise return the max range.
	/// </summary>
	private bool ScanPair(double scan_pitch, double scan_roll, double ship_pitch, double ship_roll, double max_range, out double dpos, out double dneg) {
		double cos_pitch = Math.Cos(Helpers.degToRad*scan_pitch);
		double cos_roll = Math.Cos(Helpers.degToRad*scan_roll);
		dpos = ScanDir(scan_pitch,scan_roll,ship_pitch,ship_roll,max_range)*cos_pitch*cos_roll;
		dneg = ScanDir(-scan_pitch,-scan_roll,ship_pitch,ship_roll,max_range)*cos_pitch*cos_roll;
		if (dpos < 0 || dneg < 0) {
			dpos = dneg = MAX_TERRAIN_DIST;
			return true;
		}
		return false;
	}

	public double RecommandFwdSpeed() {
		if (!exists) return 0;

		return RecommendSpeed(d_fwd, d_rear, d_fwd_wide, d_rear_wide,
								d_fwd_left, d_fwd_right, d_rear_left, d_rear_right);
	}

	public double RecommandLeftSpeed() {
		if (!exists) return 0;

		return RecommendSpeed(d_left, d_right, d_left_wide, d_right_wide,
								d_fwd_left, d_rear_left, d_fwd_right, d_rear_right);
	}

	private double RecommendSpeed(double d_pos, double d_neg, double d_wide_pos, double d_wide_neg, double d_diag1, double d_diag2, double d_diag3, double d_diag4) {

		double alt = GetDistance();
		double maxspeed = Math.Min(alt,HORIZ_MAX_SPEED);

		dz = Helpers.Interpolate(500,2000,HORIZ_DEADZONE,0,alt);

		double vbase = Math.Atan2(d_pos - d_neg , Math.Tan(Helpers.degToRad*angle)*(d_pos + d_neg));
		double vpos, vpos_raw;

		if (double_radar && mode == ScanMode.DoubleWide) {

			double vwide = Math.Atan2(d_wide_pos - d_wide_neg,  Math.Tan(Helpers.degToRad*double_angle)*(d_wide_pos + d_wide_neg));
			
			double vdiag = Math.Atan2(d_diag1 + d_diag2 - d_diag3 - d_diag4 , Math.Tan(Helpers.degToRad*diag_angle)*(d_diag1 + d_diag2 + d_diag3 + d_diag4));
			vpos_raw = vbase + vwide + vdiag;

			if (d_wide_pos < d_pos && vpos_raw>0 && d_pos > 0) 
				vpos_raw *= Math.Pow(d_wide_pos/d_pos,2);

			if (d_wide_neg < d_neg && vpos_raw<0 && d_neg > 0) 
				vpos_raw *= Math.Pow(d_wide_neg/d_neg,2);

			if (d_pos < alt && vpos_raw>0 && alt > 0)
				vpos_raw *= d_pos/alt;

			if (d_neg < alt && vpos_raw<0 && alt > 0)
				vpos_raw *= d_neg/alt;

			vpos = vpos_raw * SPEED_SCALE;


		} else {
			vpos = vbase * 3 * SPEED_SCALE;
		}
		return Helpers.MaxAbs(Helpers.DeadZone(vpos, dz), maxspeed);
	}

	

	public string AltitudeDebugString() {
		if (!exists) return "[NO RADAR !]";

		return "[RADAR]"
		+ "\nRange:" + altScanRange.ToString("000.0") + "m"
		+ " Avail:" + altitudeRadar.AvailableScanRange.ToString("000.0") + "m"
		+ "\nReturn type: " + radar_return.Type
		+ " Age: " + alt_age;

	}

	public string TerrainDebugString() {
		if (!exists) return "[NO RADAR !]";

		return "[TERRAIN]"
			+ "\nAv range: " + terrainRadar.AvailableScanRange.ToString("00000") + "m"
			+ " Scan: " + angle.ToString("00.0") + "°/" + double_angle.ToString("00.0") + " Dist: " + terrainScanRange.ToString("000.0") + "m"
			+ "\nFw: " + d_fwd.ToString("000.0") + "/" + d_fwd_wide.ToString("000.0")
			+ " Rr: " + d_rear.ToString("000.0") + "/" + d_rear_wide.ToString("000.0")
			+ " Fw spd: " + RecommandFwdSpeed().ToString("00.0")
			+ "\nLf: " + d_left.ToString("000.0") + "/" + d_left_wide.ToString("000.0")
			+ " Rt: " + d_right.ToString("000.0") + "/" + d_right_wide.ToString("000.0")
			+ " Lf spd: " + RecommandLeftSpeed().ToString("00.0");
	}

	public List<string> LogNames() {
		return new List<string>{"d_fwd", "d_rear", "d_left", "d_right"};
	}

	public List<double> LogValues() {
		return new List<double>{d_fwd, d_rear, d_left, d_right};
	}
}

/// <summary>
/// See tech doc §10
/// </summary>
public class HorizontalThrusters {
	ShipBlocks ship;
	PIDController fwdPID,leftPID;
	IMyShipController cockpit;
	readonly int DELAY;
	int timer;

	public HorizontalThrusters(ShipBlocks ship, int delay, double KP, double KI, double KD, double AImax) {
		this.ship = ship;
		this.cockpit = ship.ship_ctrller;
		this.DELAY = delay;
		fwdPID = new PIDController(KP,KI,KD,-AImax,AImax,0.5,1);
		leftPID = new PIDController(KP,KI,KD,-AImax,AImax,0.5,1);
	}

	public void Disable() {
		ship.fwdThr.Disable();
		ship.rearThr.Disable();
		ship.leftThr.Disable();
		ship.rightThr.Disable();
		fwdPID.Reset();
		leftPID.Reset();
	}

	public void Tick(double fwdSpeed, double leftSpeed, double fwdSpeedSetpoint, double leftSpeedSetpoint, double shipMass, bool deadZone, bool overridable) {

		// If the player provides inputs, disable the override
		if ((overridable && (cockpit.MoveIndicator.Length() > 0.0f)) || cockpit.RotationIndicator.Length() > 0.0f)  {

			Disable();
			timer = 0;

		} else if (timer > DELAY) {

			// Compute difference between speed and setpoint
			double fwdDelta  = fwdSpeedSetpoint  - fwdSpeed;
			double leftDelta = leftSpeedSetpoint - leftSpeed;
			double dz = deadZone ? 0.05 :0;

			// Compute the thrust to apply to the ship to correct the speed difference, using the speed ratio
			fwdPID.UpdatePID(fwdDelta);
			leftPID.UpdatePID(leftDelta);

			// Apply the thrust to the thrusters, considering the sign of the difference
			ship.fwdThr.ApplyThrust(Helpers.DeadZone(fwdPID.output,dz)*shipMass,0,0);
			ship.rearThr.ApplyThrust(Helpers.DeadZone(-fwdPID.output,dz)*shipMass,0,0);
			ship.leftThr.ApplyThrust(Helpers.DeadZone(leftPID.output,dz)*shipMass,0,0);
			ship.rightThr.ApplyThrust(Helpers.DeadZone(-leftPID.output,dz)*shipMass,0,0);

		} else {
			timer++;
		}

	}

	public void UpdateThrust() {
		ship.fwdThr.UpdateThrust();
		ship.rearThr.UpdateThrust();
		ship.leftThr.UpdateThrust();
		ship.rightThr.UpdateThrust();
	}

	public string DebugString() {
		string str = "[FWD PID]: " + fwdPID.DebugString();
		str += "\n[LEFT PID]: " + leftPID.DebugString();
		return str;
	}

	public List<string> LogNames() {
		return new List<string>{"fwd_pid_output","left_pid_output"};
	}

	public List<double> LogValues() {
		return new List<double>{fwdPID.output,leftPID.output};
	}
}

/// <summary>
/// A group of thrusters, that can mix atmospheric, ion, and hydrogen, all thrusting in the same direction. See tech doc §11
/// </summary>
public class ThrGroup {
	// Thrust values in Newtons
	public double max_hthrust, max_ithrust, max_athrust, max_pthrust, max_total_thrust;
	public double eff_hthrust, eff_ithrust, eff_athrust, eff_pthrust, eff_total_thrust;
	public double current_hthrust, current_ithrust, current_athrust, current_pthrust, current_total_thrust;
	public float atmo_override, ion_override, h2_override, p_override;
	public double [] ithrust_density = new double[11];
	public double [] pthrust_density = new double[11];
	public double [] athrust_density = new double[11];

	List<IMyThrust> athrusters, ithrusters, hthrusters, pthrusters;

	double athr_wanted, ithr_wanted, hthr_wanted, pthr_wanted;


	public ThrGroup(List<IMyThrust> thrusters) {

		athrusters = new List<IMyThrust>();
		ithrusters = new List<IMyThrust>();
		hthrusters = new List<IMyThrust>();
		pthrusters = new List<IMyThrust>();

		// Separate thruster by type
		// Add custom/modded thrusters here if needed
		foreach (var thr in thrusters) {
			string name = thr.BlockDefinition.SubtypeName.ToLower();
			string displayname = thr.DefinitionDisplayNameText.ToString().ToLower();
			if (name.Contains("hydrogen") || name.Contains("epstein") || name.Contains("rcs")) hthrusters.Add(thr);
			else if (name.Contains("ion") || displayname.Contains("ion")) ithrusters.Add(thr);
			else if (name.Contains("atmo")) athrusters.Add(thr);
			else if (name.Contains("prototech")) pthrusters.Add(thr);
		}
	}

	public void UpdateThrust() {

		eff_athrust = eff_ithrust = eff_hthrust = eff_pthrust = 0;
		max_athrust = max_ithrust = max_hthrust = max_pthrust = 0;
		current_athrust = current_ithrust = current_hthrust = current_pthrust = 0;

		foreach (IMyThrust athr in athrusters) {
			if (!athr.Closed && athr.IsWorking) {
				eff_athrust 	+= athr.MaxEffectiveThrust;
				max_athrust 	+= athr.MaxThrust;
				current_athrust += athr.CurrentThrust;
			}
		}
		
		foreach (IMyThrust ithr in ithrusters) {
			if (!ithr.Closed && ithr.IsWorking) {
				eff_ithrust 		+= ithr.MaxEffectiveThrust;
				max_ithrust 		+= ithr.MaxThrust;
				current_ithrust 	+= ithr.CurrentThrust;
			}
		} 

		foreach (IMyThrust hthr in hthrusters) {
			if (!hthr.Closed && hthr.IsWorking ) {
				eff_hthrust 		+= hthr.MaxEffectiveThrust;
				max_hthrust 		+= hthr.MaxThrust;
				current_hthrust 	+= hthr.CurrentThrust;
			}
		} 

		foreach (IMyThrust pthr in pthrusters) {
			if (!pthr.Closed && pthr.IsWorking ) {
				eff_pthrust 		+= pthr.MaxEffectiveThrust;
				max_pthrust 		+= pthr.MaxThrust;
				current_pthrust 	+= pthr.CurrentThrust;
			}
		}

		eff_total_thrust = eff_athrust + eff_ithrust + eff_hthrust + eff_pthrust;
		max_total_thrust = max_athrust + max_ithrust + max_hthrust + max_pthrust;
		current_total_thrust = current_athrust + current_ithrust + current_hthrust + current_pthrust;
	}

	public void ApplyThrust (double thr_wanted, double min_athrust, double min_ithrust) {
		
		athr_wanted = Helpers.SatMinMax(thr_wanted,min_athrust,eff_athrust);
		pthr_wanted = Helpers.SatMinMax(thr_wanted-athr_wanted,min_ithrust,eff_pthrust);
		ithr_wanted = Helpers.SatMinMax(thr_wanted-athr_wanted-pthr_wanted,min_ithrust-pthr_wanted, eff_ithrust);
		hthr_wanted = thr_wanted-pthr_wanted-ithr_wanted-athr_wanted;
		
		// Compute the overrides with a small dead zone

		// By default, atmos are overriden to the max so that they update their max effective thrust (game bug?)
		
		if (eff_athrust > 0) {
			atmo_override = (float)Helpers.SatMinMax(athr_wanted/eff_athrust,0,1);
			if (atmo_override < 0.01) atmo_override = 0.000001f;
		} else {
			atmo_override = 1;
		}
		
		if (eff_ithrust > 0) {
			ion_override = (float)Helpers.SatMinMax(ithr_wanted/eff_ithrust,0,1);
			if (ion_override < 0.01) ion_override = 0.000001f;
		} else {
			ion_override = 0;
		}

		if (eff_hthrust > 0) {
			h2_override = (float)Helpers.SatMinMax(hthr_wanted/eff_hthrust,0,1);
			if (h2_override < 0.01) h2_override = 0.000001f;
		} else {
			h2_override = 0;
		}

		if (eff_pthrust > 0) {
			p_override = (float)Helpers.SatMinMax(pthr_wanted/eff_pthrust,0,1);
			if (p_override < 0.01) p_override = 0.000001f;
		} else {
			p_override = 0;
		}
		
		// Apply the thrust override to thrusters
		
		foreach (IMyThrust alifter in athrusters) {
			alifter.Enabled = true;
			alifter.ThrustOverridePercentage = atmo_override;
		}

		foreach (IMyThrust ilifter in ithrusters) {
			ilifter.Enabled = true;
			ilifter.ThrustOverridePercentage = ion_override;
		}

		foreach (IMyThrust hlifter in hthrusters) {
			hlifter.Enabled = true;
			hlifter.ThrustOverridePercentage = h2_override;  
		}

		foreach (IMyThrust plifter in pthrusters) {
			plifter.Enabled = true;
			plifter.ThrustOverridePercentage = p_override;  
		}

	}

	public void Disable() {
		foreach (IMyThrust alifter in athrusters) {
			alifter.ThrustOverride = 0;
			alifter.Enabled = true;
		}

		foreach (IMyThrust ilifter in ithrusters) {
			ilifter.ThrustOverride = 0;
			ilifter.Enabled = true;
		}

		foreach (IMyThrust hlifter in hthrusters) {
			hlifter.ThrustOverride = 0;
			hlifter.Enabled = true;
		}

		foreach (IMyThrust plifter in pthrusters) {
			plifter.ThrustOverride = 0;
			plifter.Enabled = true;
		}
	}

	// Find the worst atmo density (minimizes atmo + ion thrust)
	public double WorstDensity()
	{
		if (max_athrust + max_ithrust*0.2 + max_pthrust*0.3 < max_ithrust+max_pthrust)
			return 1;
		else
			return 0.3;
	}

	public double AtmoThrustForAtmoDensity(double density) {
		return Math.Max(max_athrust * (Math.Min(density, 1)*1.43f - 0.43f),0);
	}

	public double IonThrustForAtmoDensity(double density) {
		return max_ithrust * (1-0.8f*Math.Min(density, 1));
	}

	public double PrototechThrustForAtmoDensity(double density) {
		return max_pthrust * (1-0.7f*Math.Min(density, 1));
	}

	public void UpdateDensitySweep() {
		for (int i=0; i<11; i++) {
			ithrust_density[i] = IonThrustForAtmoDensity(i/10.0);
			athrust_density[i] = AtmoThrustForAtmoDensity(i/10.0);
			pthrust_density[i] = PrototechThrustForAtmoDensity(i/10.0);
		}
	}

	public string Inventory() {
		return "(" + ithrusters.Count + " Ion, " + athrusters.Count + " A, " + hthrusters.Count + " H, " + pthrusters.Count + " P)";
	}

	public string DebugString() {
		return "[OVERRIDE] A:" +atmo_override.ToString("+0.00;-0.00") + " I:" +ion_override.ToString("+0.00;-0.00") + " H:" +h2_override.ToString("+0.00;-0.00") + "P: "+p_override.ToString("+0.00;-0.00") + " WD"+WorstDensity().ToString("0.00");
	}

}

/// <summary>
/// Class used to time the execution of the script and provide statistics for each of
/// the main tasks (the tick1, tick10 and tick100 ones)
/// See tech doc §12.3
/// </summary>
public class RunTimeCounter {

	readonly Program program;

	const int NB_TIMES = 10;
	RollingBuffer t1_buffer = new RollingBuffer(NB_TIMES);
	RollingBuffer t10_buffer = new RollingBuffer(NB_TIMES);
	RollingBuffer t100_buffer = new RollingBuffer(NB_TIMES);

	public RunTimeCounter(Program program) {
		this.program = program;
	}

	public void Count(bool ranTick1,bool ranTick10,bool ranTick100) {
		double runtime = program.Runtime.LastRunTimeMs;
		if (ranTick1 && !ranTick10 && !ranTick100) t1_buffer.Add(runtime);
		if (ranTick10 && !ranTick100) t10_buffer.Add(runtime);
		if (ranTick100) t100_buffer.Add(runtime);
	}

	public string RunTimeString()
	{
		string s="";
		s += "Avg t1:" + t1_buffer.Average().ToString("0.00")+"ms";
		s += ", t10: " + t10_buffer.Average().ToString("0.00")+"ms";
		s += ", t100:"+t100_buffer.Average().ToString("0.00")+"ms";
		s += "\nMax t1:" + t1_buffer.Max().ToString("0.00") + "ms";
		s += ", t10: " + t10_buffer.Max().ToString("0.00") + "ms";
		s += ", t100:"+t100_buffer.Max().ToString("0.00")+"ms";

		return s;
	}

	public List<string> LogNames() {
		return new List<string>{"Avg t1","Avg t10","Avg t100","Max t1","Max t10","Max t100"};
	}

	public List<double> LogValues() {
		return new List<double>{t1_buffer.Average(),t10_buffer.Average(),t100_buffer.Average(),t1_buffer.Max(),t10_buffer.Max(),t100_buffer.Max()};
	}

}


/// <summary>
/// Misc helper functions. See tech doc §12.4
/// </summary>
public class Helpers
{

	public static double NotNan(double val)
	{
		if (double.IsNaN(val))
			return 0;
		return val;
	}

	// The max value has priority, ie if min>max, the function returns max

	public static double SatMinMax(double value, double min, double max) {
		if (value > max) return max;
		if (value < min) return min;
		return value;
	}

	/// <summary>
	/// Returns the value clamped to the range [-maxabs,maxabs], keeping the sign of the original value.
	/// </summary>
	public static double MaxAbs(double value, double maxabs) {
		return Math.Min(Math.Abs(value),maxabs) * Math.Sign(value);
	} 

	/// <summary>
	/// Returns the minimum of three values.
	/// </summary>
	public static double Min3(double a, double b, double c) {
		return Math.Min(a,Math.Min(b,c));
	}

	/// <summary>
	/// Returns the maximum of three values.
	/// </summary>
	public static double Max3(double a, double b, double c) {
		return Math.Max(a,Math.Max(b,c));
	}

	/// <summary>
	/// Returns 0 if the value is within [-deadzone ; deadzone], otherwise returns the value.
	/// </summary>
	public static double DeadZone(double value, double deadzone) {
		if (Math.Abs(value) < deadzone) {
			return 0;
		} else {
			return value;
		}
	}

	/// <summary>
	/// Interpolate between two points (X1,Y1) and (X2,Y2) to find the value Y at X.
	/// If X is outside the range [X1,X2], it returns Y1 or Y2 depending on the side.
	/// If X1 == X2, it returns Y1
	/// </summary>
	public static double Interpolate(double X1, double X2, double Y1, double Y2, double x) {
		if (X1==X2) return Y1;
		if (x <= X1) return Y1;
		if (x >= X2) return Y2;

		return Y1 + (Y2-Y1) * (x-X1) / (X2-X1);
	}

	/// <summary>
	/// Mixes two values a and b with a ratio. The ratio is clamped between 0 and 1.
	/// </summary>
	public static double Mix(double a, double b, double ratio_of_a) {
		double ratio = SatMinMax(ratio_of_a,0,1);
		return a*ratio + b*(1-ratio);
	}

	public static double g_to_ms2(double g) {
		return g*9.81;
	}

	public static double ms2_to_g(double a) {
		return a/9.81;
	}

	public static void Rectangle(MySpriteDrawFrame frame, float x1, float x2, float y1, float y2, VRageMath.RectangleF view, float thickness, VRageMath.Color color) {

		float[] x = new float[4] {(x1+x2)/2, (x1+x2)/2, x1, x2};
		float[] y = new float[4] {y1, y2, (y1+y2)/2, (y1+y2)/2};
		float[] w = new float[4] {x2-x1, x2-x1, thickness, thickness};
		float[] h = new float[4] {thickness, thickness, y2-y1, y2-y1};

		for (int i=0; i<4; i++) {

			MySprite s = MySprite.CreateSprite
			(
				"SquareSimple",
				new Vector2(x[i],y[i]) + view.Position,
				new Vector2(w[i],h[i])
			);
			s.Color = color;
			frame.Add(s);

		}
	}

	/// <summary>
	/// Formats a double value to a compact string representation.
	/// Values below 1000 use three characters, with one decimal place for values between 10 and 100, and two decimal places for values below 10.
	/// </summary>
	public static string FormatCompact(double value) {
		if (Math.Abs(value) > 100)
			return value.ToString("000");
		else if (Math.Abs(value) > 10)
			return value.ToString("00.0");
		else
			return value.ToString("0.00");
	}

	public static string Truncate(string str, int maxLength) {
		if (str.Length > maxLength)
			return str.Substring(0, maxLength);

		return str;
	}

	public static int FindN(string inString, string prefix)
	{
	// Parcourir les chiffres 0 à 9
	for (int N = 0; N <= 9; N++){
		string prefixN = prefix + N.ToString();
		if (inString.Contains(prefixN))
			return N;
	}

	return -1;
	}

	public static int FindN(string inString, List<string> prefixes)
	{
		// Parcourir les chiffres 0 à 9
		foreach (string prefix in prefixes) {
			for (int N = 0; N <= 9; N++) {
				string prefixN = prefix + N.ToString();
				if (inString.Contains(prefixN)) 
					return N;
			}
		}
        return -1;
    }
	
	public const double halfPi = Math.PI / 2;
	public const double radToDeg = 180 / Math.PI;
	public const double degToRad = Math.PI / 180;
}

/// <summary>
/// Records internal variables in CSV format. See tech doc §12.2
/// </summary>
public class Logger 
{

	List<string> names = new List<string>();
	List<List<double>> records = new List<List<double>>();
	int cnter;
	int FACTOR;
	double tstart=0;
	bool allow;
	

	public Logger(List<string> names, int factor, bool allow) {
		this.names = names;
		this.FACTOR = factor;
		this.allow = allow;
	}

	public void Clear() {
		records.Clear();
		cnter = 0;
	}

	public void Log(List<double> record) {
		if (!allow) return;

		if (cnter == 0)
			tstart = DateTime.Now.TimeOfDay.TotalMilliseconds;

		if (cnter % FACTOR == 0) {
			List<double> new_record = new List<double>();
			new_record.Add(DateTime.Now.TimeOfDay.TotalMilliseconds-tstart);
			new_record.AddRange(record);
			records.Add(new_record);
		}
		cnter++;
		
	}

	public string Output() {

		// Format the output as a CSV
		// First line : names of the columns
		string output = "time(ms),";
		foreach (string name in names) 
			output += name + ",";

		output += "\n";
		
		// Each line is a record
		foreach (List<double> record in records) {
			foreach (double value in record) 
				output += value.ToString("0.00") + ",";

			output += "\n";
		}

		return output;
	}
}

public class MovingAverage {
	
	double[] values;
	int index, size;
	double sum;
	
	public MovingAverage(int set_size) {
		values = new double[set_size];
		index = 0;
		size = set_size;
		sum = 0;
		for (int i=0; i<size; i++) {
			values[i] = 0;
		}
	}
	
	public double AddValue(double value) {
		sum -= values[index];
		values[index] = value;
		sum += value;
		index = (index+1) % size;
		return sum / size;
	}

	public double Get() {
		return sum / size;
	}
	
	public void Clear() {
		Set(0);
	}

	public void Set(double value) {
		sum = value*size;
		for (int i=0; i<size; i++) {
			values[i] = value;
		}
	}
	
}
		

public class RollingBuffer
{
	private double[] buffer;
	private int index;

	public RollingBuffer(int size)
	{
		buffer = new double[size];
		index = 0;
	}

	public void Add(double item)
	{
		buffer[index] = item;
		index = (index + 1) % buffer.Length;
	}

	public double[] GetBuffer()
	{
		return buffer;
	}

	public double Average()
	{
		return buffer.Average();
	}

	public double Max()
	{
		return buffer.Max();
	}

}

public class RateLimiter
{
	private double maxRatePositive;
	private double maxRateNegative;
	private double lastValue;

	public RateLimiter(double maxRatePositive, double maxRateNegative)
	{
		this.maxRatePositive = maxRatePositive;
		this.maxRateNegative = maxRateNegative;
		lastValue = 0;
	}

	public double Limit(double value)
	{
		double delta = value - lastValue;

		if (delta > maxRatePositive)
			value = lastValue + maxRatePositive;
		else if (delta < maxRateNegative)
			value = lastValue + maxRateNegative;

		lastValue = value;
		return value;
	}

	public void Init(double initialValue)
	{
		lastValue = initialValue;
	}
}

public class AutoPilot
{

	// Speed set-points in m/s
	public double fwdSpeedSP, leftSpeedSP, vertSpeedSP;
	// Desired speed for mode 4 (autopilot) in m/s
	public double mode4DesiredSpeed, mode4DesiredAltitude;
	// Maximum safe speed in m/s considering ground altitude (low value near the ground, higher value the higher the ship flies)
	public double safeSpeed;
	// Estimated ground altitude in meters forward of the ship based on a camera raycast with a 40° angle
	// (ex : if the ship is currently at 30m from the ground, and the ground is flat, forward should also be 30m
	// if the ground slopes up, forward is <30m, if the ground slopes down, forward is >30m)
	public double forward=-1;
	// Reference for the altitude set-point
	public enum AltitudeMode {
		Ground,
		SeaLevel
	}
	public AltitudeMode altitudeMode;
	public MovingAverage altitudeFilter;
	// Configuration parameters from SLMConfiguration
	readonly double speedIncrement, maxSpeed, ssamin, ssamax, ssmin, ssmax;
	// PID acting on altitude and outputting a vertical speed setpoint
	PIDController alt_PID;
	MovingAverage fwdSpeedFilter, leftSpeedFilter, safeSpeedFilter, fwdAltFilter;

	public AutoPilot(SLMConfiguration config) {
		alt_PID = new PIDController(config.altKp, config.altKi, config.altKd, config.alt_aiMin, config.alt_aiMax, config.altAdFilt, config.altAdMax);
		fwdSpeedFilter = new MovingAverage(config.speedFilterLength);
		leftSpeedFilter = new MovingAverage(config.speedFilterLength);
		altitudeFilter = new MovingAverage(config.altFilterLength);
		safeSpeedFilter = new MovingAverage(config.safeSpeedFilterLength);
		fwdAltFilter = new MovingAverage(10);
		speedIncrement = config.speedIncrement;
		maxSpeed = config.maxSpeed;
		ssamin = config.safeSpeedAltMin;
		ssamax = config.safeSpeedAltMax;
		ssmin = config.safeSpeedMin;
		ssmax = config.safeSpeedMax;
	}

	public void Init() {
		fwdSpeedSP = 0;
		leftSpeedSP = 0;
		vertSpeedSP = 0;
		alt_PID.Reset();
		fwdSpeedFilter.Clear();
		leftSpeedFilter.Clear();
		altitudeFilter.Clear();
		safeSpeedFilter.Clear();
		altitudeMode=AltitudeMode.Ground;
	}

	// Instant direct control with the controller or keyboard : when the key is pressed
	// then the speed setpoint is a fixed value depending on altitude
	// Can move forward, backward, left, right.
	// This is used in mode 3
	public void UpdateSpeedDirect(Vector3 moveIndicator) {
		fwdSpeedSP = 0;
		leftSpeedSP = 0;

		double safe = safeSpeedFilter.Get();
		
		if (moveIndicator.Z > 0.0f) 
			fwdSpeedFilter.AddValue(-safe);
		else if (moveIndicator.Z < 0.0f)
			fwdSpeedFilter.AddValue(safe);
		else
			fwdSpeedFilter.AddValue(0);

		fwdSpeedSP = fwdSpeedFilter.Get();
			
		if (moveIndicator.X > 0.0f)
			leftSpeedFilter.AddValue(-safe);
		else if (moveIndicator.X < 0.0f)
			leftSpeedFilter.AddValue(safe);
		else
			leftSpeedFilter.AddValue(0);

		leftSpeedSP = leftSpeedFilter.Get();
	}

	// Progressive control with the controller or keyboard : when the forward/back key is pressed
	// then the speed setpoint is increased or reduced progressively
	// Can only move forward.
	// This is used in mode 4
	public void UpdateSpeedProgressive(Vector3 moveIndicator) {

		leftSpeedSP = 0;

		if (moveIndicator.Z > 0.0f && mode4DesiredSpeed>=speedIncrement) 
			mode4DesiredSpeed-=speedIncrement;
		else if (moveIndicator.Z < 0.0f && mode4DesiredSpeed<maxSpeed)
			mode4DesiredSpeed+=speedIncrement;

		double safe = safeSpeedFilter.Get();

		fwdSpeedSP = Math.Min(mode4DesiredSpeed,safe);
	}

	// Create a vertical speed set-point to maintain set altitude
	public void UpdateVertSpeedSP(double gndAltitude, double slAltitude, double shipPitch, double shipRoll) {

		altitudeFilter.AddValue(mode4DesiredAltitude);
		
		double altDelta=0;
		double relevantGndAltitude;

		fwdAltFilter.AddValue(forward);
		// Anticipation of the ground sloping up forward of the ship
		// If the "forward" distance is equal to the safespeed max altitude, that
		// means the camera did not return a hit, and we ignore it.
		relevantGndAltitude = forward < ssamax ? Math.Min(gndAltitude, fwdAltFilter.Get()+5) : gndAltitude;

		switch (altitudeMode) {

			// In ground reference mode, we simply maintain the desired ground altitude
			case AltitudeMode.Ground:
				altDelta = altitudeFilter.Get() - relevantGndAltitude;
				break;

			// In sea level reference mode, the desired speed is given more importance
			// thus we maintain a sufficient ground altitude to stay at the desired speed
			// and climb at higher altitudes (relative to sea level) if needed.
			case AltitudeMode.SeaLevel:

				// Compute the altitude where the safe speed is the desired speed
				double minGNDaltitude = Helpers.Interpolate(ssmin,ssmax,ssamin,ssamax,mode4DesiredSpeed);

				double altDeltaSL = altitudeFilter.Get() - slAltitude;
				double altDeltaGND = minGNDaltitude - gndAltitude;

				altDelta = Math.Max(altDeltaSL, altDeltaGND);
				break;
		}
		
		// Above a certain altitude, the delta value fed to the PID is reduced
		// for a smoother fly
		altDelta = altDelta / Math.Max(1,relevantGndAltitude/50);

		alt_PID.UpdatePIDController(altDelta, -5, 5);

		// A correction factor is applied in feedforward to climb more quickly
		// if the altitude is really too low
		vertSpeedSP = alt_PID.output + Math.Max(altDelta - 10, 0) * 0.5;

		
	}

	public void UpdateSafeSpeed(double gndAltitude) {
		safeSpeedFilter.AddValue(Helpers.Interpolate(ssamin,ssamax,ssmin,ssmax,Math.Min(gndAltitude,forward)));
	}

	public string DebugString() {
		return "[AUTOPILOT]\nforward:"+Helpers.FormatCompact(forward)+"m PIDout:"+Helpers.FormatCompact(alt_PID.output)+"m/s\n" + alt_PID.DebugString();
	}

}


// End of partial class
    }
}
