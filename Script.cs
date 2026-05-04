using EmptyKeys.UserInterface.Generated.DataTemplatesContracts_Bindings;
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
//using System.Numerics;

//using System.Numerics;

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
using VRageRender.Messages;



namespace IngameScript
{
	partial class Program : MyGridProgram
	{

		

#region mdk preserve
/*
------
SOFT LANDING MANAGER by silverbluemx
------

Automatically land safely on planets optimizing your fuel.
Scans the terrain and guides the ship to a safe landing spot.
Best for use with 1/r² gravity mods such as Real Orbits and
1000m/s speed mods (but not required).

Version 2.3 - 2026-05-03

New features:
- Side cameras.
- Land to GPS target
- Easier configuration for Vanilla SE

Fixes:
- Removed gravity auto-detection (not reliable enough), added manual configuration
- Improved profile display
- Don't turn on thrusters in init mode 0.
- Can estimate atmo density with Prototech Thrusters
- Fixed error in ComputeLWRTarget that underestimated ship TWR ability in mode1

Planet catalog:
- added 18 planets by Herr Doktor, HIC, Doctor Octoganapus, DestroyerSTAR

See Steam Workshop and README.md on GitHub for more info :
https://github.com/silverbluemx/SE_SoftLandingManager
Script has been minified, see original code on GitHub with comments and doc.

*/


// Configure here the tags used for ship blocks.

public class SEGameConfig
{
	// Game speed limit in m/s. If using Vanilla SE, set to 100.
	public double MaxSpeed = 1000;
	// Gravity exponent. If using Vanilla SE, set to 2. If using Real Orbits, set to 7.
	public double GravExp = 2;
}

public class SLMShipConfiguration
{

	public List<string> CTRLLER_NAME, RADAR_NAME, TERRAIN_RADAR_NAME, SIDE_CAM_NAME, IGNORE_NAME, LCD_NAME, DEBUGLCD_NAME, LANDING_TIMER_NAME, LIFTOFF_TIMER_NAME, ON_TIMER_NAME, OFF_TIMER_NAME, SOUND_NAME, CONNECTOR_NAME;

	public SLMShipConfiguration()
	{

		IGNORE_NAME = new List<string> { "SLMignore" };

		// RECOMMENDED

		CTRLLER_NAME = new List<string> { "SLMref", "reference", "Reference" };

		RADAR_NAME = new List<string> { "SLMradar" };

		TERRAIN_RADAR_NAME = new List<string> { "SLMterrainradar" };

		SIDE_CAM_NAME = new List<string> { "SLMsidecam" };

		// OPTIONAL

		LCD_NAME = new List<string> { "SLMdisplay" };

		DEBUGLCD_NAME = new List<string> { "SLMdebug" };

		LANDING_TIMER_NAME = new List<string> { "SLMlanding" };

		LIFTOFF_TIMER_NAME = new List<string> { "SLMliftoff" };

		ON_TIMER_NAME = new List<string> { "SLMon" };

		OFF_TIMER_NAME = new List<string> { "SLMoff" };

		SOUND_NAME = new List<string> { "SLMsound" };

		CONNECTOR_NAME = new List<string> { "SLMconnector" };
	}

}

#endregion

// Below this point the code is minified using MDK to stay below the 100000 characters limit.
// See https://github.com/silverbluemx/SE_SoftLandingManager for the full source code

// ---------------------------------------------------------------------------------
// ---- There should be no reason to change the parameters below for normal use ----
// ---------------------------------------------------------------------------------

/// <summary>
/// Configure here many settings for how the script behaves.
/// </summary>
public class SLMConfig
{
	// Variable description is provided when initialized in the constructor, to save on code size (100 000 characters limit for SE scripts is really tight...)
	public readonly double altitudeOffset,defaultASLmeters,gravTransHigh,gravTransLow,aiMax,aiMin,vertKp,vertKi,vertKd,vertAdFilt,vertAdMax,LWRoffset,LWRsafetyfactor,LWRlimit,accelLimit,vSpeedMax,vSpeedDefault,LWR_mix_gnd_ratio,speedTgtGradient,elecLwrSufficient,mode1IonSpeed,mode1AtmoSpeed,finalSpeed,finalSpeedAltitude,panicDelta,panicRatio,marginalMax,marginalWarn,h2MarginWarning,landingTimerAltitude,liftoffTimerAltitude,radarMaxRange,maxAngle,gyroResponse,gyroRpmScale,horizKp,horizKi,horizKd,horizAiMax,speedScale,inertiaRatioSmall,inertiaRatioLarge,m4InitAlt,m4InitSpeed,alt_aiMax,alt_aiMin,altKp,altKi,altKd,altAdFilt,altAdMax,minVertSpeed,speedIncrement,maxSpeed,safeSpeedAltMin,safeSpeedAltMax,safeSpeedMin,safeSpeedMax,mode5ThrustRatio,mode5MaxSpeed;
	public readonly bool autoSwitchExponent, autoLevel, useGyro, useThrusters, startHover, ALLOW_LOGGING;
	public readonly int smartDelayTime,speedFilterLength, altFilterLength, safeSpeedFilterLength,LOG_FACTOR,DisableDelay;

	public SLMConfig()
	{
		// Offset to the altitude value (ex : if the ship reads altitude 5m when landed, set it to 5)
		altitudeOffset = 0;

		// If set to true, the script tries to find the correct gravity exponent when descending.
		// Set it to false to force it to always use the one configured above.
		autoSwitchExponent = true;

		// Set any of these to false to disable the features by default
		// (they can always be switched on/off using commands when the script is running)
		autoLevel = true;

		useGyro = true;
		useThrusters = true;

		// If set to true, the script will start in hover mode (mode3) the first time
		// it's compiled. Note that after that, the script will start in the same mode
		// as when the game was last saved.
		startHover = true;

		// ALTITUDE CORRECTION

		// Default value for the height of the ground above sea level. This guess must be used
		// when measuring distance with the radar and the game API doesn't provide altitude yet.
		defaultASLmeters = 500;

		// SURFACE GRAVITY ESTIMATOR

		// Above the transition altitude, surface gravity comes from the gravity estimator
		// Below the transition altitude, local gravity value is used directly
		// In between, we use a linear interpolation between the two
		gravTransHigh = 4000;
		gravTransLow = 1000;

		// VERTICAL PID CONTROLLER
		// Coefficients of the PID controller used to control vertical speed
		// Input in m/s (vertical speed delta), output in thrust-to-weight ratio (thrust set point)
		aiMax = 4;
		aiMin = -0.1;
		vertKp = 0.4;
		vertKi = 0.05;
		vertKd = 10;
		// Low-pass filter of the D component (0:no filtering, 1:values don't move!)
		vertAdFilt = 0.8;
		vertAdMax = 0.5;

		// LANDING PROFILE THRUST SETTINGS

		// Margins applied to the ship LWR when computing the vertical speed target.
		// Used = (real LWR - offset) / LWRsafetyfactor
		LWRoffset = 0.0;
		LWRsafetyfactor = 1.1;
		/// Maximum Lift-to-weight ratios allowed for all landing modes. If the planet
		/// has low gravity, this slows down the landing.
		LWRlimit = 5;
		/// Maximum acceleration allowed for all landing modes (in m/s² including gravity)
		accelLimit = 30;

		// SPEED TARGET COMPUTATION AT HIGH ALTITUDE
		vSpeedMax = 500;
		vSpeedDefault = 200;
		LWR_mix_gnd_ratio = 0.7;
		/// <summary>
		/// How fast the speed set-point is allowed to decrease. The unit is in m/s per tick,
		/// so 1 is 60 m/s² and 0.2 is 12 m/s². The value must be negative
		/// </summary>
		speedTgtGradient = -0.2;

		// MODE 1 SETTINGS

		/// <summary>If electrical thrusters can provide at least this LWR then hydrogen thrusters will not be used.</summary>
		elecLwrSufficient = 1.15;

		/// <summary>In mode1, at high altitude ion thrusters are used to offset the ship
		/// weight if speed is at or above that value (m/s)</summary>
		mode1IonSpeed = 115;
		/// <summary>In mode1, at low altitude atmospheric thrusters are used to offset the ship
		/// weight if speed is at or above that value (m/s)</summary>
		mode1AtmoSpeed = 20;

		// SPEED TARGET FOR FINAL LANDING

		/// <summary>Constant speed (m/s) used to finish landing</summary>
		finalSpeed = 1.5;
		/// <summary>Altitude in meters when the final speed begins</summary>
		finalSpeedAltitude = 20;

		// PANIC/MARGINAL DETECTION
		panicDelta = 5;
		panicRatio = 50;
		marginalMax = 10;
		marginalWarn = 5;
		// H2 warning in %
		h2MarginWarning = 5;

		// LANDING/LIFTOFF TIMERS TRIGGER ALTITUDE
		// The small difference acts as a hysteresis
		landingTimerAltitude = 200;
		liftoffTimerAltitude = 250;

		// RADAR CONFIGURATION
		// The camera used as a ground radar will limit itself to this range (in meters)
		radarMaxRange = 2e5;

		// LEVELER AND TERRAIN AVOIDANCE SETTINGS

		// Leveler settings
		/// <summary>Maximum ship tilt angle (°) used by the leveler</summary>
		maxAngle = 20;
		/// <summary>After being overriden by the pilot, the script resumes control of the ship after this time (in units of tick1, ie 20 = 1/3 second)</summary>
		smartDelayTime = 20;
		gyroResponse = 5;
		gyroRpmScale = 0.1;

		// Terrain Avoidance settings

		horizKp = 0.5;
		horizKi = 0.1;
		horizKd = 0.1;
		horizAiMax = 0.05;
		speedScale = 20;
		inertiaRatioSmall = 1e7;
		inertiaRatioLarge = 6e8;

		// AUTOPILOT SETTINGS (for mode 4)

		// Initial altitude and speed when mode 4 is activated.
		m4InitAlt = 50;
		m4InitSpeed = 0;

		// Altitude PID controller 
		// Coefficients of the PID controller used to control altitude speed
		// Input in m (altitude delta), output in m/s (vertical speed set-point)
		alt_aiMax = 2;
		alt_aiMin = -0.1;
		altKp = 0.5;
		altKi = 0.1;
		altKd = 0.5;
		altAdFilt = 0.8;
		altAdMax = 0.5;
		minVertSpeed = -10;

		// Pilot control settings settings
		speedIncrement = 0.1;
		maxSpeed = 100;

		speedFilterLength = 30;
		altFilterLength = 30;
		safeSpeedFilterLength = 10;

		// Safe speed settings
		safeSpeedAltMin = 10;
		safeSpeedAltMax = 400;
		safeSpeedMin = 3;
		safeSpeedMax = 200;

		// ASTEROID LANDING SETTINGS (for mode 5)

		/// <summary>
		/// Fraction of the available thrust that will be used to land on asteroids
		/// </summary>
		mode5ThrustRatio = 0.5;
		/// <summary>
		/// Maximum speed allowed in mode 5. Since there's no gravity, it's only a
		/// tradeoff of waiting time vs safety and fuel usage.
		/// </summary>
		mode5MaxSpeed = 95;

		// LOGGING
		ALLOW_LOGGING = false;
		LOG_FACTOR = 2;

		// MISC
		DisableDelay = 3;

	}
	
}
/// <summary>
/// Source of the vertical speed setpoint
/// </summary>
public enum SPSrc { None, Profile, AltGravFormula, GravFormula, FinalSpeed, Hold, Unable, RDV, Escape}

/// <summary>
/// Source of the altitude value
/// </summary>
public enum AltSrc { Undef, Ground, Radar }

/// <summary>
/// Source of surface gravity estimate
/// </summary>
public enum GravSrc { Undef, Identified, Estimate, Local }

/// <summary>
/// Gravity warning type
/// </summary>
public enum WarnType { Info, Good, Risk, Bad }

/// <summary>
/// Current terrain scan mode
/// </summary>
public enum ScanMode { NoRadar, SingStby, SingNarr, DbleStby, DbleEarly, DbleWide }

/// <summary>
/// Struct with the 3 speed set points (forward, left, vertical)
/// </summary>
public struct Speed { public double v, f, l; }

/// <summary>
/// Structure with information about thrusters, with separate values for atmospheric, ion, hydrogen and prototech
/// </summary>
public struct ThrustStat { 
	public double atmo, ion, hydro, proto;
	public double Total => atmo + ion + hydro + proto; 
	}

/// <summary>
/// Struct with planet data
/// </summary>
public struct P
{
	public string Shortname, Name;
	public double AtmoDensitySL, AtmoLimitAltitude, HillParam, GSeaLevel;
	public bool Precise, Set;

	// Constructor for planet struct. It ensures that no negative value is set for the parameters.
	public P(string shortName, string name, double atmoDensitySL, double atmoLimitAltitude, double hillParam, double gSeaLevel, bool precise = true, bool set = true)
	{
		Shortname = shortName.ToLower();
		Name = name;
		AtmoDensitySL = atmoDensitySL >= 0 ? atmoDensitySL : 0;
		AtmoLimitAltitude = atmoLimitAltitude >= 0 ? atmoLimitAltitude : 0;
		HillParam = hillParam >= 0 ? hillParam : 0;
		GSeaLevel = gSeaLevel >= 0 ? gSeaLevel : 0;
		Precise = precise;
		Set = set;
	}
}

/// <summary>
/// List of planets with their data. See tech doc Appendix A.
/// </summary>
public class PlanetCatalog
{
	List<P> Catalog;

	public PlanetCatalog()
	{

		// The "shortname" is used to identify the planet in the command line arguments and must be unique.

		Catalog = new List<P>
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
new P("unknown", "Unknown Planet", 1, 2, .065, 1, false, false),

new P("dynvacuum", "Deduced Vacuum Planet", 0, 0, .065, 1, false, true),

// We assume a moderately dense atmosphere but that doesn't go as high as Earthlike
new P("dynatmo", "Deduced Atmo Planet", .8, 1, .065, 1, false, true),

new P("vacuum", "Generic Vacuum Planet", 0, 0, .065, 1, false, true),
// We assume a moderately dense atmosphere but that doesn't go as high as Earthlike
new P("atmo", "Generic Atmo Planet", .8, 1, .065, 1, false, true),

// Vanilla planets of space engineers. Values read directly from PlanetGeneratorDefinitions.sbc or Pertam.sbc or Triton.sbc
new P("pertam", "Pertam", 1, 2, .025, 1.2),
new P("triton", "Triton", 1, .47, .20, 1),
new P("earth", "Earthlike", 1, 2, .12, 1),
new P("alien", "Alien", 1.2, 2, .12, 1.1),
new P("mars", "Mars (vanilla)", 1, 2, .12, .9),
new P("moon", "Moon (vanilla)", 0, 1, .03, .25),

// no value in .sbc file for atmo_density_sealevel and atmo_limit_altitude ?!
new P("europa", "Europa", .5, 1, .06, .25),
new P("titan", "Titan", .5, 1, .03, .25),

// Below are additional planets from mods or custom planets that I like a lot

// by Major Jon
new P("komorebi", "Komorebi", 1.12, 2.4, .032, 1.14),
new P("orlunda", "Orlunda", .89, 6, .01, 1.12),
new P("trelan", "Trelan", 1, 1.2, .1285, .92),
new P("teal", "Teal", 1, 2, .02, 1),
new P("kimi", "Kimi", 0, 1, 0, .05),
new P("qun", "Qun", 0, 1, .25, .42),
new P("tohil", "Tohil", .5, 1, .03, .328),
new P("satreus", "Satreus", .9, 1.5, .04, .95),

new P("agni", "Agni", .55, 2.3, .022, 1.27),
new P("cauldron", "Cauldron", 1, 3.5, .01, 1.58),
// Kor (Cauldron System) is lower on the list to avoid collision with Valkor from Infinite
new P("tellus", "Tellus", 1, 2.7, .06, 1),

// by Elindis
new P("pyke", "Pyke", 1.5, 2, .06, 1.42),
new P("saprimentas", "Saprimentas", 1.5, 2, .07, .96),
new P("aulden", "Aulden", 1.2, 2, .10, .82),
new P("silona", "Silona", .85, 2, .03, .64),

// by Infinite
new P("argus", "Argus", .79, 2, .01, 1.45),
new P("aridus", "Aridus", 1.3, 1, .1, .5),
new P("microtech", "Microtech", 1, .5, .25, 1f),
new P("hurston", "Hurston", 1, 1.9, .11, 1.1),
new P("ignis", "Ignis", .85, 3, .005, 1.08),
new P("tharsis", "Tharsis", .85, 3, .015, .75),
new P("umbris", "Umbris", 0, 0, .05, .19),
new P("valkor", "Valkor", 1, .3, .165, 1.05),
new P("theros", "Theros", 1, .73, .1, .95),
new P("thanatos", "Thanatos", 1.5, 2.8, .04, 1.4),
new P("halcyon", "Halcyon", .85, 1.3, .3, .5),

// from the Solar System Pack by Infinite
new P("terra", "(Terra) Earth by Infinite", 2, .9, .02, 1),
new P("luna", "Luna by Infinite", 0, 1, .07, .16),
// because the script uses the first match, "mars" will still match the vanilla Mars
new P("sspmars", "Mars by Infinite", .006, 2, .09, .38),
new P("venus", "Venus by Infinite", 92, 2, .04, .9),
new P("mercury", "Mercury by Infinite", 0, 1, .1, .37),
new P("ceres", "Ceres by Infinite", 0, .5, .1, .05),
new P("deimos", "Deimos by Infinite", 0, 0, .8, .05),
new P("phobos", "Phobos by Infinite", 0, 0, 1, .05),

new P("callisto", "Callisto by Infinite", 0, .5, .04, .12),
new P("europa", "Europa by Infinite", 0, .5, .04, .13),
new P("ganymede", "Ganymede by Infinite", 0, 0, .04, .14),
new P("sspio", "Io by Infinite", 0, 0, .025, .18),

new P("dione", "Dione by Infinite", 0, .5, .06, .05),
new P("enceladus", "Enceladus by Infinite", 0, .5, .02, .05),
new P("iapetus", "Iapetus by Infinite", 0, 0, .03, .05),
new P("mimas", "Mimas by Infinite", 0, .5, .07, .05),
new P("rhea", "Rhea by Infinite", 0, 0, .06, .05),
new P("thetys", "Thetys by Infinite", 0, .5, .09, .05),
new P("titan", "Titan by Infinite", 1.5, 3, .01, .14),

new P("ariel", "Ariel by Infinite", 0, .5, .03, .05),
new P("charon", "Charon by Infinite", 0, .5, .03, .05),
new P("miranda", "Miranda by Infinite", 0, .5, .05, .08),
new P("oberon", "Oberon by Infinite", 0, .5, .03, .05),
new P("pluto", "Pluto by Infinite", .00001, 0, .03, .06),
new P("titania", "Titania by Infinite", 0, .5, .03, .05),
new P("triton", "Triton by Infinite", 0, .5, .03, .07),
new P("umbriel", "Umbriel by Infinite", 0, .5, .03, .05),

// Mirathi System also by Infinite
new P("acheris", "Acheris", 1.5, 2, .0003, 1.36),
new P("ares", "Ares", .85, 3, .025, .53),
new P("euterpe", "Euterpe", .1, 2, .025, .19),
new P("gaia", "Gaia", 1, 3, .03, .97),
new P("nyxion", "Nyxion", 0, 0, .095, .22),
new P("tartarus", "Tartarus", 1.1, 1.5, .08, 1.13),
new P("tarvos", "Tarvos", .85, 3, .03, .75),
new P("vulcanis", "Vulcanis", .85, 3, .065, .9),
new P("zephyr", "Zephyr", 10, 3, .01, 3.24),
new P("calliope", "Calliope", 1, 3, .03, .92),
new P("calypso", "Calypso", .2, 3, .03, .63),
new P("cryos", "Cryos", .1, 2, .065, .09),
new P("erebus", "Erebus", 0, 0, .028, .32),

// by Almirante Orlock
new P("helghan", "Helghan", 1.2, 3.5, .01, 1.1),

// by Fizzy
new P("arcadia", "Arcadia", 1, 2, .04, 1.17),
new P("sarilla", "Sarilla", 0, 0, .14, .74),
new P("anteros", "Anteros", 1.10, 1.69, .07, 1.32),
new P("chimera", "Chimera", 1.22, 1.5, .1, 1),
new P("zira", "Zira", 0, 0, .14, .16),
new P("celaeno", "Celaeno", 1.02, 6.5, .02, .93),
new P("scylla", "Scylla", 0, 0, .01, .32),

// by SlowpokeFarm
new P("dustydesert", "Dusty Desert Planet", 1, 2, .12, 1),

// Urdavis System by sam
new P("gamadon", "Gamadon", .8, 2, .15, .72),
new P("kuma", "Kuma", 1, .5, .1, 1),
new P("mieliv", "Mieliv", 1, .5, .1, 1),
new P("sario", "Sario", 0, 0, .30, .3),

// by Major Jon again, but lower on the list
// to avoid collision with valkor
new P("kor", "Kor", 0, 0, .03, .74),

// by Herr Doktor
new P("miasma", "Miasma", 1.1, 1.8, .02, 1.2),
new P("jormun", "Jormun", 1, 2, .08, 1),
new P("vermilion", "Vermilion", 1, 2, .08, 1),
new P("tibur", "Tibur", .7, 1.8, .05, .5),
new P("hel", "Hel", .8, 1.8, .03, .5),
new P("glacies", "Glacies", .4, 2, .03, .5),
new P("saveen", "Saveen", 0, 2, .02, .6),

// by HIC
new P("hicprime", "HIC Prime", 1, 2, .03, 1),

// by Doctor Octoganapus
new P("mythu", "Mythu", 1.3, 2, .03, .63),
new P("terminus", "Terminus", 1.25, 2, .03, 1.17),
new P("warte", "Warte", 0, 1, .55, .05),

// by DestroyerSTAR
new P("zenitaia","Zenitaia",1,4,.029,1),
new P("nivis","Nivis",1, 4, .023, 1),
new P("torr","Torr",.1, .47, .022, .18314),
new P("sulfate","Sulfate", .1, .47, .03, .4),
new P("calcaria","Calcaria",1, 3.5, .01, 1),
new P("relicta","Relicta",1, 3, .06, 1),
new P("acribus","Acribus",.9, 3, .015, 1.4)

};
	}

	/// <summary>
	/// Return the planet that matches a name given in the script command
	/// </summary>
	/// <param name="command">Input string containing the name</param>
	/// <param name="found">Output boolean, true if the planet if found</param>
	/// <returns>Planet (if not found, returns the "unknown" planet</returns>
	public P get_planet(string command, out bool found)
	{
		foreach (P candidate in Catalog)
		{
			if (command.ToLower().Contains(candidate.Shortname))
			{
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
public class ShipBlocks

{

	

	// lifts are the thruster that thrust upwards in the ship reference frame, and are used to control vertical speed and altitude. 
	public ThrGroup lifts, fwdThr, rearThr, leftThr, rightThr, downThr;
	public List<IMyTextSurface> MainDisplays, DebugDisplays;
	public List<IMyParachute> parachutes;
	public List<IMyTerminalBlock> landingTimers, liftoffTimers;
	public List<IMyTerminalBlock> onTimers, offTimers;
	public List<IMyGyro> gyros;
	public List<IMyLandingGear> gears;
	public List<IMyTerminalBlock> radars;
	public List<IMyTerminalBlock> terrainradars;
	public List<IMyTerminalBlock> sidecams;
	public List<IMyTerminalBlock> soundblocks;
	public List<IMyGasTank> h2Tanks;
	public IMyShipController Ctrller;

	public IMyCameraBlock fwdCam, rearCam, leftCam, rightCam;
	public IMyShipConnector Connector;


	public ShipBlocks()
	{
		// Only lists are initialized here, the rest is done in GetBlocks()
		MainDisplays = new List<IMyTextSurface>(); 
		DebugDisplays = new List<IMyTextSurface>();
		parachutes = new List<IMyParachute>();
		landingTimers = new List<IMyTerminalBlock>();
		liftoffTimers = new List<IMyTerminalBlock>();
		onTimers = new List<IMyTerminalBlock>();
		offTimers = new List<IMyTerminalBlock>();
		gyros = new List<IMyGyro>();
		gears = new List<IMyLandingGear>();
		radars = new List<IMyTerminalBlock>();
		sidecams = new List<IMyTerminalBlock>();
		terrainradars = new List<IMyTerminalBlock>();
		soundblocks = new List<IMyTerminalBlock>();
		h2Tanks = new List<IMyGasTank>();
	}

}



LandingManager mngr;
RunTimeCounter runtime;
Logger logger;
bool ranTick1 = false;
bool ranTick10 = false;
bool ranTick100 = false;
bool log = false;


// Instantiate a shared instance of the INI parser for storing configuration when saving
MyIni _ini = new MyIni();

public Program()
{
	// The constructor for the program. Run once when the script is lauched the first time
	// and when the savegame loads.

	Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10 | UpdateFrequency.Update100;

	SLMShipConfiguration shipconfig = new SLMShipConfiguration();
	SEGameConfig seconfig = new SEGameConfig();
	ShipBlocks ship = GetBlocks(shipconfig);

	SLMConfig config = new SLMConfig();

	PlanetCatalog catalog = new PlanetCatalog();

	runtime = new RunTimeCounter(this);
	mngr = new LandingManager(config, ship, catalog, runtime, seconfig, config.startHover);

	logger = new Logger(mngr.AllLogNames(), config.LOG_FACTOR, config.ALLOW_LOGGING);

	// Load and restore the saved configuration

	Restore(config, seconfig);

}

/// <summary>
/// Save the state of the script when the game is saved. It writes some values in the Storage string that will be read when the game is loaded to restore the state of the script.
/// </summary>
public void Save()
{
	// Save some settings in an INI using the storage string
	// From https://spaceengineers.wiki.gg/wiki/Scripting/Handling_Configuration_and_Storage
	// In particular, if the game is saved with the script in hover (mode3) or autopilot (mode4)
	// then we don't want the ship to crash when the savegame is loaded.

	_ini.Clear();
	_ini.Set("SLM", "mode", mngr.Mode);
	_ini.Set("SLM", "mode4alt", mngr.AP.m4AltSP);
	_ini.Set("SLM", "mode4speed", mngr.AP.m4SpdSP);
	_ini.Set("SLM", "mode4SL", mngr.AP.altMode == AutoPilot.AltMode.SeaLevel ? true : false);
	Storage = _ini.ToString();
}

/// <summary>
/// Restore the state of the script when the game is loaded. It reads the values saved in the Storage string and applies them to the script state.
/// </summary>
/// <param name="config"></param>
/// <param name="seconfig"></param>
public void Restore(SLMConfig config, SEGameConfig seconfig)
{
	_ini.TryParse(Storage);
	int _modeToRestore = _ini.Get("SLM", "mode").ToInt32(0);
	double _m4AltToRestore = _ini.Get("SLM", "mode4alt").ToDouble(config.m4InitAlt);
	double _m4SpeedToRestore = _ini.Get("SLM", "mode4speed").ToDouble(config.m4InitSpeed);
	bool _m4SL = _ini.Get("SLM", "mode4SL").ToBoolean(false);
	switch (_modeToRestore)
	{
		case 0:
			// Nothing to do, mode 0 is the default mode for the LandingManager constructor
			Echo ("Restored mode 0");
			break;

		case 1:
			mngr.SetMode1or2(1, true);
			Echo ("Restored mode 1");
			break;

		case 2:
			mngr.SetMode1or2(2, true);
			Echo ("Restored mode 2");
			break;

		case 3:
			mngr.SetMode3(true);
			Echo ("Restored mode 3");
			break;

		case 4:
			// Resume to the same altitude and speed
			mngr.SetMode4(_m4AltToRestore, _m4SpeedToRestore);
			Echo ("Restored mode 4");
			break;

		case 5:
			mngr.SetMode5();
			Echo ("Restored mode 5");
			break;
	}
	
}

public void Main(string arg, UpdateType updateSource)

{
	runtime.Count(ranTick1, ranTick10, ranTick100);

	// MANAGE ARGUMENTS AND REFRESH SOURCE
	if ((updateSource & (UpdateType.Trigger | UpdateType.Terminal)) != 0)
	{
		if (arg == "off" || arg.Contains("mode0"))
		{

			mngr.SetMode0();
			log = false;
			logger.Clear();

		}
		else
		{

			// The script accepts a lot of different arguments to control the ship and the script settings. The argument is not case-sensitive and can include multiple commands at once, for example "mode4 altup altgnd gpson mars" will switch to mode 4, increase the altitude set-point, switch it to be relative to ground, turn GPS on and set the planet to Mars.
			// We create a dictionary of possible commands and their associated actions, then we loop through the dictionary and execute the actions for the commands that are included in the argument.

			var actions = new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase)
			{
				["angleoff"] = mngr.DisableAngle,
				["angleon"] = mngr.EnableAngle,
				["angleswitch"] = mngr.SwitchAngle,
				["thrustersoff"] = mngr.DisableThrust,
				["thrusterson"] = mngr.EnableThrust,
				["thrustersswitch"] = mngr.SwitchThrust,
				["leveloff"] = mngr.DisableLeveler,
				["levelon"] = mngr.EnableLeveler,
				["levelswitch"] = mngr.SwitchLeveler,
				["altup"] = mngr.M4IncreaseAltitude,
				["altdown"] = mngr.M4DecreaseAltitude,
				["speedup"] = mngr.M4IncreaseSpeed,
				["speeddown"] = mngr.M4DecreaseSpeed,
				["altswitch"] = mngr.M4AltSwitch,
				["altgnd"] = mngr.M4AltGND,
				["altsl"] = mngr.M4AltSL,
				["gpson"] = () => mngr.EnableGPS(Me.CustomData),
				["gpsoff"] = mngr.DisableGPS,
				["gpsswitch"] = () => mngr.SwitchGPS(Me.CustomData),
				["mode1"] = () => { mngr.SetMode1or2(1); log = true; },
				["mode2"] = () => { mngr.SetMode1or2(2); log = true; },
				["mode3"] = () => { mngr.SetMode3(); log = true; },
				["mode4"] = () => { mngr.SetMode4(); log = true; },
				["mode5"] = () => { mngr.SetMode5(); log = true; },
				["mode6"] = () => { mngr.SetMode6(); log = true; },
				["dumplog"] = () => { log = false; Me.CustomData = logger.Output(); },
				["clearlog"] = logger.Clear
			};

			foreach (var kv in actions)
				if (arg.Contains(kv.Key))
					kv.Value();

			mngr.SetPlanet(arg);

		}
	}

	ranTick1 = false;
	ranTick10 = false;
	ranTick100 = false;

	if ((updateSource & UpdateType.Update100) != 0)
	{
		ranTick100 = true;
		mngr.Tick100();
	}

	if ((updateSource & UpdateType.Update10) != 0)
	{
		ranTick10 = true;
		mngr.Tick10();
	}

	if ((updateSource & UpdateType.Update1) != 0)
	{
		ranTick1 = true;
		mngr.Tick1();

		if (log) logger.Log(mngr.AllLogValues());

	}

}


/// <summary>
/// Get all the blocks needed by the script and return them in a ShipBlocks object
/// </summary>
public ShipBlocks GetBlocks(SLMShipConfiguration conf)
{

	var s = new ShipBlocks();

	Echo("SOFT LANDING MANAGER");

	// Filter function to find only blocks that are on the same grid as the script
	// and include none of the ignore patterns in their name
	Func<IMyTerminalBlock, bool> filter = b =>
	{
		bool result = b.IsSameConstructAs(Me);
		foreach (string name in conf.IGNORE_NAME)
		{
			if (b.CustomName.Contains(name)) result = false;
		}
		return result;
	};

	// Action to search for blocks based on their name from a list of strings.
	Action<List<IMyTerminalBlock>, List<string>, string, Func<IMyTerminalBlock, bool>> SearchBlocks = (blocksList, names, descr, filtr) =>
	{
		List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
		foreach (string name in names)
		{
			GridTerminalSystem.SearchBlocksOfName(name, temp, filtr);
			blocksList.AddRange(temp);
		}
		Echo("Found " + blocksList.Count + " " + descr);
	};

	// Action to seach for text surfaces in the named blocks and select the appropriate surface
	Action<List<IMyTextSurface>, List<string>, string, Func<IMyTerminalBlock, bool>> SearchSurfaces = (blocksList, prefixes, descr, filtr) =>
	{

		List<IMyTerminalBlock> temp = new List<IMyTerminalBlock>();
		SearchBlocks(temp, prefixes, "possible display(s)", filtr);

		foreach (IMyTerminalBlock b in temp)
		{

			if (b is IMyTextPanel)
			{

				// If its a simple text panel, add it directly
				blocksList.Add(b as IMyTextSurface);

			}
			else if (b is IMyTextSurfaceProvider)
			{

				// Otherwise, select the appropriate text surface based on its name
				IMyTextSurfaceProvider p = (IMyTextSurfaceProvider)b;
				if (p.SurfaceCount >= 1 && p.UseGenericLcd)
				{
					int N = H.FindN(b.CustomName, prefixes);
					if (N >= 0)
					{
						blocksList.Add(p.GetSurface(N));
					}
					else
					{
						blocksList.Add(p.GetSurface(0));
					}
				}
			}
		}
		Echo("Found " + blocksList.Count + " " + descr);
	};

	// Look for blocks based on the name

	SearchBlocks(s.radars, conf.RADAR_NAME, "radars(s)", filter);
	SearchBlocks(s.terrainradars, conf.TERRAIN_RADAR_NAME, "terrain radars(s)", filter);
	SearchBlocks(s.sidecams, conf.SIDE_CAM_NAME, "side camera(s)", filter);
	SearchBlocks(s.landingTimers, conf.LANDING_TIMER_NAME, "landing timer(s)", filter);
	SearchBlocks(s.liftoffTimers, conf.LIFTOFF_TIMER_NAME, "liftoff timer(s)", filter);
	SearchBlocks(s.onTimers, conf.ON_TIMER_NAME, "on timer(s)", filter);
	SearchBlocks(s.offTimers, conf.OFF_TIMER_NAME, "off timer(s)", filter);
	SearchBlocks(s.soundblocks, conf.SOUND_NAME, "sound block(s)", filter);

	// Find text surfaces

	SearchSurfaces(s.MainDisplays, conf.LCD_NAME, "valid display(s)", filter);
	SearchSurfaces(s.DebugDisplays, conf.DEBUGLCD_NAME, "valid debug display(s)", filter);


	// Look for blocks based on the type

	GridTerminalSystem.GetBlocksOfType(s.parachutes, filter);
	Echo("Found " + s.parachutes.Count + " parachutes");

	GridTerminalSystem.GetBlocksOfType(s.gyros, filter);
	Echo("Found " + s.gyros.Count + " gyros");

	GridTerminalSystem.GetBlocksOfType(s.gears, filter);
	Echo("Found " + s.gears.Count + " landing gears");

	var all_tanks = new List<IMyGasTank>();
	GridTerminalSystem.GetBlocksOfType(all_tanks, filter);
	foreach (IMyGasTank tank in all_tanks)
	{
		if (tank.BlockDefinition.SubtypeName.Contains("Hydrogen"))
		{
			Echo("Found h2 tank:" + tank.CustomName);
			s.h2Tanks.Add(tank);
		}
	}

	// Find a suitable ship controller.
	// Prefer the one that matches one of the configured names, otherwise select the first available one
	var named_ctrllers = new List<IMyTerminalBlock>();
	SearchBlocks(named_ctrllers, conf.CTRLLER_NAME, "possible controller(s)", filter);

	if (named_ctrllers.Count >= 1)
	{
		s.Ctrller = named_ctrllers[0] as IMyShipController;
		Echo("Using controller:" + s.Ctrller.CustomName);
	}
	else
	{
		var possible_controllers = new List<IMyShipController>();
		GridTerminalSystem.GetBlocksOfType(possible_controllers, b => b.CanControlShip);
		if (possible_controllers.Count == 0)
		{
			throw new Exception("Error: no suitable cockpit or remote control block.");
		}
		else
		{
			s.Ctrller = possible_controllers[0];
			Echo("Using controller:" + s.Ctrller.CustomName);
		}
	}

	// Extract the controller orientation to compare with other blocks
	Matrix MatrixCockpit;
	s.Ctrller.Orientation.GetMatrix(out MatrixCockpit);

	// Check radar orientation consistency
	foreach (IMyTerminalBlock radar in s.radars)
	{
		Matrix MatrixRadar;
		radar.Orientation.GetMatrix(out MatrixRadar);

		if (MatrixRadar.Forward != MatrixCockpit.Down || MatrixRadar.Up != MatrixCockpit.Forward)
		{
			Echo("Warning: radar " + radar.CustomName + " wrong orientation.");
		}
	}

	// Place side cameras on the left and right of the ship based on their orientation, and check consistency
	foreach (IMyTerminalBlock cam in s.sidecams) 
	{
		Matrix MatrixCam;
		cam.Orientation.GetMatrix(out MatrixCam);

		if (MatrixCam.Forward == MatrixCockpit.Forward && MatrixCam.Up == MatrixCockpit.Up)
		{
			s.fwdCam = cam as IMyCameraBlock;
			Echo("Fwd cam: " + cam.CustomName);
		}
		else if (MatrixCam.Forward == MatrixCockpit.Backward && MatrixCam.Up == MatrixCockpit.Up)
		{
			s.rearCam = cam as IMyCameraBlock;
			Echo("Rear cam: " + cam.CustomName);
		}
		else if (MatrixCam.Forward == MatrixCockpit.Left && MatrixCam.Up == MatrixCockpit.Up)
		{
			s.leftCam = cam as IMyCameraBlock;
			Echo("Left cam: " + cam.CustomName);
		}
		else if (MatrixCam.Forward == MatrixCockpit.Right && MatrixCam.Up == MatrixCockpit.Up)
		{
			s.rightCam = cam as IMyCameraBlock;
			Echo("Right cam: " + cam.CustomName);
		}
		else
		{
			Echo("Warning: side camera " + cam.CustomName + " wrong orientation.");
		}
	}

	// Find thrusters based on orientation
	// Get all thrusters in a temporary list, then place them in the appropriate list based on their orientation compared to the ship controller orientation.
	var AllThr = new List<IMyThrust>();

	var FwdThr = new List<IMyThrust>();
	var RearThr = new List<IMyThrust>();
	var LeftThr = new List<IMyThrust>();
	var RightThr = new List<IMyThrust>();
	var DownThr = new List<IMyThrust>();
	var Lifters = new List<IMyThrust>();

	GridTerminalSystem.GetBlocksOfType(AllThr, filter);

	foreach (IMyThrust t in AllThr)
	{

		Matrix MatrixThrust;
		t.Orientation.GetMatrix(out MatrixThrust);

		if (MatrixThrust.Forward == MatrixCockpit.Down)
			Lifters.Add(t);
		else if (MatrixThrust.Forward == MatrixCockpit.Backward)
			FwdThr.Add(t);
		else if (MatrixThrust.Forward == MatrixCockpit.Forward)
			RearThr.Add(t);
		else if (MatrixThrust.Forward == MatrixCockpit.Right)
			LeftThr.Add(t);
		else if (MatrixThrust.Forward == MatrixCockpit.Left)
			RightThr.Add(t);
		else if (MatrixThrust.Forward == MatrixCockpit.Up)
			DownThr.Add(t);
	}

	// The constructor of ThrGroup sorts them out by type (ion, atmo etc.)

	s.lifts = new ThrGroup(Lifters, "lifters");
	Echo("Found " + s.lifts.Inventory() + " lifters");

	s.fwdThr = new ThrGroup(FwdThr, "fwr thr");
	Echo("Found " + s.fwdThr.Inventory() + " fwd thr");

	s.rearThr = new ThrGroup(RearThr, "rear thr");
	Echo("Found " + s.rearThr.Inventory() + " rear thr");

	s.leftThr = new ThrGroup(LeftThr, "left thr");
	Echo("Found " + s.leftThr.Inventory() + " left thr");

	s.rightThr = new ThrGroup(RightThr, "right thr");
	Echo("Found " + s.rightThr.Inventory() + " right thr");

	s.downThr = new ThrGroup(DownThr, "down thr");
	Echo("Found " + s.downThr.Inventory() + " down thr");

	// Find the connector, if any
	var named_connectors = new List<IMyTerminalBlock>();
	SearchBlocks(named_connectors, conf.CONNECTOR_NAME, "possible connector(s)", filter);

	if (named_connectors.Count > 0)
	{
		// Check orientation consistency with the ship controller, the connector must face downwards
		Matrix MatrixConnector;
		named_connectors[0].Orientation.GetMatrix(out MatrixConnector);
		if (MatrixConnector.Forward == MatrixCockpit.Down)
		{
			s.Connector = named_connectors[0] as IMyShipConnector;
			Echo("Found connector: " + s.Connector.CustomName);

		}
		else
		{
			Echo("Warning: connector " + named_connectors[0].CustomName + " wrong orientation.");
		}
		
	}

	// Return the final object
	return s;
}

/// <summary>
/// Main class for the landing manager
/// </summary>
public class LandingManager
{

	// TODO : too many attibutes used as global variables !
	// This should be refactored at some point !

	/// <summary>
	/// Operating mode. 0 = off, 1 = economy landing, 2 = fast landing, 3 = hover, 4 = autopilot, 5 = space rendez-vous, 6 = escape gravity well
	/// </summary>
	public int Mode = 0;
	/// <summary>
	/// Whether or not ship tilting is used to control horizontal speed. If false, the
	/// gyroscopes will still be used to level the ship if Level = true.
	/// </summary>
	public bool Angle;
	/// <summary>
	/// Whether or not the horizontal thrusters are used to control horizontal speed
	/// </summary>
	public bool HorizThr;
	/// <summary>
	/// Whether or not the script controls the ship orientation with gyroscopes
	/// both to simply auto-level the ship and to manager horizontal speed.
	/// </summary>
	public bool Level;
	/// <summary>
	/// Whether the horizontal speed is controlled to reach a GPS coordinate, or to avoid the terrain.
	/// </summary>
	public bool GPS = false;
	/// <summary>
	/// Whether the GPS mode is automatically cancelled because the target is invalid or unreachable
	/// </summary>
	public bool GPScancelled=false;
	/// <summary>
	/// The atmospheric density value reconstructed from ion, prototech or atmospheric
	/// thrusters efficiency, and parachutes. Value of -1 means invalid.
	/// </summary>
	double obsDensity = -1;
	/// <summary>
	/// Value of the gravity field at the location of the ship, in m/s²
	/// </summary>
	double gravNow = 0;
	/// <summary>
	/// Expected value of the gravity field at ground level, in m/s²
	/// </summary>
	double gndGravExp;
	/// <summary>
	/// Current ship weight (mass * gravity) in Newtons
	/// </summary>
	double shipWeight = 0;
	/// <summary>
	/// In meters. This is the best available value for the distance between the
	/// ship legs and the ground surface (planet terrain, grid ex : landing pad
	/// or asteroid surface), using a combination of the altitude returned by the ship
	/// controller API and the ground radar. If completely unknown, it will be set
	/// to the value of GroundRadar.UNDEFINED_ALTITUDE which is a very large value
	/// (example 1e6 m). Negative values may be possible depending on how the offset
	/// is configured.
	/// </summary>
	double gndAlt = 0;
	/// <summary>
	/// In meters, the best available value for the altitude relative to sea level.
	/// (on most planet, sea level is just a reference, the sea (ice) level itself
	/// may be different)
	/// </summary>
	double slAlt = 0;
	double gndSlOffset = 0;
	/// <summary>
	/// Difference between radar altitude and ship controller altitude
	/// </summary>
	double radarOffset = 0;


	ThrustStat currentLWR, groundLWR;
	/// <summary>
	/// Lift-to-weight ratio used to compute the speed set-point with the altitude/gravity formula
	/// and the gravity formula. Not used when using the profile, which is more accurate.
	/// Uses a mix of the lift to weight ratio at the current altitude, and the expected
	/// lift-to-weight ratio at the planet surface.
	/// </summary>
	double lwrTargetSelected = 0;
	
	Speed spdSP, spd;
	/// <summary>
	/// Difference between vertical speed set point and current vertical speed (m/s)
	/// </summary>
	double vSpeedDelta = 0;
	/// <summary>
	/// Thrust command in N for the vertical thrusters
	/// </summary>
	double thrCmd = 0;
	/// <summary>
	/// Lift to weight ratio command (no unit) for the vertical thrusters
	/// </summary>
	double lwrCmd = 0;
	bool panic = false;
	/// <summary>
	/// Flag to allow the script to switch to mode0 (off) in some conditions. It is set to a positive value and then decremented, and the script is allowed to switch to mode 0 only when it reaches 0. This is used to avoid switching off the script too quickly in case of a temporary loss of radar signal or other sensor issues.
	/// </summary>
	int allowDisable = 0;
	int marginal = 0;

	bool allowLandingTimer = false, allowLiftoffTimer = false;

	bool blink;
	bool horizlimit=false;
	int tickIndex=0;
	WarnType warnState = WarnType.Info;

	SPSrc spdSrc = SPSrc.None;
	AltSrc altSrc = AltSrc.Undef;
	GravSrc gravSrc = GravSrc.Undef;

	SLMConfig c;
	ShipBlocks s;
	SEGameConfig seconfig;
	SurfaceGravityEstimator estim;
	ShipInfo sInfo;
	LiftoffProfileBuilder prof;
	P planet;
	PlanetCatalog catalog;
	PIDController vertPID;
	AutoLeveler lvlr;
	GroundRadar rdr;
	HorizontalThrusters horizThr;
	RunTimeCounter runtime;
	MovingAverage leftSpdTgt, fwdSpdTgt, altFilter;
	/// <summary>
	/// Vertical speed set-point using a rate limiter. It's allowed to increase very quickly
	/// but is forced to decrease slowly.
	/// </summary>
	RateLimiter speedTgt;
	public AutoPilot AP;
	GPSGuide gpsGuide;




	/// <summary>
	/// Constructor for the LandingManager class
	/// </summary>

	public LandingManager(SLMConfig conf, ShipBlocks ship_defined, PlanetCatalog catalog_input, RunTimeCounter runTime, SEGameConfig seConfig, bool startHover = false)
	{
		c = conf;
		s = ship_defined;
		catalog = catalog_input;
		runtime = runTime;
		seconfig = seConfig;

		estim = new SurfaceGravityEstimator(seconfig.GravExp);

		sInfo = new ShipInfo(s, c);
		prof = new LiftoffProfileBuilder(seconfig);
		vertPID = new PIDController(c.vertKp, c.vertKi, c.vertKd, c.aiMin, c.aiMax, c.vertAdFilt, c.vertAdMax);

		lvlr = new AutoLeveler(s.Ctrller, s.gyros, Math.Min(c.maxAngle, sInfo.MaxAngle()), c.smartDelayTime, c.gyroResponse, c.gyroRpmScale);

		rdr = new GroundRadar(s.radars, s.terrainradars, c.radarMaxRange, c.speedScale, s.fwdCam, s.rearCam, s.leftCam, s.rightCam);

		horizThr = new HorizontalThrusters(s, c.smartDelayTime, c.horizKp, c.horizKi, c.horizKd, c.horizAiMax);

		leftSpdTgt = new MovingAverage(3);
		fwdSpdTgt = new MovingAverage(3);
		altFilter = new MovingAverage(3);
		speedTgt = new RateLimiter(999, c.speedTgtGradient);
		AP = new AutoPilot(c);
		gpsGuide = new GPSGuide(s, sInfo, seconfig);

		// These initial settings can be dynamically changed by the pilot
		Angle = c.useGyro;
		HorizThr = c.useThrusters;
		Level = c.autoLevel;

		SetMode0();
		SetUpLCDs();

		if (startHover)
			SetMode3();

	}

	// ------------------------------
	// PUBLIC METHODS
	// ------------------------------


	/// <summary>
	/// Switch to mode 0 (off or standby mode)
	/// </summary>
	public void SetMode0()
	{
		Mode = 0;
		s.lifts.Disable();
		s.downThr.Disable();
		horizThr.Disable();
		rdr.DisableRadar();
		rdr.DisableSide();
		SetPlanet("unknown");
		TriggerOffTimers();
		prof.Invalidate();
		spdSrc = SPSrc.None;
		altSrc = AltSrc.Undef;
		gravSrc = GravSrc.Undef;
		rdr.mode = ScanMode.NoRadar;
		lvlr.Disable();
		estim.Reset();
		InitLandingLiftoffTimers();
		marginal = 0;
		GPS = false;
		GPScancelled = false;
	}

	/// <summary>
	/// Switch to mode 1 or 2 : main function of the script to manage landings
	/// Some actions are skipped if we were already in mode 1 or 2 before
	/// </summary>
	public void SetMode1or2(int mode, bool force=false)
	{
		if (mode != 1 && mode != 2) return;

		if (!DisableConditions() || force)
		{

			s.lifts.TurnOn();
			horizThr.TurnOn();

			if (Mode != 2 && Mode != 1)
			{
				// Only if the SLM was off previously
				rdr.StartRadar();
				rdr.StartSide();
				TriggerOnTimers();
				prof.Invalidate();
				speedTgt.Init(-c.vSpeedMax);
			}

			Mode = mode;
			s.Ctrller.DampenersOverride = false;
			if (Level) lvlr.Enable();
			allowDisable = c.DisableDelay;
			InitLandingLiftoffTimers();
		}
	}


	/// <summary>
	/// Switch to mode 3 : hover mode with horizontal speed control
	/// </summary>
	public void SetMode3(bool force=false)
	{
		if (!DisableConditions() || force)
		{

			Mode = 3;
			s.lifts.TurnOn();
			horizThr.TurnOn();
			s.Ctrller.DampenersOverride = true;
			if (Level) lvlr.Enable();
			spdSrc = SPSrc.None;
			s.lifts.Disable();
			allowDisable = c.DisableDelay;
			rdr.DisableRadar();
			rdr.DisableSide();
			InitLandingLiftoffTimers();
			marginal = 0;
			GPS=false;
		}
	}

	/// <summary>
	/// Switch to mode 4 : autopilot with altitude / forward speed hold. Note that here we
	/// do not check for the disable conditions, that way the autopilot mode can start
	/// from a landed ship and automatically unlock landing gears and begin flying
	/// </summary>
	public void SetMode4(double altitude = 0, double speed = 0, AutoPilot.AltMode altitudeMode = AutoPilot.AltMode.Ground)
	{
		Mode = 4;
		s.lifts.TurnOn();
		horizThr.TurnOn();
		s.Ctrller.DampenersOverride = false;

		AP.m4AltSP = (altitude == 0) ? Math.Max(c.m4InitAlt, gndAlt) : altitude;

		AP.m4SpdSP = (speed == 0) ? c.m4InitSpeed : speed;

		speedTgt.Init(0);
		if (Level) lvlr.Enable();
		spdSrc = SPSrc.Hold;
		AP.Init();
		AP.altMode = altitudeMode;

		vertPID.Reset();
		GearUnLock();
		allowDisable = c.DisableDelay;
		rdr.StartRadar();
		rdr.StartFwd();
		InitLandingLiftoffTimers();
		marginal = 0;
		GPS=false;
	}

	/// <summary>
	/// Switch to mode 5 : landing on a target in zero gravity
	/// </summary>
	public void SetMode5(bool force=false)
	{
		if (gravNow == 0 || force)
		{
			Mode = 5;
			s.lifts.TurnOn();
			s.downThr.TurnOn();
			s.Ctrller.DampenersOverride = false;
			speedTgt.Init(0);
			spdSrc = SPSrc.None;
			vertPID.Reset();
			allowDisable = c.DisableDelay;
			rdr.StartRadar();
			InitLandingLiftoffTimers();
			marginal = 0;
			rdr.DisableSide();
			GPS=false;
		}
	}

	/// <summary>
	/// Switch to mode 6 : automatic take-off from the surface, and go outside the gravity well economically.
	/// </summary>
	public void SetMode6(bool force=false)
	{
		if (gravNow>0 || force)
		{
			Mode=6;
			s.lifts.TurnOn();
			speedTgt.Init(0);
			GearUnLock();
			allowDisable = c.DisableDelay;
			lvlr.Enable();
			rdr.DisableSide();
			GPS=false;
		}
	}

/// <summary>
/// Set the current planet by name, and if the planet is unknown, set the atmospheric density to the worst possible value to allow the script to work in some conditions even without a known planet.
/// </summary>
/// <param name="name"></param>
	public void SetPlanet(string name)
	{
		bool found;
		P tplanet = catalog.get_planet(name, out found);
		if (found) planet = tplanet;
		if (planet.Shortname == "unknown")
			planet.AtmoDensitySL = s.lifts.WorstDensity();
	}

	// See tech doc §2.2
	public void Tick100()
	{

		// PART A : ALL MODES
		// Nothing

		// PART B : MODE-SPECIFIC ACTIONS

		if (Mode == 1 || Mode == 2 || Mode == 6)
		{
			estim.UpdateEstimates(gravNow, slAlt, planet.HillParam);
		}

		if (Mode != 5)
			UpdatePlanetAtmo();

		// PART C : ALL MODES

		ManageSoundBlocks();
		sInfo.UpdateMass();
		sInfo.UpdateInertia();
		UpdateMaxGravitiesAndWarning();

		s.lifts.UpdateDensitySweep();
		if (allowDisable > 0)
			allowDisable--;

	}

	// See tech doc §2.2
	public void Tick10()
	{

		// PART A : ALL MODES

		UpdateGrav();
		UpdateShipWeight();
		s.lifts.UpdateThrust();
		horizThr.UpdateThrust();
		s.downThr.UpdateThrust();
		UpdateAvailableLWR();

		ComputeSurfaceGravityEstimate();

		UpdateLWRTarget();

		// PART B : MODE-SPECIFIC ACTIONS


		if ((Mode == 1) || (Mode == 2))
		{
			UpdateProfile();
			if (GPS && gpsGuide.ShouldCancel(lvlr.Pitch, lvlr.Roll, gndAlt))
			{
				GPS = false;
				GPScancelled = true;
			}
				
		}
		else if (Mode == 3)
		{
			AP.UpdateSafeSpeed(gndAlt, -1, -1);
		}
		else if (Mode == 4)
		{
			bool scanned1, scanned2;

			// cos(40°) = 0.766
			const double COS40 = 0.766;
			double fwd1ScanDistance = (c.safeSpeedAltMax + 10) / COS40;
			double fwd1ScanMeasure = rdr.ScanDir(40, 0, lvlr.Pitch, -lvlr.Roll, fwd1ScanDistance, out scanned1);
			if (scanned1)
			{
				AP.fwdAlt1 = COS40 * fwd1ScanMeasure;
				AP.fwdValid1 = fwd1ScanMeasure < fwd1ScanDistance ? true : false;
			}

			// sin(10°) = 0.342
			const double SIN20 = 0.342;
			double fwd2ScanDistance = (c.safeSpeedAltMax + 10) / SIN20;
			double fwd2ScanMeasure = rdr.sideScan.ScanFwdOnly(lvlr.Pitch, fwd2ScanDistance,out scanned2);
			if (scanned2)
			{
				AP.fwdAlt2 = SIN20*fwd2ScanMeasure;
				AP.fwdValid2 = fwd2ScanMeasure < fwd2ScanDistance ? true : false;
			}

			AP.UpdateSafeSpeed(gndAlt, AP.fwdAlt1, AP.fwdAlt2);

		}
		else if (Mode == 5)
		{
			rdr.ScanForAltitude(0, 0);
		}

		// PART C : ALL MODES

		UpdateDisplays();
		ManageTimers();
		ManagePanicParachutes();
		UpdateDebugDisplays();

		// PART D : Manage next mode transition

		// In mode 6, once we've exited the gravity well, enable the dampeners to stop the ship before turning off the SLM
		if (Mode == 6 && gravNow==0)
			s.Ctrller.DampenersOverride = true;

		// In mode 5, once we've reached the asteroid or ship, we turn on the dampeners and stop the script
		if (Mode == 5 && gndAlt < c.finalSpeedAltitude)
		{
			SetMode0();
			s.Ctrller.DampenersOverride = true;
		}

		// Disable the SLM if: no gravity or we've landed (detected from the altitude) or landing gear locked
		if (Mode != 0 && allowDisable == 0 && DisableConditions())
			SetMode0();

	}


	// See tech doc §2.2
	public void Tick1()
	{
		tickIndex++;
		if (tickIndex >= 10)
			tickIndex = 0;

		UpdateAltitude();

		if ((Mode == 1) || (Mode == 2))
		{
			// PART 1 : EXECUTED EVERY TICK

			// Manage vertical speed

			
			UpdateShipSpeedsInGravity();
			rdr.IncrementAltAge();

			UpdateSpeedSPInGravity();
			vSpeedDelta = spdSP.v - spd.v;

			vertPID.UpdatePIDController(vSpeedDelta, c.aiMin, currentLWR.Total);
			ApplyThrustOverrideInGravity(vertPID.output);

			// Manage horizontal speed

			UpdateHorizSpeedSetPoint();

			if (Level)
			{
				if (Angle)
					lvlr.Tick(spd, spdSP);
				else
					lvlr.Tick();
			}

			if (HorizThr)
				horizThr.Tick(spd, spdSP, sInfo.mass, Angle, true);

			// PART 2 : This uses tickIndex to execute some actions less frequently than tick1, but also to not do them all together in tick10. That way we spread the radar scans and reduce load spikes.

			if (tickIndex == 0)
			{
				rdr.ScanForAltitude(lvlr.Pitch, -lvlr.Roll);
			}
			else if (tickIndex == 3 && !GPS)
			{
				if (Angle || HorizThr)
					rdr.ScanTerrain(lvlr.Pitch, -lvlr.Roll);
			}
			else if (tickIndex == 6 && !GPS)
			{
				if ((Angle || HorizThr) && gndAlt < 2000)
					rdr.ScanSide(lvlr.Pitch, lvlr.Roll);
			}

		}
		else if (Mode == 3)
		{
			UpdateAltitude();
			UpdateShipSpeedsInGravity();

			// Get the commands from the pilot
			AP.UpdateSpeedDirect(s.Ctrller.MoveIndicator);

			spdSP.f = AP.spdSP.f;
			spdSP.l = AP.spdSP.l;

			// Apply horizontal speed control with ship tilt and thrusters
			if (Angle)
				lvlr.Tick(spd, spdSP);
			else
				lvlr.Tick();
			if (HorizThr)
				horizThr.Tick(spd, spdSP, sInfo.mass, Angle, false);


		}
		else if (Mode == 4)
		{
			UpdateAltitude();
			UpdateShipSpeedsInGravity();

			// HORIZONTAL GUIDANCE

			// Get the horitontal speed commands from the pilot
			AP.UpdateSpeedProgressive(s.Ctrller.MoveIndicator);
			spdSP.f = AP.spdSP.f;
			spdSP.l = AP.spdSP.l;

			// Apply horizontal speed control with ship tilt and thrusters
			if (Angle)
				lvlr.Tick(spd, spdSP);
			else
				lvlr.Tick();
			if (HorizThr)
				horizThr.Tick(spd, spdSP, sInfo.mass, Angle, true);

			// VERTICAL GUIDANCE

			// Get the vertical speed command from the autopilot
			AP.UpdateVertSpeedSP(gndAlt, slAlt, gravNow);
			spdSP.v = AP.spdSP.v;
			vSpeedDelta = spdSP.v - spd.v;

			// Feed the speed error to the vertical PID and apply vertical thrust
			vertPID.UpdatePIDController(vSpeedDelta, c.aiMin, currentLWR.Total);
			ApplyThrustOverrideInGravity(vertPID.output);


		}
		else if (Mode == 5)
		{

			// EXPERIMENTAL
			UpdateShipSpeedsInSpace();
			UpdateSpeedSPInSpace();

			// PID controller and thrusters are only controlled if we have an actual speed set-point
			// so that the pilot can give the initial speed themselves and the ship won't brake during
			// radar initialization.
			if (spdSrc != SPSrc.None)
			{
				vSpeedDelta = spdSP.v - spd.v;
				vertPID.UpdatePIDController(vSpeedDelta, c.aiMin, 0.1);
				ApplyThrustOverrideInSpace(vertPID.output);
			}

		}
		else if (Mode == 6)
		{	
			UpdateAltitude();
			UpdateShipSpeedsInGravity();

			// Default formula if the estimator doesn't have a proper value. This formula causes a slower, less efficient ascend.
			double bestspeed=50*gravNow;

			if (estim.confBest > .9)
			{
				double FINAL = H.g_to_ms2(0.05);

				double R = estim.RBest, G = estim.gBest;

				double term = Math.Sqrt(gravNow / G) - Math.Sqrt(FINAL / G);

				if (term > 0)
					bestspeed = Math.Sqrt(2.0 * G * R * term);

			}

			spdSP.v = H.Min(seconfig.MaxSpeed-5, c.vSpeedMax , bestspeed);
			spdSrc = SPSrc.Escape;

			vSpeedDelta = spdSP.v - spd.v;
			vertPID.UpdatePIDController(vSpeedDelta, c.aiMin, 0.1);
			ApplyThrustOverrideInGravity(vertPID.output);
			lvlr.Tick();
			

		}
	}


	// ------------------------------
	// PRIVATE METHODS WITH SIDE-EFFECTS INSIDE THE CLASS
	// (they update class attributes used as global variables but otherwise don't have an effect on the ship)
	// ------------------------------

	private void UpdateProfile()
	{

		double confidenceBest = estim.confBest;
		double radiusBest = estim.RBest;

		// Profile computation requires to know the planet radius, so if we don't have sufficient
		// confidence then don't compute the profile at all.
		if (confidenceBest > 0.95)
		{
			// If we don't know the planet from a catalog, use the estimated surface gravity
			if (!planet.Precise == false)
				planet.GSeaLevel = H.ms2_to_g(gndGravExp);

			// In any case, we need to use the estimated planet radius
			if (Mode == 1)
				prof.Compute(c.finalSpeedAltitude + gndSlOffset, sInfo, planet, radiusBest, c.accelLimit, c.LWRlimit, c.elecLwrSufficient, c.LWRsafetyfactor, c.vSpeedMax, c.mode1IonSpeed, c.finalSpeed, s.lifts, seconfig.GravExp);

			if (Mode == 2)
				prof.Compute(c.finalSpeedAltitude + gndSlOffset, sInfo, planet, radiusBest, c.accelLimit, c.LWRlimit, c.LWRlimit, c.LWRsafetyfactor, c.vSpeedMax, c.vSpeedMax, c.finalSpeed, s.lifts, seconfig.GravExp);
		}
	}

	private void UpdateShipWeight()
	{
		// Compute ship weight
		shipWeight = sInfo.mass * gravNow;
	}

	private void UpdateGrav()
	{
		gravNow = s.Ctrller.GetNaturalGravity().Length();
	}


	private void UpdateAvailableLWR()
	{
		currentLWR.atmo = LWR(gravNow, sInfo.mass, s.lifts.Eff.atmo);
		currentLWR.ion = LWR(gravNow, sInfo.mass, s.lifts.Eff.ion);
		currentLWR.hydro = LWR(gravNow, sInfo.mass, s.lifts.Eff.hydro);
		currentLWR.proto = LWR(gravNow, sInfo.mass, s.lifts.Eff.proto);

		groundLWR.atmo = LWR(gndGravExp, sInfo.mass, s.lifts.AtmoThrustForDensity(planet.AtmoDensitySL));
		groundLWR.ion = LWR(gndGravExp, sInfo.mass, s.lifts.IonThrustForDensity(planet.AtmoDensitySL));
		groundLWR.proto = LWR(gndGravExp, sInfo.mass, s.lifts.PrototechThrustForDensity(planet.AtmoDensitySL));
		groundLWR.hydro = LWR(gndGravExp, sInfo.mass, s.lifts.Max.hydro);
	}

	private void UpdateMaxGravitiesAndWarning()
	{

		double maxGNow = (sInfo.mass > 0) ? H.ms2_to_g(s.lifts.AtmoThrustForDensity(planet.AtmoDensitySL) + s.lifts.IonThrustForDensity(planet.AtmoDensitySL) + s.lifts.PrototechThrustForDensity(planet.AtmoDensitySL) + s.lifts.Max.hydro) / (sInfo.mass * c.LWRsafetyfactor * (1 + c.LWRoffset)) : 0;

		warnState = (maxGNow < H.ms2_to_g(gndGravExp)) ? WarnType.Bad : WarnType.Good;
	}


	private void UpdateSpeedSPInGravity()
	{

		// See tech doc §3.1

		double tempSP;

		if (gndAlt < GroundRadar.UNDEF)
		{

			if (gndAlt > c.finalSpeedAltitude)
			{

				// Above the limit where we switch to constant speed

				if (prof.IsValid())
				{

					// If we have a valid profile, use it !
					tempSP = -prof.InterpolateSpeed(slAlt);
					spdSrc = SPSrc.Profile;

				}
				else if (lwrTargetSelected > 1)
				{

					// If not, and the ship seems to have adequate LWR, use the altitude/gravity formula
					tempSP = -Math.Sqrt(2 * (gndAlt - c.finalSpeedAltitude) * (lwrTargetSelected - 1) * gndGravExp) - c.finalSpeed;
					spdSrc = SPSrc.AltGravFormula;

				}
				else
				{

					// If the ship doesn't seem to have adequate LWR, then we abort the landing
					// and stop where we are (if possible).
					tempSP = -c.finalSpeed;
					spdSrc = SPSrc.Unable;
				}

			}
			else
			{

				// Below the limit, we switch to constant speed

				tempSP = -c.finalSpeed;
				spdSrc = SPSrc.FinalSpeed;
			}

		}
		else
		{

			// If we don't have altitude information, use temporarily the gravity formula
			tempSP = -c.vSpeedDefault * (lwrTargetSelected - 1) / H.ms2_to_g(gravNow);
			spdSrc = SPSrc.GravFormula;

		}

		tempSP = H.NotNan(tempSP);

		horizlimit=false;

		// If we have significant horizontal speed, reduce vertical speed for a safer landing
		// The idea is that the vertical speed set-point computed above corresponds to some allowed kinetic energy
		// for the current altitude, and we must remove from that the excess kinetic energy from horizontal speed.
		double horizExcess = Math.Pow(Math.Max(0, Math.Abs(spd.f) - GroundRadar.HORIZ_MAX_SPEED), 2)
										+ Math.Pow(Math.Max(0, Math.Abs(spd.l) - GroundRadar.HORIZ_MAX_SPEED), 2);

		if (horizExcess > 0)
		{
			tempSP = -Math.Sqrt(tempSP * tempSP - horizExcess);
			horizlimit=true;
		}

		tempSP = H.NotNan(tempSP);

		// Give time for GPS guidance to align with the target
		// Dist() is always positive, we do +0.1 to avoid division by zero
		if (GPS) 
		{
			double GPSmaxSpeed = gpsGuide.RecoVertSpeed(lvlr.Pitch, lvlr.Roll, gndAlt);
			if (tempSP < -GPSmaxSpeed)
			{
				tempSP = -GPSmaxSpeed;
				horizlimit=true;
			}
		}

		spdSP.v = H.SatMinMax(Math.Max(tempSP, speedTgt.Limit(tempSP)), -c.vSpeedMax, -c.finalSpeed);
	}

	private void UpdateSpeedSPInSpace()
	{
		// EXPERIMENTAL

		// speed = accel * time
		// position = 1/2 * accel * time²
		// time = sqrt(2 * position / accel)
		// speed = sqrt(2 * accel *position)
		if (gndAlt < GroundRadar.UNDEF - 5)
		{

			// Consider an acceleration linked to the ship capability, up to some limit
			double accel = Math.Min(s.lifts.Eff.Total / sInfo.mass * c.mode5ThrustRatio, c.accelLimit);

			if (gndAlt > c.finalSpeedAltitude)
			{
				// Compute for constant deceleration up to the transition altitude, where the ship should be stopped
				spdSP.v = -Math.Sqrt(2 * accel * (gndAlt - c.finalSpeedAltitude));
				spdSP.v = Math.Max(spdSP.v, -c.mode5MaxSpeed);
				spdSrc = SPSrc.RDV;
			}
			else
			{
				spdSP.v = 0;
				spdSrc = SPSrc.None;
			}

		}
		else
		{
			spdSP.v = 0;
			spdSrc = SPSrc.None;
		}
	}

	/// <summary>
	/// Update the ship speeds in the vertical, forward and left direction, considering the gravity direction.
	/// </summary>
	private void UpdateShipSpeedsInGravity()
	{
		Vector3D normlinvel = Vector3D.Normalize(s.Ctrller.GetShipVelocities().LinearVelocity);
		Vector3D normal_gravity = -Vector3D.Normalize(s.Ctrller.GetNaturalGravity());

		double shipSpeed = s.Ctrller.GetShipSpeed();

		spd.v = H.NotNan(Vector3D.Dot(normlinvel, normal_gravity)) * shipSpeed;
		spd.f = H.NotNan(Vector3D.Dot(normlinvel, Vector3D.Cross(normal_gravity, s.Ctrller.WorldMatrix.Right)) * shipSpeed);
		spd.l = H.NotNan(Vector3D.Dot(normlinvel, Vector3D.Cross(normal_gravity, s.Ctrller.WorldMatrix.Forward)) * shipSpeed);

	}

	private void UpdateShipSpeedsInSpace()
	{

		Vector3D velocities = s.Ctrller.GetShipVelocities().LinearVelocity;

		spd.v = velocities.Dot(s.Ctrller.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Up));
	}

	/// <summary>
	/// Updates the horizontal speed set-points using a combination of the recommendations
	/// from the ground radar and the side scan.
	/// </summary>
	private void UpdateHorizSpeedSetPoint()
		{

		if (Angle || HorizThr)
		{
			if (GPS)
			{
				// If the GPS guidance is enabled, we use it as the main source for horizontal speed set-point, and we limit it to a safe value
				spdSP.f = gpsGuide.RecoFwdSpeed(lvlr.Pitch, gndAlt, Angle);
				spdSP.l = gpsGuide.RecoLeftSpeed(lvlr.Roll, gndAlt, Angle);
			}
			else
			{
				// If not, we use the radar recommendations, which are based on the current terrain and obstacles
				fwdSpdTgt.AddValue(rdr.RecoFwdSpeed());
				leftSpdTgt.AddValue(rdr.RecoLeftSpeed());
				spdSP.f = fwdSpdTgt.Get();
				spdSP.l = leftSpdTgt.Get();
				
			}
		}
		else
		{
			spdSP.l = spdSP.f = 0;
		}

			// If the player provides inputs, we override the recommanded speed from the radar
			var moveIndicator = s.Ctrller.MoveIndicator;
			if (moveIndicator.Z > 0.1f)
				spdSP.f = -10;
			else if (moveIndicator.Z < -0.1f)
				spdSP.f = 10;

			if (moveIndicator.X > 0.1f)
				spdSP.l = -10;
			else if (moveIndicator.X < -0.1f)
				spdSP.l = 10;
		}

	/// <summary>
	/// Updates the gndAltitude (ground altitude), slAltitude (sea level altitude) and
	/// gnd_sl_offset (difference between them) using a combination of the altitude returned
	/// by the ship controller API and the ground radar.
	/// </summary>
	private void UpdateAltitude()
	{

		// See tech doc §4.1

		double ctrllerAltSurf, radarAlt;

		bool ctrllerAltSurfValid = s.Ctrller.TryGetPlanetElevation(MyPlanetElevation.Surface, out ctrllerAltSurf);

		if (rdr.exists && rdr.valid && rdr.active)
		{

			radarAlt = rdr.GetDistance();

			// Because the radar cannot update each tick, we combine with the
			// ship controller altitude between each radar scan. At the moment
			// of the radar scan (age of the scan = 0), we compute the offset
			// between the two sources, and in between scans (age > 0) we apply
			// this offset to the controller altitude (if we have one)

			// This is needed when the ship has some horizontal speed, because the
			// distance to the rayscan hit no longer represents the correct distance
			// to the ground immediately below the ship.

			if (ctrllerAltSurfValid)
			{
				if (rdr.altAge <= 1)
					radarOffset = radarAlt - ctrllerAltSurf;
				altFilter.AddValue(ctrllerAltSurf + radarOffset);
			}
			else
			{
				altFilter.AddValue(radarAlt);
			}
			gndAlt = altFilter.Get();
			altSrc = AltSrc.Radar;

		}
		else if (ctrllerAltSurfValid)
		{

			gndAlt = ctrllerAltSurf;
			altSrc = AltSrc.Ground;

		}
		else
		{

			gndAlt = GroundRadar.UNDEF;
			altSrc = AltSrc.Undef;
		}

		gndAlt -= c.altitudeOffset;

		// Update the altitude offset to the sea level
		// If we don't have a valid sea level altitude from the controller
		// then we use a default one.

		double ctrllerAltSl;

		bool ctrllerAltSlValid = s.Ctrller.TryGetPlanetElevation(MyPlanetElevation.Sealevel, out ctrllerAltSl);

		gndSlOffset = (ctrllerAltSlValid && ctrllerAltSurfValid) ? ctrllerAltSl - ctrllerAltSurf : c.defaultASLmeters;

		slAlt = gndAlt + gndSlOffset;

	}




	private void UpdateLWRTarget()
	{

		// Update the various LWR targets,for the current conditions as well as expected conditions on the planet surface

		double lwrTargetHere = ComputeLWRTarget(gravNow, Mode, currentLWR);

		double lwrTargetGnd = ComputeLWRTarget(gndGravExp, Mode, groundLWR);

		lwrTargetSelected = H.Mix(lwrTargetGnd, lwrTargetHere, c.LWR_mix_gnd_ratio);
	}

	private void UpdatePlanetAtmo()
	{

		// Estimate outside atmo density and update the planet object with the best guess unless a precise planet is defined

		double parachute_density = (s.parachutes.Count > 0 && s.parachutes[0].Atmosphere > 0.01) ? (double)s.parachutes[0].Atmosphere : -1;
		double athrusters_density = (s.lifts.Max.atmo > 0 && s.lifts.Eff.atmo > 1) ? s.lifts.Eff.atmo / s.lifts.Max.atmo * 0.7 + 0.3 : -1;
		double ithrusters_density = (s.lifts.Max.ion > 0 && s.lifts.Eff.ion > 1) ? (1 - s.lifts.Eff.ion / s.lifts.Max.ion) / 0.8 : -1;
		double pthrusters_density = (s.lifts.Max.proto > 0 && s.lifts.Eff.proto > 1) ? (1 - s.lifts.Eff.proto / s.lifts.Max.proto) / 0.7 : -1;
		obsDensity = H.Max(parachute_density, athrusters_density, ithrusters_density, pthrusters_density);

		bool found;
		// If disabled at high altitude, we need to set the planet to unknown
		if (Mode == 0 && gndAlt > 10000)
			planet = catalog.get_planet("unknown", out found);

		if (planet.Precise == false)
		{

			if (planet.Shortname == "unknown") planet.AtmoDensitySL = s.lifts.WorstDensity();

			if (planet.Shortname == "atmo") planet.AtmoDensitySL = Math.Max(planet.AtmoDensitySL, s.lifts.WorstDensity());

			if (obsDensity > -1)
			{
				if (obsDensity > 0.8 && gndAlt < 10000)
				{
					planet = catalog.get_planet("dynatmo", out found);
					planet.AtmoDensitySL = Math.Max(obsDensity, s.lifts.WorstDensity());
				}

				if (obsDensity < 0.2 && gndAlt < 1000)
				{
					planet = catalog.get_planet("dynvacuum", out found);
				}
			}
		}
	}

	/// <summary>
	/// Estimates the surface gravity with what's available and update the value
	/// in planet.g_sealevel
	/// </summary>
	private void ComputeSurfaceGravityEstimate()
	{

		if (planet.Precise)
		{

			// If the planet is selected by the pilot, we use that directly
			gravSrc = GravSrc.Identified;
			gndGravExp = H.g_to_ms2(planet.GSeaLevel);

		}
		else
		{

			double gnd_estimate = H.Interpolate(0, 1, gravNow, estim.gBest, estim.confBest);
			gravSrc = (estim.confBest > 0.9) ? GravSrc.Estimate : GravSrc.Undef;
			gndGravExp = Math.Max(gravNow, H.Interpolate(c.gravTransLow, c.gravTransHigh, gravNow, gnd_estimate, gndAlt));
			planet.GSeaLevel = H.ms2_to_g(gndGravExp);

			if (gndAlt < c.gravTransLow)
				gravSrc = GravSrc.Local;
		}
	}

	public void EnableLeveler()
	{
		Level = true;
		lvlr.Enable();
	}

	public void DisableLeveler()
	{
		Level = false;
		lvlr.Disable();
	}

	public void SwitchLeveler()
	{
		if (Level) DisableLeveler();
		else EnableLeveler();
	}

	public void EnableThrust()
	{
		HorizThr = true;
	}

	public void DisableThrust()
	{
		HorizThr = false;
		horizThr.Disable();
	}

	public void SwitchThrust()
	{
		if (HorizThr) DisableThrust();
		else EnableThrust();
	}

	public void EnableAngle()
	{
		Angle = true;
	}

	public void DisableAngle()
	{
		Angle = false;
	}

	public void SwitchAngle()
	{
		if (Angle) DisableAngle();
		else EnableAngle();
	}

	public void EnableGPS(string data)
	{
		GPScancelled = false;
		GPS = gpsGuide.UpdateTargetFromGPS(data);
		if (gpsGuide.ShouldCancel(lvlr.Pitch, lvlr.Roll, gndAlt))
		{
			GPS = false;
			GPScancelled = true;
		}
	}

	public void DisableGPS()
	{
		GPS = false;
		GPScancelled = false;
	}

	public void SwitchGPS(string data)
	{
		if (GPS) DisableGPS();
		else EnableGPS(data);
	}

	public void M4IncreaseSpeed()
	{
		AP.m4SpdSP += 5;
	}

	public void M4DecreaseSpeed()
	{
		AP.m4SpdSP = Math.Max(AP.m4SpdSP - 5, 0);
	}

	public void M4IncreaseAltitude()
	{
		AP.m4AltSP += 10;
	}

	public void M4DecreaseAltitude()
	{
		AP.m4AltSP = Math.Max(AP.m4AltSP - 10, 0);
	}

	public void M4AltSwitch()
	{
		if (AP.altMode == AutoPilot.AltMode.Ground)
		{
			M4AltSL();
		}
		else
		{
			M4AltGND();
		}
	}

	public void M4AltGND()
	{
		AP.altMode = AutoPilot.AltMode.Ground;
		AP.m4AltSP = gndAlt;
		AP.altFilter.Set(gndAlt);
	}

	public void M4AltSL()
	{
		AP.altMode = AutoPilot.AltMode.SeaLevel;
		AP.m4AltSP = slAlt;
		AP.altFilter.Set(slAlt);
	}




	// ------------------------------
	// PRIVATE METHODS WITH SIDE-EFFECTS ON THE SHIP
	// (they perfom actions on the ship blocks)
	// ------------------------------

	// Setup the LCDs (display type, font size etc.)
	private void SetUpLCDs()
	{

		foreach (IMyTextSurface d in s.MainDisplays)
		{
			//d.Enabled = true;
			d.ContentType = ContentType.SCRIPT;
			d.Script = "None";
			d.ScriptBackgroundColor = VRageMath.Color.Black;
		}

		foreach (IMyTextSurface d in s.DebugDisplays)
		{
			//d.Enabled = true;
			d.ContentType = ContentType.TEXT_AND_IMAGE;
			d.Font = "Monospace";
			d.FontColor = VRageMath.Color.White;
			d.FontSize = 0.40f;
		}

	}

	public void UpdateDebugDisplays()
	{

		foreach (IMyTextSurface d in s.DebugDisplays)
		{

			// Compact debug info
			var sb = new StringBuilder();
			sb.AppendLine("-- SLM debug --");
			sb.AppendLine(runtime.RunTimeString());
			sb.AppendLine($"Density: {obsDensity:0.00} (cat){planet.AtmoDensitySL:0.00}");
			sb.AppendLine(sInfo.DebugString());
			sb.AppendLine($"LWR tgt: {lwrTargetSelected:0.00}");
			sb.AppendLine($"cmd: {lwrCmd:0.00} {thrCmd:0.00} N");
			sb.AppendLine($"Alt: {slAlt:0.0}, SL offset {gndSlOffset:000}m");
			sb.AppendLine(lvlr.DebugString());
			sb.AppendLine(estim.DebugString());
			sb.AppendLine(rdr.AltitudeDebugString());
			sb.AppendLine(rdr.TerrainDebugString());
			sb.AppendLine("[VERT PID] " + vertPID.DebugString());
			sb.AppendLine(s.lifts.DebugString());
			sb.AppendLine(horizThr.DebugString());
			sb.AppendLine(AP.DebugString());
			sb.AppendLine(gpsGuide.DebugString(lvlr.Pitch, lvlr.Roll, gndAlt));
			sb.AppendLine(prof.DebugString());
			d.WriteText(sb.ToString());
		}

	}

	// Update the main displays
	public void UpdateDisplays()
	{

		const float PLMARGIN = 5, PTMARGIN = 5, PBMARGIN = 35;
		const float HMAX = 20;
		VRageMath.Color GRAY = VRageMath.Color.Gray, WHITE = VRageMath.Color.White, RED = VRageMath.Color.Red, YELLOW = VRageMath.Color.Yellow, CYAN = VRageMath.Color.Cyan, GREEN = VRageMath.Color.Green, BLUE = VRageMath.Color.Blue;
		const TextAlignment LEFT = TextAlignment.LEFT;
		const TextAlignment CENTER = TextAlignment.CENTER;

		blink = !blink;

		foreach (IMyTextSurface d in s.MainDisplays)
		{

			VRageMath.RectangleF view;

			float speed, speed_scale, alt_scale, xpos, ypos;

			view = new VRageMath.RectangleF(
				(d.TextureSize - d.SurfaceSize) / 2f,
				d.SurfaceSize
			);

			var vp = view.Position;

			// First, we adapt drawing to screen size

			float width = d.SurfaceSize[0], height = d.SurfaceSize[1];
			float LEFT_MARGIN, PLEFT, PRIGHT, HLEFT, HTOP, VLEFT, VTOP, TTOP, PTOP, PBOTTOM, HVER, ALEFT, ATOP, SLEFT, STOP, Tsize, HSIZE, THR_CUR_W, THR_MAX_W, THR_SW_W;
			bool showDetails = false, compact = false;

			Tsize = 1f;

			if (width > 400) 
			{
				LEFT_MARGIN = 40;
				PLEFT = 150;
				PRIGHT = 30;
				HLEFT = 160;
				VLEFT = 40;
				THR_CUR_W = 20;
				THR_MAX_W = 5;
				THR_SW_W = 5;
			}
			else
			{
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



			if (height > 300)
			{
				TTOP = 10;
				PTOP = 70;
				PBOTTOM = height - 112;
				HSIZE = 40;
				HVER = PBOTTOM + HSIZE + 10;
				VTOP = HTOP = height - 100;
				showDetails = true;
				ALEFT = PLEFT + 5;
				ATOP = (PTOP + PBOTTOM) / 2 - 10;
				STOP = PBOTTOM - PBMARGIN - 20;
				SLEFT = (PLEFT + width - PRIGHT) / 2 - 20;
			}
			else
			{
				TTOP = 5;
				PTOP = 55;
				PBOTTOM = height - 70;
				HSIZE = 28;
				HVER = PBOTTOM + HSIZE + 4;
				VTOP = HTOP = height - 70;
				ALEFT = PLEFT + 5;
				ATOP = (PTOP + PBOTTOM) / 2 - 30;
				STOP = (PTOP + PBOTTOM) / 2 - 30;
				SLEFT = (PLEFT + width - PRIGHT) / 2 - 5;
				Tsize = 0.75f;
			}

			if (width < 300)
			{
				compact = true;
				PLEFT = 80;
				HLEFT = 85;
				THR_CUR_W = 15;
				THR_MAX_W = 3;
				THR_SW_W = 3;
				Tsize = 0.65f;
				ALEFT = PLEFT + 5;
				SLEFT = ALEFT + 45;

			}

			var frm = d.DrawFrame();

			// BLOCK 1 : TOP INFORMATION

			frm.Add(TextSprite(
				"Soft Landing Manager\n" + planet.Name + " (g=" + seconfig.GravExp.ToString() + ")",
				width / 2,
				TTOP,
				view,
				WHITE,
				CENTER,
				Tsize));

			// BLOCK 2 : THRUST AND GRAVITY INDICATION

			// THRUST INDICATION

			// Thrust scaling in px/(m/s²)
			float THR_SCALE = (PBOTTOM - PTOP) / 40;

			// Helper method to draw thrust bars
			// list[0] = value A
			// list[1] = value B
			// list[2] = value C
			// list[3] = value D 
			// list[4] = x position
			// list[5] = width
			Action<List<double>, VRageMath.Color, VRageMath.Color, VRageMath.Color, VRageMath.Color> drawThrustBars = (list, colorA, colorB, colorC, colorD) =>
			{
				float Aref = (float)(list[0] / sInfo.mass) * THR_SCALE;
				MySprite sA = MySprite.CreateSprite(
					"SquareSimple",
					new Vector2((float)list[4], PBOTTOM - Aref / 2) + vp,
					new Vector2((float)list[5], Aref)
				);
				sA.Color = colorA;
				frm.Add(sA);

				float Bref = (float)(list[1] / sInfo.mass) * THR_SCALE;
				MySprite sB = MySprite.CreateSprite(
					"SquareSimple",
					new Vector2((float)list[4], PBOTTOM - Aref - Bref / 2) + vp,
					new Vector2((float)list[5], Bref)
				);
				sB.Color = colorB;
				frm.Add(sB);

				float Cref = (float)(list[2] / sInfo.mass) * THR_SCALE;
				MySprite sC = MySprite.CreateSprite(
					"SquareSimple",
					new Vector2((float)list[4], PBOTTOM - Aref - Bref - Cref / 2) + vp,
					new Vector2((float)list[5], Cref)
				);
				sC.Color = colorC;
				frm.Add(sC);

				float Dref = (float)(list[3] / sInfo.mass) * THR_SCALE;
				MySprite sD = MySprite.CreateSprite(
					"SquareSimple",
					new Vector2((float)list[4], PBOTTOM - Aref - Bref - Cref - Dref / 2) + vp,
					new Vector2((float)list[5], Dref)
				);
				sD.Color = colorD;
				frm.Add(sD);
			};

			// Draw the thrust bars for the current and max thrust
			List<List<double>> data = new List<List<double>>();
			data.Add(new List<double> { s.lifts.Now.atmo, s.lifts.Now.proto, s.lifts.Now.ion, s.lifts.Now.hydro, LEFT_MARGIN + THR_CUR_W / 2 + 5, THR_CUR_W });
			data.Add(new List<double> { s.lifts.Eff.atmo, s.lifts.Eff.proto, s.lifts.Eff.ion, s.lifts.Eff.hydro, LEFT_MARGIN + THR_CUR_W + THR_MAX_W / 2 + 10, THR_MAX_W });

			foreach (List<double> list in data)
				drawThrustBars(list, GREEN, VRageMath.Color.DarkBlue, BLUE, RED);

			// Draw the thrust bars for the density sweep (the order is not the same)
			double sweep_left = LEFT_MARGIN + THR_CUR_W + THR_MAX_W + 15;
			List<List<double>> data2 = new List<List<double>>();
			for (int i = 0; i < 11; i++)
			{
				data2.Add(new List<double> { s.lifts.Eff.hydro, s.lifts.pTDensity[i], s.lifts.iTDensity[i], s.lifts.aTDensity[i], sweep_left + THR_SW_W / 2 + i * THR_SW_W, THR_SW_W });
			}

			foreach (List<double> list in data2)
				drawThrustBars(list, RED, VRageMath.Color.DarkBlue, BLUE, GREEN);

			// Gravity estimate
			if (gravSrc != GravSrc.Undef)
			{
				float grav_x = (float)(sweep_left + THR_SW_W / 2 + Math.Min(planet.AtmoDensitySL, 1) * 10 * THR_SW_W);
				frm.Add(MySprite.CreateSprite
				(
					"SquareSimple",
					new Vector2(grav_x, PBOTTOM - (float)gndGravExp * THR_SCALE) + vp,
					new Vector2(10, 10)
				));

				frm.Add(TextSprite(
					H.ms2_to_g(gndGravExp).ToString("0.00") + "g",
					(float)sweep_left,
					PBOTTOM - (float)gndGravExp * THR_SCALE - 50,
					view,
					warnState == WarnType.Bad ? RED : WHITE,
					LEFT,
					Tsize));
			}

			// Current observed density
			if (obsDensity > -1)
			{
				float dens_x = (float)(sweep_left + THR_SW_W / 2 + Math.Min(obsDensity, 1) * 10 * THR_SW_W);
				frm.Add(MySprite.CreateSprite
				(
					"SquareSimple",
					new Vector2(dens_x, PBOTTOM - 5) + vp,
					new Vector2(2, 10)
				));
			}

			// Gravity scale (dots each 1G)
			for (int i = 0; i < 5; i++)
			{
				frm.Add(MySprite.CreateSprite
				(
					"SquareSimple",
					new Vector2(LEFT_MARGIN, PBOTTOM - (float)H.g_to_ms2(i) * THR_SCALE) + vp,
					new Vector2(5, 5)
				));
			}

			// Bottom Line
			frm.Add(MySprite.CreateSprite
			(
				"SquareSimple",
				new Vector2(50 + LEFT_MARGIN, PBOTTOM) + vp,
				new Vector2(100, 2)
			));


			// BLOCK 3 SPEED PROFILE

			// HHOR : horizontal position of the center of the horizontal speed display
			// HVER : vertical position
			float HHOR = width - PRIGHT - HSIZE;
			float HSCALE = HSIZE / HMAX;
			float speed_ref, alt_ref;

			// Scale the profile display for various altitudes

			if (gndAlt < 1600)
			{
				speed_ref = 300;
				alt_ref = 2000;
			}
			else if (gndAlt < 6400)
			{
				speed_ref = 400;
				alt_ref = 8000;
			}
			else if (gndAlt < 25600)
			{
				speed_ref = 550;
				alt_ref = 32000;
			}
			else
			{
				speed_ref = 550;
				alt_ref = 200000;
			}
			speed_scale = speed_ref / (width - PLEFT - PRIGHT);
			alt_scale = alt_ref / (PBOTTOM - PTOP);

			if (showDetails)
			{

				if (Mode == 1 || Mode == 2)
				{
					if (prof.IsValid() && prof.IsComputed())
					{
						using(frm.Clip((int)(PLEFT+vp.X),(int)(PTOP+vp.Y), (int)(width - PRIGHT - PLEFT), (int)(PBOTTOM - PTOP)))
						{
							// Draw the profile with crosses
							for (int i = 0; i < prof.altSl.Count() - 1; i++)
							{
								speed = (float)Math.Min(prof.vertSpeed[i], c.vSpeedMax);
								ypos = PBOTTOM - 70 - ((float)(prof.altSl[i] - gndSlOffset) / alt_scale);
								xpos = width - PRIGHT - 20 - speed / speed_scale;

								
									frm.Add(new MySprite()
									{
										Type = SpriteType.TEXT,
										Data = "+",
										Position = new Vector2(xpos, ypos) + vp,
										RotationOrScale = 1.5f,
										Color = new VRageMath.Color((float)prof.hRatio[i], (float)prof.aRatio[i], (float)prof.iRatio[i]),
										Alignment = CENTER,
										FontId = "White"
									});
								}
						}

						// H2 margin

						double h2_capa = sInfo.H2_capa_liters();

						if (h2_capa > 0 && s.lifts.Max.hydro > 0)
						{
							double h2_margin = (sInfo.H2_stored_liters() - prof.InterpolateH2Used(slAlt))/ h2_capa * 100;

							frm.Add(TextSprite(
								"H2 Margin : " + h2_margin.ToString("00") + "%",
								width - PRIGHT - 200,
								PTOP + PTMARGIN,
								view,
								(h2_margin > c.h2MarginWarning) ? WHITE : RED,
								LEFT));
						}

						if (panic)
						{
							frm.Add(new MySprite()
							{
								Type = SpriteType.TEXT,
								Data = "PANIC",
								Position = new Vector2(width / 2, 170) + vp,
								RotationOrScale = 2f,
								Color = RED,
								FontId = "White"
							});
						}

						// Legend

						frm.Add(TextSprite(
							((width - PRIGHT - PLEFT) * speed_scale).ToString("000") + "m/s",
							PLEFT + PLMARGIN,
							PBOTTOM - PBMARGIN,
							view,
							GRAY,
							LEFT));

						frm.Add(TextSprite(
							((PBOTTOM - PTOP) * alt_scale).ToString("000") + "m",
							PLEFT + PLMARGIN,
							PTOP + PTMARGIN,
							view,
							GRAY,
							LEFT));
					}
					else if (prof.IsComputed())
					{
						frm.Add(TextSprite(
							"Unable to compute\nvalid landing profile",
							(PLEFT + width - PRIGHT) / 2,
							PTOP,
							view,
							VRageMath.Color.Orange,
							CENTER));
					}
					else
					{
						frm.Add(TextSprite(
							"No profile computed",
							(PLEFT + width - PRIGHT) / 2,
							PTOP + 25,
							view,
							WHITE,
							CENTER));
					}
				}
				else if (Mode == 3)
				{

					frm.Add(TextSprite(
							"Fly with keyboard\n keys",
							(PLEFT + width - PRIGHT) / 2,
							PTOP + 25,
							view,
							WHITE,
							CENTER));
				}
				else if (Mode == 4)
				{
					frm.Add(TextSprite(
							"Use PB cmd to\nchange speed\n & altitude",
							(PLEFT + width - PRIGHT) / 2,
							PTOP + 25,
							view,
							WHITE,
							CENTER));
				}
				else if (Mode == 5)
				{
					frm.Add(TextSprite(
							"Point at asteroid\nor ship to\n rendez-vous.",
							(PLEFT + width - PRIGHT) / 2,
							PTOP + 25,
							view,
							WHITE,
							CENTER));
				}
				else
				{
					List<string> strinfo = new List<string> { H.Truncate(s.Ctrller.CubeGrid.DisplayName, 20), Math.Round(sInfo.mass) + "kg" };
					for (int i = 0; i < strinfo.Count; i++)
					{
						frm.Add(TextSprite(
							strinfo[i],
							PLEFT + 5,
							PTOP + 5 + i * 20,
							view,
							GRAY,
							LEFT));
					}
				}
			}

			// Yellow marker for the current altitude/speed

			ypos = PBOTTOM - 70 - (float)gndAlt / alt_scale;
			xpos = width - PRIGHT - 20 + (float)spd.v / speed_scale;

			if (spdSrc == SPSrc.Profile && showDetails)
			{
				using(frm.Clip((int)(PLEFT+vp.X),(int)(PTOP+vp.Y), (int)(width - PRIGHT - PLEFT), (int)(PBOTTOM - PTOP)))
				{
					frm.Add(new MySprite()
					{
						Type = SpriteType.TEXT,
						Data = "O",
						Position = new Vector2(xpos, ypos) + vp,
						RotationOrScale = 2f,
						Color = YELLOW,
						Alignment = CENTER,
						FontId = "White"
					});
				}
			}

			// Speed indicators

			if ((Mode == 1) || (Mode == 2) || (Mode == 5) || (Mode == 6))
				frm.Add(TextSprite(
					H.Cpct(-spd.v) + "m/s",
					SLEFT,
					STOP,
					view,
					YELLOW,
					LEFT,
					Tsize));

			if ((Mode == 1) || (Mode == 2) || (Mode == 5) || (Mode == 6))
				frm.Add(TextSprite(
					H.Cpct(-spdSP.v) + "m/s",
					SLEFT,
					STOP + 20,
					view,
					CYAN,
					LEFT,
					Tsize));

			// Altitude indicators

			string alt_txt = "";

			if (Mode == 4 && AP.altMode == AutoPilot.AltMode.SeaLevel)
			{
				alt_txt = slAlt.ToString("000") + "m (SL)";
			}
			else
			{
				alt_txt = gndAlt < GroundRadar.UNDEF ? gndAlt.ToString("000") + "m" : (rdr.exists ? (rdr.active ? "INIT" : "XXX") : "XXX");
			}

			frm.Add(TextSprite(
				alt_txt,
				ALEFT,
				ATOP,
				view,
				YELLOW,
				LEFT,
				Tsize));

			if (Mode == 4)
			{
				string alt_sp_text = AP.m4AltSP.ToString("000") + "m";
				if (AP.altMode == AutoPilot.AltMode.SeaLevel)
					alt_sp_text += " (SL)";
				frm.Add(TextSprite(
					alt_sp_text,
					ALEFT,
					ATOP + 20,
					view,
					CYAN,
					LEFT,
					Tsize));
			}


			// Draw the rectangle around the speed profile, with a color depending on the warning state
			VRageMath.Color bColor;
			if (warnState == WarnType.Bad || spdSrc == SPSrc.Unable)
			{
				bColor = RED;
			}
			else if (marginal >= c.marginalWarn)
			{
				bColor = VRageMath.Color.OrangeRed;
			}
			else
			{
				bColor = WHITE;
			}
			H.Rectangle(frm, PLEFT, width - PRIGHT, PTOP, PBOTTOM, view, 2, bColor);







			// GENERAL



			// BLOCK 4 : VERTICAL MODE INFORMATION

			string info;
			switch (spdSrc)
			{
				case SPSrc.None: info = "Disabled"; break;
				case SPSrc.Profile: info = "Profile"; break;
				case SPSrc.AltGravFormula: info = "Alt/grav"; break;
				case SPSrc.GravFormula: info = "Gravity"; break;
				case SPSrc.FinalSpeed: info = "Final"; break;
				case SPSrc.Unable: info = "Unable"; break;
				case SPSrc.Hold: info = "Alt Hold"; break;
				case SPSrc.RDV: info = "Rendezvous"; break;
				case SPSrc.Escape: info = "Escape"; break;
				default: info = "Unknown"; break;
			}
			if (horizlimit)
				info += "*";

			frm.Add(TextSprite(
				"VERTICAL\nMode " + Mode + "\n" + info,
				VLEFT,
				VTOP,
				view,
				WHITE,
				LEFT,
				Tsize));

			// BLOCK 5 : HORIZONTAL MODE AND SPEED

			// Draw a rectangle for the speed scale

			H.Rectangle(frm, HHOR - HSIZE, HHOR + HSIZE, HVER + HSIZE, HVER - HSIZE, view, 2, WHITE);

			

			if ((Mode == 1) || (Mode == 2) || (Mode == 3) || Mode == 4)
			{

				// Speed limits provided by the side scanner

				H.Rectangle(frm,
					HHOR -(float)H.SatMinMax(rdr.spdLeftLim, -HMAX, HMAX) * HSCALE,
					HHOR - (float)H.SatMinMax(rdr.spdRightLim, -HMAX, HMAX) * HSCALE,
					HVER - (float)H.SatMinMax(rdr.spdFwdLim, -HMAX, HMAX) * HSCALE,
					HVER - (float)H.SatMinMax(rdr.spdRearLim, -HMAX, HMAX) * HSCALE,
					view, 1, WHITE);

				// Horiz Speed Set-Point in cyan

				var ssp = MySprite.CreateSprite
				(
					"SquareSimple",
					new Vector2(HHOR - (float)H.SatMinMax(spdSP.l, -HMAX, HMAX) * HSCALE,
								HVER - (float)H.SatMinMax(spdSP.f, -HMAX, HMAX) * HSCALE) + vp,
					new Vector2(12, 12)
				);
				ssp.Color = CYAN;
				frm.Add(ssp);

				if ((Mode == 3) || ((Mode == 4) && !((Math.Abs(spdSP.f) < Math.Abs(AP.m4SpdSP)) && blink)))
				{
					frm.Add(TextSprite(
						Mode == 3 ? spdSP.f.ToString("00") : AP.m4SpdSP.ToString("00"),
						HHOR,
						HVER + 5,
						view,
						CYAN,
						CENTER,
						Tsize));
				}
			}

			// Horiz speed now

			var snow = MySprite.CreateSprite
			(
				"SquareSimple",
				new Vector2(HHOR - (float)H.SatMinMax(spd.l, -HMAX, HMAX) * HSCALE,
							HVER - (float)H.SatMinMax(spd.f, -HMAX, HMAX) * HSCALE)
								+ vp,
				new Vector2(12, 12)
			);
			snow.Color = YELLOW;
			frm.Add(snow);

			if (rdr.obstruction)
				frm.Add(MySprite.CreateSprite
					(
						"Danger",
						new Vector2(HHOR, HVER) + vp,
						new Vector2(80, 80)
					));

			// Horizontal mode information

			string s1 = "",s2 = "";

			// s1 is the string to describe the horizontal control mode (angle +/or thrust), s2 is the string to describe the horizontal speed mode (GPS, avoidance or hover/speed hold)

			if (Angle == true && HorizThr == true)
				s1 = compact ? "G+T" : "Gyro(" + lvlr.MaxAngle().ToString("00") + "°)+thrust";
			else if (Angle == true && HorizThr == false)
				s1 = compact ? "G" : "Gyro(" + lvlr.MaxAngle().ToString("00") + "°) only";
			else if (Angle == false && HorizThr == true)
				s1 = compact ? "T" : "Thrusters only";
			else
				s1 = compact ? "Off" : "Disabled";

			if (Mode == 1 || Mode == 2)
			{
				if (GPS)
				{
					// If GPS is active, we show the target name if enough space is available, otherwise just "GPS"
					s2 = compact ? "GPS" : "GPS:"+gpsGuide.tgtName+ " (" + H.Cpct(gpsGuide.Dist(lvlr.Pitch, lvlr.Roll, gndAlt)) + "m)";
				} 
				else
				{

					// If GPS is not active, we show the avoidance mode if any, otherwise "No avoidance"
					switch (rdr.mode)
					{
						case ScanMode.DbleStby: s2 = compact ? "SBY(D)" : "Standby (D)"; break;
						case ScanMode.SingStby: s2 = compact ? "SBY(S)" : "Standby (S)"; break;
						case ScanMode.SingNarr: s2 = compact ? "Simple" : "Simple avoidance"; break;
						case ScanMode.DbleEarly: s2 = compact ? "Early" : "Early avoidance"; break;
						case ScanMode.DbleWide: s2 = compact ? "Wide" : "Wide avoidance"; break;
						default: s2 = "No avoidance"; break;
					}

					if (GPScancelled)
					{
						s2 += compact ? " !GPS" : " (GPS cancelled)";
					}
				}

			}
			else if (Mode == 3)
			{
				s2 = compact ? "Hover" : "Hover Mode";
			}
			else if (Mode == 4)
			{
				s2 = compact ? "Speed" : "Speed Hold";
			}

			frm.Add(TextSprite(
				"HORIZ\n" + s1 + "\n" + s2,
				HLEFT,
				HTOP,
				view,
				WHITE,
				LEFT,
				Tsize));

			frm.Dispose();
		}
	}

	private void ApplyThrustOverrideInGravity(double PIDoutput)
	{
		const double LWR_HELP_GPS = 0.5;
		// See tech doc §8

		// PIDoutput is in units of lift to weight ratio (no unit)

		// Compute the total thrust wanted
		// We apply some feedforward : when the speed delta is zero, the thrust command should
		// perfectly compensate the ship weight.
		lwrCmd = PIDoutput + H.InterpolateSmooth(-5, 5, 0, 2, vSpeedDelta);

		// If the GPS guide requires the lifters because the ship lateral thrust is not sufficient, we apply a minimum thrust command so that tilting the ship is effective.
		if (GPS && gpsGuide.NeedLifters() && gndAlt != GroundRadar.UNDEF && Angle)
			lwrCmd = Math.Max(lwrCmd, LWR_HELP_GPS);

		thrCmd = lwrCmd * shipWeight;

		// If the thrust wanted is higher than the total thrust available, we are in a marginal situation
		if ((thrCmd > s.lifts.Eff.Total || vSpeedDelta > 5) && spdSP.v < 0 && marginal < c.marginalMax)
		{
			marginal++;
		}
		else if (marginal > 0)
		{
			marginal--;
		}

		// Minimum atmospheric thrust in mode 1 (Newtons)
		double min_athrust = (Mode == 1) ?
			H.Interpolate(-c.mode1AtmoSpeed, -c.mode1AtmoSpeed + 5, shipWeight, 0, spd.v) : 0;
		// Minimum ion thrust in mode 1 (Newtons)
		double min_ithrust = 0;
		if (Mode == 1 && vSpeedDelta < 0)
		{
			min_ithrust = H.Interpolate(-c.mode1IonSpeed, -c.mode1IonSpeed + 5, shipWeight, 0, spd.v);
		}

		s.lifts.ApplyThrust(thrCmd, min_athrust, min_ithrust);
	}

	private void ApplyThrustOverrideInSpace(double PIDoutput)
	{
		// The PID output is usually in units of lift to weight ratio.
		// Because we're in zero gravity ship weight is zero, so we scale using 0.8 G (0.8 * 9.81m/s²)
		// Here we also apply feedforward but when the speed is the correct one then the command
		// should be zero thrust.
		const double ACCEL = 7.9;
		
		lwrCmd = PIDoutput + H.InterpolateSmooth(-5, 5, -1, 1, vSpeedDelta);
		
		if (lwrCmd >= 0)
		{
			// If the command is positive, we use the lifters
			thrCmd = lwrCmd * sInfo.mass * ACCEL;
			s.lifts.ApplyThrust(thrCmd, 0, 0);
			s.downThr.Disable();
		}
		else
		{
			// If the command is negative, we need use the "down" thrusters to pick up speed
			thrCmd = Math.Max(lwrCmd, -1) * sInfo.mass * ACCEL;
			s.downThr.ApplyThrust(-thrCmd, 0, 0);
			s.lifts.Disable();
		}
	}

	private void ManagePanicParachutes()
	{

		if ((Mode == 1 || Mode == 2) && (vSpeedDelta > gndAlt / c.panicRatio + c.panicDelta))
		{
			panic = true;
			foreach (IMyParachute parachute in s.parachutes)
			{
				parachute.OpenDoor();
			}
		}
		else
		{
			panic = false;
		}
	}

	private void ManageSoundBlocks()
	{

		foreach (IMySoundBlock sound in s.soundblocks)
		{

			if (panic)
			{
				sound.Enabled = true;
				sound.SelectedSound = "SoundBlockAlert2";
				sound.Play();
			}
			else if (warnState == WarnType.Bad || spdSrc == SPSrc.Unable)
			{
				sound.Enabled = true;
				sound.SelectedSound = "SoundBlockAlert1";
				sound.Play();
			}
		}
	}

	/// <summary>
	///  Trigger the landing and liftoff timers depending on altitude.
	/// </summary>
	private void ManageTimers()
	{

		if (gndAlt < c.landingTimerAltitude && allowLandingTimer)
		{

			foreach (IMyTimerBlock timer in s.landingTimers)
				timer.Trigger();
			allowLandingTimer = false;
			allowLiftoffTimer = true;
		}

		// Make sure that the liftoff triggers altitude is higher than the landing altitude
		if (gndAlt > Math.Max(c.liftoffTimerAltitude, c.landingTimerAltitude + 1) && allowLiftoffTimer && Mode != 5)
		{

			foreach (IMyTimerBlock timer in s.liftoffTimers)
				timer.Trigger();
			allowLiftoffTimer = false;
			allowLandingTimer = true;
		}
	}

	/// <summary>
	///  When the script is first started, arm either the landing of liftoff timer depending on altitude.
	/// </summary>
	private void InitLandingLiftoffTimers()
	{
		if (gndAlt < c.landingTimerAltitude)
		{
			allowLandingTimer = false;
			allowLiftoffTimer = true;
		}
		else
		{
			allowLandingTimer = true;
			allowLiftoffTimer = false;
		}
	}

	private void TriggerOnTimers()
	{
		foreach (IMyTimerBlock timer in s.onTimers)
			timer.Trigger();
	}

	private void TriggerOffTimers()
	{
		foreach (IMyTimerBlock timer in s.offTimers)
			timer.Trigger();
	}

	private void GearUnLock()
	{
		foreach (IMyLandingGear gear in s.gears)
		{
			gear.Unlock();
		}
	}

	// PRIVATE METHODS WITH NO SIDE-EFFECTS


	private double ComputeLWRTarget(double gravity, int mode, ThrustStat LWR)
	{

		if (gravity > 0)
		{
			double capableTWR = Math.Min(LWR.Total / c.LWRsafetyfactor - c.LWRoffset, c.LWRlimit);

			if (mode == 1)
			{
				return Math.Min(capableTWR, c.elecLwrSufficient);
			}
			else
			{
				return capableTWR;
			}

		}
		else
		{
			return 0;
		}
	}

	private double LWR(double gravity, double shipmass, double thrust)
	{
		return (gravity > 0) ? thrust / (gravity * shipmass) : 0;
	}

	private bool CheckGearLock()
	{
		foreach (IMyLandingGear gear in s.gears)
		{
			if (!gear.Closed && gear.IsWorking && gear.IsLocked)
				return true;
		}
		return false;
	}

	/// <summary>
	/// Checks for conditions when the script should turn itself off automatically
	/// such as landing gears locked, or no gravity (except in mode 5), or very close
	/// to ground.
	/// </summary>
	/// <returns></returns>
	private bool DisableConditions()
	{
		return (gravNow == 0 && Mode != 5) || gndAlt < 2 || CheckGearLock();
	}



	public List<string> LogNames()
	{
		return new List<string> { "mode", "grav_now", "vspeed", "vspeed_sp", "speed_sp_source", "gnd_altitude", "gnd_sl_offset", "alt_source", "vpid_p", "vpid_i", "vpid_d", "PIDoutput", "twr_wanted" };
	}

	public List<double> LogValues()
	{
		return new List<double> { Mode, gravNow, spd.v, spdSP.v, (float)spdSrc, gndAlt, gndSlOffset, (float)altSrc, vertPID.ap, vertPID.ai, vertPID.ad, vertPID.output, lwrCmd };
	}

	public List<string> AllLogNames()
	{
		List<string> names = new List<string>();
		names.AddRange(this.LogNames());
		names.AddRange(rdr.LogNames());
		names.AddRange(horizThr.LogNames());
		names.AddRange(AP.LogNames());

		return names;
	}

	public List<double> AllLogValues()
	{
		List<double> values = new List<double>();
		values.AddRange(this.LogValues());
		values.AddRange(rdr.LogValues());
		values.AddRange(horizThr.LogValues());
		values.AddRange(AP.LogValues());

		return values;
	}

	private MySprite TextSprite(string text, float x, float y, VRageMath.RectangleF view, VRageMath.Color color, TextAlignment align, float size = 1f)
	{
		return new MySprite()
		{
			Type = SpriteType.TEXT,
			Data = text,
			Position = new Vector2(x, y) + view.Position,
			RotationOrScale = size,
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
public class SurfaceGravityEstimator
{
	/// <summary>
	/// Current radius estimation in meters, and best estimation so far
	/// </summary>
	public double R = 0, RBest = 0;
	/// <summary>
	/// Current gravity estimation in m/s², and best estimation so far
	/// </summary>
	public double g = 0, gBest = 0;
	/// <summary>
	/// Current confidence in the estimation, and best confidence so far, between 0 and 1
	/// </summary>
	public double conf, confBest;
	double gravityExponent;

	double gravPrev = 0;
	double altSlPrev = 0;


	public SurfaceGravityEstimator(double exp)
	{
		gravityExponent = exp;
	}

	public void UpdateEstimates(double grav, double altSl, double hillparam)
	{

		double K;

		if (gravPrev == 0 || altSlPrev == 0)
		{
			// First run, initalize and don't return anything
			gravPrev = grav;
			altSlPrev = altSl;

		}
		else if (grav != gravPrev && altSl != altSlPrev && grav > 0)
		{

			double radiusNew;

			// Power method

			K = Math.Pow(gravPrev / grav, 1 / gravityExponent);

			if (K != 1)
			{
				radiusNew = (K * altSlPrev - altSl) / (1 - K);
			}
			else
			{
				// This should not happen
				radiusNew = -2;
			}

			// Confidence in the estimation is based on how much it changes from one sample to the next
			conf = Math.Pow(Math.Min(radiusNew, R) / Math.Max(radiusNew, R), 4);

			// A few extra conditions to tweak the exported confidence value
			if (radiusNew < 0) conf = 0;
			if (radiusNew > 1e7) conf = 0;
			if (radiusNew > 2e5) conf *= 0.95;

			R = radiusNew;

		}
		else
		{
			R = -3;
			conf = 0;
		}

		if (altSl + R > 0 && R > 0)
		{
			g = grav * Math.Pow((altSl + R) / (R * (1 + hillparam)), gravityExponent);
		}
		else
		{
			g = -1;
			conf = 0;
		}

		// Update best estimates if the current one is better

		conf = H.SatMinMax(conf, 0, 1);

		if (conf > confBest || conf > 0.95)
		{
			confBest = conf;
			RBest = R;
			gBest = g;
		}

		gravPrev = grav;
		altSlPrev = altSl;
	}

	public void Reset()
	{
		gravPrev = 0;
		altSlPrev = 0;
		confBest = 0;
	}

	public string DebugString()
	{

		string str = "[SURFACE GRAVITY ESTIMATOR for EXP=" + gravityExponent.ToString() + "]";

		str += "\nCurrent : R=:" + R.ToString("000000") + "m, g=" + H.ms2_to_g(g).ToString("0.00") + "g, c=" + conf.ToString("0.00");
		str += "\nBest    : R=:" + RBest.ToString("000000") + "m, g=" + H.ms2_to_g(gBest).ToString("0.00") + "g, c=" + confBest.ToString("0.00");

		return str;

	}
}

/// <summary>
/// Build a liftoff profile for a ship, based on its characteristics and the planet it is on, then used to control the ship during the landing phase by looking up the vertical speed according to altitude.
/// See tech doc §5
/// </summary>
public class LiftoffProfileBuilder
{

	public double[] vertSpeed = new double[NB_PTS];
	public double[] altSl = new double[NB_PTS];
	public double[] aRatio = new double[NB_PTS];
	// Prototech and Ion are counted together
	// since they are used together in the same way
	// (they are both electric thrusters)
	// Prototech higher efficiency (30% in full atmo) is properly accounted for
	public double[] iRatio = new double[NB_PTS];
	public double[] hRatio = new double[NB_PTS];
	// H2 used in liters
	public double[] h2Used = new double[NB_PTS];
	public double GravityExponent;

	// Time step in seconds
	const double DT_START = 0.5;
	const double DT_INCREASE=0.05;
	// Number of time steps to compute
	const int NB_PTS = 256;
	// liter per second per N of thrust
	const double H2_FLOW_RATIO = 0.000816;
	/// <summary>
	/// Time increment for the simulation
	/// </summary>
	double dt = DT_START;
	/// <summary>
	/// Increase in the time increment for the simulation
	
	/// </summary>
	// A landing profile has two attribues :
	// - computed : if the profile has been computed or not
	// - valid    : if the computed profile concludes on a successfull liftoff
	bool valid = false;
	bool computed = false;

	public LiftoffProfileBuilder(SEGameConfig seconfig)
	{
		GravityExponent = seconfig.GravExp;
	}

	/// <summary>
	/// Compute atmospheric density at a set altitude above sea level, based on planet info and radius
	/// </summary>
	/// <param name="altSL">Altitude above seal level in meters</param>
	/// <param name="planet">Instance of the Planet class with data for the current planet</param>
	/// <param name="radius">Planet radius in meters</param>
	/// <returns></returns>
	private double ComputeAtmoDensity(double altSL, P planet, double radius)
	{

		double atmoAltLimit = radius * planet.AtmoLimitAltitude * planet.HillParam;

		if (altSL > atmoAltLimit)
		{
			return 0;
		}
		else if (altSL >= 0)
		{
			return planet.AtmoDensitySL * (1 - altSL / atmoAltLimit);
		}
		else
		{
			return planet.AtmoDensitySL;
		}
	}

	/// <summary>
	/// Compute gravity value at a set altitude above sea level, based on planet info and radius
	/// </summary>
	/// <param name="altSL">Altitude above seal level in meters</param>
	/// <param name="planet">Instance of the Planet class with data for the current planet</param>
	/// <param name="radius">Planet radius in meters</param>
	/// <returns></returns>
	private double ComputeGravity(double altSL, P planet, double radius, double gravityExponent)
	{

		double planetMaxRadius = radius * (1 + planet.HillParam);

		if (altSL >= (planetMaxRadius - radius))
		{
			double raw = H.g_to_ms2(planet.GSeaLevel * Math.Pow(planetMaxRadius / (altSL + radius), gravityExponent));
			// The game implements a cutoff at 0.05g.
			if (raw > H.g_to_ms2(0.05))
			{
				return raw;
			}
			else
			{
				return 0;
			}
		}
		else
		{
			return H.g_to_ms2(planet.GSeaLevel);
		}
	}

	/// <summary>
	/// Build the altitude/speed profile by simulating a liftoff from a standstill at a specified altitude above sea level.
	/// </summary>
	/// <param name="altSlStart">Starting altitude in meters, relative to sea level</param>
	/// <param name="shipInfo">Info about the ship (mass etc.)</param>
	/// <param name="planet">Instance of the Planet class with data for the current planet</param>
	/// <param name="radius">Planet radius in meters</param>
	/// <param name="maxAccel">Maximum allowed acceleration in m/s² (including gravity)</param>
	/// <param name="maxTwr">Maximum allowed thrust to weight ratio</param>
	/// <param name="sufficientTwr">Thrust to weight ratio above which hydrogen thrusters will not be used</param>
	/// <param name="safetyfactor">Safety ratio, for example 1.1, applied to the ship thrust capabilities.</param>
	/// <param name="maxSpeed">Maximum allowed speed in m/s</param>
	/// <param name="ecoSpeed">Speed above which only electrical thrusters are used</param>
	/// <param name="initialSpeed">Initial vertical speed at the starting altitude. Useful for a smooth transition when the speed setpoint transitions from the profile to a constant speed</param>
	/// <param name="lifters">Group of thrusters providing lift (upward thrust from the reference of the cockpit)</param>
	/// <param name="gravityExponent">Exponent for the gravity calculation</param>
	public void Compute(double altSlStart, ShipInfo shipInfo, P planet, double radius, double maxAccel, double maxTwr, double sufficientTwr, double safetyfactor, double maxSpeed, double ecoSpeed, double initialSpeed, ThrGroup lifters, double gravityExponent)
	{

		// Time is in seconds
		double t = 0;
		bool temp_valid = true;

		double safetyInverse = 1 / safetyfactor;

		vertSpeed[0] = initialSpeed;
		altSl[0] = altSlStart;
		aRatio[0] = 1;
		iRatio[0] = 1;
		hRatio[0] = 1;
		h2Used[0] = 0;

		dt = DT_START;

		for (int i = 1; i < NB_PTS; i++)
		{

			t = t + dt;
			// Gravity is in m/s²
			double gravity = ComputeGravity(altSl[i - 1], planet, radius, gravityExponent);
			// Cache atmo density
			double density = ComputeAtmoDensity(altSl[i - 1], planet, radius);

			double thrMaxAccel = shipInfo.mass * Math.Min(maxAccel, gravity + 2 * t);
			double thrMaxTwr = shipInfo.mass * gravity * maxTwr;
			double thrSufficientTwr = shipInfo.mass * gravity * sufficientTwr;
			double thrMaxSpeed = (vertSpeed[i - 1] >= maxSpeed) ? shipInfo.mass * gravity : 1e99;

			// Compute maximum thrust for electric thrusters

			// Atmospheric thrusters
			double aThrust = lifters.AtmoThrustForDensity(density) * safetyInverse;
			aThrust = H.Min(aThrust, thrMaxAccel, thrMaxTwr, thrMaxSpeed);

			// Compute the ion+prototech maximum possible ion+prototech thrust for the density
			double iThrust = (lifters.IonThrustForDensity(density) + lifters.PrototechThrustForDensity(density)) * safetyInverse;
			iThrust = Math.Max(0, H.Min(iThrust, thrMaxAccel - aThrust, thrMaxTwr - aThrust, thrMaxSpeed - aThrust));

			// Compute hydrogen thrust with the same concept
			double hThrust = lifters.Max.hydro * safetyInverse;
			hThrust = Math.Max(0, H.Min(hThrust, thrMaxAccel - aThrust - iThrust, thrMaxTwr - aThrust - iThrust, thrMaxSpeed - aThrust - iThrust));
			// If electric thrusters are sufficient, H2 is not used at all
			hThrust = H.Interpolate(thrSufficientTwr, thrSufficientTwr * 1.1, hThrust, 0, aThrust + iThrust);

			// Increament total hydrogen used
			h2Used[i] = h2Used[i - 1] + hThrust * H2_FLOW_RATIO * dt;

			double totalThrust = hThrust + aThrust + iThrust;

			if (totalThrust > 0)
			{
				aRatio[i] = aThrust / totalThrust;
				iRatio[i] = iThrust / totalThrust;
				hRatio[i] = hThrust / totalThrust;
			}
			else
			{
				aRatio[i] = 0;
				iRatio[i] = 0;
				hRatio[i] = 0;
			}

			// Apply Newton formula, m/s², positive up
			double accel = totalThrust / shipInfo.mass - gravity;

			// Integrate acceleration to compute speed
			vertSpeed[i] = Math.Min(accel * dt + vertSpeed[i - 1], maxSpeed);

			// If at any point, the vertical speed becomes negative, then it is a failed liftoff
			if (vertSpeed[i] < 0)
			{
				temp_valid = false;
				break;
			}

			// Integrate speed to compute altitude above sea level
			altSl[i] = altSl[i - 1] + vertSpeed[i - 1] * dt + 0.5 * accel * dt * dt;

			dt += DT_INCREASE;

		}
		computed = true;
		valid = temp_valid;
	}


	/// <summary>
	/// Interpolates the altitude/speed profile to return the speed corresponding to the altitude given.
	/// Uses linear interpolation, with binary search
	/// If the altitude is above the final computed altitude, return the final computed speed.
	/// </summary>
	/// <param name="alt">Altitude in meters</param>
	/// <returns>Speed in m/s</returns>
	public double InterpolateSpeed(double alt)
	{
		return Interpolate(alt, ref vertSpeed);
	}

	public double InterpolateH2Used(double alt)
	{
		return Interpolate(alt, ref h2Used);
	}

	/// <summary>
	/// Interpolate data in a table using binomial search
	/// </summary>
	/// <param name="alt"></param>
	/// <param name="y"></param>
	/// <returns>Interpolated value</returns>
	private double Interpolate(double alt, ref double[] y)
	{


		int left = 0;
		int right = NB_PTS - 1;
		int m = (left + right) / 2;

		if (!valid) return 0;

		// If we are currently below the starting altitude, return the value for the starting altitude
		if (alt <= altSl[0]) return y[0];

		// If we are currently above the altitude of the last simulated point, return the speed corresponding to that
		if (alt >= altSl[NB_PTS - 1]) return y[NB_PTS - 1];

		// Binary search
		while (left <= right)
		{
			if (altSl[m] == alt)
			{
				break;
			}
			else if (altSl[m] > alt)
			{
				right = m - 1;
			}
			else
			{
				left = m + 1;
			}
			m = (left + right) / 2;
		}

		if (m + 1 >= NB_PTS) return y[NB_PTS - 1];

		// Now m is the index such that alt_sl[m] is the highest value lower than alt
		return H.Interpolate(altSl[m], altSl[m + 1], y[m], y[m + 1], alt);

	}

	public void Invalidate()
	{
		valid = false;
		computed = false;
	}

	public bool IsValid() => valid;
	public bool IsComputed() => computed;

	public string DebugString()
	{

		string str = "[LITFOFF PROFILE]";

		str += "\nComputed:" + computed.ToString() + " Valid:" + valid.ToString();
		str += "\nFinal:" + H.Cpct(vertSpeed[NB_PTS - 1]) + "m/s , " + H.Cpct(altSl[NB_PTS - 1]) + "m , " + (h2Used[NB_PTS - 1] / 1000).ToString("000.0") + "kL";
		str += "\nAlt(m) | speed (m/s) | a/i/h ratio";
		for (int i = 0; i < 10; i++)
		{
			str += "\n" + H.Cpct(altSl[i]) + "  | " + H.Cpct(vertSpeed[i]) + "  | " + aRatio[i].ToString("0.00") + " " + iRatio[i].ToString("0.00") + " " + hRatio[i].ToString("0.00");
		}

		return str;
	}
}

/// <summary>
/// Computes ship info, such as mass, inertia, H2 tank status, etc.
/// See tech doc §12.6
/// </summary>
public class ShipInfo
{
	/// <summary>
	/// Mass of the ship in kg, computed using the ship controller API
	/// </summary>
	public double mass;
	/// <summary>
	/// Inertia of the ship in kg.m², computed using the ship controller API and the ship dimensions with the formula for a rectangular prism. This is used to compute the maximum sustainable angle for the gyroscopes.
	/// </summary>
	public double inertia;

	ShipBlocks ship;
	readonly SLMConfig config;

	public ShipInfo(ShipBlocks ship, SLMConfig config)
	{
		this.ship = ship;
		this.config = config;
		UpdateMass();
		UpdateInertia();
	}

	public void UpdateMass()
	{
		mass = ship.Ctrller.CalculateShipMass().TotalMass;
	}

	public void UpdateInertia()
	{
		Vector3I extend = ship.Ctrller.CubeGrid.Max - ship.Ctrller.CubeGrid.Min;
		Vector3D size = extend * ship.Ctrller.CubeGrid.GridSize;
		double inertia_x = mass * (size.Y * size.Y + size.Z * size.Z) / 12;
		double inertia_y = mass * (size.X * size.X + size.Z * size.Z) / 12;
		double inertia_z = mass * (size.X * size.X + size.Y * size.Y) / 12;

		inertia = H.Max(inertia_x, inertia_y, inertia_z);
	}
	/// <summary>
	/// Compute the maximum angle that the ship can sustain with its gyroscopes, based on its inertia and number of gyros. Value is in degrees.
	/// </summary>
	/// <returns></returns>
	public double MaxAngle()
	{
		return Math.Min(config.maxAngle,ship.gyros.Count / inertia * (ship.Ctrller.CubeGrid.GridSizeEnum == MyCubeSize.Small ? config.inertiaRatioSmall : config.inertiaRatioLarge));
	}

	/// <summary>
	/// Compute the total amount of H2 stored in the tanks in liters
	/// </summary>
	/// <returns></returns>
	public double H2_stored_liters()
	{
		double fill = 0;
		foreach (IMyGasTank tank in ship.h2Tanks)
		{
			fill += tank.FilledRatio * tank.Capacity;
		}
		return fill;
	}

	/// <summary>
	/// Compute the total capacity of the H2 tanks in liters
	/// </summary>
	/// <returns></returns>
	public double H2_capa_liters()
	{
		double capa = 0;
		foreach (IMyGasTank tank in ship.h2Tanks)
		{
			capa += tank.Capacity;
		}
		return capa;
	}

	public string DebugString()
	{
		return "[SHIP INFO]\n" + H.Cpct(mass) + "kg " + H.Cpct(inertia) + "kg.m² " + H.Cpct(MaxAngle()) + "° " + H.Cpct(H2_stored_liters() / 1000) + "kL";
	}

}

/// <summary>
/// PID controller with anti-windup and low-pass filtering of the D component
/// See tech doc §12.5
/// </summary>
public class PIDController
{

	public double output = -1;

	public double ap, ai, ad;

	// PID coefficients (constant during execution, defined with the constructor)
	readonly double KP, KI, KD, _AiMinFixed, _AiMaxFixed, AD_FILT, AD_MAX;

	// Private PID parameters
	double delta_prev, deriv_prev;

	/// <summary>
	/// Creates a PID controller with the defined settings
	/// </summary>
	/// <param name="kp">Proportional constant</param>
	/// <param name="ki">Integral constant</param>
	/// <param name="kd">Derivative constant</param>
	/// <param name="aiMinFixed">Lower bound for the integral action</param>
	/// <param name="aiMaxFixed">Higher bound for the integral action</param>
	/// <param name="adFilt">Filtering coefficient for the derivative action</param>
	/// <param name="adMax">Max bound for the absolute value of the integral action</param>
	public PIDController(double kp, double ki, double kd, double aiMinFixed, double aiMaxFixed, double adFilt, double adMax)
	{
		KP = kp;
		KI = ki;
		KD = kd;
		_AiMinFixed = aiMinFixed;
		_AiMaxFixed = aiMaxFixed;
		AD_FILT = H.SatMinMax(adFilt, 0, 1);
		AD_MAX = adMax;
	}


	public void UpdatePID(double delta)
	{
		UpdatePIDController(delta, _AiMinFixed, _AiMaxFixed);
	}

	public void UpdatePIDController(double delta, double aiMinDynamic, double aiMaxDynamic)
	{

		delta = H.NotNan(delta);

		// P
		ap = delta * KP;

		// I
		ai = ai + delta * KI;

		// Saturate the integral component, first with fixed limits (that have priority), then with dynamic limits
		ai = H.SatMinMax(ai, _AiMinFixed, _AiMaxFixed);
		ai = H.SatMinMax(ai, aiMinDynamic, aiMaxDynamic);

		// D
		// Low-pass filtering of the derivative component
		double deriv = AD_FILT * deriv_prev + (1 - AD_FILT) * (delta - delta_prev);
		deriv_prev = deriv;
		delta_prev = delta;
		ad = deriv * KD;
		ad = H.SatMinMax(ad, -AD_MAX, AD_MAX);

		// PID output
		output = ap + ai + ad;

	}

	public void Reset()
	{
		deriv_prev = 0;
		delta_prev = 0;
		ap = 0;
		ai = 0;
		ad = 0;
	}

	public string DebugString()
	{

		return "P: " + H.Cpct(ap, 2) + " I:" + H.Cpct(ai, 2) + " D:" + H.Cpct(ad, 2);

	}
}

/// <summary>
/// Gyroscope controller to align the ship with a target orientation (from Flight Assist by Naosyth)
/// See tech doc §9
/// </summary>
public class GyroController
{
	bool gyroOverride;
	double angle;

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
		foreach (IMyGyro g in gyros)
		{
			g.Enabled = true;
			if (!state)
			{
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

		foreach (IMyGyro g in gyros)
		{

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
public class AutoLeveler
{
	/// <summary>
	/// Ship pitch in degrees, positive when the ship's nose is up, relative to the local gravity vector
	/// </summary>
	public double Pitch;
	/// <summary>
	/// Ship roll in degrees, positive when the ship's right side is down, relative to the local gravity vector
	/// </summary>
	public double Roll;

	public Speed speed, speedSP;

	readonly int delay;
	readonly double gyroResponse;
	readonly double maxAngle;
	bool Enabled = false;
	readonly IMyShipController cockpit;
	readonly GyroController gyroController;
	int timer;
	double desiredPitch, desiredRoll;



	public AutoLeveler(IMyShipController cockpit, List<IMyGyro> gyros, double maxAngle, int delay, double gyroResponse, double gyroRpmScale)
	{
		this.cockpit = cockpit;
		this.gyroController = new GyroController(gyros, gyroRpmScale);
		this.maxAngle = maxAngle;
		this.delay = delay;
		this.gyroResponse = gyroResponse;
	}

	public void Enable()
	{
		Enabled = true;
		gyroController.SetGyroOverride(true);
	}

	public void Disable()
	{
		Enabled = false;
		gyroController.SetGyroOverride(false);
		speed=default(Speed);
		speedSP=default(Speed);
	}

	/// <summary>
	/// Compute the desired ship orientation to achieve desired horizontal speed (in the forward/back
	/// direction and the left/right direction). If the desired speed are omitted, the default are zero.
	/// If the current speeds are omitted, then this function levels the ship to the local gravity and
	/// ignores horizontal speed. It then calls the gyroscopes to do the actual tilting.
	/// </summary>
	/// <param name="speedFwd">Current forward speed in m/s</param>
	/// <param name="speedLeft">Current left speedin m/s</param>
	/// <param name="speedFwdSP">Desired forward speed in m/s</param>
	/// <param name="speedLeftSP">Desired left speed in m/s</param>
	public void Tick(Speed currentSpeed = default(Speed), Speed SpeedSP = default(Speed))
	{
		speed = currentSpeed;
		speedSP = SpeedSP;


		if (Enabled)
		{

			Vector3D gravity = -Vector3D.Normalize(cockpit.GetNaturalGravity());

			Pitch = 90 - H.NotNan(Math.Acos(Vector3D.Dot(cockpit.WorldMatrix.Forward, gravity)) * H.radToDeg);
			Roll = H.NotNan(Math.Acos(Vector3D.Dot(cockpit.WorldMatrix.Right, gravity)) * H.radToDeg) - 90;

			// "smart delay" : if the pilot is actively trying to move the ship, don't auto-level
			// otherwise, auto-level after a short delay

			if (cockpit.RotationIndicator.Length() > 0.0f)
			{

				desiredPitch = Pitch;
				desiredRoll = Roll;
				gyroController.SetGyroOverride(false);
				timer = 0;

			}
			else if (timer > delay)
			{

				// After the delay, auto-level the ship

				// The desired pitch and roll are based on the ship's current velocity
				// An atan function is used to scale the desired pitch and roll to the max pitch and roll values

				gyroController.SetGyroOverride(true);
				desiredPitch = Math.Atan((speed.f - speedSP.f) / gyroResponse) / H.halfPi * maxAngle;
				desiredRoll = Math.Atan((speed.l - speedSP.l) / gyroResponse) / H.halfPi * maxAngle;

				Matrix cockpitOrientation;
				cockpit.Orientation.GetMatrix(out cockpitOrientation);
				var quatPitch = Quaternion.CreateFromAxisAngle(cockpitOrientation.Left, (float)(desiredPitch * H.degToRad));
				var quatRoll = Quaternion.CreateFromAxisAngle(cockpitOrientation.Backward, (float)(desiredRoll * H.degToRad));
				var reference = Vector3D.Transform(cockpitOrientation.Down, quatPitch * quatRoll);

				gyroController.SetTargetOrientation(reference, cockpit.GetNaturalGravity());

				gyroController.Tick();

			}
			else
			{
				timer++;
			}
		}
	}

	public void SimpleTick(Vector3D reference, Vector3D target)
	{
		gyroController.SetTargetOrientation(reference, target);
		gyroController.Tick();
	}



	public string DebugString()
	{
		string str = "[AUTO LEVELER]";
		str += "\nFwd :" + H.Cpct(speed.f) + "(" + H.Cpct(speedSP.f) + ")";
		str += "\nLeft:" + H.Cpct(speed.l) + "(" + H.Cpct(speedSP.l) + ")";
		str += "\npitch:" + H.Cpct(Pitch) + " roll:" + H.Cpct(Roll) + "max:" + H.Cpct(maxAngle);

		return str;
	}

	public double MaxAngle()
	{
		return maxAngle;
	}


}

/// <summary>
/// Handles the downward-facing cameras to scan for terrain slopes and altitude
/// See tech doc §7
/// </summary>
public class GroundRadar
{

	public bool valid = false;
	public bool exists = false;
	public bool active = false;
	public bool obstruction = false;
	public ScanMode mode;
	public int altAge = 0;
	public double spdFwdLim, spdRearLim, spdLeftLim, spdRightLim;

	public const double UNDEF = 1e6;
	public const double HORIZ_MAX_SPEED = 20;

	const double RANGE_MARGIN = 50;
	/// <summary>
	/// Distance in meters for the first attempt to scan for altitude
	/// </summary>
	const double START_RANGE = 1000;
	const double MAX_TERR_DIST_SINGLE_RDR = 180;
	const double MAX_TERR_DIST_DOUBLE_RDR = 200;
	const double DBLE_RDR_WIDE_SCAN_DIST = 1000;
	const double DBLE_RDR_INITIAL_SCAN_DIST = 5000;

	const double MIN_SCAN_ANGLE = 2;
	const double MAX_SCAN_ANGLE = 30;
	const double GROUND_SCAN_HORIZ_LENGTH = 20;
	const double HORIZ_DEADZONE = 2;
	const double CANYON_RATIO_MIN = 0;
	const double CANYON_RATIO_MAX = 2;

	const double SIDE_LIMIT_RATIO=10;
	const double SIDE_SCAN_ALT = 2000;


	readonly double RDR_MAX_RANGE;
	readonly double SPEED_SCALE;
	readonly double MAX_TERR_DIST;
	readonly bool doubleRdr;

	double terrScanRange, altScanRange;

	MyDetectedEntityInfo rdrReturn;

	// Distances measured by the downward-facing cameras
	double d_fwd, d_rear, d_left, d_right, d_fwd_wide, d_rear_wide, d_left_wide, d_right_wide, d_fwd_left, d_fwd_right, d_rear_left, d_rear_right, fwdCanyonRatio, leftCanyonRatio;
	
	double angle = 1, doubleAngle = 1, diagAngle = 1;
	double dz = HORIZ_DEADZONE;

	int scan_step = 0;


	Vector3D hitpos;
	/// <summary>
	/// Altitude radar (a IMyCameraBlock block)
	/// </summary>
	IMyCameraBlock altRdr;
	IMyCameraBlock terrRdr;
	public SideScan sideScan;

	/// <summary>
	/// Initializes the GroundRadar class by assigning the altitude and terrain radars based on the available blocks, and setting the appropriate scanning mode and parameters. The constructor handles different configurations of radar blocks, including cases where there are no radars, one radar, or multiple radars, and assigns them to the altitude and terrain radar roles accordingly. It also initializes the side scan with the provided forward, rear, left, and right blocks.
	/// </summary>
	/// <param name="rdrs">Ship radars</param>
	/// <param name="terr_rdrs">Terrain radars</param>
	/// <param name="max_range">Maximum scanning range</param>
	/// <param name="speed_scale">Speed scaling factor</param>
	/// <param name="fwd">Forward camera</param>
	/// <param name="rear">Rear camera</param>
	/// <param name="left">Left camera</param>
	/// <param name="right">Right camera</param>
	public GroundRadar(List<IMyTerminalBlock> rdrs, List<IMyTerminalBlock> terr_rdrs, double max_range, double speed_scale,IMyTerminalBlock fwd, IMyTerminalBlock rear, IMyTerminalBlock left, IMyTerminalBlock right)
	{

		if (rdrs.Count == 0 && terr_rdrs.Count == 0)
		{
			// If there is no radar, disable the radar function
			exists = false;
			mode = ScanMode.NoRadar;

		}
		else if (rdrs.Count == 1 && terr_rdrs.Count == 0)
		{
			// If only one radar is available, use it for both altitude and terrain
			altRdr = rdrs[0] as IMyCameraBlock;
			terrRdr = rdrs[0] as IMyCameraBlock;
			MAX_TERR_DIST = MAX_TERR_DIST_SINGLE_RDR;
			doubleRdr = false;
			exists = true;
			mode = ScanMode.SingStby;

		}
		else if (rdrs.Count == 0 && terr_rdrs.Count == 1)
		{
			// If only one radar is available (and misconfigured as a terrain radar), use it for both altitude and terrain
			altRdr = terr_rdrs[0] as IMyCameraBlock;
			terrRdr = terr_rdrs[0] as IMyCameraBlock;
			MAX_TERR_DIST = MAX_TERR_DIST_SINGLE_RDR;
			doubleRdr = false;
			exists = true;
			mode = ScanMode.SingStby;

		}
		else if (rdrs.Count == 0 && terr_rdrs.Count >= 2)
		{
			// If two or more radars are available (and misconfigured as a terrain radar), use the first one for altitude and the second one for terrain
			altRdr = terr_rdrs[0] as IMyCameraBlock;
			terrRdr = terr_rdrs[1] as IMyCameraBlock;
			MAX_TERR_DIST = MAX_TERR_DIST_DOUBLE_RDR;
			doubleRdr = true;
			exists = true;
			mode = ScanMode.DbleStby;

		}
		else if (rdrs.Count >= 2 && terr_rdrs.Count == 0)
		{
			// If two or more radars are available, use the first one for altitude and the second one for terrain
			altRdr = rdrs[0] as IMyCameraBlock;
			terrRdr = rdrs[1] as IMyCameraBlock;
			MAX_TERR_DIST = MAX_TERR_DIST_DOUBLE_RDR;
			doubleRdr = true;
			exists = true;
			mode = ScanMode.DbleStby;

		}
		else
		{
			// The remaining case is if there is at least one of each, use them for the appropriate role
			terrRdr = terr_rdrs[0] as IMyCameraBlock;
			altRdr = rdrs[0] as IMyCameraBlock;
			MAX_TERR_DIST = MAX_TERR_DIST_DOUBLE_RDR;
			doubleRdr = true;
			exists = true;
			mode = ScanMode.DbleStby;
		}

		RDR_MAX_RANGE = max_range;
		SPEED_SCALE = speed_scale;

		sideScan = new SideScan(fwd, rear, left, right);
	}

	public void DisableRadar()
	{
		if (!exists) return;

		altRdr.EnableRaycast = false;
		terrRdr.EnableRaycast = false;

		d_fwd = d_rear = d_left = d_right = MAX_TERR_DIST;
		d_fwd_wide = d_rear_wide = d_left_wide = d_right_wide = MAX_TERR_DIST;
		d_fwd_left = d_fwd_right = d_rear_left = d_rear_right = MAX_TERR_DIST;

		valid = false;
		active = false;
	}

	public void StartRadar()
	{
		if (!exists) return;

		altScanRange = START_RANGE;
		altRdr.EnableRaycast = true;
		terrRdr.EnableRaycast = true;
		altAge = 0;

		d_fwd = d_rear = d_left = d_right = MAX_TERR_DIST;
		d_fwd_wide = d_rear_wide = d_left_wide = d_right_wide = MAX_TERR_DIST;
		d_fwd_left = d_fwd_right = d_rear_left = d_rear_right = MAX_TERR_DIST;

		active = true;
	}

	public void DisableSide()
	{
		sideScan.Disable();
		fwdCanyonRatio = leftCanyonRatio = 0;
	}

	public void StartSide()
	{
		sideScan.Enable();
		fwdCanyonRatio = leftCanyonRatio = 0;
	}

	public void StartFwd()
	{
		sideScan.EnableFwdOnly();
	}

	public void ScanSide(double pitch, double roll)
	{
		if (GetDistance() < SIDE_SCAN_ALT)
			sideScan.Scan(pitch, roll);
	} 

	/// <summary>
	/// Attempt to cast a ray to mesure ship altitude.
	/// If the ray doesn't hit either a planet, an asteroid or a large grid,
	/// then the scan range is increased for the next attempt (up to some limit).
	/// </summary>
	public void ScanForAltitude(double pitch, double roll)
	{
		if (!exists) return;

		if (altRdr.CanScan(altScanRange))
		{
			// Compensate for ship pitch and roll when casting the ray, so that it is really
			// cast vertically down even if the ship is tilting.
			rdrReturn = altRdr.Raycast(altScanRange, (float)-pitch, (float)-roll);

			if ((rdrReturn.Type == MyDetectedEntityType.Planet || rdrReturn.Type == MyDetectedEntityType.LargeGrid || rdrReturn.Type == MyDetectedEntityType.Asteroid) && rdrReturn.HitPosition.HasValue)
			{
				// If we have a return (either a planet, or a large grid (landing pad, silo)), adjust the raycast
				// range to a little above the return distance, in order to maximize the refresh rate.
				valid = true;
				altScanRange = GetDistance() + RANGE_MARGIN;
				altAge = 0;
			}
			else
			{
				// If we have no return, invalidate the previous return and increase the scan range
				valid = false;
				altScanRange = Math.Min(altScanRange * 2, RDR_MAX_RANGE);
			}
		}
	}

	public void IncrementAltAge()
	{
		altAge++;
	}

	public double GetDistance()
	{
		if (!exists) return UNDEF;

		if (valid)
		{
			// Hitpos is updated when the radar has a return
			// Mypos is always updated
			hitpos = rdrReturn.HitPosition.Value;
			Vector3D mypos = altRdr.GetPosition();
			return VRageMath.Vector3D.Distance(hitpos, mypos);
		}
		else
		{
			return UNDEF;
		}
	}

	// TODO : functions to maintain ship alignment in mode5

	// public Vector3D HitDirection()
	// {
	// 	if (valid) {
	// 		Vector3D mypos = altitudeRadar.GetPosition(); 
	// 		hitpos = radar_return.HitPosition.Value; 
	// 		return (hitpos - mypos);
	// 	} else { 
	// 		return Vector3D.Zero;
	// 	}
	// }

	// public Vector3D RadarDirection() {

	// 	if (!exists) return Vector3D.Zero;

	// 	return altitudeRadar.CubeGrid.WorldMatrix.GetDirectionVector(Base6Directions.Direction.Forward);
	// }

	/// <summary>
	/// Perform one step of scanning the terrain below the ship. Each time this is called, the ideal
	/// radar scan distance is determined, and if it can scan, it will cast a pair of rays. The angle
	/// of the rays change at each call, to update the overall view of the ground below.
	/// </summary>
	public void ScanTerrain(double ship_pitch, double ship_roll)
	{

		// Define the scan mode
		if (!exists)
		{
			mode = ScanMode.NoRadar;
			return;
		}

		if (doubleRdr)
		{

			mode = ScanMode.DbleStby;
			terrScanRange = Math.Min(GetDistance() * 1.2 + 20, DBLE_RDR_INITIAL_SCAN_DIST);
			if (valid && GetDistance() < terrScanRange)
				mode = (GetDistance() < DBLE_RDR_WIDE_SCAN_DIST) ? ScanMode.DbleWide : ScanMode.DbleEarly;

		}
		else
		{

			mode = ScanMode.SingStby;
			terrScanRange = MAX_TERR_DIST;
			if (valid && GetDistance() < terrScanRange)
				mode = ScanMode.SingNarr;
		}

		double scan_angle_raw = Math.Atan(GROUND_SCAN_HORIZ_LENGTH / GetDistance()) * H.radToDeg;

		angle = H.SatMinMax(scan_angle_raw, MIN_SCAN_ANGLE, MAX_SCAN_ANGLE - 5);
		diagAngle = H.SatMinMax(scan_angle_raw * 1.414, MIN_SCAN_ANGLE, MAX_SCAN_ANGLE);
		doubleAngle = H.SatMinMax(scan_angle_raw * 2, MIN_SCAN_ANGLE, MAX_SCAN_ANGLE);

		// Perform the scan

		if (mode == ScanMode.SingNarr || mode == ScanMode.DbleEarly || mode == ScanMode.DbleWide)
		{

			if (terrRdr.CanScan(2 * terrScanRange))
			{
				ScanStep(ship_pitch, ship_roll);
			}

		}
		else
		{
			d_fwd = d_rear = d_left = d_right = MAX_TERR_DIST;
			d_fwd_wide = d_rear_wide = d_left_wide = d_right_wide = MAX_TERR_DIST;
			d_fwd_left = d_fwd_right = d_rear_left = d_rear_right = MAX_TERR_DIST;
		}
	}

	

	/// <summary>
	/// Cast a ray to the specified pitch and yaw angles (relative to the vertical)
	/// and if the ray touches a planet or a large grid, return the distance.
	/// If the ray hit the ship, return -1.
	/// Otherwise return the max range.
	/// </summary>
	public double ScanDir(double scan_pitch, double scan_yaw, double ship_pitch, double ship_roll, double max_range, out bool scanned)
	{

		// Define the scan mode
		if (!exists)
		{
			mode = ScanMode.NoRadar;
			scanned = false;
			return max_range + 1;
		}

		if (terrRdr.CanScan(max_range))
		{

			scanned = true;

			// The camera can only cast rays to 45° off-center, so we saturate the
			// casting angle to that value. It means that if the ship is tilted a lot
			// the casting angle will be less that requested, but its better than nothing.
			float cast_pitch = H.MaxAbs((float)(scan_pitch - ship_pitch), 45);
			float cast_yaw = H.MaxAbs((float)(scan_yaw - ship_roll), 45);
			MyDetectedEntityInfo temp_return = terrRdr.Raycast(max_range, cast_pitch, cast_yaw);

			if ((temp_return.Type == MyDetectedEntityType.Planet || temp_return.Type == MyDetectedEntityType.LargeGrid) && temp_return.HitPosition.HasValue)
			{
				return VRageMath.Vector3D.Distance(temp_return.HitPosition.Value, terrRdr.GetPosition());
			}
			else if (temp_return.EntityId == terrRdr.CubeGrid.EntityId)
			{
				return -1;
			}
			else
			{
				return max_range;
			}

		}
		else
		{
			scanned = false;
			return max_range + 1;
		}
	}

	

	public double RecoFwdSpeed()
	{
		if (!exists) return 0;

		double spdFwdRaw = RecommendSpeed(d_fwd, d_rear, d_fwd_wide, d_rear_wide,
									d_fwd_left, d_fwd_right, d_rear_left, d_rear_right);

		if (sideScan.validFwd && sideScan.fwdDist < SideScan.MAX_RANGE && sideScan.validRear && sideScan.rearDist < SideScan.MAX_RANGE)
		{
			// Detect if we are in a canyon by looking at the ratio between
			// the altitude and the distance to the obstacles in front and rear.
			// If we are in a canyon, then we give more importance to the side scan recommendations,
			// which are more reactive and can detect closer obstacles,
			// while the radar can be confused by the canyon walls.
			fwdCanyonRatio = GetDistance() / (sideScan.fwdDist + sideScan.rearDist);

			spdFwdRaw = H.Interpolate(CANYON_RATIO_MIN, CANYON_RATIO_MAX, spdFwdRaw, sideScan.RecommendFwdSpeed(), fwdCanyonRatio);

			spdFwdRaw = H.MaxAbs(spdFwdRaw, HORIZ_MAX_SPEED);
		}

		if (sideScan.validFwd)
		{
			spdFwdLim = sideScan.fwdDist / SIDE_LIMIT_RATIO;
			spdFwdRaw = Math.Min(spdFwdRaw, spdFwdLim);
		}
		if (sideScan.validRear)
		{
			spdRearLim = -sideScan.rearDist / SIDE_LIMIT_RATIO;
			spdFwdRaw = Math.Max(spdFwdRaw, spdRearLim);
		}
	
		return spdFwdRaw;
	}

	public double RecoLeftSpeed()
	{
		if (!exists) return 0;

		double spdLeftRaw = RecommendSpeed(d_left, d_right, d_left_wide, d_right_wide,
						d_fwd_left, d_rear_left, d_fwd_right, d_rear_right);

		if (sideScan.validLeft && sideScan.leftDist < SideScan.MAX_RANGE && sideScan.validRight && sideScan.rightDist < SideScan.MAX_RANGE)
		{
			
			leftCanyonRatio = GetDistance() / (sideScan.leftDist + sideScan.rightDist);

			spdLeftRaw = H.Interpolate(CANYON_RATIO_MIN, CANYON_RATIO_MAX, spdLeftRaw, sideScan.RecommendLeftSpeed(), leftCanyonRatio);

			spdLeftRaw = H.MaxAbs(spdLeftRaw, HORIZ_MAX_SPEED);
		}

		if (sideScan.validLeft)
		{
			spdLeftLim = sideScan.leftDist / SIDE_LIMIT_RATIO;
			spdLeftRaw = Math.Min(spdLeftRaw, spdLeftLim);
		}
		if (sideScan.validRight)
		{
			spdRightLim = -sideScan.rightDist / SIDE_LIMIT_RATIO;
			spdLeftRaw = Math.Max(spdLeftRaw, spdRightLim);
		}

		return spdLeftRaw;
	}

	


	public string AltitudeDebugString()
	{
		if (!exists) return "[NO RADAR !]";

		return "[RADAR]"
		+ "\nRange:" + H.Cpct(altScanRange) + "m"
		+ " Avail:" + H.Cpct(altRdr.AvailableScanRange) + "m"
		+ "\nReturn type: " + rdrReturn.Type
		+ " Age: " + altAge;

	}

	public string TerrainDebugString()
	{
		if (!exists) return "[NO RADAR !]";

		return "[TERRAIN] Av range: " + H.Cpct(terrRdr.AvailableScanRange) + "m"
			+ " Scan: " + angle.ToString("00.0") + "°/" + doubleAngle.ToString("00.0") + " Dist: " + H.Cpct(terrScanRange) + "m"
			+ "\nFw: " + H.Cpct(d_fwd) + "/" + H.Cpct(d_fwd_wide)
			+ " Rr: " + H.Cpct(d_rear) + "/" + H.Cpct(d_rear_wide)
			+ " Fw spd: " + H.Cpct(RecoFwdSpeed())
			+ "\nLf: " + H.Cpct(d_left) + "/" + H.Cpct(d_left_wide)
			+ " Rt: " + H.Cpct(d_right) + "/" + H.Cpct(d_right_wide)
			+ " Lf spd: " + H.Cpct(RecoLeftSpeed())
			+ "\n"+sideScan.DebugString()
			+ "\nCanyon ratios (Fwd, Left): " + fwdCanyonRatio.ToString("0.00") + " " + leftCanyonRatio.ToString("0.00");
	}

	public List<string> LogNames()
	{
		return new List<string> { "d_fwd", "d_rear", "d_left", "d_right" };
	}

	public List<double> LogValues()
	{
		return new List<double> { d_fwd, d_rear, d_left, d_right };
	}

	// Private functions

	/// <summary>
	/// Perform one step of scanning the terrain in a double radar configuration
	/// </summary>
	private void ScanStep(double ship_pitch, double ship_roll)
	{
		switch (scan_step)
		{
			case 0:
				obstruction = false;
				obstruction = ScanPair(angle, 0, ship_pitch, ship_roll, terrScanRange, out d_fwd, out d_rear);
				scan_step++;
				break;

			case 1:
				obstruction = ScanPair(0, -angle, ship_pitch, ship_roll, terrScanRange, out d_left, out d_right);
				// If we only have one radar, the next scan will be back to step 1
				// otherwise continue with other steps. When in ScanMode.DoubleEarly mode,
				// the next steps do nothing, so the radar does the same steps as for a single
				// mode and then has a short pause.
				if (mode == ScanMode.SingNarr)
					scan_step = 0;
				else
					scan_step++;
				break;

			case 2:
				if (mode == ScanMode.DbleWide)
					obstruction = ScanPair(doubleAngle, 0, ship_pitch, ship_roll, terrScanRange, out d_fwd_wide, out d_rear_wide);
				scan_step++;
				break;

			case 3:
				if (mode == ScanMode.DbleWide)
					obstruction = ScanPair(0, -doubleAngle, ship_pitch, ship_roll, terrScanRange, out d_left_wide, out d_right_wide);
				scan_step++;
				break;

			case 4:
				if (mode == ScanMode.DbleWide)
					obstruction = ScanPair(diagAngle, -diagAngle, ship_pitch, ship_roll, terrScanRange, out d_fwd_left, out d_rear_right);
				scan_step++;
				break;

			case 5:
				if (mode == ScanMode.DbleWide)
					obstruction = ScanPair(diagAngle, diagAngle, ship_pitch, ship_roll, terrScanRange, out d_fwd_right, out d_rear_left);
				scan_step = 0;
				break;
		}
	}

	/// <summary>
	/// Cast a pair of rays to the specified pitch and yaw angles (relative to the vertical)
	/// symmetrical around the vertical, using information about the current ship pitch and roll angles.
	/// If both rays touch a planet or a large grid, return the distance for each ray, projected along the vertical axis,
	/// in the out values.
	/// Otherwise return the max range.
	/// </summary>
	private bool ScanPair(double scan_pitch, double scan_roll, double ship_pitch, double ship_roll, double max_range, out double dpos, out double dneg)
	{
		double cos_pitch = Math.Cos(H.degToRad * scan_pitch);
		double cos_roll = Math.Cos(H.degToRad * scan_roll);
		bool scannedPos, scannedNeg;
		dpos = ScanDir(scan_pitch, scan_roll, ship_pitch, ship_roll, max_range, out scannedPos) * cos_pitch * cos_roll;
		dneg = ScanDir(-scan_pitch, -scan_roll, ship_pitch, ship_roll, max_range, out scannedNeg) * cos_pitch * cos_roll;
		if (dpos < 0 || dneg < 0)
		{
			dpos = dneg = MAX_TERR_DIST;
			return true;
		}
		return false;
	}

	private double RecommendSpeed(double d_pos, double d_neg, double d_wide_pos, double d_wide_neg, double d_diag1, double d_diag2, double d_diag3, double d_diag4)
	{

		double alt = GetDistance();

		double vbase = Math.Atan2(d_pos - d_neg, Math.Tan(H.degToRad * angle) * (d_pos + d_neg));
		double vpos, vpos_raw;

		if (doubleRdr && mode == ScanMode.DbleWide)
		{
			double vwide = Math.Atan2(d_wide_pos - d_wide_neg, Math.Tan(H.degToRad * doubleAngle) * (d_wide_pos + d_wide_neg));
			double vdiag = Math.Atan2(d_diag1 + d_diag2 - d_diag3 - d_diag4, Math.Tan(H.degToRad * diagAngle) * (d_diag1 + d_diag2 + d_diag3 + d_diag4));
			vpos_raw = vbase + vwide + vdiag;

			if (d_wide_pos < d_pos && vpos_raw > 0 && d_pos > 0)
				vpos_raw *= Math.Pow(d_wide_pos / d_pos, 2);

			if (d_wide_neg < d_neg && vpos_raw < 0 && d_neg > 0)
				vpos_raw *= Math.Pow(d_wide_neg / d_neg, 2);

			if (d_pos < alt && vpos_raw > 0 && alt > 0)
				vpos_raw *= d_pos / alt;

			if (d_neg < alt && vpos_raw < 0 && alt > 0)
				vpos_raw *= d_neg / alt;

			vpos = vpos_raw * SPEED_SCALE;
		}
		else
		{
			vpos = vbase * 3 * SPEED_SCALE;
		}
		
		double maxspeed = Math.Min(alt, HORIZ_MAX_SPEED);
		dz = H.Interpolate(500, 2000, HORIZ_DEADZONE, 0, alt);

		return H.MaxAbs(H.DeadZone(vpos, dz), maxspeed);
	}
}

/// <summary>
/// EXPERIMENTAL : side scanning cameras to supplement the downward looking
/// </summary>
public class SideScan
{
	public bool validFwd, validRear, validLeft, validRight;
	public const double MAX_RANGE = 400;
	public double fwdDist=0, rearDist=0, leftDist=0, rightDist=0;

	IMyCameraBlock fwdCam, rearCam, leftCam, rightCam;
	MyDetectedEntityInfo[] fwdReturn, rearReturn, leftReturn, rightReturn;
	bool turn=false;
	int divFwd=0,divLeft=0;
	const int DIVS = 3;
	const float DOWN_ANGLE_LAND = 10;
	const float DOWN_ANGLE_PILOT = 20;
	const float START_ANGLE = 15;
	const float END_ANGLE = -15;
	const float ANGLE_STEP = (START_ANGLE - END_ANGLE) / DIVS;
	const float DIST_MAX=200;
	const float SPEED_MAX=20;

	public SideScan(IMyTerminalBlock fwd, IMyTerminalBlock rear, IMyTerminalBlock left, IMyTerminalBlock right)
	{
		fwdCam = fwd as IMyCameraBlock;
		rearCam = rear as IMyCameraBlock;
		leftCam = left as IMyCameraBlock;
		rightCam = right as IMyCameraBlock;

		ResetReturns();

		validFwd = fwdCam != null;
		validRear = rearCam != null;
		validLeft = leftCam != null;
		validRight = rightCam != null;
		
	}

	public void Disable()
	{
		if (validFwd)
			fwdCam.EnableRaycast = false;

		if (validRear)
			rearCam.EnableRaycast = false;

		if (validLeft)
			leftCam.EnableRaycast = false;

		if (validRight)
			rightCam.EnableRaycast = false;

		fwdDist = rearDist = leftDist = rightDist = MAX_RANGE;
		ResetReturns();
	}

	public void Enable()
	{
		if (validFwd)
			fwdCam.EnableRaycast = true;
		if (validRear)
			rearCam.EnableRaycast = true;
		if (validLeft)
			leftCam.EnableRaycast = true;
		if (validRight)
			rightCam.EnableRaycast = true;

		fwdDist = rearDist = leftDist = rightDist = MAX_RANGE;
		ResetReturns();
	}

	public void EnableFwdOnly()
	{
		Disable();
		if (validFwd)
			fwdCam.EnableRaycast = true;
	}

	public void Scan(double ship_pitch, double ship_roll)
	{
		if (turn && !(validFwd && !fwdCam.CanScan(MAX_RANGE) || validRear && !rearCam.CanScan(MAX_RANGE)))
		{
			if (validFwd)
			{
				fwdReturn[divFwd] = fwdCam.Raycast(MAX_RANGE, (float) -ship_pitch-DOWN_ANGLE_LAND, START_ANGLE-ANGLE_STEP*divFwd);
				fwdDist = ProcessReturn(fwdReturn, fwdCam);
			}

			if (validRear)
			{
				rearReturn[divFwd] = rearCam.Raycast(MAX_RANGE, (float) ship_pitch-DOWN_ANGLE_LAND, START_ANGLE-ANGLE_STEP*divFwd);
				rearDist = ProcessReturn(rearReturn, rearCam);
			}

			turn = false;
			divFwd++;
			if (divFwd >= DIVS) divFwd = 0;
		}


		if (!turn && !(validLeft && !leftCam.CanScan(MAX_RANGE) || validRight && !rightCam.CanScan(MAX_RANGE)))
		{
			if (validLeft)
			{
				leftReturn[divLeft] = leftCam.Raycast(MAX_RANGE, (float) -ship_roll-DOWN_ANGLE_LAND, START_ANGLE-ANGLE_STEP*divLeft);
				leftDist = ProcessReturn(leftReturn, leftCam);
			}

			if (validRight)
			{
				rightReturn[divLeft] = rightCam.Raycast(MAX_RANGE, (float) ship_roll-DOWN_ANGLE_LAND, START_ANGLE-ANGLE_STEP*divLeft);
				rightDist = ProcessReturn(rightReturn, rightCam);
			}

			turn = true;
			divLeft++;
			if (divLeft >= DIVS) divLeft = 0;
		}
	}

	public double ScanFwdOnly(double ship_pitch, double maxRange, out bool scanned)
	{
		scanned = false;
		double dist =maxRange; 
		if (validFwd && fwdCam.CanScan(maxRange))
		{
			var Return = new MyDetectedEntityInfo[1];
			Return[0] = fwdCam.Raycast(maxRange, (float)-ship_pitch-DOWN_ANGLE_PILOT, 0);
			dist = ProcessReturn(Return, fwdCam);
			dist = dist == MAX_RANGE ? maxRange : dist;
			scanned = true;
		}
		return dist;
	}

	public double RecommendFwdSpeed()
	{
		if (!validFwd || !validRear) return 0;

		fwdDist = ProcessReturn(fwdReturn, fwdCam);
		rearDist = ProcessReturn(rearReturn, rearCam);

		if (fwdDist < MAX_RANGE  && rearDist < MAX_RANGE)
		{
			return H.Interpolate(-DIST_MAX, DIST_MAX, -SPEED_MAX, SPEED_MAX, fwdDist- rearDist);
		}
		else
		{
			return 0;
		}
	}

	public double RecommendLeftSpeed()
	{
		if (!validLeft || !validRight) return 0;

		leftDist = ProcessReturn(leftReturn, leftCam);
		rightDist = ProcessReturn(rightReturn, rightCam);

		if (leftDist < MAX_RANGE && rightDist < MAX_RANGE)
		{
			return H.Interpolate(-DIST_MAX, DIST_MAX, -SPEED_MAX, SPEED_MAX, leftDist - rightDist);
		}
		else
		{
			return 0;
		}
	}

	public string DebugString()
	{
		string str = "[SIDE SCAN]";

			str += "\nF: " + H.Cpct(fwdDist) + "m";
			str += " R: " + H.Cpct(rearDist) + "m" ;
			str += " " + H.Cpct(RecommendFwdSpeed()) + "m/s";

		if (validLeft && validRight)

			str += "\nL: " + H.Cpct(leftDist) + "m";
			str += " R: " + H.Cpct(rightDist) + "m";
			str += " " + H.Cpct(RecommendLeftSpeed()) + "m/s";

		return str;
	}

	/// <summary>
	/// Process the array of returns from a side scan, and return an average distance to the obstacles in the scanned direction,
	/// considering only planets and large grids as obstacles. If no obstacle is detected, return the max range.
	/// </summary>
	/// <param name="scanReturn">An array of detected entities returned by a side scan</param>
	/// <param name="camera">The camera block that performed the scan</param>
	/// <returns>The average distance to obstacles in the scanned direction, or MAX_RANGE if no obstacle is detected</returns>
	private double ProcessReturn(MyDetectedEntityInfo[] scanReturn, IMyCameraBlock camera)
	{
		double total=0;
		double nb=scanReturn.Length;
		int i=0;

		foreach (var entity in scanReturn)
		{
			if ((entity.Type == MyDetectedEntityType.Planet || entity.Type == MyDetectedEntityType.LargeGrid) && entity.HitPosition.HasValue)
			{
				total += VRageMath.Vector3D.Distance(entity.HitPosition.Value, camera.GetPosition());

			}
			else
			{
				total += MAX_RANGE;
			}
			i++;

		}
		return total / nb;
		
	}

	private void ResetReturns()
	{
		fwdReturn = new MyDetectedEntityInfo[DIVS];
		rearReturn = new MyDetectedEntityInfo[DIVS];
		leftReturn = new MyDetectedEntityInfo[DIVS];
		rightReturn = new MyDetectedEntityInfo[DIVS];
	}

}

/// <summary>
/// See tech doc §10
/// </summary>
public class HorizontalThrusters
{
	ShipBlocks ship;
	PIDController fwdPID, leftPID;
	IMyShipController cockpit;
	readonly int DELAY;
	int timer;

	public HorizontalThrusters(ShipBlocks ship, int delay, double KP, double KI, double KD, double AImax)
	{
		this.ship = ship;
		this.cockpit = ship.Ctrller;
		this.DELAY = delay;
		fwdPID = new PIDController(KP, KI, KD, -AImax, AImax, 0.5, 1);
		leftPID = new PIDController(KP, KI, KD, -AImax, AImax, 0.5, 1);
	}

	public void Disable()
	{
		ship.fwdThr.Disable();
		ship.rearThr.Disable();
		ship.leftThr.Disable();
		ship.rightThr.Disable();
		fwdPID.Reset();
		leftPID.Reset();
	}

	public void TurnOn()
	{
		ship.fwdThr.TurnOn();
		ship.rearThr.TurnOn();
		ship.leftThr.TurnOn();
		ship.rightThr.TurnOn();
	}

	/// <summary>
	/// Compute and then apply thrust with the horizontal thrusters in order to achieve a desired
	/// horizontal speed (forward/back and left/right)
	/// </summary>
	/// <param name="shipMass">Mass of the ship (kg)</param>
	/// <param name="deadZone">Use or not a small dead zone around zero thrust</param>
	/// <param name="overridable">If the pilot can override with the keyboard keys</param>
	public void Tick(Speed speed, Speed speedSP, double shipMass, bool deadZone, bool overridable)
	{

		// If the player provides inputs, disable the override
		if ((overridable && (cockpit.MoveIndicator.Length() > 0.0f)) || cockpit.RotationIndicator.Length() > 0.0f)
		{

			Disable();
			timer = 0;

		}
		else if (timer > DELAY)
		{

			// Compute difference between speed and setpoint
			double fwdDelta = speedSP.f - speed.f;
			double leftDelta = speedSP.l - speed.l;
			double dz = deadZone ? 0.05 : 0;

			// Compute the thrust to apply to the ship to correct the speed difference, using the speed ratio
			fwdPID.UpdatePID(fwdDelta);
			leftPID.UpdatePID(leftDelta);

			// Apply the thrust to the thrusters, considering the sign of the difference
			ship.fwdThr.ApplyThrust(H.DeadZone(fwdPID.output, dz) * shipMass, 0, 0);
			ship.rearThr.ApplyThrust(H.DeadZone(-fwdPID.output, dz) * shipMass, 0, 0);
			ship.leftThr.ApplyThrust(H.DeadZone(leftPID.output, dz) * shipMass, 0, 0);
			ship.rightThr.ApplyThrust(H.DeadZone(-leftPID.output, dz) * shipMass, 0, 0);

		}
		else
		{
			timer++;
		}

	}

	public void UpdateThrust()
	{
		ship.fwdThr.UpdateThrust();
		ship.rearThr.UpdateThrust();
		ship.leftThr.UpdateThrust();
		ship.rightThr.UpdateThrust();
	}

	public string DebugString()
	{
		string str = "[FWD PID]: " + fwdPID.DebugString();
		str += "\n[LEFT PID]: " + leftPID.DebugString();
		return str;
	}

	public List<string> LogNames()
	{
		return new List<string> { "fwd_pid_output", "left_pid_output" };
	}

	public List<double> LogValues()
	{
		return new List<double> { fwdPID.output, leftPID.output };
	}
}

/// <summary>
/// A group of thrusters, that can mix atmospheric, ion, and hydrogen, all thrusting in the same direction. See tech doc §11
/// </summary>
public class ThrGroup
{
	// Thrust values in Newtons
	/// <summary>
	/// Maximum thrust value in Newtons, in the best atmosphere conditions (vacuum for ion thrusters, high density atmo for the atmo thrusters, etc.)
	/// </summary>
	public ThrustStat Max;
	/// <summary>
	/// Possible thrust value in Newtons, affected by atmosphere
	/// </summary>
	public ThrustStat Eff;
	/// <summary>
	/// Thrust currently produced (read back from the game API), in Newtons
	/// </summary>
	public ThrustStat Now;
	/// <summary>
	/// Override value (in percent) applied to each thruster type
	/// </summary>
	public ThrustStat Override;

	public double[] iTDensity = new double[11];
	public double[] pTDensity = new double[11];
	public double[] aTDensity = new double[11];

	List<IMyThrust> aThrusters, iThrusters, hThrusters, pThrusters;


	string GroupName;


	public ThrGroup(List<IMyThrust> thrusters, string groupName = "")
	{

		aThrusters = new List<IMyThrust>();
		iThrusters = new List<IMyThrust>();
		hThrusters = new List<IMyThrust>();
		pThrusters = new List<IMyThrust>();

		// Separate thruster by type
		// Add custom/modded thrusters here if needed
		foreach (var t in thrusters)
		{
			string name = t.BlockDefinition.SubtypeName.ToLower();
			string displayname = t.DefinitionDisplayNameText.ToString().ToLower();
			if (name.Contains("hydrogen") || name.Contains("epstein") || name.Contains("rcs")) hThrusters.Add(t);
			else if (name.Contains("ion") || displayname.Contains("ion")) iThrusters.Add(t);
			else if (name.Contains("atmo")) aThrusters.Add(t);
			else if (name.Contains("prototech")) pThrusters.Add(t);
		}
		GroupName = groupName;
	}

	public void UpdateThrust()
	{

		Eff = default(ThrustStat);
		Max = default(ThrustStat);
		Now = default(ThrustStat);

		foreach (IMyThrust at in aThrusters)
		{
			if (!at.Closed && at.IsWorking)
			{
				Eff.atmo += at.MaxEffectiveThrust;
				Max.atmo += at.MaxThrust;
				Now.atmo += at.CurrentThrust;
			}
		}

		foreach (IMyThrust it in iThrusters)
		{
			if (!it.Closed && it.IsWorking)
			{
				Eff.ion += it.MaxEffectiveThrust;
				Max.ion += it.MaxThrust;
				Now.ion += it.CurrentThrust;
			}
		}

		foreach (IMyThrust ht in hThrusters)
		{
			if (!ht.Closed && ht.IsWorking)
			{
				Eff.hydro += ht.MaxEffectiveThrust;
				Max.hydro += ht.MaxThrust;
				Now.hydro += ht.CurrentThrust;
			}
		}

		foreach (IMyThrust pt in pThrusters)
		{
			if (!pt.Closed && pt.IsWorking)
			{
				Eff.proto += pt.MaxEffectiveThrust;
				Max.proto += pt.MaxThrust;
				Now.proto += pt.CurrentThrust;
			}
		}
	}

	/// <summary>
	/// Use the thruster group to apply the wanted thrust, starting from the electric ones
	/// and using hydrogen thrusters only if needed. It's also possibly to force a minimum
	/// amount of atmospheric or ion thrust.
	/// </summary>
	/// <param name="wantedThrust">Wanted thrust in Newtons</param>
	/// <param name="aThrustMin">Forced minimum atmospheric thrust in Newtons</param>
	/// <param name="iThrustMin">Forced minimum ion/prototech thrust in Newtons</param>
	public void ApplyThrust(double wantedThrust, double aThrustMin, double iThrustMin)
	{
		ThrustStat thrust;
		Override = default(ThrustStat);

		thrust.atmo = H.SatMinMax(wantedThrust, aThrustMin, Eff.atmo);
		thrust.proto = H.SatMinMax(wantedThrust - thrust.atmo, iThrustMin, Eff.proto);
		thrust.ion = H.SatMinMax(wantedThrust - thrust.atmo - thrust.proto, iThrustMin - thrust.proto, Eff.ion);
		thrust.hydro = wantedThrust - thrust.atmo - thrust.proto - thrust.ion;

		Override.atmo =ApplyOverride(Eff.atmo, thrust.atmo, aThrusters);
		Override.ion  =ApplyOverride(Eff.ion, thrust.ion, iThrusters);
		Override.hydro=ApplyOverride(Eff.hydro, thrust.hydro, hThrusters);
		Override.proto=ApplyOverride(Eff.proto, thrust.proto, pThrusters);

	}

	/// <summary>
	/// Disable control of the thrusters by the script (the thrusters themselves remain on)
	/// </summary>
	public void Disable()
	{
		DisableList(aThrusters);
		DisableList(iThrusters);
		DisableList(hThrusters);
		DisableList(pThrusters);
	}

	/// <summary>
	/// Disable control of the thrusters by the script (the thrusters themselves remain on)
	/// </summary>
	public void TurnOn()
	{
		TurnOnList(aThrusters);
		TurnOnList(iThrusters);
		TurnOnList(hThrusters);
		TurnOnList(pThrusters);

	}

	/// <summary>
	/// Find the worst case atmo density for this thruster group
	/// (the one that minimizes atmo + ion + prototech thrust)
	/// </summary>
	/// <returns>Atmosphere density</returns>
	public double WorstDensity()
	{
		if (Max.atmo + Max.ion * 0.2 + Max.proto * 0.3 < Max.ion + Max.proto)
			return 1;
		else
			return 0.3;
	}

	/// <summary>
	/// Compute the thrust that atmospheric thrusters in this group can provide
	/// for a set atmosphere density
	/// </summary>
	/// <param name="density">atmosphere density</param>
	/// <returns>Thrust in Newtons</returns>
	public double AtmoThrustForDensity(double density)
	{
		return Math.Max(Max.atmo * (Math.Min(density, 1) * 1.43f - 0.43f), 0);
	}

	/// <summary>
	/// Compute the thrust that ion thrusters in this group can provide
	/// for a set atmosphere density
	/// </summary>
	/// <param name="density">atmosphere density</param>
	/// <returns>Thrust in Newtons</returns>
	public double IonThrustForDensity(double density)
	{
		return Max.ion * (1 - 0.8f * Math.Min(density, 1));
	}

	/// <summary>
	/// Compute the thrust that prototech thrusters in this group can provide
	/// for a set atmosphere density
	/// </summary>
	/// <param name="density">atmosphere density</param>
	/// <returns>Thrust in Newtons</returns>
	public double PrototechThrustForDensity(double density)
	{
		return Max.proto * (1 - 0.7f * Math.Min(density, 1));
	}

	public void UpdateDensitySweep()
	{
		for (int i = 0; i < 11; i++)
		{
			iTDensity[i] = IonThrustForDensity(i / 10.0);
			aTDensity[i] = AtmoThrustForDensity(i / 10.0);
			pTDensity[i] = PrototechThrustForDensity(i / 10.0);
		}
	}

	public string Inventory()
	{
		return "(" + iThrusters.Count + " I, " + aThrusters.Count + " A, " + hThrusters.Count + " H, " + pThrusters.Count + " P)";
	}

	public string DebugString()
	{
		return "[" + GroupName + "] A:" + Override.atmo.ToString("+0.00;-0.00") + " I:" + Override.ion.ToString("+0.00;-0.00") + " H:" + Override.hydro.ToString("+0.00;-0.00") + "P: " + Override.proto.ToString("+0.00;-0.00") + " WD" + WorstDensity().ToString("0.00") + "\nA: " + H.Cpct(Eff.atmo) + " I: " + H.Cpct(Eff.ion) + " H: " + H.Cpct(Eff.hydro) + " P: " + H.Cpct(Eff.proto);
	}

	private void DisableList(List<IMyThrust> thrusters)		
	{
		foreach (IMyThrust t in thrusters)
		t.ThrustOverride = 0;
	}

	private void TurnOnList(List<IMyThrust> thrusters)		
	{
		foreach (IMyThrust t in thrusters)
		{
			t.ThrustOverride = 0;
			t.Enabled = true;
		}

	}

	/// <summary>
	/// Compute and apply the overrides with a small dead zone
	/// If override is exactly zero then thrusters accept inputs from the player
	/// or the dampeners. Therefore we set a tiny value instead.
	/// </summary>
	/// <param name="eff"></param>
	/// <param name="thrust"></param>
	/// <param name="thrusters"></param>
	private double ApplyOverride(double eff, double thrust, List<IMyThrust> thrusters)
	{
		
		const float DZ = 0.01f;
		const float TINY = 0.000001f;

		double over;
		if (eff > 0)
		{
			over = (float)H.SatMinMax(thrust / eff, 0, 1);
			if (over < DZ) over = TINY;
		}
		else
		{
			over = TINY;
		}

		// Apply the thrust override to thrusters

		foreach (IMyThrust t in thrusters)
		{
			t.Enabled = true;
			t.ThrustOverridePercentage = (float) over;
		}

		return over;
	}

}

/// <summary>
/// Class used to time the execution of the script and provide statistics for each of
/// the main tasks (the tick1, tick10 and tick100 ones)
/// See tech doc §12.3
/// </summary>
public class RunTimeCounter
{

	readonly Program program;
	// Buffer sized for approximately 2 seconds
	RollingBuffer t1_buffer = new RollingBuffer(120);
	RollingBuffer t10_buffer = new RollingBuffer(12);
	RollingBuffer t100_buffer = new RollingBuffer(2);

	public RunTimeCounter(Program program)
	{
		this.program = program;
	}

	public void Count(bool ranTick1, bool ranTick10, bool ranTick100)
	{
		double runtime = program.Runtime.LastRunTimeMs;
		if (ranTick1 && !ranTick10 && !ranTick100) t1_buffer.Add(runtime);
		if (ranTick10 && !ranTick100) t10_buffer.Add(runtime);
		if (ranTick100) t100_buffer.Add(runtime);
	}

	public string RunTimeString()
	{
		string s = "";
		s += "Avg t1:" + t1_buffer.Average().ToString("0.00") + "ms";
		s += ", t10: " + t10_buffer.Average().ToString("0.00") + "ms";
		s += ", t100:" + t100_buffer.Average().ToString("0.00") + "ms";
		s += "\nMax t1:" + t1_buffer.Max().ToString("0.00") + "ms";
		s += ", t10: " + t10_buffer.Max().ToString("0.00") + "ms";
		s += ", t100:" + t100_buffer.Max().ToString("0.00") + "ms";

		return s;
	}

	public List<string> LogNames()
	{
		return new List<string> { "Avg t1", "Avg t10", "Avg t100", "Max t1", "Max t10", "Max t100" };
	}

	public List<double> LogValues()
	{
		return new List<double> { t1_buffer.Average(), t10_buffer.Average(), t100_buffer.Average(), t1_buffer.Max(), t10_buffer.Max(), t100_buffer.Max() };
	}

}


/// <summary>
/// Misc helper functions. See tech doc §12.4
/// </summary>
public class H
{

	public static double NotNan(double val)
	{
		if (double.IsNaN(val))
			return 0;
		return val;
	}

	/// <summary>
	/// Saturate a value within min and max values. The max value has priority, ie if min>max, the function returns max
	/// </summary>
	public static double SatMinMax(double value, double min, double max)
	{
		if (value > max || min > max) return max;
		if (value < min) return min;
		return value;
	}

	/// <summary>
	/// Returns the value clamped to the range [-maxabs,maxabs], keeping the sign of the original value.
	/// </summary>
	public static double MaxAbs(double value, double maxabs)
	{
		return Math.Min(Math.Abs(value), maxabs) * Math.Sign(value);
	}

	/// <summary>
	/// Returns the value clamped to the range [-maxabs,maxabs], keeping the sign of the original value.
	/// </summary>
	public static float MaxAbs(float value, float maxabs)
	{
		return Math.Min(Math.Abs(value), maxabs) * Math.Sign(value);
	}

	/// <summary>
	/// Returns the minimum of three values.
	/// </summary>
	public static double Min(double a, double b, double c)
	{
		return Math.Min(a, Math.Min(b, c));
	}

	/// <summary>
	/// Returns the minimum of four values.
	/// </summary>
	public static double Min(double a, double b, double c, double d)
	{
		return Math.Min(a, Math.Min(b, Math.Min(c, d)));
	}

	/// <summary>
	/// Returns the maximum of three values.
	/// </summary>
	public static double Max(double a, double b, double c)
	{
		return Math.Max(a, Math.Max(b, c));
	}

	/// <summary>
	/// Returns the maximum of four values.
	/// </summary>
	public static double Max(double a, double b, double c, double d)
	{
		return Math.Max(a, Math.Max(b, Math.Max(c, d)));
	}



	/// <summary>
	/// Returns 0 if the value is within [-deadzone ; deadzone], otherwise returns the value.
	/// </summary>
	public static double DeadZone(double value, double deadzone)
	{
		if (Math.Abs(value) < deadzone)
		{
			return 0;
		}
		else
		{
			return value;
		}
	}

	/// <summary>
	/// Interpolate between two points (X1,Y1) and (X2,Y2) to find the value Y at X.
	/// If X is outside the range [X1,X2], it returns Y1 or Y2 depending on the side.
	/// If X1 == X2, it returns Y1
	/// </summary>
	public static double Interpolate(double X1, double X2, double Y1, double Y2, double x)
	{
		if (X1 == X2) return Y1;
		if (x <= X1) return Y1;
		if (x >= X2) return Y2;

		return Y1 + (Y2 - Y1) * (x - X1) / (X2 - X1);
	}

	/// <summary>
	/// Interpolate smoothly between two points (X1,Y1) and (X2,Y2) to find the value Y at X.
	/// This uses a smooth interpolation curbe with zero slope at both ends
	/// If X is outside the range [X1,X2], it returns Y1 or Y2 depending on the side.
	/// If X1 == X2, it returns Y1
	/// </summary>
	public static double InterpolateSmooth(double X1, double X2, double Y1, double Y2, double x)
	{
		if (X1 == X2) return Y1;
		if (x <= X1) return Y1;
		if (x >= X2) return Y2;

		// Scale X to the range 0 to 1
		// The if above ensures that there is no division by zero

		double t = (x - X1) / (X2 - X1);

		return Y1 + (Y2 - Y1) * t * t * (3.0 - 2.0 * t);
	}

	/// <summary>
	/// Mixes two values a and b with a ratio. The ratio is clamped between 0 and 1.
	/// </summary>
	public static double Mix(double a, double b, double ratio_of_a)
	{
		double ratio = SatMinMax(ratio_of_a, 0, 1);
		return a * ratio + b * (1 - ratio);
	}

	public static double g_to_ms2(double g)
	{
		return g * 9.81;
	}

	public static double ms2_to_g(double a)
	{
		return a / 9.81;
	}

	public static void Rectangle(MySpriteDrawFrame frame, float x1, float x2, float y1, float y2, VRageMath.RectangleF view, float thickness, VRageMath.Color color)
	{

		float[] x = new float[4] { (x1 + x2) / 2, (x1 + x2) / 2, x1, x2 };
		float[] y = new float[4] { y1, y2, (y1 + y2) / 2, (y1 + y2) / 2 };
		float[] w = new float[4] { x2 - x1, x2 - x1, thickness, thickness };
		float[] h = new float[4] { thickness, thickness, y2 - y1, y2 - y1 };

		for (int i = 0; i < 4; i++)
		{

			MySprite s = MySprite.CreateSprite
			(
				"SquareSimple",
				new Vector2(x[i], y[i]) + view.Position,
				new Vector2(w[i], h[i])
			);
			s.Color = color;
			frame.Add(s);

		}
	}

	/// <summary>
	/// Formats a double value to a compact string representation.
	/// Values below 1000 use three characters, with one decimal place for values between 10 and 100, and two decimal places for values below 10.
	/// </summary>
	public static string Cpct(double value)
	{
		if (Math.Abs(value) > 100)
			return value.ToString("000");
		else if (Math.Abs(value) > 10)
			return value.ToString("00.0");
		else
			return value.ToString("0.00");
	}

	public static string Cpct(double value, int digits)
	{
		if (digits == 3)
			return value.ToString("+0.000;-0.000");
		else if (digits == 2)
			return value.ToString("+0.00;-0.00");
		else if (digits == 1)
			return value.ToString("+0.0;-0.0");
		else
			return value.ToString();
	}

	public static string Truncate(string str, int maxLength)
	{
		if (str.Length > maxLength)
			return str.Substring(0, maxLength);

		return str;
	}

	public static int FindN(string inString, List<string> prefixes)
	{
		// Parcourir les chiffres 0 à 9
		foreach (string prefix in prefixes)
		{
			for (int N = 0; N <= 9; N++)
			{
				string prefixN = prefix + N.ToString();
				if (inString.Contains(prefixN))
					return N;
			}
		}
		return -1;
	}

	public static double Average(double[] values)
	{
		if (values.Length == 0) return 0;
		double sum = 0;
		foreach (double value in values)
			sum += value;
		return sum / values.Length;
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
	double tstart = 0;
	bool allow;


	public Logger(List<string> names, int factor, bool allow)
	{
		this.names = names;
		this.FACTOR = factor;
		this.allow = allow;
	}

	public void Clear()
	{
		records.Clear();
		cnter = 0;
	}

	public void Log(List<double> record)
	{
		if (!allow) return;

		if (cnter == 0)
			tstart = DateTime.Now.TimeOfDay.TotalMilliseconds;

		if (cnter % FACTOR == 0)
		{
			List<double> new_record = new List<double>();
			new_record.Add(DateTime.Now.TimeOfDay.TotalMilliseconds - tstart);
			new_record.AddRange(record);
			records.Add(new_record);
		}
		cnter++;

	}

	public string Output()
	{

		// Format the output as a CSV
		// First line : names of the columns
		string output = "time(ms),";
		foreach (string name in names)
			output += name + ",";

		output += "\n";

		// Each line is a record
		foreach (List<double> record in records)
		{
			foreach (double value in record)
				output += value.ToString("0.00") + ",";

			output += "\n";
		}

		return output;
	}
}

public class MovingAverage
{

	double[] values;
	int index, size;
	double sum;

	public MovingAverage(int set_size)
	{
		values = new double[set_size];
		index = 0;
		size = set_size;
		sum = 0;
		for (int i = 0; i < size; i++)
		{
			values[i] = 0;
		}
	}

	public double AddValue(double value)
	{
		sum -= values[index];
		values[index] = value;
		sum += value;
		index = (index + 1) % size;
		return sum / size;
	}

	public double Get()
	{
		return sum / size;
	}

	public void Clear()
	{
		Set(0);
	}

	public void Set(double value)
	{
		sum = value * size;
		for (int i = 0; i < size; i++)
		{
			values[i] = value;
		}
	}

}


public class RollingBuffer
{
	double[] buffer;
	int index;

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
	double maxRatePositive;
	double maxRateNegative;
	double lastValue;

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

	// Actual speed set-points in m/s that will be sent to the thruster controller
	public Speed spdSP;
	// Desired speed for mode 4 (autopilot) in m/s
	public double m4SpdSP, m4AltSP;



	/// <summary>
	/// Estimated ground altitude in meters forward of the ship based on a camera raycast with a 40° angle
	/// (ex : if the ship is currently at 30m from the ground, and the ground is flat, forward should also be 30m
	/// if the ground slopes up, forward is <30m, if the ground slopes down, forward is >30m)
	/// </summary>
	public double fwdAlt1,fwdAlt2;
	public bool fwdValid1, fwdValid2;

	// Reference for the altitude set-point
	public enum AltMode
	{
		Ground,
		SeaLevel
	}
	public AltMode altMode;
	public MovingAverage altFilter;
	// Configuration parameters from SLMConfiguration
	/// <summary>
	/// Increment of the speed (in m/s) each 1/60 sec when keys are pressed. A value of
	/// 0.1 means that is the key is pressed continuously for 1 second, the speed target
	/// changes by 6 m/s.
	/// </summary>
	readonly double speedIncrement;
	readonly double maxSpeed, ssamin, ssamax, ssmin, ssmax, minVertSpeed;
	// PID acting on altitude and outputting a vertical speed setpoint
	PIDController alt_PID;
	MovingAverage fwdSpeedFilter, leftSpeedFilter, fwdAlt1Filter, fwdAlt2Filter;
	/// <summary>
	/// Maximum safe speed in m/s considering ground altitude (low value near the ground, higher value the higher the ship flies)
	/// </summary>
	MovingAverage safeSpeedFilter;

	public AutoPilot(SLMConfig c)
	{
		alt_PID = new PIDController(c.altKp, c.altKi, c.altKd, c.alt_aiMin, c.alt_aiMax, c.altAdFilt, c.altAdMax);
		fwdSpeedFilter = new MovingAverage(c.speedFilterLength);
		leftSpeedFilter = new MovingAverage(c.speedFilterLength);
		altFilter = new MovingAverage(c.altFilterLength);
		safeSpeedFilter = new MovingAverage(c.safeSpeedFilterLength);
		fwdAlt1Filter = new MovingAverage(3);
		fwdAlt2Filter = new MovingAverage(3);
		speedIncrement = c.speedIncrement;
		maxSpeed = c.maxSpeed;
		ssamin = c.safeSpeedAltMin;
		ssamax = c.safeSpeedAltMax;
		ssmin = c.safeSpeedMin;
		ssmax = c.safeSpeedMax;
		minVertSpeed = c.minVertSpeed;
		fwdAlt1 = ssamax;
		fwdAlt2 = ssamax;
		fwdValid1 = false;
		fwdValid2 = false;
	}

	public void Init()
	{
		spdSP = new Speed { f = 0, l = 0, v = 0 };
		alt_PID.Reset();
		fwdSpeedFilter.Clear();
		leftSpeedFilter.Clear();
		altFilter.Clear();
		safeSpeedFilter.Clear();
		altMode = AltMode.Ground;
	}

	// Instant direct control with the controller or keyboard : when the key is pressed
	// then the speed setpoint is a fixed value depending on altitude
	// Can move forward, backward, left, right.
	// This is used in mode 3
	public void UpdateSpeedDirect(Vector3 moveIndicator)
	{
		spdSP.f = 0;
		spdSP.l = 0;

		double safe = safeSpeedFilter.Get();

		if (moveIndicator.Z > 0.0f)
			fwdSpeedFilter.AddValue(-safe);
		else if (moveIndicator.Z < 0.0f)
			fwdSpeedFilter.AddValue(safe);
		else
			fwdSpeedFilter.AddValue(0);

		spdSP.f = fwdSpeedFilter.Get();

		if (moveIndicator.X > 0.0f)
			leftSpeedFilter.AddValue(-safe);
		else if (moveIndicator.X < 0.0f)
			leftSpeedFilter.AddValue(safe);
		else
			leftSpeedFilter.AddValue(0);

		spdSP.l = leftSpeedFilter.Get();
	}

	// Progressive control with the controller or keyboard : when the forward/back key is pressed
	// then the speed setpoint is increased or reduced progressively
	// Can only move forward.
	// This is used in mode 4
	public void UpdateSpeedProgressive(Vector3 moveIndicator)
	{

		spdSP.l = 0;

		if (moveIndicator.Z > 0.0f && m4SpdSP >= speedIncrement)
			m4SpdSP -= speedIncrement;
		else if (moveIndicator.Z < 0.0f && m4SpdSP < maxSpeed)
			m4SpdSP += speedIncrement;

		spdSP.f = Math.Min(m4SpdSP, safeSpeedFilter.Get());
	}

	/// <summary>
	/// Create a vertical speed set-point to maintain set altitude
	/// </summary>
	/// <param name="gndAltitude"></param>
	/// <param name="slAltitude"></param>
	/// <param name="gravity"></param>
	public void UpdateVertSpeedSP(double gndAltitude, double slAltitude, double gravity)
	{

		altFilter.AddValue(m4AltSP);

		double altDelta = 0;
		double relevantGndAltitude;


		// Anticipation of the ground sloping up forward of the ship
		// If the "forward" distance is invalid, we ignore it.
		if (fwdValid1)
			fwdAlt1Filter.AddValue(fwdAlt1);
		if (fwdValid2)
			fwdAlt2Filter.AddValue(fwdAlt2);

		if (m4SpdSP > 1) 
		{
			relevantGndAltitude = fwdValid1 ? Math.Min(gndAltitude, fwdAlt1Filter.Get() + 1) : gndAltitude;
			relevantGndAltitude = fwdValid2 ? Math.Min(relevantGndAltitude, fwdAlt2Filter.Get() + 1) : relevantGndAltitude;
		}
		else
			relevantGndAltitude = gndAltitude;

		switch (altMode)
		{

			// In ground reference mode, we simply maintain the desired ground altitude
			case AltMode.Ground:
				altDelta = altFilter.Get() - relevantGndAltitude;
				break;

			// In sea level reference mode, the desired speed is given more importance
			// thus we maintain a sufficient ground altitude to stay at the desired speed
			// and climb at higher altitudes (relative to sea level) if needed.
			// This also has the benefit of not hitting mountains !
			case AltMode.SeaLevel:

				// Compute the altitude where the safe speed is the desired speed
				double minGNDaltitude = H.Interpolate(ssmin, ssmax, ssamin, ssamax, m4SpdSP);

				double altDeltaSL = altFilter.Get() - slAltitude;
				double altDeltaGND = minGNDaltitude - relevantGndAltitude;

				altDelta = Math.Max(altDeltaSL, altDeltaGND);
				break;
		}

		// Above a certain altitude, the delta value fed to the PID is reduced
		// for a smoother fly
		altDelta = altDelta / H.SatMinMax(relevantGndAltitude / 50, .1, 1);

		alt_PID.UpdatePIDController(altDelta, -0.5, 5);

		// A correction factor is applied in feedforward to climb more quickly
		// if the altitude is really too low
		spdSP.v = alt_PID.output + Math.Max(altDelta - 10, 0) * 0.5;

		// Saturate the vertical speed set point to avoid overshooting the target altitude
		// This is based on the vertical kinetic energy compared to the potential energy remaining
		// m*g*h = 1/2 * m * v²
		// So when v = sqrt(2*g*h) then the acquired vertical speed is sufficient to perform the desired altitude change
		if (altDelta > 0)
		{
			double equiv_speed = Math.Sqrt(2 * gravity * altDelta + 5);
			spdSP.v = Math.Min(spdSP.v, equiv_speed);
		}

		// Saturate the vertical speed set point (for negative values) as configured
		spdSP.v = Math.Max(spdSP.v, minVertSpeed);

	}

	public void UpdateSafeSpeed(double gndAltitude, double forward1, double forward2)
	{
		double alt = (forward1 > 0) ? Math.Min(gndAltitude, forward1) : gndAltitude;
		alt = forward2 > 0 ? Math.Min(alt, forward2) : alt;
		safeSpeedFilter.AddValue(H.Interpolate(ssamin, ssamax, ssmin, ssmax, alt));
	}

	public string DebugString()
	{
		return "[AUTOPILOT]\nfwdAlt:" + H.Cpct(fwdAlt1) + "m " + H.Cpct(fwdAlt1Filter.Get()) + "m " + fwdValid1.ToString() 
		+ "\nfwdAlt2:" + H.Cpct(fwdAlt2) + "m " + H.Cpct(fwdAlt2Filter.Get()) + "m " + fwdValid2.ToString()
		+ "\nPIDout:" + H.Cpct(alt_PID.output) + "m/s " + H.Cpct(spdSP.v) + " ms/s\n" + alt_PID.DebugString();
	}

	public List<string> LogNames()
	{
		return new List<string> { "fwdAlt", "fwdAltFilter", "vertSpeedSP" };
	}

	public List<double> LogValues()
	{
		return new List<double> { fwdAlt1, fwdAlt1Filter.Get(), spdSP.v };
	}

}

public class GPSGuide 
{
	public string tgtName="Unknown";
	Vector3D tgt = Vector3D.Zero;
	ShipBlocks ship;
	ShipInfo info;
	/// <summary>
	/// If the ratio of altitude / (horizontal distance to the target) is below this value, we cancel GPS guidance and make an emergency landing.
	/// </summary>
	const double SLOPE_CANCEL = 0.5;
	/// <summary>
	/// The vertical speed recommandation aims for this slope, as the ratio of altitude / (horizontal distance to the target).
	/// </summary>
	const double SLOPE_RECO = 1;
	const double DIST_RATIO_CANCEL = 2;
	
	/// <summary>
	/// Proportional gain used when the distance to the target is below DIST_SWITCH, to compute the recommended speed as Kp*distance. This is used to have a smoother stop when close to the target. A value of 0.1 means that at 10m from the target, the recommended speed is 1 m/s, and at 1m from the target, the recommended speed is 0.1 m/s.
	/// </summary>
	const double KP = 0.12;
	/// <summary>
	/// Maximum recommended speed in m/s when flying at high altitude, in m/s.
	/// </summary>
	readonly double MAXSPD;
	/// <summary>
	/// Ratio used to compute the maximum recommended speed based on altitude. The maximum recommended speed is computed as alt/ALTRATIO, with a minimum of 1 m/s and a maximum of MAXSPD. A value of 15 means that at 150m of altitude, the maximum recommended speed is 10 m/s, and at 300m of altitude, the maximum recommended speed is 20 m/s.
	/// </summary>
	const double ALTRATIO = 15;
	/// <summary>
	/// Distance below which the speed is built with a proportional model (the further, the higher the speed), and above which the speed is built with a constant acceleration model (the further, the higher the speed, but with a lower slope than the proportional model). This is to have a smoother stop when close to the target, and a more reactive behavior when far from the target.
	/// </summary>
	const double DIST_SWITCH=10;
	/// <summary>
	/// The ship lateral thrust capability is divided by this value when computing the recommended speed, to have a margin and avoid saturating the thrusters. A value of 1.3 means that we use at most 77% of the available thrust for the speed recommendation, leaving a margin of 23% to correct for unexpected situations without saturating the thrusters.
	/// </summary>
	const double MARGIN_THR = 1.3;
	/// <summary>
	/// Same as MARGIN_THR but for the tilt-based speed recommendation. We use a larger margin because ship tilt has a reaction delay.
	/// </summary>
	const double MARGIN_TILT = 5;
	/// <summary>
	/// If in any direction (forward, backward, left, right) the maximum available acceleration from the main thrusters is below a certain threshold (in m/s²), then we consider that the ship needs to use lift thrusters and ship tilt to have more control authority in this direction
	/// </summary>
	const double MIN_LATERAL_ACCEL = 0.5;
	

	public GPSGuide(ShipBlocks s, ShipInfo i, SEGameConfig seconfig)
	{
		ship = s;
		info = i;
		MAXSPD = Math.Min(seconfig.MaxSpeed/1.414, 150);
	}


	/// <summary>
	/// Compute the distance to the target in the forward direction of the ship, correcting for the current pitch angle and altitude.
	/// </summary>
	/// <param name="pitch">Pitch angle in degrees, positive = nose up</param>
	/// <param name="alt">Ground altitude in meters</param>
	/// <returns></returns>
	public double DistFwd(double pitch, double alt)
	{
		
		return DistDir(pitch, alt, ship.Ctrller.WorldMatrix.Forward);
	}

	/// <summary>
	/// Compute the distance to the target in the left direction of the ship, correcting for the current roll angle and altitude.
	/// </summary>
	/// <param name="roll">Roll angle in degrees, positive = roll right</param>
	/// <param name="alt">Ground altitude in meters</param>
	/// <returns></returns>
	public double DistLeft(double roll, double alt)
	{
		return DistDir(roll, alt, ship.Ctrller.WorldMatrix.Left);
	}

	/// <summary>
	/// Compute the distance to the target in the horizontal plane, correcting for the current pitch and roll angles and altitude.
	/// </summary>
	/// <param name="pitch"></param>
	/// <param name="roll"></param>
	/// <param name="alt"></param>
	/// <returns></returns>
	public double Dist(double pitch, double roll, double alt)
	{
		double fwd = DistFwd(pitch, alt);
		double left = DistLeft(roll, alt);
		return Math.Sqrt(fwd * fwd + left * left);
	}

	/// <summary>
	/// Compute a recommended forward speed to reach the target.
	/// </summary>
	/// <param name="pitch"></param>
	/// <param name="alt"></param>
	/// <returns></returns>
	public double RecoFwdSpeed(double pitch, double alt, bool angle=false)
	{
		// Maximum acceleration from thrusters
		double maxAccel = Math.Min(ship.fwdThr.Eff.Total,ship.rearThr.Eff.Total) / (info.mass*MARGIN_THR+1);
		return RecoSpeed(DistFwd(pitch, alt) , Math.Max(maxAccel, angle ? MaxAccelFromTilt(): 0), alt);
	}

	public double RecoLeftSpeed(double roll, double alt, bool angle=false)
	{
		// Maximum acceleration from thrusters
		double maxAccel = Math.Min(ship.leftThr.Eff.Total, ship.rightThr.Eff.Total) / (info.mass*MARGIN_THR+1);
		return RecoSpeed(DistLeft(roll, alt), Math.Max(maxAccel, angle ? MaxAccelFromTilt(): 0), alt);
	}

	/// <summary>
	/// Compute a maximum speed recommendation based on altitude, to avoid crashing into the ground. The higher the altitude, the higher the maximum recommended speed.
	/// </summary>
	/// <param name="alt"></param>
	/// <returns></returns>
	public double maxSpeed(double alt)
	{
		return Math.Min(alt / ALTRATIO + 1, MAXSPD);
	}

	public string DebugString(double pitch,double roll, double alt)
	{
		return "[GPS GUIDE] Dist fwd: "+ H.Cpct(DistFwd(pitch, alt)) + "m left: " + H.Cpct(DistLeft(roll, alt)) + "m Max speed: " + H.Cpct(maxSpeed(alt)) + "m/s\nReco fwd spd: " + H.Cpct(RecoFwdSpeed(pitch, alt)) + "m/s left spd: " + H.Cpct(RecoLeftSpeed(roll, alt)) + "m/s";
	}

	/// <summary>
	/// Update the target position from a GPS string. The GPS string format is "GPS:name:x:y:z:". The function returns true if the GPS string is valid and the target position is updated, false otherwise.
	/// </summary>
	/// <param name="gpsString"></param>
	/// <returns></returns>
	public bool UpdateTargetFromGPS(string gpsString)
	{

		// GPS string format is "GPS:name:x:y:z:"
		string[] parts = gpsString.Split(':');
		if (parts.Length >= 5 && parts[0] == "GPS")
		{
			double x, y, z;
			if (double.TryParse(parts[2], out x) && double.TryParse(parts[3], out y) && double.TryParse(parts[4], out z))
			{
				tgtName = parts[1];
				tgt = new Vector3D(x, y, z);
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Determine if the ship needs to apply lift thrusters to change speed horizontally. If in any direction (forward, backward, left, right) the maximum available acceleration from the main thrusters is below a certain threshold (in m/s²), then we consider that the ship needs to use lift thrusters and ship tilt to have more control authority in this direction. The function returns false if the altitude is unknown, as we can't take a decision in this case.
	/// </summary>
	/// <param name="alt"></param>
	/// <returns></returns>
	public bool NeedLifters()
	{
		return H.Min(ship.fwdThr.Eff.Total, ship.rearThr.Eff.Total, ship.leftThr.Eff.Total, ship.rightThr.Eff.Total)/info.mass < MIN_LATERAL_ACCEL;
	}

	public double RecoVertSpeed(double pitch, double roll, double gndAlt)
	{
		return maxSpeed(gndAlt) * SLOPE_RECO * gndAlt / (Dist(pitch, roll, gndAlt)+0.1);
	}

	/// <summary>
	/// Determine if the GPS guidance should be cancelled and an emergency landing should be performed, based on reachability of the target considering horizontal distance and altitude.
	/// </summary>
	/// <param name="pitch"></param>
	/// <param name="roll"></param>
	/// <param name="alt"></param>
	/// <returns></returns>
	public bool ShouldCancel(double pitch, double roll, double alt)
	{
		if (alt == GroundRadar.UNDEF) return false;
		return alt / (Dist(pitch, roll, alt)+0.1) < SLOPE_CANCEL || Vector3D.Distance(tgt, Center()) / alt > DIST_RATIO_CANCEL;
	}

	/// <summary>
	/// Compute the distance to the target in a given direction (forward or left), correcting for the current angle (pitch or roll) and altitude.
	/// </summary>
	/// <param name="angle"></param>
	/// <param name="alt"></param>
	/// <param name="dir"></param>
	/// <returns></returns>
	private double DistDir(double angle, double alt, VRageMath.Vector3D dir)
	{
		// If altitude is unknown, can't apply the correction, so we return zero to avoid giving a wrong distance value
		if (alt == GroundRadar.UNDEF) return 0;
		
		// To get the distance to the target in the forward direction, we project the vector from the ship to the target on the forward vector of the ship,
		// then we apply a correction to take into account the altitude and the pitch angle of the ship.
		return Vector3D.Dot(tgt - Center(), dir) / Math.Cos(angle*H.degToRad) + alt*Math.Tan(angle*H.degToRad);
	}

	private Vector3D Center()
	{
		// If the ship has a named connector, then its position is used as the reference, otherwise use the center of the ship.
		return ship.Connector != null ? ship.Connector.GetPosition() : ship.Ctrller.CubeGrid.WorldAABB.Center;
	}

	/// <summary>
	/// Compute a recommended speed to reach the target based on the distance to the target in a given direction, and the maximum acceleration that can be achieved in this direction (based on the thrusters and the tilt). The further the target, the higher the recommended speed, up to a maximum value depending on altitude. If the distance is below a certain threshold, the recommended speed is proportional to the distance (for a smoother stop), otherwise it is based on a constant acceleration model.
	/// </summary>
	/// <param name="dist"></param>
	/// <param name="maxAccel"></param>
	/// <param name="alt"></param>
	/// <returns></returns>
	private double RecoSpeed(double dist, double maxAccel, double alt)
	{
		double speed = Math.Abs(dist) > DIST_SWITCH ? Math.Sqrt(2 * maxAccel * (Math.Abs(dist) - DIST_SWITCH / 2)) * Math.Sign(dist) : KP * dist;

		return H.MaxAbs(speed,maxSpeed(alt));
	}

	/// <summary>
	/// Compute the maximum lateral acceleration that the ship can achieve if using the maximum allowed tilt and the lift thrusters.
	/// </summary>
	/// <returns></returns>
	private double MaxAccelFromTilt()
	{
		return ship.lifts.Eff.Total * Math.Sin(info.MaxAngle()*H.degToRad) / (info.mass*MARGIN_TILT+1);
	}
}


		// End of partial class
	}
}
