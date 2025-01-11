
/*
-------------------------------------
SOFT LANDING MANAGER by silverbluemx
-------------------------------------

A script to automatically manage your thrusters to land safely on planets while
optimizing your fuel and energy use.

Version 1.0 - 2024-12-21 - 	First public release
Version 1.1 - 2025-01-11 - 	Added pre-computation of landing profile from ship, atmosphere and gravity model
							Added visualisatio of landing profile on a separate display
							Added planet catalog to fine-tune the landing profile

Designed for use with:
- inverse square law gravity mods such as Real Orbits
- high speed limit mods such as 1000m/s speed mod
If you're not using both of these, then the script won't work well but also is not really needed.
- ships with not a lof of margin for vertical thrust (ex : lift to weight ratio of 1.5)

Functions:
- computes and follows an optimal vertical speed profile for the descent
- prioritizes electric thrusters (atmospheric and ion) before using hydrogen ones
- uses a radar (raycasts from a downward facing camera) to measure your altitude way above
	what the game normally provides (useful for planets with gravity extending more than 100km from them!)
- computes the maximum lift that your ship is capable of (both in vacuum and ideal atmosphere)
- provides an estimate of the surface gravity for the planet
- warns if the ship is not capable of landing on the planet
- automatically deploy parachutes if about to crash

Installation:
- (optional but recommanded) install a downwards facing camera on your ship
- install the script in a programmable block
- (optional but recommanded) configure the names of the downward camera and ship controller (cockpit, helm etc.)
- (optional) configure LDCs, timers, sound blocks etc. as needed, see below for the functions they provide
- recompile the script to let it autoconfigure itself
- (recommanded) Install and configure on your ship an auto-levelling script such as flight assist or other

Usage:
- Set your ship in the gravity field of a planet, properly leveled
- Activate your auto-levelling script, or keep the ship level manually
- Activate mode1 or mode2 to have the script manage your descent
- (optionnally) Set vacuum or atmosphere mode, or select a planet to optimize the descent profile
- Once landed, check if the script automatically switched to mode0 (off), if not turn it off yourself

Command line arguments:
- mode0 : turns the script off (the LCDs still give you information about your ship capabilities)
- mode1 : activate a descent mode that prioritizes electric thrusters (atmospheric and ions)
	It fires the ion thrusters early to bleed the gravitational potential energy and reduce the work
	left for the hydrogen thrusters. However this uses more total electrical energy than mode2
- mode2 : activate a faster descent mode that fires thrusters only when needed
	It is possible to switch between mode1 and mode2 during the descent, for example use mode2 to
	let the ship pick up speed, and then switch to mode1 to try and maintain that speed using ion thrusters
- vacuum : tells the script to expect vacuum at the planet surface (atmo thrusters won't work, max ion efficiency)
- atmo : tells the script to expect atmosphere at the planet surface (atmo thrusters will work, low ion efficiency)
- unknown : tells the script that the surface atmosphere conditions are unknown (default setting)
- earth, mars, moon etc. : optimize the script for the selected planet
Can be combined, ex : mode1mars, mode2atmo, etc.

*/

// Settings

/// <summary>
/// Configuration class for the Soft Landing Manager script.
/// Contains settings for various ship components and their tags.
/// CONFIGURE HERE THE TAGS USED FOR SHIP BLOCKS
/// </summary>
public class SLMShipConfiguration {

	// OPTINAL BUT RECOMMANDED : Reference controller (seat, cockpit, etc.) to use for the ship orientation.
	// This is optional, if the script doesnt find a controller with this name then it
	// tries to find another suitable controller on the grid.
	public readonly string ctrller_name =  "SLMref";

	// OPTIONAL BUT RECOMMANDED : Name of downward-facing camera used as a ground radar
	// (to measure altitude from very long distance
	// and also account for landing pads above or below a planet surface)
	public readonly string radar_name =  "SLMradar";

	// OPTIONAL : Include this tag in downward facing thruster (ie thrusters that lift the ship)
	// that you want this script to ignore. For example, on an auxiliary drone.
	public readonly string lifter_ignore_name = "SLMignore";

	// OPTIONAL : Name of the main display for the script (there can be any number of them, or none at all)
	public readonly string lcd_name = "SLMdisplay";

	// OPTIONAL : Additional debug display for the script (there can be any number of them, or none at all)
	public readonly string debuglcd_name = "SLMdebug";

	// OPTIONAL : Additional graphical display for the script (there can be any number of them, or none at all)
	public readonly string graphlcd_name = "SLMgraph";

	// OPTIONAL : Name of timer blocks that will be triggered a little before landing (ex : extend landing gear)
	public readonly string landing_timer_name =  "SLMlanding";

	// OPTIONAL : Name of timer blocks that will be triggered a little after liftoff landing (ex : retract landing gear)
	public readonly string liftoff_timer_name =  "SLMliftoff";
	// OPTIONAL : Name of timer blocks that will be triggered when the SLM activates (ex : by the command "mode1")
	public readonly string on_timer_name =  "SLMon";
	// OPTIONAL : Name of timer blocks that will be triggered when the SLM disactivates (ex : by the command "off", or at landing)
	public readonly string off_timer_name =  "SLMoff";

	// OPTIONAL : Name of a sound block used to warn if expected surface gravity is higher than what
	// the ship can handle or the ship is in panic mode (incapable of slowing down enough)
	public readonly string sound_name =  "SLMsound";
	
}

public class SLMConfiguration {

	// SPEED/ALTITUDE PROFILE MODE

	// False to always use the old descent profile formula (same as v1.0).
	public readonly bool USE_PROFILE = true;
	// True to recompute the profile only 1,6sec, to reduce the script performance impact. False to recompute the profile every 160ms.
	public readonly bool SLOW_PROFILE_UPDATE = false;
	
	// ALTITUDE CORRECTION

	// Offset to the altitude value (ex : if the ship reads altitude 5m when landed, set it to 5)
	public readonly double altitude_offset = 0;
	

	// PID CONTROLLER CONFIGURATION
	public readonly float ai_max = 4;
	public readonly float ai_min = -0.1f;
	public readonly float kp = 0.05f;
	public readonly float ki = 0.05f;
	public readonly float kd = 10f;
	public readonly float ad_filt=0.5f; // Low-pass filtering of the derivative component (0:no filtering, 1:values don't move!)

	// LIFT TO WEIGHT RATIO SETTINGS

	// Margins applied to the ship LWR when computing the vertical speed target.
	public readonly double LWRoffset = 0.0;
	public readonly double LWRsafetyfactor = 1.1;
	// The ship will not use a lift weight ratio higher than this limit
	public readonly double LWRlimit = 3;
	// The ship will not apply vertical acceleration higher than this limit
	public readonly double accel_limit = 30; // m/s² including gravity

	// SPEED TARGET COMPUTATION AT HIGH ALTITUDE
	public readonly double vspeed_default = 200;
	public readonly double vspeed_safe_limit = 500;
	// When computing the vertical speed target, with the altitude/gravity formula
	// the script uses a mix of the lift to weight
	// ratio at the current altitude, and the expected lift-to-weight ratio at the planet surface
	// This setting defines the weight for the surface value (ex : 0.6 = 60% ground, 40% now)
	// A high value gives a more cautious landing, a low value a faster and riskier one
	public readonly double LWR_mix_surf_ratio = 0.6;

	// MODE 1 SETTINGS

	// In mode 1, if the absolute value of the vertical speed is higher than this limit
	// and the ship is above this altitude, then ion thrusters will be set to completely
	// compensate the ship weight (if they are capable)
	public readonly double early_ion_alt_limit = 2000;
	public readonly double early_ion_speed_limit = 100;

	// In mode 1, the ship evaluates if electric thrusters (ion and atmospheric) are
	// sufficient on their own to land, otherwise it uses the hydrogen thrusters as well.
	
	// LWR of the electric thrusters when they start to be prioritized
	public readonly double elec_LWR_start = 1.3;
	// LWR of the electric thrusters when they are considered sufficient
	public readonly double elec_LWR_sufficient = 2;

	// SPEED TARGET FOR FINAL LANDING

	// Below the transition altitude, the vertical speed target is a constant
	// and in addition, we revert to radar altitude measurement (if available)
	public readonly double transition_altitude = 20;
	public readonly double final_speed = 1.5;

	// PANIC MODE

	// The ship will enter panic mode if the delta between speed target and actual speed
	// is higher than this setting, while below the configured altitude
	public readonly double panic_altitude = 2000;
	public readonly double panic_speed_delta = 30;

	// LANDING/LIFTOFF TIMERS TRIGGER ALTITUDE
	public readonly double landing_timer_altitude = 200;
	public readonly double liftoff_timer_altitude = 250;

	// RADAR CONFIGURATION
	// The camera used as a ground radar will limit itself to this range (in meters)
	public readonly double radar_max_range = 2e5;

	// SURFACE GRAVITY ESTIMATOR
	public readonly double grav_est_altitude_transition_start = 10000;
	public readonly double grav_est_altitude_transition_end = 5000;
	public readonly double ground_grav_estimate_proportion = 0.8;
}

public enum SetPointSource {
	None,
	Profile,
	AltGravFormula,
	GravFormula,
	FinalSpeed

}

public struct PlanetInfo {
	public string shortname;
	public string name;
	public float atmo_density_sealevel;
	public float atmo_limit_altitude;
	public float hillparam;
	public float g_sealevel;
	public bool identified;
	public bool ignore_atmo;
}

public class PlanetCatalog {
	private List<PlanetInfo> Catalog;

	public PlanetCatalog() {

		Catalog = new List<PlanetInfo>();

		PlanetInfo unknown = new PlanetInfo();

		// The most generic planet, we don't know anything about it.
		// It must always be in first position
		// It is defined here as having a thick atmosphere to lower ion effectiveness but because it
		// is unknown then the script will also completely ignore atmospheric thrusters capability
		unknown.shortname = "unknown";
		unknown.name ="Unknown Planet";
		unknown.atmo_density_sealevel=1;
		unknown.atmo_limit_altitude=2;
		unknown.hillparam=0.20f;
		unknown.g_sealevel=1f;
		unknown.ignore_atmo=true;
		unknown.identified=false;
		Catalog.Add(unknown);

		PlanetInfo vacuum = new PlanetInfo();
		vacuum.shortname = "vacuum";
		vacuum.name ="Generic Vacuum Planet";
		vacuum.atmo_density_sealevel=0;
		vacuum.atmo_limit_altitude=0;
		vacuum.hillparam=0.1f;
		vacuum.g_sealevel=1f;
		vacuum.ignore_atmo=false;
		vacuum.identified=false;
		Catalog.Add(vacuum);

		PlanetInfo atmo = new PlanetInfo();
		// We assume a dense atmosphere but that doesn't go as high as Earthlike
		atmo.shortname = "atmo";
		atmo.name ="Generic Atmo Planet";
		atmo.atmo_density_sealevel=1;
		atmo.atmo_limit_altitude=1;
		atmo.hillparam=0.05f;
		atmo.g_sealevel=1f;
		atmo.ignore_atmo=false;
		atmo.identified=false;
		Catalog.Add(atmo);

		// The following are the vanilla planets of space engineers.
		// Values are read directly from the .sbc files (PlanetGeneratorDefinitions.sbc or Pertam.sbc or Triton.sbc)

		PlanetInfo pertam = new PlanetInfo();
		pertam.shortname = "pertam";
		pertam.name ="Pertam";
		pertam.atmo_density_sealevel=1;
		pertam.atmo_limit_altitude=2;
		pertam.hillparam=0.025f;
		pertam.g_sealevel=1.2f;
		pertam.ignore_atmo=false;
		pertam.identified=true;
		Catalog.Add(pertam);

		PlanetInfo triton = new PlanetInfo();
		triton.shortname = "triton";
		triton.name ="Triton";
		triton.atmo_density_sealevel=1;
		triton.atmo_limit_altitude=0.47f;
		triton.hillparam=0.20f;
		triton.g_sealevel=1f;
		triton.ignore_atmo=false;
		triton.identified=true;
		Catalog.Add(triton);

		PlanetInfo earthlike = new PlanetInfo();
		earthlike.shortname = "earth";
		earthlike.name ="Earthlike";
		earthlike.atmo_density_sealevel=1;
		earthlike.atmo_limit_altitude=2f;
		earthlike.hillparam=0.12f;
		earthlike.g_sealevel=1f;
		earthlike.ignore_atmo=false;
		earthlike.identified=true;
		Catalog.Add(earthlike);

		PlanetInfo alien = new PlanetInfo();
		alien.shortname = "alien";
		alien.name ="Alien";
		alien.atmo_density_sealevel=1.2f;
		alien.atmo_limit_altitude=2f;
		alien.hillparam=0.12f;
		alien.g_sealevel=1.1f;
		alien.ignore_atmo=false;
		alien.identified=true;
		Catalog.Add(alien);

		PlanetInfo mars = new PlanetInfo();
		mars.shortname = "mars";
		mars.name ="Mars";
		mars.atmo_density_sealevel=1;
		mars.atmo_limit_altitude=2;
		mars.hillparam=0.12f;
		mars.g_sealevel=0.9f;
		mars.ignore_atmo=false;
		mars.identified=true;
		Catalog.Add(mars);

		PlanetInfo moon = new PlanetInfo();
		moon.shortname = "moon";
		moon.name ="Moon";
		moon.atmo_density_sealevel=0;
		moon.atmo_limit_altitude=1;
		moon.hillparam=0.03f;
		moon.g_sealevel=0.25f;
		moon.ignore_atmo=false;
		moon.identified=true;
		Catalog.Add(moon);

		PlanetInfo europa = new PlanetInfo();
		europa.shortname = "europa";
		europa.name ="Europa";
		europa.atmo_density_sealevel=0.5f; // no value in .sbc file ?!
		europa.atmo_limit_altitude=1;	// no value in .sbc file ?!
		europa.hillparam=0.06f;
		europa.g_sealevel=0.25f;
		europa.ignore_atmo=false;
		europa.identified=true;
		Catalog.Add(europa);

		PlanetInfo titan = new PlanetInfo();
		titan.shortname = "titan";
		titan.name ="Titan";
		titan.atmo_density_sealevel=0.5f; // no value in .sbc file ?!
		titan.atmo_limit_altitude=1;	// no value in .sbc file ?!
		titan.hillparam=0.03f;
		titan.g_sealevel=0.25f;
		titan.ignore_atmo=false;
		titan.identified=true;
		Catalog.Add(titan);

	}

	public PlanetInfo get_planet(string command) {
		foreach (PlanetInfo candidate in Catalog) {
			if (command.Contains(candidate.shortname)) {
				return candidate;
			}
		}

		return Catalog[0];
	}
}

public class ShipBlocks {

	public List<IMyTerminalBlock> aero_lifters;
	public List<IMyTerminalBlock> ion_lifters;
	public List<IMyTerminalBlock> h2_lifters;
	public List<IMyTerminalBlock> SLM_displays;
	public List<IMyTerminalBlock> SLM_debug;
	public List<IMyTerminalBlock> SLM_graph;
	public List<IMyParachute> parachutes;
	public IMyShipController ship_ctrller;
	public List<IMyTerminalBlock> landing_timers;
	public List<IMyTerminalBlock> liftoff_timers;
	public List<IMyTerminalBlock> on_timers;
	public List<IMyTerminalBlock> off_timers;
	public IMyCameraBlock radar;
	public bool ship_has_radar = false;
	public IMySoundBlock soundblock;
	public bool ship_has_soundblock = false;

}


	
public LandingManager manager;



public Program()
{
    // The constructor.

    Runtime.UpdateFrequency = UpdateFrequency.Update1 | UpdateFrequency.Update10 | UpdateFrequency.Update100;
	
	SLMShipConfiguration shipconfig = new SLMShipConfiguration();
	ShipBlocks ship = GetBlocks(shipconfig);

	SLMConfiguration config = new SLMConfiguration();

	PlanetCatalog catalog = new PlanetCatalog();

	manager = new LandingManager(config, ship, catalog);

}


public class Helpers
{

	// Some inspiration from  Flight Assist by Naosyth

    public static double NotNan(double val)
    {
        if (double.IsNaN(val))
            return 0;
        return val;
    }

	public static float NotNan(float val)
    {
        if (float.IsNaN(val))
            return 0;
        return val;
    }
	
	const double BIT_SPACING = 255.0 / 7.0;
	public static char ColorToChar(byte r, byte g, byte b) {
		return (char)(0xe100 + ((int)Math.Round(r / BIT_SPACING) << 6) + ((int)Math.Round(g / BIT_SPACING) << 3) + (int)Math.Round(b / BIT_SPACING));
	}

	// The max value has priority, ie if min>max, the function returns max
	// Version for floats
	public static float SaturateMinMaxPrioritizeMaxLimit(float value, float min, float max) {
		return Math.Min(Math.Max(value,min),max);
	}

	// Version for doubles
	public static double SaturateMinMaxPrioritizeMaxLimit(double value, double min, double max) {
		return Math.Min(Math.Max(value,min),max);
	}

	public static double g_to_ms2(double g) {
		return g*9.81;
	}

	public static double ms2_to_g(double a) {
		return a/9.81;
	}
	
	
	
}


public void Main(string argument, UpdateType updateSource)

{

	// MANAGE ARGUMENTS AND REFRESH SOURCE
	if ((updateSource & (UpdateType.Trigger | UpdateType.Terminal)) != 0)
  	{
		if (argument == "off") {
			manager.ConfigureMode0();
		} else if (argument.Contains("mode1")) {
			manager.ConfigureMode1();
		} else if (argument.Contains("mode2")) {
			manager.ConfigureMode2();
		}

		manager.SetPlanet(argument);
	}

	if ((updateSource & UpdateType.Update100) != 0) {
		manager.Tick100();
	}

	if ((updateSource & UpdateType.Update10) != 0) {
		manager.Tick10();
	}

	if ((updateSource & UpdateType.Update1) != 0) {
		manager.Tick1();
	}
	
}



public ShipBlocks GetBlocks(SLMShipConfiguration config) {

	// Scan the grid for the appropriately set up blocks
    // and return a ShipBlocks object

	// Temporary lists

	List<IMyTerminalBlock> aero_lifters = new List<IMyTerminalBlock>();
	List<IMyTerminalBlock> ion_lifters = new List<IMyTerminalBlock>();
	List<IMyTerminalBlock> h2_lifters = new List<IMyTerminalBlock>();
	List<IMyTerminalBlock> SLM_displays = new List<IMyTerminalBlock>();
	List<IMyTerminalBlock> SLM_debug = new List<IMyTerminalBlock>();
	List<IMyTerminalBlock> SLM_graph = new List<IMyTerminalBlock>();
	List<IMyParachute> parachutes = new List<IMyParachute>();
	IMyShipController ship_ctrller;
	List<IMyTerminalBlock> landing_timers = new List<IMyTerminalBlock>();
	List<IMyTerminalBlock> liftoff_timers = new List<IMyTerminalBlock>();
	List<IMyTerminalBlock> on_timers = new List<IMyTerminalBlock>();
	List<IMyTerminalBlock> off_timers = new List<IMyTerminalBlock>();
	List<IMyTerminalBlock> possible_radars = new List<IMyTerminalBlock>();
	List<IMyTerminalBlock> possible_sound = new List<IMyTerminalBlock>();

	Echo ("SOFT LANDING MANAGER");

	// Look for blocks based on the name

    GridTerminalSystem.SearchBlocksOfName(config.lcd_name, SLM_displays);
	Echo ("Found "+SLM_displays.Count+" display(s)");

	GridTerminalSystem.SearchBlocksOfName(config.debuglcd_name, SLM_debug);
	Echo ("Found "+SLM_debug.Count+" debug display(s)");

	GridTerminalSystem.SearchBlocksOfName(config.graphlcd_name, SLM_graph);
	Echo ("Found "+SLM_graph.Count+" graph display(s)");

	GridTerminalSystem.SearchBlocksOfName(config.radar_name, possible_radars);
	Echo ("Found "+possible_radars.Count+" radar(s)");

	GridTerminalSystem.SearchBlocksOfName(config.landing_timer_name, landing_timers);
	Echo ("Found "+landing_timers.Count+" landing timer(s)");

	GridTerminalSystem.SearchBlocksOfName(config.liftoff_timer_name, liftoff_timers);
	Echo ("Found "+liftoff_timers.Count+" liftoff timer(s)");

	GridTerminalSystem.SearchBlocksOfName(config.on_timer_name, on_timers);
	Echo ("Found "+on_timers.Count+" on timer(s)");

	GridTerminalSystem.SearchBlocksOfName(config.off_timer_name, off_timers);
	Echo ("Found "+off_timers.Count+" off timer(s)");

	GridTerminalSystem.SearchBlocksOfName(config.sound_name, possible_sound);
	Echo ("Found "+possible_sound.Count+" sound blocks(s)");


	// Look for blocks based on the type

	GridTerminalSystem.GetBlocksOfType(parachutes);
	Echo ("Found "+parachutes.Count+" parachutes");

	// Find a suitable ship controller.
	// We prefer the one that matches the configured name, otherwise select the first available
	ship_ctrller = GridTerminalSystem.GetBlockWithName(config.ctrller_name) as IMyShipController;
	if (ship_ctrller == null) {
		List<IMyShipController> possible_controllers = new List<IMyShipController>();
		GridTerminalSystem.GetBlocksOfType(possible_controllers);
		if (possible_controllers.Count == 0) {
			throw new Exception("Error: no suitable cockpit or remote control block.");
		} else {
			ship_ctrller = possible_controllers[0];
			Echo ("Found a ship controller:" + ship_ctrller.CustomName);
		}
	} else {
		Echo ("Using configured controller:" + ship_ctrller.CustomName);
	}

	// Find thrusters (lifters) based on orientation

	List<IMyThrust> possible_thrusters = new List<IMyThrust>();
	GridTerminalSystem.GetBlocksOfType(possible_thrusters);

	foreach (IMyThrust thru in possible_thrusters) {

		if (!thru.CustomName.Contains(config.lifter_ignore_name)) {

			// If the thruster does not have the ignore tag (configurable), then it is a candidate
			// First we check the direction

			Matrix MatrixCockpit,MatrixThrust;
			thru.Orientation.GetMatrix(out MatrixThrust);
			ship_ctrller.Orientation.GetMatrix(out MatrixCockpit);

			if (MatrixThrust.Forward==MatrixCockpit.Down) {

				// If the thruster goes in the correct direction (up)
				// we add it to the correct list

				string name = thru.DefinitionDisplayNameText.ToString();

				if (name.Contains("Hydrogen")) {
					h2_lifters.Add(thru);
				}

				if (name.Contains("Ion") || name.Contains("Prototech")) {
					ion_lifters.Add(thru);
				}

				if (name.Contains("Atmo")) {
					aero_lifters.Add(thru);
				}
			} 

		} else {
			Echo ("Ignored thruster:" + thru.CustomName);
		}
	}
	Echo ("Found "+h2_lifters.Count+" h2 lifters");
	Echo ("Found "+ion_lifters.Count+" ion lifters");
	Echo ("Found "+aero_lifters.Count+" aero lifters");

	

	// Create the final object

	ShipBlocks block = new ShipBlocks();
	block.aero_lifters = aero_lifters;
	block.ion_lifters = ion_lifters;
	block.h2_lifters = h2_lifters;
	block.SLM_displays = SLM_displays;
	block.SLM_debug = SLM_debug;
	block.SLM_graph = SLM_graph;
	block.parachutes = parachutes;
	block.ship_ctrller = ship_ctrller;
	block.landing_timers = landing_timers;
	block.liftoff_timers = liftoff_timers;
	block.on_timers = on_timers;
	block.off_timers = off_timers;

	if (possible_sound.Count > 0) {
		
		block.soundblock = (IMySoundBlock) possible_sound[0];
		block.ship_has_soundblock = true;
	}

	if (possible_radars.Count > 0) {
		
		block.radar = (IMyCameraBlock) possible_radars[0];
		block.ship_has_radar = true;
	}
	

	return block;
}

public class LandingManager {

	// TODO : too many attibutes used as global variables !
	// This should be refactored at some point !

	public int mode = 0;

	double realPressure = 0;

	double naturgraval=0;
	double shipweight = 0;
	double gnd_altitude = 0; 

	double current_aLWR = 0;
	double current_iLWR = 0;
	double current_hLWR = 0;
	double max_aLWR_gnd_vacuum = 0;
	double max_iLWR_gnd_vacuum = 0;
	double max_hLWR_gnd_vacuum = 0;
	double max_aLWR_gnd_atmo = 0;
	double max_iLWR_gnd_atmo = 0;
	double max_hLWR_gnd_atmo = 0;

	double current_LWRtarget = 0;
	double ground_LWRtarget_vacuum = 0;
	double ground_LWRtarget_atmo = 0;

	double vspeed_sp = 0;
	double vspeed = 0;

	double delta = 0;
	float atmo_override = -1;
	float ion_override = -1;
	float h2_override = -1;
	bool panic = false;
	bool marginal = false;
	bool H2_needed = false;

	bool landing_timer_allowed = false;
	bool liftoff_timer_allowed = false;

	double radar_distance = 1e6;
	double radar_avail_range=0;
	double radar_range = 100000;
	bool radar_valid = false;
	MyDetectedEntityInfo radar_return;

	Vector3D hitpos;
	Vector3D mypos;
	string altitude_source;
	double graval_expected;
	double maxg_vacuum;
	double maxg_atmo;
	bool warning_vacuum = false;
	bool warning_atmo = false;
	double debug_htr_wanted;
	double debug_itr_wanted;
	double debug_atr_wanted;
	double debug_tr_wanted;
	SetPointSource speed_sp_source=SetPointSource.None;

	SLMConfiguration config;
	ShipBlocks ship;
	EarlySurfaceGravityEstimator surf_grav_estimator;
	ShipInfo shipinfo;
	LiftoffProfileBuilder profile;
	PlanetInfo planet;
	PlanetCatalog catalog;
	PIDController PID;



	
	public LandingManager(SLMConfiguration conf, ShipBlocks ship_defined, PlanetCatalog catalog_input) {
		config = conf;
		ship = ship_defined;
		catalog = catalog_input;

		surf_grav_estimator = new EarlySurfaceGravityEstimator();
		shipinfo = new ShipInfo();
		profile = new LiftoffProfileBuilder();
		PID = new PIDController(conf.kp, conf.ki, conf.kd, conf.ai_min, conf.ai_max, conf.ad_filt);

		ConfigureMode0();
		SetUpLCDs();
		
	}

	// ------------------------------
	// PUBLIC METHODS
	// ------------------------------

	

	public void ConfigureMode0() {
		mode = 0;
		Disable_Override();
		DisableRadar();
		SetPlanet("unknown");
		TriggerOffTimers();
		profile.Invalidate();
		speed_sp_source = SetPointSource.None;
	}

	public void ConfigureMode1() {
		
		if (mode == 0) {

			// These actions only if the SLM was off previously
			// (the pilot may switch between mode1 and mode2 during the
			// descent and we don't want to reset everything)

			StartRadar();
			radar_distance = 1e7;
			TriggerOnTimers();
			profile.Invalidate();
		}

		mode = 1;
		ship.ship_ctrller.DampenersOverride = false;
	}

	public void ConfigureMode2() {
		
		
		if (mode == 0) {

			// These actions only if the SLM was off previously

			StartRadar();
			radar_distance = 1e7;
			TriggerOnTimers();
			profile.Invalidate();
		}
		
		mode = 2;
		ship.ship_ctrller.DampenersOverride = false;
	}

	public void SetPlanet(string name) {
		planet = catalog.get_planet(name);
	}

	// Executed every 100 ticks (approx 1,6sec)
	public void Tick100() {
		surf_grav_estimator.UpdateEstimates(naturgraval, gnd_altitude, config.grav_est_altitude_transition_end);
		
		if (config.SLOW_PROFILE_UPDATE  && config.USE_PROFILE == true ) {
			UpdateProfile();
			UpdateGraphDisplays();
		} else if (config.USE_PROFILE == true && mode == 0) {
			UpdateGraphDisplays();
		}
		

		ManageSoundBlocks();
	}
	
	// Executed every 10 ticks (approx 160msec)
	public void Tick10() {
		UpdatePressure();
		UpdateShipWeight();
		UpdateAvailableLWR();

		graval_expected = EstimateSurfaceGravity();

		shipinfo.UpdateMass(ship);
		shipinfo.UpdateThrust(ship);
		
		if ((mode == 1) || (mode == 2)) {
			ManageRadars();

			// In mode 1 and 2, altitude is updated in Tick1, if we're in another mode
			// then we need to update it here.

			UpdateAltitude();

			if (config.SLOW_PROFILE_UPDATE == false && config.USE_PROFILE == true) {
				UpdateProfile();
				UpdateGraphDisplays();
			}
		}

		ManageTimers();
		ManageParachutes();
		UpdateDisplays();
		UpdateDebugDisplays();

		// Manage next mode transition

		// If there's not gravity, disable the SLM
		if (naturgraval == 0) {
			ConfigureMode0();
		}
		
		// If we've landed, disable the SLM
		if (gnd_altitude < 2) {
			ConfigureMode0();
		}
	}


	// Executed every ticks (16msec)
	public void Tick1() {

		if ((mode == 1) || (mode == 2)) {
			UpdateAltitude();
			UpdateVertSpeed();
			UpdateLWRTarget();
			UpdateSpeedsp();
			delta = vspeed_sp - vspeed;
			
			PID.UpdatePIDController((float)delta, (float)(current_aLWR + current_iLWR + current_hLWR));
			ApplyThrustOverride(PID.PIDoutput);
		}
	}


	// ------------------------------
	// PRIVATE METHODS WITH SIDE-EFFECTS INSIDE THE CLASS
	// (they update class attributes used as global variables
	// but otherwise don't have an effect on the ship)
	// ------------------------------

	private void UpdateProfile() {
		
		if (surf_grav_estimator.confidence > 0.95) {
			// If we don't know the planet from a catalog, use the estimated
			// surface gravity
			if (planet.identified == false) {
				planet.g_sealevel = (float)EstimateSurfaceGravity()/9.81f;
			}

			// In any case, we need to use the estimated planet radius
			if (mode == 1) {
			profile.Compute((float)config.transition_altitude, shipinfo, planet, (float)surf_grav_estimator.est_radius, (float)config.accel_limit, (float)config.elec_LWR_sufficient, (float)config.LWRsafetyfactor);
			}

			if (mode == 2) {
				profile.Compute((float)config.transition_altitude, shipinfo, planet, (float)surf_grav_estimator.est_radius, (float)config.accel_limit, (float)config.LWRlimit, (float)config.LWRsafetyfactor);
			}
		}
	}


	

	private void UpdatePressure() {
		// Compute atmospheric pressure
		
		if (ship.parachutes.Count > 0) {
			IMyParachute PSensor = ship.parachutes[0];
			
			realPressure = Math.Floor( (double) PSensor.Atmosphere*100 );
		}
	}

	private void UpdateShipWeight() {
		// Compute ship weight
		
		MyShipMass masse =  ship.ship_ctrller.CalculateShipMass();
		shipinfo.mass = masse.TotalMass;

		Vector3D naturgrav = ship.ship_ctrller.GetNaturalGravity();
		naturgraval = naturgrav.Length();
		
		shipweight = shipinfo.mass*naturgraval;
	}

	
	private void UpdateAvailableLWR() {
		// Compute the max available effective lift to weight ratio for
		// - the current ship condition
		// - maximum possible value at ground level in presence of atmosphere
		// - maximum possible value at ground level in vacuum

		current_aLWR=ComputeLiftToWeightRatio(naturgraval,shipinfo.mass,shipinfo.eff_atmo_thrust);
		current_iLWR=ComputeLiftToWeightRatio(naturgraval,shipinfo.mass,shipinfo.eff_ion_thrust);
		current_hLWR=ComputeLiftToWeightRatio(naturgraval,shipinfo.mass,shipinfo.eff_hydro_thrust);

		max_aLWR_gnd_atmo =ComputeLiftToWeightRatio(graval_expected,shipinfo.mass,shipinfo.max_atmo_thrust);
		max_iLWR_gnd_atmo =ComputeLiftToWeightRatio(graval_expected,shipinfo.mass,shipinfo.max_ion_thrust*0.2);
		max_hLWR_gnd_atmo =ComputeLiftToWeightRatio(graval_expected,shipinfo.mass,shipinfo.max_hydro_thrust);

		max_aLWR_gnd_vacuum =0;
		max_iLWR_gnd_vacuum =ComputeLiftToWeightRatio(graval_expected,shipinfo.mass,shipinfo.max_ion_thrust);
		max_hLWR_gnd_vacuum =ComputeLiftToWeightRatio(graval_expected,shipinfo.mass,shipinfo.max_hydro_thrust);


	}

	private void UpdateMaxAllowedGravities() {

		maxg_vacuum = (shipinfo.mass>0) ? (shipinfo.max_ion_thrust+shipinfo.max_hydro_thrust)/(shipinfo.mass*9.81*config.LWRsafetyfactor*(1+config.LWRoffset)) : 0;
		maxg_atmo = (shipinfo.mass>0) ? (shipinfo.max_atmo_thrust+shipinfo.max_ion_thrust*0.2+shipinfo.max_hydro_thrust)/(shipinfo.mass*9.81*config.LWRsafetyfactor*(1+config.LWRoffset)) : 0;

		warning_vacuum = maxg_vacuum < graval_expected / 9.81;
		warning_atmo = maxg_atmo < graval_expected / 9.81;

	}


	private void UpdateSpeedsp() {

		// The speed set-point is computed with 4 possible methods:
		// |Altitude							|Landing Profile			|Method
		// |------------------------------------|---------------------------|
		// |available & above transition		|computed & valid			|detailed profile computed by the Liftoff Profile Builder
		// |available & above transition		|not computed or not valid	|explicit formula assuming a time-reversed take-off with constant acceleration and gravity
		// |available & below transition		|-							|constant final speed
		// |not available						|-							|back-up formula simply using the local gravity 

		if (gnd_altitude < 1e6) {

			if (gnd_altitude > config.transition_altitude) {

				if (profile.IsValid() && config.USE_PROFILE) {

					vspeed_sp = - profile.Interpolate((float)gnd_altitude);
					speed_sp_source = SetPointSource.Profile;

				} else {

					vspeed_sp = - Math.Sqrt(2 * (gnd_altitude-config.transition_altitude) * (current_LWRtarget-1)*graval_expected) -config.final_speed;
					speed_sp_source = SetPointSource.AltGravFormula;
				}

			} else {

				vspeed_sp = -config.final_speed;
				speed_sp_source = SetPointSource.FinalSpeed;				
			}

		} else {
			
			vspeed_sp = -config.vspeed_default/(naturgraval/9.81)*(current_LWRtarget-1);
			speed_sp_source = SetPointSource.GravFormula;
		}

		// The formula for the speed set-point is derived assuming a time-reversed take-off
		// with constant acceleration

		// Newton formula : 
		// mass * acceleration = forces, with forces = lift - weight

		// Divide by mass:

		// acceleration = lift/mass - gravity
		// acceleration = (lift/weight - 1)*gravity

		// If initial altitude and speed are zero, and we assume acceleration is constant:

		// speed = acceleration * time;
		// altitude = 1/2 * acceleration * time^2

		// Then solve for time as a function of altitude

		// time = sqrt(2*altitude/acceleration)
		
		// Substitude for time in the speed formula:

		// speed = acceleration * sqrt(2*altitude/acceleration)
		// speed = sqrt(2*altitude*acceleration)

		// Finally :

		// speed = sqrt(2*altitude*(lift/weight - 1)*gravity)

		// Because gravity, lift and weight are not actually constant, this formula provides an approximation
		// that is more and more incorrect at high altitude and thus margins must be applied here and there so
		// that the ship is capable of following the changes in the set-point

		// If the expected LWR is less than one, the speed set point will be 0.

		

		
		vspeed_sp = Helpers.NotNan(vspeed_sp);
		vspeed_sp = Helpers.SaturateMinMaxPrioritizeMaxLimit(vspeed_sp,-config.vspeed_safe_limit,-config.final_speed);
	}

	private void UpdateVertSpeed() {
		// Update vertical velocity measurement

		MyShipVelocities velocities = ship.ship_ctrller.GetShipVelocities();
		Vector3D linvel = velocities.LinearVelocity;

		Vector3D normal_gravity = -Vector3D.Normalize(ship.ship_ctrller.GetNaturalGravity());
		vspeed = Helpers.NotNan(Vector3D.Dot(linvel, normal_gravity));
	}

	private void UpdateAltitude() {

		// Update ship altitude (distance from surface) by combining altitude from controller (as shown on HUD)
		// and radar (raytracing from ground-facing camera) as follows:

		// Altitude from controller			Altitude from radar			Method
		// ----------------------------------------------------
		// available & above transition		-							Use altitude from surface
		// available & below transition		available					Use radar altitude
		// available & below transition		not available				Use altitude from surface
		// not available					available					Use radar altitude
		// not available					not available				default value (1e6)

		double altitude_from_ctrller;
		double altitude_radar;

		bool altitude_from_ctrller_valid = ship.ship_ctrller.TryGetPlanetElevation(MyPlanetElevation.Surface, out altitude_from_ctrller);
		
		if (altitude_from_ctrller_valid == true && altitude_from_ctrller > config.transition_altitude) {

			// If we can get the altitude to surface and it is high enough, use it
			// (if the ship is not level and performing attitude adjustments, then the
			// radar return is not directly below)

			gnd_altitude = altitude_from_ctrller;
			altitude_source = "surface";

		} else if (radar_valid == true && (radar_return.Type ==  MyDetectedEntityType.Planet || radar_return.Type == MyDetectedEntityType.LargeGrid) && radar_return.HitPosition.HasValue) {

			// Use radar+inertial navigation in the following cases:
			// - too high for TryGetPlanetElevation to return a result
			// - below the transition altitude (in case the landing surface is not at planet surface, ex: landing pad, silo, etc.)

			hitpos = radar_return.HitPosition.Value; // Updated when the radar has a return
			mypos = ship.ship_ctrller.GetPosition(); // Always updated

			altitude_radar=VRageMath.Vector3D.Distance(hitpos,mypos);

			gnd_altitude = altitude_radar;
			altitude_source = "radar";

		} else if (altitude_from_ctrller_valid == true) {

			// No radar return, but at least an altitude value

			gnd_altitude = altitude_from_ctrller;
			altitude_source = "surface";

		} else {

			// No altitude available at all, use a default huge value
			gnd_altitude = 1e6;
			altitude_source = "unavailable";
		}

		gnd_altitude = gnd_altitude - config.altitude_offset;
	}

	


	private void UpdateLWRTarget() {

		// Update the various lift-to-weight ratios targets,
		// for the current conditions as well as expected conditions
		// on the planet surface
		
		current_LWRtarget = ComputeLWRTarget(naturgraval, mode, current_aLWR, current_iLWR, current_hLWR, config.elec_LWR_sufficient, config.elec_LWR_start, config.LWRsafetyfactor, config.LWRoffset, config.LWRlimit);
		
		ground_LWRtarget_vacuum = ComputeLWRTarget(graval_expected, mode, max_aLWR_gnd_vacuum, max_iLWR_gnd_vacuum, max_hLWR_gnd_vacuum, config.elec_LWR_sufficient, config.elec_LWR_start, config.LWRsafetyfactor, config.LWRoffset, config.LWRlimit);
	
		ground_LWRtarget_atmo = ComputeLWRTarget(graval_expected, mode, max_aLWR_gnd_atmo, max_iLWR_gnd_atmo, max_hLWR_gnd_atmo, config.elec_LWR_sufficient, config.elec_LWR_start, config.LWRsafetyfactor, config.LWRoffset, config.LWRlimit);

		// The LWR target used to compute the speed target will be a mix of the
		// target at the current conditions and at ground conditions.

		// If the pilot selected a planet type (with atmosphere or vacuum) we use only
		// this one, otherwise we mix the two possibilities.

		if (planet.ignore_atmo) {

			current_LWRtarget = (1-config.LWR_mix_surf_ratio)*current_LWRtarget + 0.5*config.LWR_mix_surf_ratio*ground_LWRtarget_vacuum + 0.5*config.LWR_mix_surf_ratio*ground_LWRtarget_atmo;

		} else if (planet.atmo_density_sealevel > 0.8) {

			current_LWRtarget = (1-config.LWR_mix_surf_ratio)*current_LWRtarget + config.LWR_mix_surf_ratio*ground_LWRtarget_atmo;

		} else if (planet.atmo_density_sealevel < 0.2) {

			current_LWRtarget = (1-config.LWR_mix_surf_ratio)*current_LWRtarget + config.LWR_mix_surf_ratio*ground_LWRtarget_vacuum;
		}
	}

	// ------------------------------
	// PRIVATE METHODS WITH SIDE-EFFECTS ON THE SHIP
	// (they perfom actions on the ship blocks)
	// ------------------------------

	private void DisableRadar() {
		if (ship.ship_has_radar) {
			ship.radar.EnableRaycast = false;
		}
	}

	private void StartRadar() {
		radar_range = 1000;
		if (ship.ship_has_radar) {
			ship.radar.EnableRaycast = true;
		}
	}

	// Setup the LCDs (display type, font size etc.)
	private void SetUpLCDs() {

		foreach (IMyTextPanel maindisplay in ship.SLM_displays) {
			
			maindisplay.Enabled = true;
			maindisplay.ContentType = ContentType.TEXT_AND_IMAGE;
			maindisplay.Font = "Monospace";
			maindisplay.FontColor = VRageMath.Color.White;
			maindisplay.FontSize = 0.8f;
		}

		foreach (IMyTextPanel debugdisplay in ship.SLM_debug) {
			
			debugdisplay.Enabled = true;
			debugdisplay.ContentType = ContentType.TEXT_AND_IMAGE;
			debugdisplay.Font = "Monospace";
			debugdisplay.FontColor = VRageMath.Color.White;
			debugdisplay.FontSize = 0.6f;
		}

		foreach (IMyTextPanel graphdisplay in ship.SLM_graph) {
			
			graphdisplay.Enabled = true;
			graphdisplay.ContentType = ContentType.SCRIPT;
			graphdisplay.BackgroundColor=VRageMath.Color.Black;
		}
	}

	private void Disable_Override() {

		foreach (IMyThrust alifter in ship.aero_lifters) {
			alifter.ThrustOverridePercentage = -1;
		}   

		foreach (IMyThrust ilifter in ship.ion_lifters) {
			ilifter.ThrustOverridePercentage = -1;
		} 		

		foreach (IMyThrust hlifter in ship.h2_lifters) {
			hlifter.ThrustOverridePercentage =  -1;  
		} 
	}

	private void UpdateDisplays() {

		double current_a_relthrust = shipinfo.current_atmo_thrust/shipinfo.mass;
		double current_i_relthrust = shipinfo.current_ion_thrust/shipinfo.mass;
		double current_h_relthrust = shipinfo.current_hydro_thrust/shipinfo.mass;
		
		double eff_a_relthrust = shipinfo.eff_atmo_thrust/shipinfo.mass;
		double eff_i_relthrust = shipinfo.eff_ion_thrust/shipinfo.mass;
		double eff_h_relthrust = shipinfo.eff_hydro_thrust/shipinfo.mass;

		string ATMO_SQUARE = Helpers.ColorToChar(100,255,100).ToString();
		string ION_SQUARE = Helpers.ColorToChar(100,100,255).ToString();
		string HYDRO_SQUARE = Helpers.ColorToChar(255,100,100).ToString();

		UpdateMaxAllowedGravities();

		foreach (IMyTextPanel maindisplay in ship.SLM_displays) {

			maindisplay.FontColor = VRageMath.Color.White;

			if (marginal) {
				maindisplay.FontColor = VRageMath.Color.Yellow;
			}

			if (planet.ignore_atmo == true & (warning_atmo | warning_vacuum) 
				| planet.atmo_density_sealevel < 0.2 & warning_vacuum
				| planet.atmo_density_sealevel > 0.8 & warning_atmo) {
				maindisplay.FontColor = VRageMath.Color.Orange;
			}

			if (panic) {
				maindisplay.FontColor = VRageMath.Color.Red;
			}

			// Info displayed in all modes

			maindisplay.WriteText("SOFT LANDING MANAGER");
			maindisplay.WriteText("\n---------------",true);
			
			
			maindisplay.WriteText("\nGravity now :" + Math.Round(naturgraval/9.81,2).ToString("0.00") +"g", true);
			if (planet.identified == true) {
				maindisplay.WriteText("\nSurface grav:" + Math.Round(Helpers.ms2_to_g(graval_expected),2).ToString("0.00") + "g (known)",true);
			} else if (surf_grav_estimator.confidence > 0.8) {
				maindisplay.WriteText("\nSurface grav:" + Math.Round(Helpers.ms2_to_g(graval_expected),2).ToString("0.00") + "g (estim)",true);
			} else if (surf_grav_estimator.confidence > 0.2) {
				maindisplay.WriteText("\nSurface grav:" + Math.Round(Helpers.ms2_to_g(graval_expected),2).ToString("0.00") + "g (low conf.)",true);
			} else if (gnd_altitude < config.grav_est_altitude_transition_end) {
				maindisplay.WriteText("\nSurface grav:" + Math.Round(Helpers.ms2_to_g(graval_expected),2).ToString("0.00") + "g (here)",true);
			} else {
				maindisplay.WriteText("\nSurface gravity unknown",true);
			}

			maindisplay.WriteText("\n",true);

			maindisplay.WriteText("\n"+ship.ship_ctrller.CubeGrid.DisplayName+" info:",true);
			maindisplay.WriteText("\nTotal mass: " + Math.Round(shipinfo.mass) + "kg", true);
			maindisplay.WriteText("\nMax: " + Math.Round(maxg_vacuum,2).ToString("0.00") + "g (vac), "+Math.Round(maxg_atmo,2).ToString("0.00")+"g (atmo)", true);

			maindisplay.WriteText("\n",true);

			if (gnd_altitude < 1e6) {
				maindisplay.WriteText("\nAltitude: " +  Math.Round(gnd_altitude,1).ToString("+000000.0") + " " + altitude_source, true);
			} else {
				maindisplay.WriteText("\nAltitude unknown", true);
			}

			if (!ship.ship_has_radar) {
				maindisplay.WriteText("(no radar!)", true);
			}

			// Mode indicator

			if (mode == 0) {
				maindisplay.WriteText("\nMode 0 - DISABLED", true);
			} else if (mode == 1) {
				maindisplay.WriteText("\nMode 1", true);
			} else if (mode == 2) {
				maindisplay.WriteText("\nMode 2", true);
			}

			// Additional info when active

			if (mode == 1 | mode == 2) {
				maindisplay.WriteText(" - " + planet.name, true);

				switch (speed_sp_source) {
					case SetPointSource.None:
						maindisplay.WriteText("\nNot active", true);
						break;

					case SetPointSource.Profile:
						maindisplay.WriteText("\nFollowing descent profile", true);
						break;

					case SetPointSource.AltGravFormula:
						maindisplay.WriteText("\nAltitude/gravity formula", true);
						break;

					case SetPointSource.GravFormula:
						maindisplay.WriteText("\nGravity formula", true);
						break;

					case SetPointSource.FinalSpeed:
						maindisplay.WriteText("\nFinal landing speed", true);
						break;

					default:
						break;
				}
		
				maindisplay.WriteText("\nV Speed: " +  Math.Round(vspeed,1).ToString("000.0") + ", tgt: "+Math.Round(vspeed_sp,1).ToString("000.0"), true);
				
				maindisplay.WriteText("\nAvail:",true);
				
				for (int i=0; i<eff_a_relthrust/2; i=i+1) {
					maindisplay.WriteText(ATMO_SQUARE, true);
				}
				for (int i=0; i<eff_i_relthrust/2; i=i+1) {
					maindisplay.WriteText(ION_SQUARE, true);
				}
				for (int i=0; i<eff_h_relthrust/2; i=i+1) {
					maindisplay.WriteText(HYDRO_SQUARE, true);
				}
				
				maindisplay.WriteText("\nUsed :",true);
				
				for (int i=0; i<current_a_relthrust/2; i=i+1) {
					maindisplay.WriteText(ATMO_SQUARE, true);
				}
				for (int i=0; i<current_i_relthrust/2; i=i+1) {
					maindisplay.WriteText(ION_SQUARE, true);
				}
				for (int i=0; i<current_h_relthrust/2; i=i+1) {
					maindisplay.WriteText(HYDRO_SQUARE, true);
				}
			}
		}
	}

	public void UpdateDebugDisplays() {

		foreach (IMyTextPanel debugdisplay in ship.SLM_debug) {

			// Additional information for debug mode

			debugdisplay.WriteText("-- SLM debug --");
			debugdisplay.WriteText("\nOutside Pressure: " + realPressure + "%", true);
			if (naturgraval>0) {
				debugdisplay.WriteText("\nAvailable LWR now:",true);
				debugdisplay.WriteText("\nAtmo:" + Math.Round(current_aLWR,2).ToString("0.00") + " Ion:" + Math.Round(current_iLWR,2).ToString("0.00")+ " H2:" + Math.Round(current_hLWR,2).ToString("0.00") + " Total:" + Math.Round(current_aLWR+current_iLWR+current_hLWR,2).ToString("0.00"), true); 
			}
			
			debugdisplay.WriteText("\n[Speed target computation]", true);
			debugdisplay.WriteText("\nLWR target now    : " + Math.Round(current_LWRtarget,2), true);

			debugdisplay.WriteText("\n[Surface gravity estimator]", true);
			debugdisplay.WriteText("\nEstim radius:"+Math.Round(surf_grav_estimator.est_radius,0).ToString("000000")+" grav:"+Math.Round(surf_grav_estimator.est_gravity,2).ToString("0.00")+", confid:"+Math.Round(surf_grav_estimator.confidence,2),true);
			
			debugdisplay.WriteText("\n[Radar]", true);
			debugdisplay.WriteText("\nRange:" + Math.Round(radar_range) + ", avail:" + Math.Round(radar_avail_range),true);
			debugdisplay.WriteText("\nDistance @return:" + Math.Round(radar_distance) +",now:"+ Math.Round(Vector3D.Distance(ship.ship_ctrller.GetPosition(),radar_return.Position)),true);
			debugdisplay.WriteText("\nReturn type: " + radar_return.Type,true);

			
			debugdisplay.WriteText("\n[PID]", true);
			debugdisplay.WriteText("\nP: " +  Math.Round(PID.ap,2).ToString("+0.00;-0.00") + " I:" + Math.Round(PID.ai,2).ToString("+0.00;-0.00") +" D:"+Math.Round(PID.ad,2).ToString("+0.00;-0.00"),true);
			
			debugdisplay.WriteText("\n[Thrust wanted]", true);
			debugdisplay.WriteText("\nThr wanted:   " + Math.Round(debug_tr_wanted), true);
			debugdisplay.WriteText("\nA: " + Math.Round(debug_atr_wanted).ToString("000000"), true);
			debugdisplay.WriteText(",I: " + Math.Round(debug_itr_wanted).ToString("000000"), true);
			debugdisplay.WriteText(",H: " + Math.Round(debug_htr_wanted).ToString("000000"), true);

			debugdisplay.WriteText("\n[Thrust overrides]", true);
			debugdisplay.WriteText("\nAtmo:" +Math.Round(atmo_override,2).ToString("+0.00;-0.00")+ " Ion:" +Math.Round(ion_override,2).ToString("+0.00;-0.00") + " H2:" +Math.Round(h2_override,2).ToString("+0.00;-0.00"),  true);
			
			debugdisplay.WriteText("\n[Speed profile]", true);
			debugdisplay.WriteText("\nValid:"+ profile.IsValid().ToString() + ",final_alt"+profile.GetFinalAlt().ToString("00000") + "speed:"+ profile.GetFinalSpeed().ToString("0000"), true);
			debugdisplay.WriteText("\nAlt  ;speed", true);
			debugdisplay.WriteText("\n"+profile.alt_sl[0].ToString("000.0") +";"+profile.vert_speed[0].ToString("000.0"), true);
			debugdisplay.WriteText("\n"+profile.alt_sl[1].ToString("000.0") +";"+profile.vert_speed[1].ToString("000.0"), true);
			debugdisplay.WriteText("\n"+profile.alt_sl[2].ToString("000.0") +";"+profile.vert_speed[2].ToString("000.0"), true);
			debugdisplay.WriteText("\n"+profile.alt_sl[3].ToString("000.0") +";"+profile.vert_speed[3].ToString("000.0"), true);
			debugdisplay.WriteText("\n"+profile.alt_sl[4].ToString("000.0") +";"+profile.vert_speed[4].ToString("000.0"), true);

		}

	}

	public void UpdateGraphDisplays() {

		foreach (IMyTextPanel graphdisplay in ship.SLM_graph) {

			//graphdisplay.WriteText("\n[PID]", true);

			//IMyTextSurface _drawingSurface;
			
			//_drawingSurface = graphdisplay.GetSurface(0);
			IMyTextSurface _drawingSurface;
			VRageMath.RectangleF _viewport;
			float width;
			float height;
			float speed;
			float speed_scale;
			float alt_scale;
			string alt_scale_legend;


			if (gnd_altitude < 3000) {
				speed_scale = 0.8f;
				alt_scale=8;
				alt_scale_legend = "Final stage";
			} else if (gnd_altitude < 10000) {
				speed_scale = 1.4f;
				alt_scale=22;
				alt_scale_legend = "Medium altitude stage";
			} else {
				speed_scale = 1.4f;
				alt_scale=120;
				alt_scale_legend = "High altitude stage";
			}

			_drawingSurface = graphdisplay;

			_viewport = new VRageMath.RectangleF(
				(_drawingSurface.TextureSize - _drawingSurface.SurfaceSize) / 2f,
				_drawingSurface.SurfaceSize
			);

			width=_drawingSurface.SurfaceSize[0];
			height=_drawingSurface.SurfaceSize[1];

			var frame = graphdisplay.DrawFrame();

			if (profile.IsValid() && profile.IsComputed()) {

				// Draw the profile with white crosses

				for (int i=0; i<profile.alt_sl.Count()-1;i++) {


					speed = Math.Min(profile.vert_speed[i],(float)config.vspeed_safe_limit);

					var sprite_profile_point = new MySprite()
					{
						Type = SpriteType.TEXT,
						Data = "+",
						Position = new Vector2(width-speed/speed_scale-30,height-(profile.alt_sl[i]/alt_scale+50)) + _viewport.Position,
						RotationOrScale = 2f ,
						Color = VRageMath.Color.White,
						Alignment = TextAlignment.CENTER /* Center the text on the position */,
						FontId = "White"
					};
					// Add the sprite to the frame
					frame.Add(sprite_profile_point);
				}

				// Red marker for the current altitude/speed

				var sprite_red_marker = new MySprite()
				{
					Type = SpriteType.TEXT,
					Data = "O",
					Position = new Vector2(width+(float)vspeed/speed_scale-30,(float)(height-(gnd_altitude/alt_scale+50))) + _viewport.Position,
					RotationOrScale = 2f ,
					Color = VRageMath.Color.Red,
					Alignment = TextAlignment.CENTER /* Center the text on the position */,
					FontId = "White"
				};
				frame.Add(sprite_red_marker);

				var sprite_scale_legend = new MySprite()
				{
					Type = SpriteType.TEXT,
					Data = alt_scale_legend,
					Position = new Vector2(width/2,50) + _viewport.Position,
					RotationOrScale = 1f ,
					Color = VRageMath.Color.White,
					Alignment = TextAlignment.CENTER /* Center the text on the position */,
					FontId = "White"
				};
				frame.Add(sprite_scale_legend);

				if (panic) {
					var sprite_panic = new MySprite()
					{
						Type = SpriteType.TEXT,
						Data = "PANIC",
						Position = new Vector2(width/2,100) + _viewport.Position,
						RotationOrScale = 2f ,
						Color = VRageMath.Color.Red,
						Alignment = TextAlignment.CENTER /* Center the text on the position */,
						FontId = "White"
					};
					frame.Add(sprite_panic);
				}




			} else if (profile.IsComputed() && mode != 0) {

				var sprite_novalid1 = new MySprite()
				{
					Type = SpriteType.TEXT,
					Data = "--- WARNING ---",
					Position = new Vector2(width/2,80) + _viewport.Position,
					RotationOrScale = 1.5f ,
					Color = VRageMath.Color.Red,
					Alignment = TextAlignment.CENTER /* Center the text on the position */,
					FontId = "White"
				};
				frame.Add(sprite_novalid1);

				var sprite_novalid2 = new MySprite()
				{
					Type = SpriteType.TEXT,
					Data = "Ship unable to land safely",
					Position = new Vector2(width/2,130) + _viewport.Position,
					RotationOrScale = 1f ,
					Color = VRageMath.Color.Red,
					Alignment = TextAlignment.CENTER /* Center the text on the position */,
					FontId = "White"
				};
				frame.Add(sprite_novalid2);

				var sprite_novalid3 = new MySprite()
				{
					Type = SpriteType.TEXT,
					Data = "for expected surface conditons !",
					Position = new Vector2(width/2,150) + _viewport.Position,
					RotationOrScale = 1f ,
					Color = VRageMath.Color.Red,
					Alignment = TextAlignment.CENTER /* Center the text on the position */,
					FontId = "White"
				};
				frame.Add(sprite_novalid3);

				var sprite_novalid4 = new MySprite()
				{
					Type = SpriteType.TEXT,
					Data = "Abort landing or manually select",
					Position = new Vector2(width/2,170) + _viewport.Position,
					RotationOrScale = 1f ,
					Color = VRageMath.Color.White,
					Alignment = TextAlignment.CENTER /* Center the text on the position */,
					FontId = "White"
				};
				frame.Add(sprite_novalid4);

				var sprite_novalid5 = new MySprite()
				{
					Type = SpriteType.TEXT,
					Data = "planet or surface atmospheric conditons.",
					Position = new Vector2(width/2,190) + _viewport.Position,
					RotationOrScale = 1f ,
					Color = VRageMath.Color.White,
					Alignment = TextAlignment.CENTER /* Center the text on the position */,
					FontId = "White"
				};
				frame.Add(sprite_novalid5);


			} else {

				var sprite_noprofile = new MySprite()
				{
					Type = SpriteType.TEXT,
					Data = "No profile computed",
					Position = new Vector2(width/2,120) + _viewport.Position,
					RotationOrScale = 1f ,
					Color = VRageMath.Color.White,
					Alignment = TextAlignment.CENTER /* Center the text on the position */,
					FontId = "White"
				};
				frame.Add(sprite_noprofile);
			}

			var sprite_title = new MySprite()
			{
				Type = SpriteType.TEXT,
				Data = "Soft Landing Manager - descent profile",
				Position = new Vector2(width/2,30) + _viewport.Position,
				RotationOrScale = 1f ,
				Color = VRageMath.Color.White,
				Alignment = TextAlignment.CENTER /* Center the text on the position */,
				FontId = "White"
			};
			frame.Add(sprite_title);

			var sprite_fast = new MySprite()
			{
				Type = SpriteType.TEXT,
				Data = "Fast",
				Position = new Vector2(60,height-40) + _viewport.Position,
				RotationOrScale = 1f ,
				Color = VRageMath.Color.Gray,
				Alignment = TextAlignment.CENTER /* Center the text on the position */,
				FontId = "White"
			};
			frame.Add(sprite_fast);

			var sprite_slow = new MySprite()
			{
				Type = SpriteType.TEXT,
				Data = "Slow",
				Position = new Vector2(width-40,height-40) + _viewport.Position,
				RotationOrScale = 1f ,
				Color = VRageMath.Color.Gray,
				Alignment = TextAlignment.CENTER /* Center the text on the position */,
				FontId = "White"
			};
			frame.Add(sprite_slow);

			var sprite_high = new MySprite()
			{
				Type = SpriteType.TEXT,
				Data = "High",
				Position = new Vector2(40,60) + _viewport.Position,
				RotationOrScale = 1f ,
				Color = VRageMath.Color.Gray,
				Alignment = TextAlignment.CENTER /* Center the text on the position */,
				FontId = "White"
			};
			frame.Add(sprite_high);

			var sprite_low = new MySprite()
			{
				Type = SpriteType.TEXT,
				Data = "Low",
				Position = new Vector2(40,height-60) + _viewport.Position,
				RotationOrScale = 1f ,
				Color = VRageMath.Color.Gray,
				Alignment = TextAlignment.CENTER /* Center the text on the position */,
				FontId = "White"
			};
			frame.Add(sprite_low);

			frame.Dispose();
		}
	}

	private void ApplyThrustOverride(double PIDoutput) {

		double thr_wanted;
		double athr_wanted;
		double ithr_wanted;
		double hthr_wanted;

		// Compute the total thrust wanted

		double sigmoid = Math.Tanh(delta);
		thr_wanted = (PIDoutput+current_LWRtarget*sigmoid) * shipweight;
		
		// Compute the thrust wanted for each thruster type
		
		if (mode == 1 && vspeed < -5) {
			athr_wanted = Helpers.SaturateMinMaxPrioritizeMaxLimit(thr_wanted,shipweight,shipinfo.eff_atmo_thrust);
		} else {
			athr_wanted = Helpers.SaturateMinMaxPrioritizeMaxLimit(thr_wanted,0,shipinfo.eff_atmo_thrust);
		}
		
		if (mode == 1 && gnd_altitude > config.early_ion_alt_limit && vspeed < -config.early_ion_speed_limit && delta < 0) {
			ithr_wanted = Helpers.SaturateMinMaxPrioritizeMaxLimit(thr_wanted-athr_wanted,shipweight,shipinfo.eff_ion_thrust);
		} else {
			ithr_wanted = Helpers.SaturateMinMaxPrioritizeMaxLimit(thr_wanted-athr_wanted,0, shipinfo.eff_ion_thrust);
		}
		
		hthr_wanted = thr_wanted-ithr_wanted-athr_wanted;
		
		if (thr_wanted > shipinfo.eff_atmo_thrust + shipinfo.eff_ion_thrust + shipinfo.eff_hydro_thrust) {
			marginal = true;
		} else {
			marginal = false;
		}

		debug_atr_wanted = athr_wanted;
		debug_htr_wanted = hthr_wanted;
		debug_itr_wanted = ithr_wanted;
		debug_tr_wanted = thr_wanted;
		
		// Compute the overrides

		// By default, atmos are overriden to the max so that they update their max effective thrust
		// If they are actually capable to produce thrust, we compute the actual wanted thrust override
		
		if (shipinfo.eff_atmo_thrust > 0) {
			atmo_override = Helpers.SaturateMinMaxPrioritizeMaxLimit(Convert.ToSingle(athr_wanted/shipinfo.eff_atmo_thrust),0,1);
		} else {
			atmo_override = 1;
		}
		
		if (shipinfo.eff_ion_thrust > 0) {
			ion_override = Helpers.SaturateMinMaxPrioritizeMaxLimit(Convert.ToSingle(ithr_wanted/shipinfo.eff_ion_thrust),0,1);
		} else {
			ion_override = 0;
		}

		if (shipinfo.eff_hydro_thrust > 0) {
			h2_override = Helpers.SaturateMinMaxPrioritizeMaxLimit(Convert.ToSingle(hthr_wanted/shipinfo.eff_hydro_thrust),0,1);
		} else {
			h2_override = 0;
		}
		
		// Apply the thrust override to thrusters
		
		foreach (IMyThrust alifter in ship.aero_lifters) {
			alifter.ThrustOverridePercentage = atmo_override;
		}   

		foreach (IMyThrust ilifter in ship.ion_lifters) {
			ilifter.ThrustOverridePercentage = ion_override;
		}  		

		foreach (IMyThrust hlifter in ship.h2_lifters) {
			
			hlifter.ThrustOverridePercentage = h2_override;  
			if (H2_needed) {
				hlifter.Enabled = true;
			}
		} 
	}

	private void ManageParachutes() {
		if (delta > config.panic_speed_delta && gnd_altitude < config.panic_altitude) {
			panic = true;

			foreach (IMyParachute parachute in ship.parachutes) {
				parachute.OpenDoor();
			}

		} else {
			panic = false;
		}
	}

	private void ManageSoundBlocks() {
		
		if (ship.ship_has_soundblock == true) {

			if (panic) {
				ship.soundblock.SelectedSound = "SoundBlockAlert2";
				ship.soundblock.Play();
			} else if (warning_atmo || warning_vacuum) {
				ship.soundblock.SelectedSound = "SoundBlockAlert1";
				ship.soundblock.Play();
			}
		}
	}

	private void ManageTimers() {

		if (gnd_altitude < config.landing_timer_altitude) {
			if ((ship.landing_timers != null) && (landing_timer_allowed == true)) {
				foreach (IMyTimerBlock timer in ship.landing_timers) {
					timer.Trigger();
				}
			}

			landing_timer_allowed = false;
			liftoff_timer_allowed = true;
		}

		// In case of miconfiguration, make sure that the liftoff triggers altitude is
		// higher than the landing altitude
		if (gnd_altitude > Math.Max(config.liftoff_timer_altitude,config.landing_timer_altitude+1)) {

			if ((ship.liftoff_timers != null) && (liftoff_timer_allowed == true)) {
				foreach (IMyTimerBlock timer in ship.liftoff_timers) {
					timer.Trigger();
				}
			}

			liftoff_timer_allowed = false;
			landing_timer_allowed = true;
		}
	}

	private void TriggerOnTimers() {
		foreach (IMyTimerBlock timer in ship.on_timers) {
			timer.Trigger();
		}
	}

	private void TriggerOffTimers() {
		foreach (IMyTimerBlock timer in ship.off_timers) {
			timer.Trigger();
		}
	}

	private void ManageRadars() {

		if (ship.ship_has_radar) {

			Vector3D mypos_at_return_time;

			// If we have a previous return, increase the scan range for faster refresh
			if (radar_distance<1e6) {
				radar_range = radar_distance+100;
			}

			radar_avail_range = ship.radar.AvailableScanRange;

			if (ship.radar.CanScan(radar_range)) {

				radar_return = ship.radar.Raycast(radar_range);

				if ((radar_return.Type ==  MyDetectedEntityType.Planet || radar_return.Type == MyDetectedEntityType.LargeGrid) && radar_return.HitPosition.HasValue) {

					// If we have a return (either a planet, or a large grid (landing pad, silo)), update the radar altitude

					radar_valid = true;
					hitpos = radar_return.HitPosition.Value;
					mypos_at_return_time = ship.ship_ctrller.GetPosition();
					radar_distance = VRageMath.Vector3D.Distance(hitpos,mypos_at_return_time); // only used for display on LCDs
					
				} else {

					// If we have no return, invalidate the previous return and increase the scan range
					radar_valid = false;
					radar_distance = 1e6;
					radar_range = Math.Min(radar_range*2,config.radar_max_range);
				}
			}
		}
	}

	// ------------------------------
	// PRIVATE METHODS WITH NO SIDE-EFFECTS
	// ------------------------------

	// Estimates the surface gravity with what's available and return the value in m/s²
	private double EstimateSurfaceGravity() {

		if (planet.identified) {

			// If the pilot selected the planet from the database, it's easy !

			return planet.g_sealevel*9.81;

		} else {

			// Otherwise, we have to use the estimator

			// First, we compute a ground estimate, weighted with the estimator confidence (low confidence means we use some proportion of the current gravity)
			// Then, we mix that unconditionnaly with a proportion of the current gravity (proportion is configurable)

			double weighted_ground_estimate = surf_grav_estimator.confidence*surf_grav_estimator.est_gravity + (1-surf_grav_estimator.confidence)*naturgraval;
			double high_alt_estimate = config.ground_grav_estimate_proportion*weighted_ground_estimate + (1-config.ground_grav_estimate_proportion)*naturgraval;

			if (gnd_altitude > config.grav_est_altitude_transition_start) {

				// At high altitude, we use the high altitude estimation

				return high_alt_estimate;

			} else if (gnd_altitude <= config.grav_est_altitude_transition_start && gnd_altitude > config.grav_est_altitude_transition_end) {

				// In between, linear interpolation

				double ratio = (gnd_altitude-config.grav_est_altitude_transition_end)/(config.grav_est_altitude_transition_start-config.grav_est_altitude_transition_end);
				return ratio*high_alt_estimate + (1-ratio)*naturgraval;

			} else {

				// At low altitude, only the measured local gravity

				return naturgraval;
			}
		}
	}

	private double ComputeLWRTarget(double gravity, int mode, double aLWR, double iLWR, double hLWR, double LWR_sufficient, double LWR_start, double factor, double offset, double limit) {

		// Compute the lift to weight ratio
		// that will be used to compute the vertical speed set-point :
		// In mode 1:
		// - Only electric thrusters when they are sufficient
		// - All thrusters when needed
		// - Progressive transition between
		// In other modes:
		// - Allways all thrusters

		double computedLWRtarget = 0;

		if (gravity>0) {
			
			if (mode == 1) {
				if (aLWR + iLWR > LWR_sufficient) {

					// Electric thrusters are sufficient
					// Hydrogen thrusters not used

					H2_needed = false;
					computedLWRtarget = aLWR + iLWR;

				} else if (aLWR + iLWR > LWR_start) {

					// Transition between electric and hydrogen thrusters

					H2_needed = true;

					double ratio = (aLWR+iLWR-LWR_start)/(LWR_sufficient-LWR_start);

					computedLWRtarget = Math.Min(aLWR + iLWR + hLWR * (1-ratio),LWR_sufficient);
				
				} else {

					// Hydrogen thrusters needed

					H2_needed = true;
					computedLWRtarget = aLWR + iLWR + hLWR;

				}
			} else {

				computedLWRtarget = aLWR + iLWR + hLWR;
				H2_needed = true;

			}
			
			// Compute final target : apply margins (ratio and offset), then limit to the max value
			computedLWRtarget = Math.Min((computedLWRtarget/factor) - offset,limit);
		} else {
			computedLWRtarget = 0;
		}

		return computedLWRtarget;
	}

	private double ComputeLiftToWeightRatio(double gravity, double shipmass, double thrust) {
		return (gravity>0) ? thrust / (gravity*shipmass) : 0;
	}
	
}



public class EarlySurfaceGravityEstimator {

	// This allows to estimate the surface gravity of a planet
	// while still very far from it.
	// This assumes an inverse square law, ie use of the "Real Orbits" mode

	double prev_grav = 0;

	double prev_alt = 0;
	public double est_radius=0;
	public double est_gravity=0;
	public double confidence;
	



	public void UpdateEstimates(double grav, double alt, double alt_offset) {

		// Solve the equation :
		// grav * (radius+alt)² = prev_grav * (radius+prev_alt)²
		// for the unknown radius

		// At each call, use the new updated values for the computations
		// and then push them to the old values for the next update

		double A;
		double B;
		double C;
		double delta;
		double new_est_radius;

		if (prev_grav == 0 || prev_alt == 0) {
			// First run, initalize and don't return anything
			prev_grav = grav;
			prev_alt = alt;
			new_est_radius = -1;

		} else if (grav != prev_grav && alt != prev_alt && grav > 0) {

			A = grav - prev_grav;
			B = grav*alt - prev_grav*prev_alt;
			C = grav*alt*alt - prev_grav*prev_alt*prev_alt;
			delta = B*B - 4*A*C;

			prev_grav = grav;
			prev_alt = alt;

			if (delta >=0) {
				new_est_radius =  (-B + Math.Sqrt(delta))/(2*A);

				if (new_est_radius > 1e6) {
					confidence = 0;
				} else {
					// Confidence in the estimation is based on how much it changes from one sample to the next
					confidence = Math.Min(new_est_radius,est_radius)/Math.Max(new_est_radius,est_radius);
				}

				est_radius = new_est_radius;
				 
			} else {
				// This should not happen
				est_radius =  -2;
				confidence = 0;
			}

		} else {
			est_radius =  -3;
			confidence = 0;
		}

		if (alt+est_radius > 0 && est_radius>0) {

			double coeff = grav * Math.Pow(alt+est_radius,2); // coeff is K in g=K*1/r²
			est_gravity = coeff / Math.Pow(est_radius+alt_offset,2);
			
		} else {
			est_gravity = -1;
			confidence = 0;
		}

		confidence = Helpers.SaturateMinMaxPrioritizeMaxLimit(confidence,0,1);
	}

	public void Reset() {
		prev_grav = 0;
		prev_alt = 0;
	}
}


public class LiftoffProfileBuilder {

	private const float DT_START=1; 			// Time step in seconds
	private const int NB_PTS=256;	// Number of time steps to compute
	private float dt=DT_START;

	public float[] vert_speed = new float[NB_PTS];
	public float[] alt_sl = new float[NB_PTS];

	// A landing profile has two attribues :
	// - computed : if the profile has been computed or not
	// - valid    : if the computed profile concludes on a successfull liftoff, meaning that the vertical
	// speed is always positive. If the profile is computed but invalid, that means the ship is not capable
	// of exiting the planet gravity well. It is however possible that the ship is capable of landing safely
	// (ex : with a lot of atmopheric thrusters) but this landing profile cannot be used to control landing
	// and a backup method will be needed.
	private bool valid = false;
	private bool computed = false;

	// Compute atmospheric density at a set altitude above sea level, based on planet info and radius
	private float ComputeAtmoDensity(float alt_above_sl, PlanetInfo planet, float radius) {
		
		float atmo_alt = radius*planet.atmo_limit_altitude*planet.hillparam;

		if (alt_above_sl > atmo_alt) {
			return 0;
		} else if (alt_above_sl>=0) {
			return planet.atmo_density_sealevel * (1-alt_above_sl/atmo_alt);
		} else {
			return planet.atmo_density_sealevel;
		}
	}

	// Compute gravity value at a set altitude above sea level, based on planet info and radius
	private float ComputeGravity(float alt_above_sl, PlanetInfo planet, float radius) {
		
		float PlanetMaxRadius = radius * (1 + planet.hillparam);

		if (alt_above_sl >= (PlanetMaxRadius-radius)) {
			return planet.g_sealevel * (PlanetMaxRadius/(alt_above_sl+radius))*(PlanetMaxRadius/(alt_above_sl+radius));
		} else {
			return planet.g_sealevel;
		}
	}

	// Build the altitude/speed profile by simulating a liftoff from a standstill at the surface
	public void Compute(float alt_start, ShipInfo shipinfo, PlanetInfo planet, float radius, float max_accel, float max_twr, float safetyfactor) {

		float gravity;		// m/s²
		float athrust;		// N
		float ithrust;		// N
		float hthrust;		// N
		float total_thrust;	// N
		float thrust_max_accel;
		float thrust_max_twr;

		float accel;		// m/s², positive up
		bool temp_valid = true;

		vert_speed[0]=0;
		alt_sl[0]=alt_start;

		dt=DT_START;

		for (int i=1; i<NB_PTS; i++ ) {

			// Atmospheric density above 1 does not provide more thrust to atmo thruster
			// and doesn't lower the ion effectiveness even more.

			gravity = 9.81f*ComputeGravity(alt_sl[i-1], planet, radius);

			thrust_max_accel = shipinfo.mass*max_accel;
			thrust_max_twr = shipinfo.mass*gravity*max_twr;

			// Compute maximum thrust for electric thrusters

			if (planet.ignore_atmo) {

				// Since we're going down, it's safe to assume that whatever thrust the atmo thrusters
				// can provide right now, they can provide at least as much for the remainder of the descent
				athrust = shipinfo.eff_atmo_thrust;
				ithrust = shipinfo.IonThrustForAtmoDensity(ComputeAtmoDensity(alt_sl[i-1], planet, radius));

			} else {
				athrust = shipinfo.AtmoThrustForAtmoDensity(ComputeAtmoDensity(alt_sl[i-1], planet, radius));
				ithrust = shipinfo.IonThrustForAtmoDensity(ComputeAtmoDensity(alt_sl[i-1], planet, radius));
			}

			// Compute the hydrogen thrust so as not to exceed the limit

			hthrust=Math.Max(0,Math.Min(shipinfo.max_hydro_thrust, Math.Min(thrust_max_accel-athrust-ithrust, thrust_max_twr-athrust-ithrust)));

			total_thrust=(hthrust+athrust+ithrust)/safetyfactor;

			// Apply Newton formula

			accel = total_thrust/shipinfo.mass - gravity;

			// Integrate acceleration to compute speed

			vert_speed[i]= accel * dt + vert_speed[i-1];

			// If at any point, the vertical speed becomes negative, then it is a failed liftoff

			if (vert_speed[i] < 0) {
				temp_valid = false;
				break;
			}

			// Integrate speed to compute altitude above sea level

			alt_sl[i] = 0.5f * accel * dt*dt + vert_speed[i-1] * dt + alt_sl[i-1];

			dt=dt+0.05f;

		}
		computed = true;
		valid = temp_valid;
	}

	// Interpolates the altitude/speed profile to return the speed corresponding to the altitude given.
	// Uses linear interpolation, with binary search
	// If the altitude is above the final computed altitude, return the final computed speed.
	public float Interpolate(float alt) {

		int left = 0;
		int right = NB_PTS-1;
		int m=(left + right) / 2;

		if (valid == false) {
			return 0;
		}

		// If we are currently below the starting altitude, return 0

		if (alt<=alt_sl[0]) {
			return 0;
		}

		// If we are currently above the altitude of the last simulated point, return the speed corresponding to that

		if (alt >= alt_sl[NB_PTS-1]) {
			return vert_speed[NB_PTS-1];
		}

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

		if (m+1>=NB_PTS) {
			 return vert_speed[NB_PTS-1];
		}

		// Now m is the index such that alt_sl[m] is the highest value lower than alt

		return vert_speed[m] + (alt-alt_sl[m]) * (vert_speed[m+1]-vert_speed[m]) / (alt_sl[m+1]-alt_sl[m]);
	}

	public void Invalidate() {
		valid = false;
		computed = false;
	}

	public bool IsValid() {
		return valid;
	}

	public bool IsComputed() {
		return computed;
	}

	public float GetFinalSpeed() {
		if (valid & computed) {
			return vert_speed[NB_PTS-1];
		} else {
			return 0;
		}
	}

	public float GetFinalAlt() {
		if (valid & computed) {
			return alt_sl[NB_PTS-1];
		} else {
			return 0;
		}
	}
}

public class ShipInfo {
	public float mass;				// kg

	public float max_hydro_thrust; 	// Newtons
	public float max_ion_thrust;	// Newtons
	public float max_atmo_thrust;	// Newtons

	public float eff_hydro_thrust; 	// Newtons
	public float eff_ion_thrust;	// Newtons
	public float eff_atmo_thrust;	// Newtons

	public float current_hydro_thrust; 	// Newtons
	public float current_ion_thrust;	// Newtons
	public float current_atmo_thrust;	// Newtons

	public void UpdateMass(ShipBlocks ship) {
		MyShipMass masse =  ship.ship_ctrller.CalculateShipMass();
		mass = masse.TotalMass;
	}

	public void UpdateThrust(ShipBlocks ship) {

		eff_atmo_thrust=0;
		eff_ion_thrust=0;
		eff_hydro_thrust=0;
		max_atmo_thrust=0;
		max_ion_thrust=0;
		max_hydro_thrust=0;
		current_atmo_thrust=0;
		current_ion_thrust=0;
		current_hydro_thrust=0;

		foreach (IMyThrust alifter in ship.aero_lifters) {
			if (alifter.IsWorking) {
				eff_atmo_thrust = eff_atmo_thrust + alifter.MaxEffectiveThrust;
				max_atmo_thrust = max_atmo_thrust + alifter.MaxThrust;
				current_atmo_thrust = current_atmo_thrust + alifter.CurrentThrust;
			}
		}    
		
		foreach (IMyThrust ilifter in ship.ion_lifters) {
			if (ilifter.IsWorking) {
				eff_ion_thrust = eff_ion_thrust + ilifter.MaxEffectiveThrust;
				max_ion_thrust = max_ion_thrust + ilifter.MaxThrust;
				current_ion_thrust = current_ion_thrust + ilifter.CurrentThrust;
			}
		} 

		foreach (IMyThrust hlifter in ship.h2_lifters) {
			if (hlifter.IsWorking ) {
				eff_hydro_thrust = eff_hydro_thrust + hlifter.MaxEffectiveThrust;
				max_hydro_thrust = max_hydro_thrust + hlifter.MaxThrust;
				current_hydro_thrust = current_hydro_thrust + hlifter.CurrentThrust;
			}
		} 
	}

	public float AtmoThrustForAtmoDensity(float density) {

		float density_effective = Math.Min(density, 1);
		return max_atmo_thrust * density_effective;
	}

	public float IonThrustForAtmoDensity(float density) {

		float density_effective = Math.Min(density, 1);
		return max_ion_thrust * (1-0.8f*density_effective);
	}
	
}

public class PIDController {

	// PID coefficients (constant during execution, defined with the constructor)
	private readonly float KP;
	private readonly float KI;
	private readonly float KD;
	private readonly float AI_MIN;
	private readonly float AI_MAX_FIXED;
	private readonly float AD_FILT;

	// PID parameters
	float delta_prev = 0;
	float deriv_prev = 0;
	public float ap = 0;
	public float ai = 0;
	public float ad = 0;
	public float PIDoutput = -1;

	

	public PIDController(double kp, double ki, double kd, double ai_min, double ai_max_fixed, double ad_filt) {
		KP=(float)kp;
		KI=(float)ki;
		KD=(float)kd;
		AI_MIN=(float)ai_min;
		AI_MAX_FIXED=(float)ai_max_fixed;
		AD_FILT=(float)Helpers.SaturateMinMaxPrioritizeMaxLimit(ad_filt,0,1);
	}



	public void UpdatePIDController(float delta, float ai_max_dynamic) {

		// The output of the PID is in units of thrust-to-weight ratio (dimentionless)

		PIDoutput = -1;

		delta = Helpers.NotNan(delta);

		// Low-pass filtering of the derivative component
		
		float deriv = AD_FILT * deriv_prev + (1-AD_FILT) *(delta - delta_prev);

		deriv_prev = deriv;
		delta_prev = delta;
		
		// P
		ap = delta * KP;

		// I
		ai = ai + delta*KI;

		ai = Helpers.SaturateMinMaxPrioritizeMaxLimit(ai, AI_MIN, AI_MAX_FIXED);

		ai = Math.Min(ai, ai_max_dynamic);
		
		// D
		ad = deriv*KD;
		
		// PID output
		PIDoutput = ap + ai + ad;

	}

	public void ResetPIDController() {
		deriv_prev=0;
		delta_prev=0;
		ap=0;
		ai=0;
		ad=0;
	}
}





