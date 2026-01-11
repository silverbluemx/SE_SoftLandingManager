/*
-------------------------------------
SOFT LANDING MANAGER by silverbluemx
-------------------------------------

A script to automatically manage your thrusters to land safely on planets while
optimizing your fuel and energy use. Also scans the terrain and guides the ship
to a safe landing spot, avoiding obstacles and steep slopes.
Best for use with inverse square law gravity mods such as Real Orbits and high
speed limit mods such as 1000m/s speed mod, but these are not required.

Version 2.2 - 2025-12-24

New features:
- Mode is restored when saving/loading the game
- Script can be set to start in hover mode by default
- Automatically detects gravity exponent (7 for vanilla game or 2 with Real Orbits)
- Mode 1 and 2 cooperate better when you provide horizontal commands and terrain
  avoidance is active.
- When landing, vertical speed is decreased if horizontal speed is too high

Beta features:
- Added experimental mode 5 for landing on asteroids in empty space (no gravity)
  using the ship "down" thrusters to pick up the initial speed. The pilot (you)
  remain tasked with pointing the "radar" camera to the target.

Fixes:
- Improved mode 4 look ahead feature in sea level mode
- Improved landing feedforward thrust value (really zero when above speed setpoint)
- Corrected mode 3 safe speed too low if using mode 4 previously
- Better profile speed limit following if ship has a lot of ion/prototech thrusters
- Radar is turned off in mode3 (hover) to save 1kW on the camera block
- Corrected horizontal speed display if angle and thrusters are disabled in mode 1 & 2

Planet catalog:
- Corrected atmospheric density for Agni
- Hillparam for unknown planets set to 0.065 (instead of 0.1 for vacuum and 0.05 for atmo)

See the Steam Workshop page and README.md and technical.md files on GitHub for more information :
https://github.com/silverbluemx/SE_SoftLandingManager
Published script has been minified, see original code on GitHub with comments and documentation.

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
    // If set to true, the script tries to find the correct gravity exponent when descending.
    // Set it to false to force it to always use the one configured above.
    public readonly bool autoSwitchExponent = true;


    // Set any of these to false to disable the features by default
    // (they can always be switched on/off using commands when the script is running)
    public readonly bool autoLevel = true; 
    public readonly bool terrainAvoidGyro = true;
    public readonly bool terrainAvoidThrusters = true;

    // If set to true, the script will start in hover mode (mode3) the first time
    // it's compiled. Note that after that, the script will start in the same mode
    // as when the game was last saved.
    public readonly bool startHover = true;


    // ---------------------------------------------------------------------------------
    // ---- There should be no reason to change the parameters below for normal use ----
    // ---------------------------------------------------------------------------------
    
    // Below this point the code is minified using MDK to stay below the 100000 characters limit.
    // See https://github.com/silverbluemx/SE_SoftLandingManager for the full source code

public readonly double defaultASLmeters=500;public readonly double gravTransitionHigh=4000;public readonly double
gravTransitionLow=1000;public readonly double aiMax=4;public readonly double aiMin=-0.1;public readonly double vertKp=0.4;public readonly
double vertKi=0.05;public readonly double vertKd=10;public readonly double vertAdFilt=0.8;public readonly double vertAdMax=0.5
;public readonly double LWRoffset=0.0;public readonly double LWRsafetyfactor=1.1;public readonly double LWRlimit=5;public
readonly double accelLimit=30;public readonly double vSpeedSafeLimit=500;public readonly double vSpeedDefault=200;public
readonly double LWR_mix_gnd_ratio=0.7;public readonly double elecLwrSufficient=2;public readonly double mode1IonAltLimit=2000;
public readonly double mode1IonSpeed=115;public readonly double mode1AtmoSpeed=20;public readonly double finalSpeed=1.5;public
readonly double finalSpeedAltitude=20;public readonly double panicDelta=5;public readonly double panicRatio=100;public readonly
double marginalMax=10;public readonly double marginalWarn=5;public readonly double h2MarginWarning=5;public readonly double
landingTimerAltitude=200;public readonly double liftoffTimerAltitude=250;public readonly double radarMaxRange=2e5;public readonly double
maxAngle=20;public readonly int smartDelayTime=20;public readonly double gyroResponsiveness=5;public readonly double
gyroRpmScale=0.1;public readonly double horizKp=0.5;public readonly double horizKi=0.1;public readonly double horizKd=0.1;public
readonly double horizAiMax=0.05;public readonly double speedScale=20;public readonly double inertiaRatioSmall=1e7;public
readonly double inertiaRatioLarge=6e8;public readonly double mode4InitialAlt=50;public readonly double mode4InitialSpeed=0;
public readonly double alt_aiMax=2;public readonly double alt_aiMin=-0.1;public readonly double altKp=0.5;public readonly
double altKi=0.1;public readonly double altKd=0.5;public readonly double altAdFilt=0.8;public readonly double altAdMax=0.5;
public readonly double speedIncrement=0.1;public readonly double maxSpeed=100;public readonly int speedFilterLength=30;public
readonly int altFilterLength=30;public readonly int safeSpeedFilterLength=10;public readonly double safeSpeedAltMin=10;public
readonly double safeSpeedAltMax=400;public readonly double safeSpeedMin=3;public readonly double safeSpeedMax=200;public
readonly double mode5ThrustRatio=0.5;public readonly double mode5MaxSpeed=95;public readonly bool ALLOW_LOGGING=false;public
readonly int LOG_FACTOR=2;public readonly int DisableDelay=3;}public enum SPSource{None,Profile,AltGravFormula,GravFormula,
FinalSpeed,Hold,Unable,RDV}public enum AltSource{Undefined,Ground,Radar}public enum GravSource{Undefined,Identified,Estimate,Local
}public enum WarnType{Info,Good,Risk,Bad}public enum ScanMode{NoRadar,SingleStandby,SingleNarrow,DoubleStandby,
DoubleEarly,DoubleWide}public struct Planet{public string Shortname,Name;public double AtmoDensitySL,AtmoLimitAltitude,HillParam,
GSeaLevel;public bool Precise,Set;public Planet(string shortName,string name,double atmoDensitySL,double atmoLimitAltitude,double
hillParam,double gSeaLevel,bool precise=true,bool set=true){Shortname=shortName.ToLower();Name=name;AtmoDensitySL=atmoDensitySL>=
0?atmoDensitySL:0;AtmoLimitAltitude=atmoLimitAltitude>=0?atmoLimitAltitude:0;HillParam=hillParam>=0?hillParam:0;GSeaLevel
=gSeaLevel>=0?gSeaLevel:0;Precise=precise;Set=set;}}public class PlanetCatalog{List<Planet>Catalog;public PlanetCatalog()
{Catalog=new List<Planet>{new Planet("unknown","Unknown Planet",1,2,0.065,1,false,false),new Planet("dynvacuum",
"Deduced Vacuum Planet",0,0,0.065,1,false,true),new Planet("dynatmo","Deduced Atmo Planet",0.8,1,0.065,1,false,true),new Planet("vacuum",
"Generic Vacuum Planet",0,0,0.065,1,false,true),new Planet("atmo","Generic Atmo Planet",0.8,1,0.065,1,false,true),new Planet("pertam","Pertam",
1,2,0.025,1.2),new Planet("triton","Triton",1,0.47,0.20,1),new Planet("earth","Earthlike",1,2,0.12,1),new Planet("alien",
"Alien",1.2,2,0.12,1.1),new Planet("mars","Mars (vanilla)",1,2,0.12,0.9),new Planet("moon","Moon (vanilla)",0,1,0.03,0.25),new
Planet("europa","Europa",0.5,1,0.06,0.25),new Planet("titan","Titan",0.5,1,0.03,0.25),new Planet("komorebi","Komorebi",1.12,
2.4,0.032,1.14),new Planet("orlunda","Orlunda",0.89,6,0.01,1.12),new Planet("trelan","Trelan",1,1.2,0.1285,0.92),new Planet
("teal","Teal",1,2,0.02,1),new Planet("kimi","Kimi",0,1,0,0.05),new Planet("qun","Qun",0,1,0.25,0.42),new Planet("tohil",
"Tohil",0.5,1,0.03,0.328),new Planet("satreus","Satreus",0.9,1.5,0.04,0.95),new Planet("agni","Agni",0.55,2.3,0.022,1.27),new
Planet("cauldron","Cauldron",1,3.5,0.01,1.58),new Planet("tellus","Tellus",1,2.7,0.06,1),new Planet("pyke","Pyke",1.5,2,0.06,
1.42),new Planet("saprimentas","Saprimentas",1.5,2,0.07,0.96),new Planet("aulden","Aulden",1.2,2,0.10,0.82),new Planet(
"silona","Silona",0.85,2,0.03,0.64),new Planet("argus","Argus",0.79,2,0.01,1.45),new Planet("aridus","Aridus",1.3,1,0.1,0.5),new
Planet("microtech","Microtech",1,0.5,0.25,1f),new Planet("hurston","Hurston",1,1.9,0.11,1.1),new Planet("ignis","Ignis",0.85,3
,0.005,1.08),new Planet("tharsis","Tharsis",0.85,3,0.015,0.75),new Planet("umbris","Umbris",0,0,0.05,0.19),new Planet(
"valkor","Valkor",1,0.3,0.165,1.05),new Planet("theros","Theros",1,0.73,0.1,0.95),new Planet("thanatos","Thanatos",1.5,2.8,0.04,
1.4),new Planet("halcyon","Halcyon",0.85,1.3,0.3,0.5),new Planet("terra","(Terra) Earth by Infinite",2,0.9,0.02,1),new
Planet("luna","Luna by Infinite",0,1,0.07,0.16),new Planet("sspmars","Mars by Infinite",0.006,2,0.09,0.38),new Planet("venus",
"Venus by Infinite",92,2,0.04,0.9),new Planet("mercury","Mercury by Infinite",0,1,0.1,0.37),new Planet("ceres","Ceres by Infinite",0,0.5,
0.1,0.05),new Planet("deimos","Deimos by Infinite",0,0,0.8,0.05),new Planet("phobos","Phobos by Infinite",0,0,1,0.05),new
Planet("callisto","Callisto by Infinite",0,0.5,0.04,0.12),new Planet("europa","Europa by Infinite",0,0.5,0.04,0.13),new Planet
("ganymede","Ganymede by Infinite",0,0,0.04,0.14),new Planet("io","Io by Infinite",0,0,0.025,0.18),new Planet("dione",
"Dione by Infinite",0,0.5,0.06,0.05),new Planet("enceladus","Enceladus by Infinite",0,0.5,0.02,0.05),new Planet("iapetus",
"Iapetus by Infinite",0,0,0.03,0.05),new Planet("mimas","Mimas by Infinite",0,0.5,0.07,0.05),new Planet("rhea","Rhea by Infinite",0,0,0.06,
0.05),new Planet("thetys","Thetys by Infinite",0,0.5,0.09,0.05),new Planet("titan","Titan by Infinite",1.5,3,0.01,0.14),new
Planet("ariel","Ariel by Infinite",0,0.5,0.03,0.05),new Planet("charon","Charon by Infinite",0,0.5,0.03,0.05),new Planet(
"miranda","Miranda by Infinite",0,0.5,0.05,0.08),new Planet("oberon","Oberon by Infinite",0,0.5,0.03,0.05),new Planet("pluto",
"Pluto by Infinite",0.00001,0,0.03,0.06),new Planet("titania","Titania by Infinite",0,0.5,0.03,0.05),new Planet("triton",
"Triton by Infinite",0,0.5,0.03,0.07),new Planet("umbriel","Umbriel by Infinite",0,0.5,0.03,0.05),new Planet("acheris","Acheris",1.5,2,
0.0003,1.36),new Planet("ares","Ares",0.85,3,0.025,0.53),new Planet("euterpe","Euterpe",0.1,2,0.025,0.19),new Planet("gaia",
"Gaia",1,3,0.03,0.97),new Planet("nyxion","Nyxion",0,0,0.095,0.22),new Planet("tartarus","Tartarus",1.1,1.5,0.08,1.13),new
Planet("tarvos","Tarvos",0.85,3,0.03,0.75),new Planet("vulcanis","Vulcanis",0.85,3,0.065,0.9),new Planet("zephyr","Zephyr",10,
3,0.01,3.24),new Planet("calliope","Calliope",1,3,0.03,0.92),new Planet("calypso","Calypso",0.2,3,0.03,0.63),new Planet(
"cryos","Cryos",0.1,2,0.065,0.09),new Planet("erebus","Erebus",0,0,0.028,0.32),new Planet("helghan","Helghan",1.2,3.5,0.01,1.1)
,new Planet("arcadia","Arcadia",1,2,0.04,1.17),new Planet("sarilla","Sarilla",0,0,0.14,0.74),new Planet("anteros",
"Anteros",1.10,1.69,0.07,1.32),new Planet("chimera","Chimera",1.22,1.5,0.1,1),new Planet("zira","Zira",0,0,0.14,0.16),new Planet(
"celaeno","Celaeno",1.02,6.5,0.02,0.93),new Planet("scylla","Scylla",0,0,0.01,0.32),new Planet("dustydesert",
"Dusty Desert Planet",1,2,0.12,1),new Planet("gamadon","Gamadon",0.8,2,0.15,0.72),new Planet("kuma","Kuma",1,0.5,0.1,1),new Planet("mieliv",
"Mieliv",1,0.5,0.1,1),new Planet("sario","Sario",0,0,0.30,0.3),new Planet("kor","Kor",0,0,0.03,0.74)};}public Planet get_planet(
string command,out bool found){foreach(Planet candidate in Catalog){if(command.ToLower().Contains(candidate.Shortname)){found=
true;return candidate;}}found=false;return Catalog[0];}}public class ShipBlocks{public ThrGroup lifters,fwdThr,rearThr,
leftThr,rightThr,downThr;public List<IMyTextSurface>MainDisplays,DebugDisplays;public List<IMyParachute>parachutes;public
IMyShipController shipCtrller;public List<IMyTerminalBlock>landing_timers,liftoff_timers;public List<IMyTerminalBlock>on_timers,
off_timers;public List<IMyGyro>gyros;public List<IMyLandingGear>gears;public List<IMyTerminalBlock>radars;public List<
IMyTerminalBlock>soundblocks;public List<IMyGasTank>h2_tanks;public ShipBlocks(){MainDisplays=new List<IMyTextSurface>();DebugDisplays=
new List<IMyTextSurface>();parachutes=new List<IMyParachute>();landing_timers=new List<IMyTerminalBlock>();liftoff_timers=
new List<IMyTerminalBlock>();on_timers=new List<IMyTerminalBlock>();off_timers=new List<IMyTerminalBlock>();gyros=new List<
IMyGyro>();gears=new List<IMyLandingGear>();radars=new List<IMyTerminalBlock>();soundblocks=new List<IMyTerminalBlock>();
h2_tanks=new List<IMyGasTank>();}}LandingManager manager;RunTimeCounter runtime;Logger logger;bool ranTick1=false;bool ranTick10
=false;bool ranTick100=false;bool logging=false;MyIni _ini=new MyIni();public
 Program
(){Runtime.UpdateFrequency=UpdateFrequency.Update1|UpdateFrequency.Update10|UpdateFrequency.Update100;
SLMShipConfiguration shipconfig=new SLMShipConfiguration();ShipBlocks ship=GetBlocks(shipconfig);SLMConfiguration config=new
SLMConfiguration();PlanetCatalog catalog=new PlanetCatalog();runtime=new RunTimeCounter(this);manager=new LandingManager(config,ship,
catalog,runtime,config.startHover);logger=new Logger(manager.AllLogNames(),config.LOG_FACTOR,config.ALLOW_LOGGING);_ini.
TryParse(Storage);int _modeToRestore=_ini.Get("SLM","mode").ToInt32(0);double _mode4AltToRestore=_ini.Get("SLM","mode4alt").
ToDouble(config.mode4InitialAlt);double _mode4SpeedToRestore=_ini.Get("SLM","mode4speed").ToDouble(config.mode4InitialSpeed);
bool _mode4SL=_ini.Get("SLM","mode4SL").ToBoolean(false);switch(_modeToRestore){case 0:break;case 1:manager.ConfigureMode1()
;break;case 2:manager.ConfigureMode2();break;case 3:manager.ConfigureMode3();break;case 4:manager.ConfigureMode4(
_mode4AltToRestore,_mode4SpeedToRestore);break;case 5:manager.ConfigureMode5();break;}double _gravExpToRestore=_ini.Get("SLM",
"gravityExponent").ToDouble(config.gravityExponent);manager.GravExponent=_gravExpToRestore;}public void
 Save
(){_ini.Clear();_ini.Set("SLM","mode",manager.Mode);_ini.Set("SLM","mode4alt",manager.Autopilot.mode4DesiredAltitude);
_ini.Set("SLM","mode4speed",manager.Autopilot.mode4DesiredSpeed);_ini.Set("SLM","mode4SL",manager.Autopilot.altitudeMode==
AutoPilot.AltitudeMode.SeaLevel?true:false);_ini.Set("SLM","gravityExponent",manager.GravExponent);Storage=_ini.ToString();}
public void
 Main
(string arg,UpdateType updateSource){runtime.Count(ranTick1,ranTick10,ranTick100);if((updateSource&(UpdateType.Trigger|
UpdateType.Terminal))!=0){if(arg=="off"){manager.ConfigureMode0();logging=false;logger.Clear();}else{if(arg.Contains("mode1")){
manager.ConfigureMode1();logging=true;}else if(arg.Contains("mode2")){manager.ConfigureMode2();logging=true;}else if(arg.
Contains("mode3")){manager.ConfigureMode3();logging=true;}else if(arg.Contains("mode4")){manager.ConfigureMode4();logging=true;}
else if(arg.Contains("mode5")){manager.ConfigureMode5();logging=true;}if(arg.Contains("angleoff"))manager.DisableAngle();
else if(arg.Contains("angleon"))manager.EnableAngle();else if(arg.Contains("angleswitch"))manager.SwitchAngle();if(arg.
Contains("thrustersoff"))manager.DisableThrust();else if(arg.Contains("thrusterson"))manager.EnableThrust();else if(arg.Contains
("thrustersswitch"))manager.SwitchThrust();if(arg.Contains("leveloff"))manager.DisableLeveler();else if(arg.Contains(
"levelon"))manager.EnableLeveler();else if(arg.Contains("levelswitch"))manager.SwitchLeveler();if(arg.Contains("altup"))manager.
Mode4IncreaseAltitude();else if(arg.Contains("altdown"))manager.Mode4DecreaseAltitude();if(arg.Contains("speedup"))manager.Mode4IncreaseSpeed
();else if(arg.Contains("speeddown"))manager.Mode4DecreaseSpeed();if(arg.Contains("altswitch"))manager.Mode4AltSwitch();
else if(arg.Contains("altgnd"))manager.Mode4AltGND();else if(arg.Contains("altsl"))manager.Mode4AltSL();if(arg.Contains(
"dumplog")){logging=false;Me.CustomData=logger.Output();}if(arg.Contains("clearlog"))logger.Clear();manager.SetPlanet(arg);}}
ranTick1=false;ranTick10=false;ranTick100=false;if((updateSource&UpdateType.Update100)!=0){ranTick100=true;manager.Tick100();}if
((updateSource&UpdateType.Update10)!=0){ranTick10=true;manager.Tick10();}if((updateSource&UpdateType.Update1)!=0){
ranTick1=true;manager.Tick1();if(logging)logger.Log(manager.AllLogValues());}}public ShipBlocks GetBlocks(SLMShipConfiguration
conf){var s=new ShipBlocks();Echo("SOFT LANDING MANAGER");Func<IMyTerminalBlock,bool>filter=b=>{bool result=b.
IsSameConstructAs(Me);foreach(string name in conf.IGNORE_NAME){if(b.CustomName.Contains(name))result=false;}return result;};Action<List<
IMyTerminalBlock>,List<string>,string,Func<IMyTerminalBlock,bool>>SearchBlocks=(blocksList,names,descr,filtr)=>{List<IMyTerminalBlock>
temp=new List<IMyTerminalBlock>();foreach(string name in names){GridTerminalSystem.SearchBlocksOfName(name,temp,filtr);
blocksList.AddRange(temp);}Echo("Found "+blocksList.Count+" "+descr);};Action<List<IMyTextSurface>,List<string>,string,Func<
IMyTerminalBlock,bool>>SearchSurfaces=(blocksList,prefixes,descr,filtr)=>{List<IMyTerminalBlock>temp=new List<IMyTerminalBlock>();
SearchBlocks(temp,prefixes,"possible display(s)",filtr);foreach(IMyTerminalBlock b in temp){if(b is IMyTextPanel){blocksList.Add(b
as IMyTextSurface);}else if(b is IMyTextSurfaceProvider){IMyTextSurfaceProvider p=(IMyTextSurfaceProvider)b;if(p.
SurfaceCount>=1&&p.UseGenericLcd){int N=Helpers.FindN(b.CustomName,prefixes);if(N>=0){blocksList.Add(p.GetSurface(N));}else{
blocksList.Add(p.GetSurface(0));}}}}Echo("Found "+blocksList.Count+" "+descr);};SearchBlocks(s.radars,conf.RADAR_NAME,"radars(s)",
filter);SearchBlocks(s.landing_timers,conf.LANDING_TIMER_NAME,"landing timer(s)",filter);SearchBlocks(s.liftoff_timers,conf.
LIFTOFF_TIMER_NAME,"liftoff timer(s)",filter);SearchBlocks(s.on_timers,conf.ON_TIMER_NAME,"on timer(s)",filter);SearchBlocks(s.off_timers,
conf.OFF_TIMER_NAME,"off timer(s)",filter);SearchBlocks(s.soundblocks,conf.SOUND_NAME,"sound block(s)",filter);
SearchSurfaces(s.MainDisplays,conf.LCD_NAME,"valid display(s)",filter);SearchSurfaces(s.DebugDisplays,conf.DEBUGLCD_NAME,
"valid debug display(s)",filter);GridTerminalSystem.GetBlocksOfType(s.parachutes,filter);Echo("Found "+s.parachutes.Count+" parachutes");
GridTerminalSystem.GetBlocksOfType(s.gyros,filter);Echo("Found "+s.gyros.Count+" gyros");GridTerminalSystem.GetBlocksOfType(s.gears,filter
);Echo("Found "+s.gears.Count+" landing gears");var all_tanks=new List<IMyGasTank>();GridTerminalSystem.GetBlocksOfType(
all_tanks,filter);foreach(IMyGasTank tank in all_tanks){if(tank.BlockDefinition.SubtypeName.Contains("Hydrogen")){Echo(
"Found h2 tank:"+tank.CustomName);s.h2_tanks.Add(tank);}}var named_ctrllers=new List<IMyTerminalBlock>();SearchBlocks(named_ctrllers,
conf.CTRLLER_NAME,"possible controller(s)",filter);if(named_ctrllers.Count>=1){s.shipCtrller=named_ctrllers[0]as
IMyShipController;Echo("Using controller:"+s.shipCtrller.CustomName);}else{var possible_controllers=new List<IMyShipController>();
GridTerminalSystem.GetBlocksOfType(possible_controllers,b=>b.CanControlShip);if(possible_controllers.Count==0){throw new Exception(
"Error: no suitable cockpit or remote control block.");}else{s.shipCtrller=possible_controllers[0];Echo("Using controller:"+s.shipCtrller.CustomName);}}Matrix MatrixCockpit;
s.shipCtrller.Orientation.GetMatrix(out MatrixCockpit);foreach(IMyTerminalBlock radar in s.radars){Matrix MatrixRadar;
radar.Orientation.GetMatrix(out MatrixRadar);if(MatrixRadar.Forward!=MatrixCockpit.Down||MatrixRadar.Up!=MatrixCockpit.
Forward){Echo("Warning: radar "+radar.CustomName+" wrong orientation.");}}var tempFwdThr=new List<IMyThrust>();var tempRearThr=
new List<IMyThrust>();var tempLeftThr=new List<IMyThrust>();var tempRightThr=new List<IMyThrust>();var tempDownThr=new List
<IMyThrust>();var tempLifters=new List<IMyThrust>();var tempAllThr=new List<IMyThrust>();GridTerminalSystem.
GetBlocksOfType(tempAllThr,filter);foreach(IMyThrust t in tempAllThr){Matrix MatrixThrust;t.Orientation.GetMatrix(out MatrixThrust);if(
MatrixThrust.Forward==MatrixCockpit.Down)tempLifters.Add(t);else if(MatrixThrust.Forward==MatrixCockpit.Backward)tempFwdThr.Add(t);
else if(MatrixThrust.Forward==MatrixCockpit.Forward)tempRearThr.Add(t);else if(MatrixThrust.Forward==MatrixCockpit.Right)
tempLeftThr.Add(t);else if(MatrixThrust.Forward==MatrixCockpit.Left)tempRightThr.Add(t);else if(MatrixThrust.Forward==MatrixCockpit
.Up)tempDownThr.Add(t);}s.lifters=new ThrGroup(tempLifters,"lifters");Echo("Found "+s.lifters.Inventory()+" lifters");s.
fwdThr=new ThrGroup(tempFwdThr,"fwr thr");Echo("Found "+s.fwdThr.Inventory()+" fwd thr");s.rearThr=new ThrGroup(tempRearThr,
"rear thr");Echo("Found "+s.rearThr.Inventory()+" rear thr");s.leftThr=new ThrGroup(tempLeftThr,"left thr");Echo("Found "+s.
leftThr.Inventory()+" left thr");s.rightThr=new ThrGroup(tempRightThr,"right thr");Echo("Found "+s.rightThr.Inventory()+
" right thr");s.downThr=new ThrGroup(tempDownThr,"down thr");Echo("Found "+s.downThr.Inventory()+" down thr");return s;}public class
LandingManager{public int Mode=0;public bool UseAngle;public bool UseHorizThr;public bool Level;public double GravExponent;double
obsDensity=-1;double gravNow=0;double shipWeight=0;double gndAltitude=0;double slAltitude=0;double gndSlOffset=0;double
radarOffset=0;double aLwrNow=0,iLwrNow=0,hLwrNow=0;double aLwrGnd=0,iLwdGnd=0,hLwrGnd=0;double lwrTargetSelected=0;double
vertSpeedSP=0,vertSpeed=0,fwdSpeedSP=0,fwdSpeed=0,leftSpeedSP=0,leftSpeed=0;double vertSpeedDelta=0;double thrCommand=0;double
lwrCommand=0;bool panic=false;int allowDisable=0;int marginal=0;bool allowLandingTimer=false,allowLiftoffTimer=false;double
gndGravExp;int GravExponentScore=0;bool blink;WarnType warnState=WarnType.Info;SPSource speedSPSrc=SPSource.None;AltSource altSrc=
AltSource.Undefined;GravSource gravSrc=GravSource.Undefined;SLMConfiguration conf;ShipBlocks ship;EarlySurfaceGravityEstimator
estimator2,estimator7;ShipInfo shipinfo;LiftoffProfileBuilder profile;Planet planet;PlanetCatalog catalog;PIDController vertPID;
AutoLeveler leveler;GroundRadar radar;HorizontalThrusters horizThrusters;RunTimeCounter runTime;MovingAverage leftSpeedTgt,
fwdSpeedTgt,altFilter;RateLimiter speedTgt;public AutoPilot Autopilot;public LandingManager(SLMConfiguration conf,ShipBlocks
ship_defined,PlanetCatalog catalog_input,RunTimeCounter runTime,bool startHover=false){this.conf=conf;ship=ship_defined;catalog=
catalog_input;this.runTime=runTime;estimator2=new EarlySurfaceGravityEstimator(2);estimator7=new EarlySurfaceGravityEstimator(7);
shipinfo=new ShipInfo(ship,this.conf);profile=new LiftoffProfileBuilder(this.conf.gravityExponent);vertPID=new PIDController(
conf.vertKp,conf.vertKi,conf.vertKd,conf.aiMin,conf.aiMax,conf.vertAdFilt,this.conf.vertAdMax);leveler=new AutoLeveler(ship.
shipCtrller,ship.gyros,Math.Min(this.conf.maxAngle,shipinfo.MaxAngle()),this.conf.smartDelayTime,this.conf.gyroResponsiveness,this.
conf.gyroRpmScale);radar=new GroundRadar(ship.radars,this.conf.radarMaxRange,this.conf.speedScale);horizThrusters=new
HorizontalThrusters(ship,this.conf.smartDelayTime,this.conf.horizKp,this.conf.horizKi,this.conf.horizKd,this.conf.horizAiMax);leftSpeedTgt=
new MovingAverage(3);fwdSpeedTgt=new MovingAverage(3);altFilter=new MovingAverage(3);speedTgt=new RateLimiter(999,-0.1);
Autopilot=new AutoPilot(this.conf);UseAngle=this.conf.terrainAvoidGyro;UseHorizThr=this.conf.terrainAvoidThrusters;Level=this.
conf.autoLevel;GravExponent=conf.gravityExponent;ConfigureMode0();SetUpLCDs();if(startHover)ConfigureMode3();}public void
ConfigureMode0(){Mode=0;ship.lifters.Disable();ship.downThr.Disable();horizThrusters.Disable();radar.DisableRadar();SetPlanet(
"unknown");TriggerOffTimers();profile.Invalidate();speedSPSrc=SPSource.None;altSrc=AltSource.Undefined;gravSrc=GravSource.
Undefined;radar.mode=ScanMode.NoRadar;leveler.Disable();estimator2.Reset();estimator7.Reset();InitLandingLiftoffTimers();}public
void ConfigureMode1(){if(!DisableConditions()){if(Mode!=2&&Mode!=1){radar.StartRadar();TriggerOnTimers();profile.Invalidate(
);speedTgt.Init(-conf.vSpeedSafeLimit);}Mode=1;ship.shipCtrller.DampenersOverride=false;if(Level)leveler.Enable();
allowDisable=conf.DisableDelay;InitLandingLiftoffTimers();}}public void ConfigureMode2(){if(!DisableConditions()){if(Mode!=2&&Mode!=
1){radar.StartRadar();TriggerOnTimers();profile.Invalidate();speedTgt.Init(-conf.vSpeedSafeLimit);}Mode=2;ship.
shipCtrller.DampenersOverride=false;if(Level)leveler.Enable();allowDisable=conf.DisableDelay;InitLandingLiftoffTimers();}}public
void ConfigureMode3(){if(!DisableConditions()){Mode=3;ship.shipCtrller.DampenersOverride=true;if(Level)leveler.Enable();
speedSPSrc=SPSource.None;ship.lifters.Disable();allowDisable=conf.DisableDelay;radar.DisableRadar();InitLandingLiftoffTimers();
marginal=0;}}public void ConfigureMode4(double altitude=0,double speed=0,AutoPilot.AltitudeMode altitudeMode=AutoPilot.
AltitudeMode.Ground){Mode=4;ship.shipCtrller.DampenersOverride=false;if(altitude==0)Autopilot.mode4DesiredAltitude=Math.Max(conf.
mode4InitialAlt,gndAltitude);else Autopilot.mode4DesiredAltitude=altitude;if(speed==0)Autopilot.mode4DesiredSpeed=conf.
mode4InitialSpeed;else Autopilot.mode4DesiredSpeed=speed;speedTgt.Init(0);if(Level)leveler.Enable();speedSPSrc=SPSource.Hold;Autopilot.
Init();Autopilot.altitudeMode=altitudeMode;vertPID.Reset();GearUnLock();allowDisable=conf.DisableDelay;radar.StartRadar();
InitLandingLiftoffTimers();}public void ConfigureMode5(){if(gravNow==0){Mode=5;ship.shipCtrller.DampenersOverride=false;speedTgt.Init(0);
speedSPSrc=SPSource.None;vertPID.Reset();allowDisable=conf.DisableDelay;radar.StartRadar();InitLandingLiftoffTimers();}}public
void SetPlanet(string name){bool found;Planet tplanet=catalog.get_planet(name,out found);if(found)planet=tplanet;if(planet.
Shortname=="unknown")planet.AtmoDensitySL=ship.lifters.WorstDensity();}public void Tick100(){if(Mode==1||Mode==2){estimator2.
UpdateEstimates(gravNow,slAltitude,planet.HillParam);estimator7.UpdateEstimates(gravNow,slAltitude,planet.HillParam);SelectBestExponent
();}if(Mode!=5)UpdatePlanetAtmo();ManageSoundBlocks();shipinfo.UpdateMass();shipinfo.UpdateInertia();
UpdateMaxGravitiesAndWarning();ship.lifters.UpdateDensitySweep();if(allowDisable>0)allowDisable--;}public void Tick10(){UpdateGrav();
UpdateShipWeight();ship.lifters.UpdateThrust();horizThrusters.UpdateThrust();ship.downThr.UpdateThrust();UpdateAvailableLWR();
ComputeSurfaceGravityEstimate();UpdateLWRTarget();if((Mode==1)||(Mode==2)){radar.ScanForAltitude(90-leveler.pitch,90-leveler.roll);if(UseAngle||
UseHorizThr)radar.ScanTerrain(90-leveler.pitch,90-leveler.roll);UpdateProfile();}else if(Mode==3){Autopilot.UpdateSafeSpeed(
gndAltitude,-1);}else if(Mode==4){const double COS40=0.766;double forward_scan_distance=(conf.safeSpeedAltMax+10)/COS40;double
tentative_scan=radar.ScanDir(40,0,90-leveler.pitch,90-leveler.roll,forward_scan_distance);Autopilot.forward=COS40*tentative_scan;if(
tentative_scan<forward_scan_distance){Autopilot.forwardValid=true;}else{Autopilot.forwardValid=false;}Autopilot.UpdateSafeSpeed(
gndAltitude,Autopilot.forward);}else if(Mode==5){radar.ScanForAltitude(0,0);}UpdateDisplays();ManageTimers();ManagePanicParachutes(
);UpdateDebugDisplays();if(Mode!=0&&allowDisable==0&&DisableConditions())ConfigureMode0();if(Mode==5&&gndAltitude<conf.
finalSpeedAltitude){ConfigureMode0();ship.shipCtrller.DampenersOverride=true;}}public void Tick1(){UpdateAltitude();if((Mode==1)||(Mode==2
)){UpdateShipSpeedsInGravity();radar.IncrementAltAge();UpdateSpeedSetPointInGravity();vertSpeedDelta=vertSpeedSP-
vertSpeed;vertPID.UpdatePIDController(vertSpeedDelta,conf.aiMin,aLwrNow+iLwrNow+hLwrNow);ApplyThrustOverrideInGravity(vertPID.
output);if(UseAngle||UseHorizThr){leftSpeedTgt.AddValue(radar.RecommandLeftSpeed());leftSpeedSP=leftSpeedTgt.Get();fwdSpeedTgt
.AddValue(radar.RecommandFwdSpeed());fwdSpeedSP=fwdSpeedTgt.Get();}else{leftSpeedSP=fwdSpeedSP=0;}var moveIndicator=ship.
shipCtrller.MoveIndicator;if(moveIndicator.Z>0.1f)fwdSpeedSP=-10;else if(moveIndicator.Z<-0.1f)fwdSpeedSP=10;if(moveIndicator.X>
0.1f)leftSpeedSP=-10;else if(moveIndicator.X<-0.1f)leftSpeedSP=10;if(Level){if(UseAngle){leveler.Tick(fwdSpeed,leftSpeed,
fwdSpeedSP,leftSpeedSP);}else{leveler.Tick();}}if(UseHorizThr)horizThrusters.Tick(fwdSpeed,leftSpeed,fwdSpeedSP,leftSpeedSP,
shipinfo.mass,UseAngle,true);}else if(Mode==3){UpdateShipSpeedsInGravity();Autopilot.UpdateSpeedDirect(ship.shipCtrller.
MoveIndicator);fwdSpeedSP=Autopilot.fwdSpeedSP;leftSpeedSP=Autopilot.leftSpeedSP;if(UseAngle)leveler.Tick(fwdSpeed,leftSpeed,
fwdSpeedSP,leftSpeedSP);else leveler.Tick();if(UseHorizThr)horizThrusters.Tick(fwdSpeed,leftSpeed,fwdSpeedSP,leftSpeedSP,shipinfo.
mass,UseAngle,false);}else if(Mode==4){UpdateShipSpeedsInGravity();Autopilot.UpdateSpeedProgressive(ship.shipCtrller.
MoveIndicator);fwdSpeedSP=Autopilot.fwdSpeedSP;leftSpeedSP=Autopilot.leftSpeedSP;if(UseAngle)leveler.Tick(fwdSpeed,leftSpeed,
fwdSpeedSP,leftSpeedSP);else leveler.Tick();if(UseHorizThr)horizThrusters.Tick(fwdSpeed,leftSpeed,fwdSpeedSP,leftSpeedSP,shipinfo.
mass,UseAngle,true);Autopilot.UpdateVertSpeedSP(gndAltitude,slAltitude,gravNow);vertSpeedSP=Autopilot.vertSpeedSP;
vertSpeedDelta=vertSpeedSP-vertSpeed;vertPID.UpdatePIDController(vertSpeedDelta,conf.aiMin,aLwrNow+iLwrNow+hLwrNow);
ApplyThrustOverrideInGravity(vertPID.output);}else if(Mode==5){UpdateShipSpeedsInSpace();UpdateSpeedSetPointInSpace();if(speedSPSrc!=SPSource.None){
vertSpeedDelta=vertSpeedSP-vertSpeed;vertPID.UpdatePIDController(vertSpeedDelta,conf.aiMin,0.1);ApplyThrustOverrideInSpace(vertPID.
output);}}}void UpdateProfile(){double confidenceBest=GravExponent==2?estimator2.confidenceBest:estimator7.confidenceBest;
double radiusBest=GravExponent==2?estimator2.radiusBest:estimator7.radiusBest;if(confidenceBest>0.95){if(!planet.Precise==
false)planet.GSeaLevel=Helpers.ms2_to_g(gndGravExp);if(Mode==1)profile.Compute(conf.finalSpeedAltitude+gndSlOffset,shipinfo,
planet,radiusBest,conf.accelLimit,conf.LWRlimit,conf.elecLwrSufficient,conf.LWRsafetyfactor,conf.vSpeedSafeLimit,conf.
finalSpeed,ship.lifters);if(Mode==2)profile.Compute(conf.finalSpeedAltitude+gndSlOffset,shipinfo,planet,radiusBest,conf.accelLimit
,conf.LWRlimit,conf.LWRlimit,conf.LWRsafetyfactor,conf.vSpeedSafeLimit,conf.finalSpeed,ship.lifters);}}void
UpdateShipWeight(){shipWeight=shipinfo.mass*gravNow;}void UpdateGrav(){gravNow=ship.shipCtrller.GetNaturalGravity().Length();}void
UpdateAvailableLWR(){aLwrNow=LWR(gravNow,shipinfo.mass,ship.lifters.aThrustEff);iLwrNow=LWR(gravNow,shipinfo.mass,ship.lifters.iThrustEff)
;hLwrNow=LWR(gravNow,shipinfo.mass,ship.lifters.hThrustEff);aLwrGnd=LWR(gndGravExp,shipinfo.mass,ship.lifters.
AtmoThrustForDensity(planet.AtmoDensitySL));iLwdGnd=LWR(gndGravExp,shipinfo.mass,ship.lifters.IonThrustForDensity(planet.AtmoDensitySL)+ship
.lifters.PrototechThrustForDensity(planet.AtmoDensitySL));hLwrGnd=LWR(gndGravExp,shipinfo.mass,ship.lifters.hThrustMax);}
void UpdateMaxGravitiesAndWarning(){double maxGNow=(shipinfo.mass>0)?Helpers.ms2_to_g(ship.lifters.AtmoThrustForDensity(
planet.AtmoDensitySL)+ship.lifters.IonThrustForDensity(planet.AtmoDensitySL)+ship.lifters.PrototechThrustForDensity(planet.
AtmoDensitySL)+ship.lifters.hThrustMax)/(shipinfo.mass*conf.LWRsafetyfactor*(1+conf.LWRoffset)):0;warnState=(maxGNow<Helpers.ms2_to_g
(gndGravExp))?WarnType.Bad:WarnType.Good;}void UpdateSpeedSetPointInGravity(){double tempVertSpeedSP;if(gndAltitude<
GroundRadar.UNDEFINED_ALTITUDE){if(gndAltitude>conf.finalSpeedAltitude){if(profile.IsValid()){tempVertSpeedSP=-profile.
InterpolateSpeed(slAltitude);speedSPSrc=SPSource.Profile;}else if(lwrTargetSelected>1){tempVertSpeedSP=-Math.Sqrt(2*(gndAltitude-conf.
finalSpeedAltitude)*(lwrTargetSelected-1)*gndGravExp)-conf.finalSpeed;speedSPSrc=SPSource.AltGravFormula;}else{tempVertSpeedSP=0;
speedSPSrc=SPSource.Unable;}}else{tempVertSpeedSP=-conf.finalSpeed;speedSPSrc=SPSource.FinalSpeed;}}else{tempVertSpeedSP=-conf.
vSpeedDefault*(lwrTargetSelected-1)/Helpers.ms2_to_g(gravNow);speedSPSrc=SPSource.GravFormula;}double horizontalExcessEnergy=Math.Pow
(Math.Max(0,Math.Abs(fwdSpeed)-GroundRadar.HORIZ_MAX_SPEED),2)+Math.Pow(Math.Max(0,Math.Abs(leftSpeed)-GroundRadar.
HORIZ_MAX_SPEED),2);tempVertSpeedSP=Helpers.NotNan(tempVertSpeedSP);tempVertSpeedSP=-Math.Sqrt(tempVertSpeedSP*tempVertSpeedSP-
horizontalExcessEnergy);tempVertSpeedSP=Helpers.NotNan(tempVertSpeedSP);vertSpeedSP=Math.Max(tempVertSpeedSP,speedTgt.Limit(tempVertSpeedSP));
vertSpeedSP=Helpers.SatMinMax(vertSpeedSP,-conf.vSpeedSafeLimit,-conf.finalSpeed);}void UpdateSpeedSetPointInSpace(){if(gndAltitude
<GroundRadar.UNDEFINED_ALTITUDE-5){double accel=Math.Min(ship.lifters.totalThrustEff/shipinfo.mass*conf.mode5ThrustRatio,
conf.accelLimit);if(gndAltitude>conf.finalSpeedAltitude){vertSpeedSP=-Math.Sqrt(2*accel*(gndAltitude-conf.finalSpeedAltitude
));vertSpeedSP=Math.Max(vertSpeedSP,-conf.mode5MaxSpeed);speedSPSrc=SPSource.RDV;}else{vertSpeedSP=0;speedSPSrc=SPSource.
None;}}else{vertSpeedSP=0;speedSPSrc=SPSource.None;}}void UpdateShipSpeedsInGravity(){MyShipVelocities velocities=ship.
shipCtrller.GetShipVelocities();Vector3D normlinvel=Vector3D.Normalize(velocities.LinearVelocity);Vector3D normal_gravity=-Vector3D
.Normalize(ship.shipCtrller.GetNaturalGravity());vertSpeed=Helpers.NotNan(Vector3D.Dot(normlinvel,normal_gravity))*ship.
shipCtrller.GetShipSpeed();fwdSpeed=Helpers.NotNan(Vector3D.Dot(normlinvel,Vector3D.Cross(normal_gravity,ship.shipCtrller.
WorldMatrix.Right))*ship.shipCtrller.GetShipSpeed());leftSpeed=Helpers.NotNan(Vector3D.Dot(normlinvel,Vector3D.Cross(normal_gravity
,ship.shipCtrller.WorldMatrix.Forward))*ship.shipCtrller.GetShipSpeed());}void UpdateShipSpeedsInSpace(){Vector3D
velocities=ship.shipCtrller.GetShipVelocities().LinearVelocity;vertSpeed=velocities.Dot(ship.shipCtrller.WorldMatrix.
GetDirectionVector(Base6Directions.Direction.Up));}void UpdateAltitude(){double ctrllerAltSurf,radarAlt;bool ctrllerAltSurfValid=ship.
shipCtrller.TryGetPlanetElevation(MyPlanetElevation.Surface,out ctrllerAltSurf);if(radar.exists&&radar.valid&&radar.active){
radarAlt=radar.GetDistance();if(ctrllerAltSurfValid){if(radar.alt_age<=1)radarOffset=radarAlt-ctrllerAltSurf;altFilter.AddValue(
ctrllerAltSurf+radarOffset);}else{altFilter.AddValue(radarAlt);}gndAltitude=altFilter.Get();altSrc=AltSource.Radar;}else if(
ctrllerAltSurfValid){gndAltitude=ctrllerAltSurf;altSrc=AltSource.Ground;}else{gndAltitude=GroundRadar.UNDEFINED_ALTITUDE;altSrc=AltSource.
Undefined;}gndAltitude-=conf.altitudeOffset;double ctrllerAltSl;bool ctrllerAltSlValid=ship.shipCtrller.TryGetPlanetElevation(
MyPlanetElevation.Sealevel,out ctrllerAltSl);gndSlOffset=(ctrllerAltSlValid&&ctrllerAltSurfValid)?ctrllerAltSl-ctrllerAltSurf:conf.
defaultASLmeters;slAltitude=gndAltitude+gndSlOffset;}void UpdateLWRTarget(){double lwrTargetHere=ComputeLWRTarget(gravNow,Mode,aLwrNow,
iLwrNow,hLwrNow);double lwrTargetGnd=ComputeLWRTarget(gndGravExp,Mode,aLwrGnd,iLwdGnd,hLwrGnd);lwrTargetSelected=Helpers.Mix(
lwrTargetGnd,lwrTargetHere,conf.LWR_mix_gnd_ratio);}void UpdatePlanetAtmo(){double parachute_density=(ship.parachutes.Count>0&&ship.
parachutes[0].Atmosphere>0.01)?(double)ship.parachutes[0].Atmosphere:-1;double athrusters_density=(ship.lifters.aThrustMax>0&&ship
.lifters.aThrustEff>1)?ship.lifters.aThrustEff/ship.lifters.aThrustMax*0.7+0.3:-1;double ithrusters_density=(ship.lifters
.iThrustMax>0&&ship.lifters.iThrustEff>1)?(1-ship.lifters.iThrustEff/ship.lifters.iThrustMax)/0.8:-1;obsDensity=Helpers.
Max3(parachute_density,athrusters_density,ithrusters_density);bool found;if(Mode==0&&gndAltitude>10000)planet=catalog.
get_planet("unknown",out found);if(planet.Precise==false){if(planet.Shortname=="unknown")planet.AtmoDensitySL=ship.lifters.
WorstDensity();if(planet.Shortname=="atmo")planet.AtmoDensitySL=Math.Max(planet.AtmoDensitySL,ship.lifters.WorstDensity());if(
obsDensity>-1){if(obsDensity>0.8&&gndAltitude<10000){planet=catalog.get_planet("dynatmo",out found);planet.AtmoDensitySL=Math.Max(
obsDensity,ship.lifters.WorstDensity());}if(obsDensity<0.2&&gndAltitude<1000){planet=catalog.get_planet("dynvacuum",out found);}}}
}void ComputeSurfaceGravityEstimate(){if(planet.Precise){gravSrc=GravSource.Identified;gndGravExp=Helpers.g_to_ms2(planet
.GSeaLevel);}else{double gravityBest=GravExponent==2?estimator2.gravityBest:estimator7.gravityBest;double confidenceBest=
GravExponent==2?estimator2.confidenceBest:estimator7.confidenceBest;double weighted_ground_estimate=Helpers.Interpolate(0,1,gravNow,
gravityBest,confidenceBest);gravSrc=(confidenceBest>0.9)?GravSource.Estimate:GravSource.Undefined;gndGravExp=Math.Max(gravNow,
Helpers.Interpolate(conf.gravTransitionLow,conf.gravTransitionHigh,gravNow,weighted_ground_estimate,gndAltitude));planet.
GSeaLevel=Helpers.ms2_to_g(gndGravExp);if(gndAltitude<conf.gravTransitionLow)gravSrc=GravSource.Local;}}public void EnableLeveler
(){Level=true;leveler.Enable();}public void DisableLeveler(){Level=false;leveler.Disable();}public void SwitchLeveler(){
Level=!Level;if(Level){leveler.Enable();}else{leveler.Disable();}}public void EnableThrust(){UseHorizThr=true;}public void
DisableThrust(){UseHorizThr=false;horizThrusters.Disable();}public void SwitchThrust(){if(UseHorizThr)DisableThrust();else
EnableThrust();}public void EnableAngle(){UseAngle=true;}public void DisableAngle(){UseAngle=false;}public void SwitchAngle(){if(
UseAngle)DisableAngle();else EnableAngle();}public void Mode4IncreaseSpeed(){Autopilot.mode4DesiredSpeed+=5;}public void
Mode4DecreaseSpeed(){Autopilot.mode4DesiredSpeed=Math.Max(Autopilot.mode4DesiredSpeed-5,0);}public void Mode4IncreaseAltitude(){Autopilot.
mode4DesiredAltitude+=10;}public void Mode4DecreaseAltitude(){Autopilot.mode4DesiredAltitude=Math.Max(Autopilot.mode4DesiredAltitude-10,0);}
public void Mode4AltSwitch(){if(Autopilot.altitudeMode==AutoPilot.AltitudeMode.Ground){Mode4AltSL();}else{Mode4AltGND();}}
public void Mode4AltGND(){Autopilot.altitudeMode=AutoPilot.AltitudeMode.Ground;Autopilot.mode4DesiredAltitude=gndAltitude;
Autopilot.altitudeFilter.Set(gndAltitude);}public void Mode4AltSL(){Autopilot.altitudeMode=AutoPilot.AltitudeMode.SeaLevel;
Autopilot.mode4DesiredAltitude=slAltitude;Autopilot.altitudeFilter.Set(slAltitude);}void SetUpLCDs(){foreach(IMyTextSurface d in
ship.MainDisplays){d.ContentType=ContentType.SCRIPT;d.Script="None";d.ScriptBackgroundColor=VRageMath.Color.Black;}foreach(
IMyTextSurface d in ship.DebugDisplays){d.ContentType=ContentType.TEXT_AND_IMAGE;d.Font="Monospace";d.FontColor=VRageMath.Color.White;
d.FontSize=0.45f;}}public void UpdateDebugDisplays(){foreach(IMyTextSurface d in ship.DebugDisplays){var sb=new
StringBuilder();sb.AppendLine("-- SLM debug --");sb.AppendLine(runTime.RunTimeString());sb.AppendLine(
$"Density: {obsDensity:0.00} (cat){planet.AtmoDensitySL:0.00}");sb.AppendLine(shipinfo.DebugString());sb.AppendLine($"LWR tgt: {lwrTargetSelected:0.00}");sb.AppendLine(
$"cmd: {lwrCommand:0.00} {thrCommand:0.00} N");sb.AppendLine($"Alt: {slAltitude:0.0}, SL offset {gndSlOffset:000}m");sb.AppendLine(leveler.DebugString());sb.
AppendLine(estimator2.DebugString());sb.AppendLine(estimator7.DebugString());sb.AppendLine(
$"Grav exponent: {GravExponent:0.00} score:{GravExponentScore:0.0}");sb.AppendLine(radar.AltitudeDebugString());sb.AppendLine(radar.TerrainDebugString());sb.AppendLine("[VERT PID] "+
vertPID.DebugString());sb.AppendLine(ship.lifters.DebugString());sb.AppendLine(horizThrusters.DebugString());sb.AppendLine(
Autopilot.DebugString());sb.AppendLine(profile.DebugString());d.WriteText(sb.ToString());}}public void UpdateDisplays(){const
float PLMARGIN=5,PTMARGIN=5,PBMARGIN=35;const float HMAX=20;VRageMath.Color GRAY=VRageMath.Color.Gray,WHITE=VRageMath.Color.
White,RED=VRageMath.Color.Red,YELLOW=VRageMath.Color.Yellow,CYAN=VRageMath.Color.Cyan,GREEN=VRageMath.Color.Green,BLUE=
VRageMath.Color.Blue;blink=!blink;foreach(IMyTextSurface d in ship.MainDisplays){VRageMath.RectangleF view;float speed,
speed_scale,alt_scale,xpos,ypos;view=new VRageMath.RectangleF((d.TextureSize-d.SurfaceSize)/2f,d.SurfaceSize);float width=d.
SurfaceSize[0];float height=d.SurfaceSize[1];float LEFT_MARGIN,PLEFT,PRIGHT,HLEFT,HTOP,VLEFT,VTOP,TTOP,PTOP,PBOTTOM,HVER,ALEFT,ATOP
,SLEFT,STOP,Tsize,HSIZE,THR_CUR_W,THR_MAX_W,THR_SW_W;bool showDetails=false;bool hcompact=false;Tsize=1f;if(width>400){
LEFT_MARGIN=40;PLEFT=150;PRIGHT=30;HLEFT=160;VLEFT=40;THR_CUR_W=20;THR_MAX_W=5;THR_SW_W=5;}else{LEFT_MARGIN=5;PLEFT=115;PRIGHT=5;
HLEFT=115;VLEFT=5;Tsize=0.75f;THR_CUR_W=20;THR_MAX_W=5;THR_SW_W=5;}if(height>300){TTOP=10;PTOP=70;PBOTTOM=height-112;HSIZE=40
;HVER=PBOTTOM+HSIZE+10;VTOP=HTOP=height-100;showDetails=true;ALEFT=PLEFT+5;ATOP=(PTOP+PBOTTOM)/2-10;STOP=PBOTTOM-PBMARGIN
-20;SLEFT=(PLEFT+width-PRIGHT)/2-20;}else{TTOP=5;PTOP=55;PBOTTOM=height-70;HSIZE=28;HVER=PBOTTOM+HSIZE+4;VTOP=HTOP=height
-70;ALEFT=PLEFT+5;ATOP=(PTOP+PBOTTOM)/2-30;STOP=(PTOP+PBOTTOM)/2-30;SLEFT=(PLEFT+width-PRIGHT)/2-5;Tsize=0.75f;}if(width<
300){hcompact=true;Tsize=0.7f;PLEFT=80;HLEFT=85;THR_CUR_W=15;THR_MAX_W=3;THR_SW_W=3;Tsize=0.65f;ALEFT=PLEFT+5;SLEFT=ALEFT+
45;}var frame=d.DrawFrame();frame.Add(TextSprite("Soft Landing Manager\n"+planet.Name+" (g="+GravExponent.ToString()+")",
width/2,TTOP,view,WHITE,TextAlignment.CENTER,Tsize));float THR_SCALE=(PBOTTOM-PTOP)/40;Action<List<double>,VRageMath.Color,
VRageMath.Color,VRageMath.Color,VRageMath.Color>drawThrustBars=(list,colorA,colorB,colorC,colorD)=>{float Aref=(float)(list[0]/
shipinfo.mass)*THR_SCALE;MySprite sA=MySprite.CreateSprite("SquareSimple",new Vector2((float)list[4],PBOTTOM-Aref/2)+view.
Position,new Vector2((float)list[5],Aref));sA.Color=colorA;frame.Add(sA);float Bref=(float)(list[1]/shipinfo.mass)*THR_SCALE;
MySprite sB=MySprite.CreateSprite("SquareSimple",new Vector2((float)list[4],PBOTTOM-Aref-Bref/2)+view.Position,new Vector2((
float)list[5],Bref));sB.Color=colorB;frame.Add(sB);float Cref=(float)(list[2]/shipinfo.mass)*THR_SCALE;MySprite sC=MySprite.
CreateSprite("SquareSimple",new Vector2((float)list[4],PBOTTOM-Aref-Bref-Cref/2)+view.Position,new Vector2((float)list[5],Cref));sC.
Color=colorC;frame.Add(sC);float Dref=(float)(list[3]/shipinfo.mass)*THR_SCALE;MySprite sD=MySprite.CreateSprite(
"SquareSimple",new Vector2((float)list[4],PBOTTOM-Aref-Bref-Cref-Dref/2)+view.Position,new Vector2((float)list[5],Dref));sD.Color=
colorD;frame.Add(sD);};List<List<double>>data=new List<List<double>>();data.Add(new List<double>{ship.lifters.aThrustNow,ship.
lifters.pThrustNow,ship.lifters.iThrustNow,ship.lifters.hThrustNow,LEFT_MARGIN+THR_CUR_W/2+5,THR_CUR_W});data.Add(new List<
double>{ship.lifters.aThrustEff,ship.lifters.pThrustEff,ship.lifters.iThrustEff,ship.lifters.hThrustEff,LEFT_MARGIN+THR_CUR_W+
THR_MAX_W/2+10,THR_MAX_W});foreach(List<double>list in data)drawThrustBars(list,GREEN,VRageMath.Color.DarkBlue,BLUE,RED);double
sweep_left=LEFT_MARGIN+THR_CUR_W+THR_MAX_W+15;List<List<double>>data2=new List<List<double>>();for(int i=0;i<11;i++){data2.Add(new
List<double>{ship.lifters.hThrustEff,ship.lifters.pThrustDensity[i],ship.lifters.iThrustDensity[i],ship.lifters.
aThrustDensity[i],sweep_left+THR_SW_W/2+i*THR_SW_W,THR_SW_W});}foreach(List<double>list in data2)drawThrustBars(list,RED,VRageMath.
Color.DarkBlue,BLUE,GREEN);if(gravSrc!=GravSource.Undefined){float grav_x=(float)(sweep_left+THR_SW_W/2+Math.Min(planet.
AtmoDensitySL,1)*10*THR_SW_W);frame.Add(MySprite.CreateSprite("SquareSimple",new Vector2(grav_x,PBOTTOM-(float)gndGravExp*THR_SCALE)+
view.Position,new Vector2(10,10)));frame.Add(TextSprite(Helpers.ms2_to_g(gndGravExp).ToString("0.00")+"g",(float)sweep_left,
PBOTTOM-(float)gndGravExp*THR_SCALE-50,view,warnState==WarnType.Bad?RED:WHITE,TextAlignment.LEFT,Tsize));}for(int i=0;i<5;i++){
frame.Add(MySprite.CreateSprite("SquareSimple",new Vector2(LEFT_MARGIN,PBOTTOM-(float)Helpers.g_to_ms2(i)*THR_SCALE)+view.
Position,new Vector2(5,5)));}frame.Add(MySprite.CreateSprite("SquareSimple",new Vector2(50+LEFT_MARGIN,PBOTTOM)+view.Position,
new Vector2(100,2)));float HHOR=width-PRIGHT-HSIZE;float HSCALE=HSIZE/HMAX;if(gndAltitude<1600){speed_scale=300/(width-
PLEFT-PRIGHT);alt_scale=2000/(PBOTTOM-PTOP);}else if(gndAltitude<6400){speed_scale=400/(width-PLEFT-PRIGHT);alt_scale=8000/(
PBOTTOM-PTOP);}else if(gndAltitude<25600){speed_scale=550/(width-PLEFT-PRIGHT);alt_scale=32000/(PBOTTOM-PTOP);}else{speed_scale
=550/(width-PLEFT-PRIGHT);alt_scale=200000/(PBOTTOM-PTOP);}if(showDetails){if(Mode==1||Mode==2){if(profile.IsValid()&&
profile.IsComputed()){for(int i=0;i<profile.alt_sl.Count()-1;i++){speed=(float)Math.Min(profile.vertSpeed[i],conf.
vSpeedSafeLimit);ypos=PBOTTOM-70-((float)(profile.alt_sl[i]-gndSlOffset)/alt_scale);xpos=width-PRIGHT-20-speed/speed_scale;if(ypos>=
PTOP&&xpos>=PLEFT){frame.Add(new MySprite(){Type=SpriteType.TEXT,Data="+",Position=new Vector2(xpos,ypos)+view.Position,
RotationOrScale=1.5f,Color=new VRageMath.Color((float)profile.hRatio[i],(float)profile.aRatio[i],(float)profile.iRatio[i]),Alignment=
TextAlignment.CENTER,FontId="White"});}}double h2_capa=shipinfo.H2_capa_liters();if(h2_capa>0&&ship.lifters.hThrustMax>0){double
h2_to_use=profile.InterpolateH2Used(slAltitude)/h2_capa*100;double h2_stored=shipinfo.H2_stored_liters()/h2_capa*100;double
h2_margin=h2_stored-h2_to_use;frame.Add(TextSprite("H2 Margin : "+h2_margin.ToString("00")+"%",width-PRIGHT-200,PTOP+PTMARGIN,
view,(h2_margin>conf.h2MarginWarning)?WHITE:RED,TextAlignment.LEFT));}if(panic){frame.Add(new MySprite(){Type=SpriteType.
TEXT,Data="PANIC",Position=new Vector2(width/2,170)+view.Position,RotationOrScale=2f,Color=RED,FontId="White"});}frame.Add(
TextSprite(((width-PRIGHT-PLEFT)*speed_scale).ToString("000")+"m/s",PLEFT+PLMARGIN,PBOTTOM-PBMARGIN,view,GRAY,TextAlignment.LEFT))
;frame.Add(TextSprite(((PBOTTOM-PTOP)*alt_scale).ToString("000")+"m",PLEFT+PLMARGIN,PTOP+PTMARGIN,view,GRAY,TextAlignment
.LEFT));}else if(profile.IsComputed()){frame.Add(TextSprite("Unable to compute\nvalid landing profile",(PLEFT+width-
PRIGHT)/2,PTOP,view,VRageMath.Color.Orange,TextAlignment.CENTER));}else{frame.Add(TextSprite("No profile computed",(PLEFT+
width-PRIGHT)/2,PTOP+25,view,WHITE,TextAlignment.CENTER));}}else if(Mode==3){frame.Add(TextSprite("Fly with keyboard\n keys",
(PLEFT+width-PRIGHT)/2,PTOP+25,view,WHITE,TextAlignment.CENTER));}else if(Mode==4){frame.Add(TextSprite(
"Use PB cmd to\nchange speed\n & altitude",(PLEFT+width-PRIGHT)/2,PTOP+25,view,WHITE,TextAlignment.CENTER));}else if(Mode==5){frame.Add(TextSprite(
"Point at asteroid\nor ship to\n rendez-vous.",(PLEFT+width-PRIGHT)/2,PTOP+25,view,WHITE,TextAlignment.CENTER));}else{List<string>strinfo=new List<string>{Helpers.
Truncate(ship.shipCtrller.CubeGrid.DisplayName,20),Math.Round(shipinfo.mass)+"kg"};for(int i=0;i<strinfo.Count;i++){frame.Add(
TextSprite(strinfo[i],PLEFT+5,PTOP+5+i*20,view,GRAY,TextAlignment.LEFT));}}}ypos=PBOTTOM-70-(float)gndAltitude/alt_scale;xpos=
width-PRIGHT-20+(float)vertSpeed/speed_scale;if(speedSPSrc==SPSource.Profile&&ypos>=PTOP&&xpos>=PLEFT){frame.Add(new MySprite
(){Type=SpriteType.TEXT,Data="O",Position=new Vector2(xpos,ypos)+view.Position,RotationOrScale=2f,Color=YELLOW,Alignment=
TextAlignment.CENTER,FontId="White"});}if((Mode==1)||(Mode==2)||(Mode==5))frame.Add(TextSprite(Helpers.FormatCompact(-vertSpeed)+
"m/s",SLEFT,STOP,view,YELLOW,TextAlignment.LEFT,Tsize));if((Mode==1)||(Mode==2)||(Mode==5))frame.Add(TextSprite(Helpers.
FormatCompact(-vertSpeedSP)+"m/s",SLEFT,STOP+20,view,CYAN,TextAlignment.LEFT,Tsize));string alt_txt="";if(Mode==4&&Autopilot.
altitudeMode==AutoPilot.AltitudeMode.SeaLevel){alt_txt=slAltitude.ToString("000")+"m (SL)";}else{alt_txt=gndAltitude<GroundRadar.
UNDEFINED_ALTITUDE?gndAltitude.ToString("000")+"m":(radar.exists?(radar.active?"INIT":"XXX"):"XXX");}frame.Add(TextSprite(alt_txt,ALEFT,
ATOP,view,YELLOW,TextAlignment.LEFT,Tsize));if(Mode==4){string alt_sp_text=Autopilot.mode4DesiredAltitude.ToString("000")+
"m";if(Autopilot.altitudeMode==AutoPilot.AltitudeMode.SeaLevel)alt_sp_text+=" (SL)";frame.Add(TextSprite(alt_sp_text,ALEFT,
ATOP+20,view,CYAN,TextAlignment.LEFT,Tsize));}VRageMath.Color bColor;if(warnState==WarnType.Bad||speedSPSrc==SPSource.Unable
){bColor=RED;}else if(marginal>=conf.marginalWarn){bColor=VRageMath.Color.OrangeRed;}else{bColor=WHITE;}Helpers.Rectangle
(frame,PLEFT,width-PRIGHT,PTOP,PBOTTOM,view,2,bColor);string info;switch(speedSPSrc){case SPSource.None:info="Disabled";
break;case SPSource.Profile:info="Profile";break;case SPSource.AltGravFormula:info="Alt/grav";break;case SPSource.GravFormula
:info="Gravity";break;case SPSource.FinalSpeed:info="Final";break;case SPSource.Unable:info="Unable";break;case SPSource.
Hold:info="Alt Hold";break;case SPSource.RDV:info="Rendezvous";break;default:info="Unknown";break;}frame.Add(TextSprite(
"VERTICAL\nMode "+Mode+"\n"+info,VLEFT,VTOP,view,WHITE,TextAlignment.LEFT,Tsize));Helpers.Rectangle(frame,HHOR-HSIZE,HHOR+HSIZE,HVER+
HSIZE,HVER-HSIZE,view,2,WHITE);if((Mode==1)||(Mode==2)||(Mode==3)||Mode==4){var ssp=MySprite.CreateSprite("SquareSimple",new
Vector2(HHOR-(float)Helpers.SatMinMax(leftSpeedSP,-HMAX,HMAX)*HSCALE,HVER-(float)Helpers.SatMinMax(fwdSpeedSP,-HMAX,HMAX)*
HSCALE)+view.Position,new Vector2(12,12));ssp.Color=CYAN;frame.Add(ssp);if((Mode==3)||((Mode==4)&&!((Math.Abs(fwdSpeedSP)<Math
.Abs(Autopilot.mode4DesiredSpeed))&&blink))){frame.Add(TextSprite(Mode==3?fwdSpeedSP.ToString("00"):Autopilot.
mode4DesiredSpeed.ToString("00"),HHOR,HVER+5,view,CYAN,TextAlignment.CENTER,Tsize));}}var snow=MySprite.CreateSprite("SquareSimple",new
Vector2(HHOR-(float)Helpers.SatMinMax(leftSpeed,-HMAX,HMAX)*HSCALE,HVER-(float)Helpers.SatMinMax(fwdSpeed,-HMAX,HMAX)*HSCALE)+
view.Position,new Vector2(12,12));snow.Color=YELLOW;frame.Add(snow);if(radar.obstruction)frame.Add(MySprite.CreateSprite(
"Danger",new Vector2(HHOR,HVER)+view.Position,new Vector2(80,80)));string str1="";if(UseAngle==true&&UseHorizThr==true){str1=
hcompact?"G+T":"Gyro("+leveler.MaxAngle().ToString("00")+"°)+thrust";}else if(UseAngle==true&&UseHorizThr==false){str1=hcompact?
"G":"Gyro("+leveler.MaxAngle().ToString("00")+"°) only";}else if(UseAngle==false&&UseHorizThr==true){str1=hcompact?"T":
"Thrusters only";}else{str1=hcompact?"Off":"Disabled";}string str2="";if(Mode==1||Mode==2){switch(radar.mode){case ScanMode.
DoubleStandby:str2=hcompact?"SBY(D)":"Standby (D)";break;case ScanMode.SingleStandby:str2=hcompact?"SBY(S)":"Standby (S)";break;case
ScanMode.SingleNarrow:str2=hcompact?"Simple":"Simple avoidance";break;case ScanMode.DoubleEarly:str2=hcompact?"Early":
"Early avoidance";break;case ScanMode.DoubleWide:str2=hcompact?"Wide":"Wide avoidance";break;default:str2="No avoidance";break;}}else if(
Mode==3){str2=hcompact?"Hover":"Hover Mode";}else if(Mode==4){str2=hcompact?"Speed":"Speed Hold";}frame.Add(TextSprite(
"HORIZ\n"+str1+"\n"+str2,HLEFT,HTOP,view,WHITE,TextAlignment.LEFT,Tsize));frame.Dispose();}}void ApplyThrustOverrideInGravity(
double PIDoutput){lwrCommand=PIDoutput+Helpers.InterpolateSmooth(-5,5,0,2,vertSpeedDelta);thrCommand=lwrCommand*shipWeight;if(
(thrCommand>ship.lifters.totalThrustEff||vertSpeedDelta>5)&&marginal<conf.marginalMax){marginal++;}else if(marginal>0){
marginal--;}double min_athrust=(Mode==1)?Helpers.Interpolate(-conf.mode1AtmoSpeed,-conf.mode1AtmoSpeed+5,shipWeight,0,vertSpeed)
:0;double min_ithrust=(Mode==1&&gndAltitude>conf.mode1IonAltLimit&&vertSpeedDelta<0)?Helpers.Interpolate(-conf.
mode1IonSpeed,-conf.mode1IonSpeed+5,shipWeight,0,vertSpeed):0;ship.lifters.ApplyThrust(thrCommand,min_athrust,min_ithrust);}void
ApplyThrustOverrideInSpace(double PIDoutput){lwrCommand=PIDoutput+Helpers.InterpolateSmooth(-5,5,-1,1,vertSpeedDelta);double accel=7.9;if(
lwrCommand>=0){thrCommand=lwrCommand*shipinfo.mass*accel;ship.lifters.ApplyThrust(thrCommand,0,0);ship.downThr.Disable();}else{
thrCommand=Math.Max(lwrCommand,-1)*shipinfo.mass*accel;ship.downThr.ApplyThrust(-thrCommand,0,0);ship.lifters.Disable();}}void
ManagePanicParachutes(){if((Mode==1||Mode==2)&&(vertSpeedDelta>gndAltitude/conf.panicRatio+conf.panicDelta)){panic=true;foreach(IMyParachute
parachute in ship.parachutes){parachute.OpenDoor();}}else{panic=false;}}void ManageSoundBlocks(){foreach(IMySoundBlock sound in
ship.soundblocks){if(panic){sound.Enabled=true;sound.SelectedSound="SoundBlockAlert2";sound.Play();}else if(warnState==
WarnType.Bad||speedSPSrc==SPSource.Unable){sound.Enabled=true;sound.SelectedSound="SoundBlockAlert1";sound.Play();}}}void
ManageTimers(){if(gndAltitude<conf.landingTimerAltitude&&allowLandingTimer){foreach(IMyTimerBlock timer in ship.landing_timers)timer
.Trigger();allowLandingTimer=false;allowLiftoffTimer=true;}if(gndAltitude>Math.Max(conf.liftoffTimerAltitude,conf.
landingTimerAltitude+1)&&allowLiftoffTimer&&Mode!=5){foreach(IMyTimerBlock timer in ship.liftoff_timers)timer.Trigger();allowLiftoffTimer=
false;allowLandingTimer=true;}}void InitLandingLiftoffTimers(){if(gndAltitude<conf.landingTimerAltitude){allowLandingTimer=
false;allowLiftoffTimer=true;}else{allowLandingTimer=true;allowLiftoffTimer=false;}}void TriggerOnTimers(){foreach(
IMyTimerBlock timer in ship.on_timers)timer.Trigger();}void TriggerOffTimers(){foreach(IMyTimerBlock timer in ship.off_timers)timer.
Trigger();}void SelectBestExponent(){const int MAX=5;const double MARGIN=1.02;if(estimator2.confidence>estimator7.confidence*
MARGIN&&GravExponentScore>-MAX)GravExponentScore--;else if(estimator7.confidence>estimator2.confidence*MARGIN&&
GravExponentScore<MAX)GravExponentScore++;if(conf.autoSwitchExponent){if(GravExponentScore==-MAX)GravExponent=2;else if(GravExponentScore
==MAX)GravExponent=7;}}void GearUnLock(){foreach(IMyLandingGear gear in ship.gears){gear.Unlock();}}double
ComputeLWRTarget(double gravity,int mode,double aLWR,double iLWR,double hLWR){double computedLWRtarget;if(gravity>0){if(mode==1){
computedLWRtarget=Math.Min(conf.elecLwrSufficient,aLWR+iLWR+hLWR);}else{computedLWRtarget=aLWR+iLWR+hLWR;}return Math.Min((
computedLWRtarget/conf.LWRsafetyfactor)-conf.LWRoffset,conf.LWRlimit);}else{return 0;}}double LWR(double gravity,double shipmass,double
thrust){return(gravity>0)?thrust/(gravity*shipmass):0;}bool CheckGearLock(){foreach(IMyLandingGear gear in ship.gears){if(!
gear.Closed&&gear.IsWorking&&gear.IsLocked)return true;}return false;}bool DisableConditions(){return(gravNow==0&&Mode!=5)||
gndAltitude<2||CheckGearLock();}public List<string>LogNames(){return new List<string>{"mode","grav_now","vspeed","vspeed_sp",
"speed_sp_source","gnd_altitude","gnd_sl_offset","alt_source","vpid_p","vpid_i","vpid_d","PIDoutput","twr_wanted"};}public List<double>
LogValues(){return new List<double>{Mode,gravNow,vertSpeed,vertSpeedSP,(float)speedSPSrc,gndAltitude,gndSlOffset,(float)altSrc,
vertPID.ap,vertPID.ai,vertPID.ad,vertPID.output,lwrCommand};}public List<string>AllLogNames(){List<string>names=new List<string
>();names.AddRange(this.LogNames());names.AddRange(radar.LogNames());names.AddRange(horizThrusters.LogNames());names.
AddRange(Autopilot.LogNames());return names;}public List<double>AllLogValues(){List<double>values=new List<double>();values.
AddRange(this.LogValues());values.AddRange(radar.LogValues());values.AddRange(horizThrusters.LogValues());values.AddRange(
Autopilot.LogValues());return values;}MySprite TextSprite(string text,float x,float y,VRageMath.RectangleF view,VRageMath.Color
color,TextAlignment align,float size=1f){return new MySprite(){Type=SpriteType.TEXT,Data=text,Position=new Vector2(x,y)+view.
Position,RotationOrScale=size,Color=color,FontId="White",Alignment=align};}}public class EarlySurfaceGravityEstimator{public
double radius=0,radiusBest=0;public double gravity=0,gravityBest=0;public double confidence,confidenceBest;double
gravityExponent;double gravPrev=0;double altSlPrev=0;public EarlySurfaceGravityEstimator(double exp){gravityExponent=exp;}public void
UpdateEstimates(double grav,double altSl,double hillparam){double K;if(gravPrev==0||altSlPrev==0){gravPrev=grav;altSlPrev=altSl;}else
if(grav!=gravPrev&&altSl!=altSlPrev&&grav>0){double radiusNew;K=Math.Pow(gravPrev/grav,1/gravityExponent);if(K!=1){
radiusNew=(K*altSlPrev-altSl)/(1-K);}else{radiusNew=-2;}confidence=Math.Pow(Math.Min(radiusNew,radius)/Math.Max(radiusNew,radius)
,4);if(radiusNew<0)confidence=0;if(radiusNew>1e7)confidence=0;if(radiusNew>2e5)confidence=confidence*0.95;radius=
radiusNew;}else{radius=-3;confidence=0;}if(altSl+radius>0&&radius>0){gravity=grav*Math.Pow((altSl+radius)/(radius*(1+hillparam)),
gravityExponent);}else{gravity=-1;confidence=0;}confidence=Helpers.SatMinMax(confidence,0,1);if(confidence>confidenceBest||confidence>
0.95){confidenceBest=confidence;radiusBest=radius;gravityBest=gravity;}gravPrev=grav;altSlPrev=altSl;}public void Reset(){
gravPrev=0;altSlPrev=0;confidenceBest=0;}public string DebugString(){string str="[SURFACE GRAVITY ESTIMATOR for EXP="+
gravityExponent.ToString()+"]";str+="\nCurrent : R=:"+radius.ToString("000000")+"m, g="+Helpers.ms2_to_g(gravity).ToString("0.00")+
"g, c="+confidence.ToString("0.00");str+="\nBest    : R=:"+radiusBest.ToString("000000")+"m, g="+Helpers.ms2_to_g(gravityBest).
ToString("0.00")+"g, c="+confidenceBest.ToString("0.00");return str;}}public class LiftoffProfileBuilder{public double[]
vertSpeed=new double[NB_PTS];public double[]alt_sl=new double[NB_PTS];public double[]aRatio=new double[NB_PTS];public double[]
iRatio=new double[NB_PTS];public double[]hRatio=new double[NB_PTS];public double[]h2Used=new double[NB_PTS];public double
GravityExponent;const double DT_START=0.5;const int NB_PTS=256;const double H2_FLOW_RATIO=0.000816;double dt=DT_START;bool valid=false;
bool computed=false;public LiftoffProfileBuilder(double gravityExponent){GravityExponent=gravityExponent;}double
ComputeAtmoDensity(double altSL,Planet planet,double radius){double atmoAltLimit=radius*planet.AtmoLimitAltitude*planet.HillParam;if(altSL
>atmoAltLimit){return 0;}else if(altSL>=0){return planet.AtmoDensitySL*(1-altSL/atmoAltLimit);}else{return planet.
AtmoDensitySL;}}double ComputeGravity(double altSL,Planet planet,double radius){double planetMaxRadius=radius*(1+planet.HillParam);if
(altSL>=(planetMaxRadius-radius)){double raw=Helpers.g_to_ms2(planet.GSeaLevel*Math.Pow(planetMaxRadius/(altSL+radius),
GravityExponent));if(raw>Helpers.g_to_ms2(0.05)){return raw;}else{return 0;}}else{return Helpers.g_to_ms2(planet.GSeaLevel);}}public
void Compute(double altSlStart,ShipInfo shipInfo,Planet planet,double radius,double maxAccel,double maxTwr,double
sufficientTwr,double safetyfactor,double maxSpeed,double initialSpeed,ThrGroup lifters){double t=0;bool temp_valid=true;double
safetyInverse=1/safetyfactor;vertSpeed[0]=initialSpeed;alt_sl[0]=altSlStart;aRatio[0]=1;iRatio[0]=1;hRatio[0]=1;h2Used[0]=0;dt=
DT_START;for(int i=1;i<NB_PTS;i++){t=t+dt;double gravity=ComputeGravity(alt_sl[i-1],planet,radius);double density=
ComputeAtmoDensity(alt_sl[i-1],planet,radius);double thrMaxAccel=shipInfo.mass*Math.Min(maxAccel,gravity+2*t);double thrMaxTwr=shipInfo.
mass*gravity*maxTwr;double thrSufficientTwr=shipInfo.mass*gravity*sufficientTwr;double athrust=Math.Max(lifters.aThrustEff,
lifters.AtmoThrustForDensity(density))*safetyInverse;athrust=Helpers.Min3(athrust,thrMaxAccel,thrMaxTwr);double ithrust=(
lifters.IonThrustForDensity(density)+lifters.PrototechThrustForDensity(density))*safetyInverse;if(vertSpeed[i-1]>=maxSpeed){
ithrust=Math.Max(0,Math.Min(ithrust,shipInfo.mass*gravity-athrust));}else{ithrust=Math.Max(0,Helpers.Min3(ithrust,thrMaxAccel-
athrust,thrMaxTwr-athrust));}double hthrust=0;if(athrust+ithrust<thrSufficientTwr){if(vertSpeed[i-1]>=maxSpeed){hthrust=Math.
Max(0,shipInfo.mass*gravity-athrust-ithrust);}else{hthrust=Math.Max(0,Helpers.Min3(lifters.hThrustMax,thrMaxAccel-athrust-
ithrust,thrMaxTwr-athrust-ithrust))*safetyInverse;}}h2Used[i]=h2Used[i-1]+hthrust*H2_FLOW_RATIO*dt;double totalThrust=hthrust+
athrust+ithrust;if(totalThrust>0){aRatio[i]=athrust/totalThrust;iRatio[i]=ithrust/totalThrust;hRatio[i]=hthrust/totalThrust;}
else{aRatio[i]=0;iRatio[i]=0;hRatio[i]=0;}double accel=totalThrust/shipInfo.mass-gravity;vertSpeed[i]=Math.Min(accel*dt+
vertSpeed[i-1],maxSpeed);if(vertSpeed[i]<0){temp_valid=false;break;}alt_sl[i]=alt_sl[i-1]+vertSpeed[i-1]*dt+0.5*accel*dt*dt;dt+=
0.05;}computed=true;valid=temp_valid;}public double InterpolateSpeed(double alt){return Interpolate(alt,ref vertSpeed);}
public double InterpolateH2Used(double alt){return Interpolate(alt,ref h2Used);}double Interpolate(double alt,ref double[]y){
int left=0;int right=NB_PTS-1;int m=(left+right)/2;if(!valid)return 0;if(alt<=alt_sl[0])return y[0];if(alt>=alt_sl[NB_PTS-1
])return y[NB_PTS-1];while(left<=right){if(alt_sl[m]==alt){break;}else if(alt_sl[m]>alt){right=m-1;}else{left=m+1;}m=(
left+right)/2;}if(m+1>=NB_PTS)return y[NB_PTS-1];return Helpers.Interpolate(alt_sl[m],alt_sl[m+1],y[m],y[m+1],alt);}public
void Invalidate(){valid=false;computed=false;}public bool IsValid()=>valid;public bool IsComputed()=>computed;public double
GetFinalSpeed(){if(valid&computed){return vertSpeed[NB_PTS-1];}else{return 0;}}public double GetFinalAlt(){if(valid&computed){return
alt_sl[NB_PTS-1];}else{return 0;}}public string DebugString(){string str="[LITFOFF PROFILE]";str+="\nComputed:"+computed.
ToString()+" Valid:"+valid.ToString();str+="\nFinal:"+vertSpeed[NB_PTS-1].ToString("000.0")+"m/s , "+alt_sl[NB_PTS-1].ToString(
"000.0")+"m , "+(h2Used[NB_PTS-1]/1000).ToString("000.0")+"kL";str+="\nAlt(m) | speed (m/s) | a/i/h ratio";for(int i=0;i<10;i++
){str+="\n"+alt_sl[i].ToString("000.0")+"  | "+vertSpeed[i].ToString("000.0")+"  | "+aRatio[i].ToString("0.00")+" "+
iRatio[i].ToString("0.00")+" "+hRatio[i].ToString("0.00");}return str;}}public class ShipInfo{public double mass;public double
inertia;ShipBlocks ship;readonly SLMConfiguration config;public ShipInfo(ShipBlocks ship,SLMConfiguration config){this.ship=
ship;this.config=config;UpdateMass();UpdateInertia();}public void UpdateMass(){mass=ship.shipCtrller.CalculateShipMass().
TotalMass;}public void UpdateInertia(){Vector3I extend=ship.shipCtrller.CubeGrid.Max-ship.shipCtrller.CubeGrid.Min;Vector3D size=
extend*ship.shipCtrller.CubeGrid.GridSize;double inertia_x=mass*(size.Y*size.Y+size.Z*size.Z)/12;double inertia_y=mass*(size.X
*size.X+size.Z*size.Z)/12;double inertia_z=mass*(size.X*size.X+size.Y*size.Y)/12;inertia=Helpers.Max3(inertia_x,inertia_y
,inertia_z);}public double MaxAngle(){return ship.gyros.Count/inertia*(ship.shipCtrller.CubeGrid.GridSizeEnum==MyCubeSize
.Small?config.inertiaRatioSmall:config.inertiaRatioLarge);}public double H2_stored_liters(){double fill=0;foreach(
IMyGasTank tank in ship.h2_tanks){fill+=tank.FilledRatio*tank.Capacity;}return fill;}public double H2_capa_liters(){double capa=0;
foreach(IMyGasTank tank in ship.h2_tanks){capa+=tank.Capacity;}return capa;}public string DebugString(){return"[SHIP INFO]\n"+
mass.ToString("000000")+"kg "+inertia.ToString("000000")+"kg.m² "+MaxAngle().ToString("00.00")+"° "+(H2_stored_liters()/1000
).ToString("0");}}public class PIDController{public double output=-1;public double ap,ai,ad;readonly double KP,KI,KD,
_AiMinFixed,_AiMaxFixed,AD_FILT,AD_MAX;double delta_prev,deriv_prev;public PIDController(double kp,double ki,double kd,double
aiMinFixed,double aiMaxFixed,double ad_filt,double ad_max){KP=kp;KI=ki;KD=kd;_AiMinFixed=aiMinFixed;_AiMaxFixed=aiMaxFixed;AD_FILT
=Helpers.SatMinMax(ad_filt,0,1);AD_MAX=ad_max;}public void UpdatePID(double delta){UpdatePIDController(delta,_AiMinFixed,
_AiMaxFixed);}public void UpdatePIDController(double delta,double aiMinDynamic,double aiMaxDynamic){delta=Helpers.NotNan(delta);ap=
delta*KP;ai=ai+delta*KI;ai=Helpers.SatMinMax(ai,_AiMinFixed,_AiMaxFixed);ai=Helpers.SatMinMax(ai,aiMinDynamic,aiMaxDynamic);
double deriv=AD_FILT*deriv_prev+(1-AD_FILT)*(delta-delta_prev);deriv_prev=deriv;delta_prev=delta;ad=deriv*KD;ad=Helpers.
SatMinMax(ad,-AD_MAX,AD_MAX);output=ap+ai+ad;}public void Reset(){deriv_prev=0;delta_prev=0;ap=0;ai=0;ad=0;}public string
DebugString(){return"P: "+Math.Round(ap,2).ToString("+0.00;-0.00")+" I:"+Math.Round(ai,2).ToString("+0.00;-0.00")+" D:"+Math.Round(
ad,2).ToString("+0.00;-0.00");}}public class GyroController{bool gyroOverride;double angle;readonly List<IMyGyro>gyros;
readonly double gyroRpmScale;Vector3D reference,target;public GyroController(List<IMyGyro>gyros,double gyroRpmScale){this.gyros=
gyros;this.gyroRpmScale=gyroRpmScale;}public void SetGyroOverride(bool state){gyroOverride=state;foreach(IMyGyro g in gyros){
if(!state){g.Pitch=0;g.Yaw=0;g.Roll=0;}g.GyroOverride=state;}}public void SetTargetOrientation(Vector3D setReference,
Vector3D setTarget){reference=setReference;target=setTarget;}public void Tick(){if(!gyroOverride)return;foreach(IMyGyro g in
gyros){Matrix localOrientation;g.Orientation.GetMatrix(out localOrientation);var localReference=Vector3D.Transform(reference,
MatrixD.Transpose(localOrientation));var localTarget=Vector3D.Transform(target,MatrixD.Transpose(g.WorldMatrix.GetOrientation()
));var axis=Vector3D.Cross(localReference,localTarget);angle=axis.Length();angle=Math.Atan2(angle,Math.Sqrt(Math.Max(0.0,
1.0-angle*angle)));if(Vector3D.Dot(localReference,localTarget)<0)angle=Math.PI-angle;axis.Normalize();axis*=Math.Max(0.002,
g.GetMaximum<float>("Roll")*MathHelper.RPMToRadiansPerSecond*(angle/Math.PI)*gyroRpmScale);g.Pitch=(float)-axis.X;g.Yaw=(
float)-axis.Y;g.Roll=(float)-axis.Z;}}}public class AutoLeveler{private readonly int delay;private readonly double
gyroResponsiveness;private readonly double maxAngle;bool Enabled=false;private readonly IMyShipController cockpit;private readonly
GyroController gyroController;int timer;double desiredPitch,desiredRoll;public double pitch,roll;public double SpeedFwd,SpeedLeft,
DesiredSpeedFwd,DesiredSpeedLeft;public AutoLeveler(IMyShipController cockpit,List<IMyGyro>gyros,double maxAngle,int delay,double
gyroResponsiveness,double gyroRpmScale){this.cockpit=cockpit;this.gyroController=new GyroController(gyros,gyroRpmScale);this.maxAngle=
maxAngle;this.delay=delay;this.gyroResponsiveness=gyroResponsiveness;}public void Enable(){Enabled=true;gyroController.
SetGyroOverride(true);}public void Disable(){Enabled=false;gyroController.SetGyroOverride(false);SpeedFwd=0;SpeedLeft=0;DesiredSpeedFwd
=0;DesiredSpeedLeft=0;}public void Tick(double speedFwd=0,double speedLeft=0,double desiredSpeedFwd=0,double
desiredSpeedLeft=0){SpeedFwd=speedFwd;SpeedLeft=speedLeft;DesiredSpeedFwd=desiredSpeedFwd;DesiredSpeedLeft=desiredSpeedLeft;if(Enabled){
Vector3D gravity=-Vector3D.Normalize(cockpit.GetNaturalGravity());pitch=Helpers.NotNan(Math.Acos(Vector3D.Dot(cockpit.
WorldMatrix.Forward,gravity))*Helpers.radToDeg);roll=Helpers.NotNan(Math.Acos(Vector3D.Dot(cockpit.WorldMatrix.Right,gravity))*
Helpers.radToDeg);if(cockpit.RotationIndicator.Length()>0.0f){desiredPitch=-(pitch-90);desiredRoll=(roll-90);gyroController.
SetGyroOverride(false);timer=0;}else if(timer>delay){gyroController.SetGyroOverride(true);desiredPitch=Math.Atan((SpeedFwd-
DesiredSpeedFwd)/gyroResponsiveness)/Helpers.halfPi*maxAngle;desiredRoll=Math.Atan((SpeedLeft-DesiredSpeedLeft)/gyroResponsiveness)/
Helpers.halfPi*maxAngle;Matrix cockpitOrientation;cockpit.Orientation.GetMatrix(out cockpitOrientation);var quatPitch=
Quaternion.CreateFromAxisAngle(cockpitOrientation.Left,(float)(desiredPitch*Helpers.degToRad));var quatRoll=Quaternion.
CreateFromAxisAngle(cockpitOrientation.Backward,(float)(desiredRoll*Helpers.degToRad));var reference=Vector3D.Transform(cockpitOrientation.
Down,quatPitch*quatRoll);gyroController.SetTargetOrientation(reference,cockpit.GetNaturalGravity());gyroController.Tick();}
else{timer++;}}}public void SimpleTick(Vector3D reference,Vector3D target){gyroController.SetTargetOrientation(reference,
target);gyroController.Tick();}public string DebugString(){string str="[AUTO LEVELER]";str+="\nFwd :"+SpeedFwd.ToString(
"000.0")+"("+DesiredSpeedFwd.ToString("000.0")+")";str+="\nLeft:"+SpeedLeft.ToString("000.0")+"("+DesiredSpeedLeft.ToString(
"000.0")+")";str+="\npitch:"+Math.Round(90-pitch,2)+" roll:"+Math.Round(roll-90,2)+"max:"+Math.Round(maxAngle,2);return str;}
public double MaxAngle(){return maxAngle;}}public class GroundRadar{public bool valid=false;public bool exists=false;public
bool active=false;public bool obstruction=false;public ScanMode mode;public int alt_age=0;public const double
UNDEFINED_ALTITUDE=1e6;public const double HORIZ_MAX_SPEED=20;const double RANGE_MARGIN=50;const double START_RANGE=1000;const double
MAX_TERRAIN_DISTANCE_SINGLE_RADAR=180;const double MAX_TERRAIN_DISTANCE_DOUBLE_RADAR=200;const double DOUBLE_RADAR_WIDE_SCAN_DISTANCE=1000;const double
DOUBLE_RADAR_INITIAL_SCAN_DISTANCE=5000;const double MIN_SCAN_ANGLE=2;const double MAX_SCAN_ANGLE=30;const double GROUND_SCAN_HORIZ_LENGTH=20;const int
MAX_SCAN_PACER=0;const double HORIZ_DEADZONE=2;readonly double RADAR_MAX_RANGE;readonly double SPEED_SCALE;readonly double
MAX_TERRAIN_DIST;readonly bool double_radar;double terrainScanRange,altScanRange;MyDetectedEntityInfo radar_return;double d_fwd,d_rear,
d_left,d_right,d_fwd_wide,d_rear_wide,d_left_wide,d_right_wide,d_fwd_left,d_fwd_right,d_rear_left,d_rear_right;double angle=1,
double_angle=1,diag_angle=1;double dz=HORIZ_DEADZONE;int scan_step=0;int scan_pacer=0;Vector3D hitpos;IMyCameraBlock altitudeRadar;
IMyCameraBlock terrainRadar;public GroundRadar(List<IMyTerminalBlock>radars,double max_range,double speed_scale){if(radars.Count==0){
exists=false;mode=ScanMode.NoRadar;}else if(radars.Count==1){altitudeRadar=radars[0]as IMyCameraBlock;terrainRadar=radars[0]as
IMyCameraBlock;MAX_TERRAIN_DIST=MAX_TERRAIN_DISTANCE_SINGLE_RADAR;double_radar=false;exists=true;mode=ScanMode.SingleStandby;}else{
altitudeRadar=radars[0]as IMyCameraBlock;terrainRadar=radars[1]as IMyCameraBlock;MAX_TERRAIN_DIST=MAX_TERRAIN_DISTANCE_DOUBLE_RADAR;
double_radar=true;exists=true;mode=ScanMode.DoubleStandby;}RADAR_MAX_RANGE=max_range;SPEED_SCALE=speed_scale;}public void
DisableRadar(){if(!exists)return;altitudeRadar.EnableRaycast=false;terrainRadar.EnableRaycast=false;d_fwd=d_rear=d_left=d_right=
MAX_TERRAIN_DIST;d_fwd_wide=d_rear_wide=d_left_wide=d_right_wide=MAX_TERRAIN_DIST;d_fwd_left=d_fwd_right=d_rear_left=d_rear_right=
MAX_TERRAIN_DIST;valid=false;active=false;}public void StartRadar(){if(!exists)return;altScanRange=START_RANGE;altitudeRadar.
EnableRaycast=true;terrainRadar.EnableRaycast=true;alt_age=0;d_fwd=d_rear=d_left=d_right=MAX_TERRAIN_DIST;d_fwd_wide=d_rear_wide=
d_left_wide=d_right_wide=MAX_TERRAIN_DIST;d_fwd_left=d_fwd_right=d_rear_left=d_rear_right=MAX_TERRAIN_DIST;active=true;}public void
ScanForAltitude(double pitch,double roll){if(!exists)return;if(altitudeRadar.CanScan(altScanRange)){radar_return=altitudeRadar.Raycast(
altScanRange,(float)-pitch,(float)-roll);if((radar_return.Type==MyDetectedEntityType.Planet||radar_return.Type==MyDetectedEntityType
.LargeGrid||radar_return.Type==MyDetectedEntityType.Asteroid)&&radar_return.HitPosition.HasValue){valid=true;altScanRange
=GetDistance()+RANGE_MARGIN;alt_age=0;}else{valid=false;altScanRange=Math.Min(altScanRange*2,RADAR_MAX_RANGE);}}}public
void IncrementAltAge(){alt_age++;}public double GetDistance(){if(!exists)return UNDEFINED_ALTITUDE;if(valid){hitpos=
radar_return.HitPosition.Value;Vector3D mypos=altitudeRadar.GetPosition();return VRageMath.Vector3D.Distance(hitpos,mypos);}else{
return UNDEFINED_ALTITUDE;}}public void ScanTerrain(double ship_pitch,double ship_roll){if(!exists){mode=ScanMode.NoRadar;
return;}if(double_radar){mode=ScanMode.DoubleStandby;terrainScanRange=Math.Min(GetDistance()*1.2+20,
DOUBLE_RADAR_INITIAL_SCAN_DISTANCE);if(valid&&GetDistance()<terrainScanRange)mode=(GetDistance()<DOUBLE_RADAR_WIDE_SCAN_DISTANCE)?ScanMode.DoubleWide:
ScanMode.DoubleEarly;}else{mode=ScanMode.SingleStandby;terrainScanRange=MAX_TERRAIN_DIST;if(valid&&GetDistance()<
terrainScanRange)mode=ScanMode.SingleNarrow;}double scan_angle_raw=Math.Atan(GROUND_SCAN_HORIZ_LENGTH/GetDistance())*Helpers.radToDeg;
angle=Helpers.SatMinMax(scan_angle_raw,MIN_SCAN_ANGLE,MAX_SCAN_ANGLE-5);diag_angle=Helpers.SatMinMax(scan_angle_raw*1.414,
MIN_SCAN_ANGLE,MAX_SCAN_ANGLE);double_angle=Helpers.SatMinMax(scan_angle_raw*2,MIN_SCAN_ANGLE,MAX_SCAN_ANGLE);if(mode==ScanMode.
SingleNarrow||mode==ScanMode.DoubleEarly||mode==ScanMode.DoubleWide){if(terrainRadar.CanScan(2*terrainScanRange)&&scan_pacer>=
MAX_SCAN_PACER){ScanStep(ship_pitch,ship_roll);scan_pacer=0;}else{scan_pacer++;}}else{d_fwd=d_rear=d_left=d_right=MAX_TERRAIN_DIST;
d_fwd_wide=d_rear_wide=d_left_wide=d_right_wide=MAX_TERRAIN_DIST;d_fwd_left=d_fwd_right=d_rear_left=d_rear_right=MAX_TERRAIN_DIST;
}}void ScanStep(double ship_pitch,double ship_roll){switch(scan_step){case 0:obstruction=false;obstruction=ScanPair(angle
,0,ship_pitch,ship_roll,terrainScanRange,out d_fwd,out d_rear);scan_step++;break;case 1:obstruction=ScanPair(0,-angle,
ship_pitch,ship_roll,terrainScanRange,out d_left,out d_right);if(mode==ScanMode.SingleNarrow)scan_step=0;else scan_step++;break;
case 2:if(mode==ScanMode.DoubleWide)obstruction=ScanPair(double_angle,0,ship_pitch,ship_roll,terrainScanRange,out d_fwd_wide
,out d_rear_wide);scan_step++;break;case 3:if(mode==ScanMode.DoubleWide)obstruction=ScanPair(0,-double_angle,ship_pitch,
ship_roll,terrainScanRange,out d_left_wide,out d_right_wide);scan_step++;break;case 4:if(mode==ScanMode.DoubleWide)obstruction=
ScanPair(diag_angle,-diag_angle,ship_pitch,ship_roll,terrainScanRange,out d_fwd_left,out d_rear_right);scan_step++;break;case 5:
if(mode==ScanMode.DoubleWide)obstruction=ScanPair(diag_angle,diag_angle,ship_pitch,ship_roll,terrainScanRange,out
d_fwd_right,out d_rear_left);scan_step=0;break;}}public double ScanDir(double scan_pitch,double scan_yaw,double ship_pitch,double
ship_roll,double max_range){if(!exists){mode=ScanMode.NoRadar;return max_range+1;}if(terrainRadar.CanScan(max_range)){float
cast_pitch=Helpers.MaxAbs((float)(scan_pitch-ship_pitch),45);float cast_yaw=Helpers.MaxAbs((float)(scan_yaw-ship_roll),45);
MyDetectedEntityInfo temp_return=terrainRadar.Raycast(max_range,cast_pitch,cast_yaw);if((temp_return.Type==MyDetectedEntityType.Planet||
temp_return.Type==MyDetectedEntityType.LargeGrid)&&temp_return.HitPosition.HasValue){return VRageMath.Vector3D.Distance(temp_return
.HitPosition.Value,terrainRadar.GetPosition());}else if(temp_return.EntityId==terrainRadar.CubeGrid.EntityId){return-1;}
else{return max_range;}}else{return max_range+1;}}bool ScanPair(double scan_pitch,double scan_roll,double ship_pitch,double
ship_roll,double max_range,out double dpos,out double dneg){double cos_pitch=Math.Cos(Helpers.degToRad*scan_pitch);double
cos_roll=Math.Cos(Helpers.degToRad*scan_roll);dpos=ScanDir(scan_pitch,scan_roll,ship_pitch,ship_roll,max_range)*cos_pitch*
cos_roll;dneg=ScanDir(-scan_pitch,-scan_roll,ship_pitch,ship_roll,max_range)*cos_pitch*cos_roll;if(dpos<0||dneg<0){dpos=dneg=
MAX_TERRAIN_DIST;return true;}return false;}public double RecommandFwdSpeed(){if(!exists)return 0;return RecommendSpeed(d_fwd,d_rear,
d_fwd_wide,d_rear_wide,d_fwd_left,d_fwd_right,d_rear_left,d_rear_right);}public double RecommandLeftSpeed(){if(!exists)return 0;
return RecommendSpeed(d_left,d_right,d_left_wide,d_right_wide,d_fwd_left,d_rear_left,d_fwd_right,d_rear_right);}double
RecommendSpeed(double d_pos,double d_neg,double d_wide_pos,double d_wide_neg,double d_diag1,double d_diag2,double d_diag3,double
d_diag4){double alt=GetDistance();double maxspeed=Math.Min(alt,HORIZ_MAX_SPEED);dz=Helpers.Interpolate(500,2000,HORIZ_DEADZONE,
0,alt);double vbase=Math.Atan2(d_pos-d_neg,Math.Tan(Helpers.degToRad*angle)*(d_pos+d_neg));double vpos,vpos_raw;if(
double_radar&&mode==ScanMode.DoubleWide){double vwide=Math.Atan2(d_wide_pos-d_wide_neg,Math.Tan(Helpers.degToRad*double_angle)*(
d_wide_pos+d_wide_neg));double vdiag=Math.Atan2(d_diag1+d_diag2-d_diag3-d_diag4,Math.Tan(Helpers.degToRad*diag_angle)*(d_diag1+
d_diag2+d_diag3+d_diag4));vpos_raw=vbase+vwide+vdiag;if(d_wide_pos<d_pos&&vpos_raw>0&&d_pos>0)vpos_raw*=Math.Pow(d_wide_pos/
d_pos,2);if(d_wide_neg<d_neg&&vpos_raw<0&&d_neg>0)vpos_raw*=Math.Pow(d_wide_neg/d_neg,2);if(d_pos<alt&&vpos_raw>0&&alt>0)
vpos_raw*=d_pos/alt;if(d_neg<alt&&vpos_raw<0&&alt>0)vpos_raw*=d_neg/alt;vpos=vpos_raw*SPEED_SCALE;}else{vpos=vbase*3*SPEED_SCALE
;}return Helpers.MaxAbs(Helpers.DeadZone(vpos,dz),maxspeed);}public string AltitudeDebugString(){if(!exists)return
"[NO RADAR !]";return"[RADAR]"+"\nRange:"+altScanRange.ToString("000.0")+"m"+" Avail:"+altitudeRadar.AvailableScanRange.ToString(
"000.0")+"m"+"\nReturn type: "+radar_return.Type+" Age: "+alt_age;}public string TerrainDebugString(){if(!exists)return
"[NO RADAR !]";return"[TERRAIN]"+"\nAv range: "+terrainRadar.AvailableScanRange.ToString("00000")+"m"+" Scan: "+angle.ToString("00.0")
+"°/"+double_angle.ToString("00.0")+" Dist: "+terrainScanRange.ToString("000.0")+"m"+"\nFw: "+d_fwd.ToString("000.0")+"/"
+d_fwd_wide.ToString("000.0")+" Rr: "+d_rear.ToString("000.0")+"/"+d_rear_wide.ToString("000.0")+" Fw spd: "+
RecommandFwdSpeed().ToString("00.0")+"\nLf: "+d_left.ToString("000.0")+"/"+d_left_wide.ToString("000.0")+" Rt: "+d_right.ToString("000.0"
)+"/"+d_right_wide.ToString("000.0")+" Lf spd: "+RecommandLeftSpeed().ToString("00.0");}public List<string>LogNames(){
return new List<string>{"d_fwd","d_rear","d_left","d_right"};}public List<double>LogValues(){return new List<double>{d_fwd,
d_rear,d_left,d_right};}}public class HorizontalThrusters{ShipBlocks ship;PIDController fwdPID,leftPID;IMyShipController
cockpit;readonly int DELAY;int timer;public HorizontalThrusters(ShipBlocks ship,int delay,double KP,double KI,double KD,double
AImax){this.ship=ship;this.cockpit=ship.shipCtrller;this.DELAY=delay;fwdPID=new PIDController(KP,KI,KD,-AImax,AImax,0.5,1);
leftPID=new PIDController(KP,KI,KD,-AImax,AImax,0.5,1);}public void Disable(){ship.fwdThr.Disable();ship.rearThr.Disable();ship
.leftThr.Disable();ship.rightThr.Disable();fwdPID.Reset();leftPID.Reset();}public void Tick(double fwdSpeed,double
leftSpeed,double fwdSpeedSetpoint,double leftSpeedSetpoint,double shipMass,bool deadZone,bool overridable){if((overridable&&(
cockpit.MoveIndicator.Length()>0.0f))||cockpit.RotationIndicator.Length()>0.0f){Disable();timer=0;}else if(timer>DELAY){double
fwdDelta=fwdSpeedSetpoint-fwdSpeed;double leftDelta=leftSpeedSetpoint-leftSpeed;double dz=deadZone?0.05:0;fwdPID.UpdatePID(
fwdDelta);leftPID.UpdatePID(leftDelta);ship.fwdThr.ApplyThrust(Helpers.DeadZone(fwdPID.output,dz)*shipMass,0,0);ship.rearThr.
ApplyThrust(Helpers.DeadZone(-fwdPID.output,dz)*shipMass,0,0);ship.leftThr.ApplyThrust(Helpers.DeadZone(leftPID.output,dz)*shipMass
,0,0);ship.rightThr.ApplyThrust(Helpers.DeadZone(-leftPID.output,dz)*shipMass,0,0);}else{timer++;}}public void
UpdateThrust(){ship.fwdThr.UpdateThrust();ship.rearThr.UpdateThrust();ship.leftThr.UpdateThrust();ship.rightThr.UpdateThrust();}
public string DebugString(){string str="[FWD PID]: "+fwdPID.DebugString();str+="\n[LEFT PID]: "+leftPID.DebugString();return
str;}public List<string>LogNames(){return new List<string>{"fwd_pid_output","left_pid_output"};}public List<double>
LogValues(){return new List<double>{fwdPID.output,leftPID.output};}}public class ThrGroup{public double hThrustMax,iThrustMax,
aThrustMax,pThrustMax,totalThrustMax;public double hThrustEff,iThrustEff,aThrustEff,pThrustEff,totalThrustEff;public double
hThrustNow,iThrustNow,aThrustNow,pThrustNow,totalThrustNow;public float aOverride,iOverride,hOverride,pOverride;public double[]
iThrustDensity=new double[11];public double[]pThrustDensity=new double[11];public double[]aThrustDensity=new double[11];List<IMyThrust
>aThrusters,iThrusters,hThrusters,pThrusters;double aThrust,iThrust,hThrust,pThrust;string GroupName;public ThrGroup(List
<IMyThrust>thrusters,string groupName=""){aThrusters=new List<IMyThrust>();iThrusters=new List<IMyThrust>();hThrusters=
new List<IMyThrust>();pThrusters=new List<IMyThrust>();foreach(var t in thrusters){string name=t.BlockDefinition.
SubtypeName.ToLower();string displayname=t.DefinitionDisplayNameText.ToString().ToLower();if(name.Contains("hydrogen")||name.
Contains("epstein")||name.Contains("rcs"))hThrusters.Add(t);else if(name.Contains("ion")||displayname.Contains("ion"))iThrusters
.Add(t);else if(name.Contains("atmo"))aThrusters.Add(t);else if(name.Contains("prototech"))pThrusters.Add(t);}GroupName=
groupName;}public void UpdateThrust(){aThrustEff=iThrustEff=hThrustEff=pThrustEff=0;aThrustMax=iThrustMax=hThrustMax=pThrustMax=0
;aThrustNow=iThrustNow=hThrustNow=pThrustNow=0;foreach(IMyThrust at in aThrusters){if(!at.Closed&&at.IsWorking){
aThrustEff+=at.MaxEffectiveThrust;aThrustMax+=at.MaxThrust;aThrustNow+=at.CurrentThrust;}}foreach(IMyThrust it in iThrusters){if(!
it.Closed&&it.IsWorking){iThrustEff+=it.MaxEffectiveThrust;iThrustMax+=it.MaxThrust;iThrustNow+=it.CurrentThrust;}}foreach
(IMyThrust ht in hThrusters){if(!ht.Closed&&ht.IsWorking){hThrustEff+=ht.MaxEffectiveThrust;hThrustMax+=ht.MaxThrust;
hThrustNow+=ht.CurrentThrust;}}foreach(IMyThrust pt in pThrusters){if(!pt.Closed&&pt.IsWorking){pThrustEff+=pt.MaxEffectiveThrust;
pThrustMax+=pt.MaxThrust;pThrustNow+=pt.CurrentThrust;}}totalThrustEff=aThrustEff+iThrustEff+hThrustEff+pThrustEff;totalThrustMax=
aThrustMax+iThrustMax+hThrustMax+pThrustMax;totalThrustNow=aThrustNow+iThrustNow+hThrustNow+pThrustNow;}public void ApplyThrust(
double wantedThrust,double aThrustMin,double iThrustMin){const float DZ=0.01f;const float TINY=0.000001f;aThrust=Helpers.
SatMinMax(wantedThrust,aThrustMin,aThrustEff);pThrust=Helpers.SatMinMax(wantedThrust-aThrust,iThrustMin,pThrustEff);iThrust=
Helpers.SatMinMax(wantedThrust-aThrust-pThrust,iThrustMin-pThrust,iThrustEff);hThrust=wantedThrust-pThrust-iThrust-aThrust;if(
aThrustEff>0){aOverride=(float)Helpers.SatMinMax(aThrust/aThrustEff,0,1);if(aOverride<DZ)aOverride=TINY;}else{aOverride=1;}if(
iThrustEff>0){iOverride=(float)Helpers.SatMinMax(iThrust/iThrustEff,0,1);if(iOverride<DZ)iOverride=TINY;}else{iOverride=TINY;}if(
hThrustEff>0){hOverride=(float)Helpers.SatMinMax(hThrust/hThrustEff,0,1);if(hOverride<DZ)hOverride=TINY;}else{hOverride=TINY;}if(
pThrustEff>0){pOverride=(float)Helpers.SatMinMax(pThrust/pThrustEff,0,1);if(pOverride<DZ)pOverride=TINY;}else{pOverride=TINY;}
foreach(IMyThrust alifter in aThrusters){alifter.Enabled=true;alifter.ThrustOverridePercentage=aOverride;}foreach(IMyThrust
ilifter in iThrusters){ilifter.Enabled=true;ilifter.ThrustOverridePercentage=iOverride;}foreach(IMyThrust hlifter in hThrusters
){hlifter.Enabled=true;hlifter.ThrustOverridePercentage=hOverride;}foreach(IMyThrust plifter in pThrusters){plifter.
Enabled=true;plifter.ThrustOverridePercentage=pOverride;}}public void Disable(){foreach(IMyThrust alifter in aThrusters){
alifter.ThrustOverride=0;alifter.Enabled=true;}foreach(IMyThrust ilifter in iThrusters){ilifter.ThrustOverride=0;ilifter.
Enabled=true;}foreach(IMyThrust hlifter in hThrusters){hlifter.ThrustOverride=0;hlifter.Enabled=true;}foreach(IMyThrust plifter
in pThrusters){plifter.ThrustOverride=0;plifter.Enabled=true;}}public double WorstDensity(){if(aThrustMax+iThrustMax*0.2+
pThrustMax*0.3<iThrustMax+pThrustMax)return 1;else return 0.3;}public double AtmoThrustForDensity(double density){return Math.Max(
aThrustMax*(Math.Min(density,1)*1.43f-0.43f),0);}public double IonThrustForDensity(double density){return iThrustMax*(1-0.8f*Math.
Min(density,1));}public double PrototechThrustForDensity(double density){return pThrustMax*(1-0.7f*Math.Min(density,1));}
public void UpdateDensitySweep(){for(int i=0;i<11;i++){iThrustDensity[i]=IonThrustForDensity(i/10.0);aThrustDensity[i]=
AtmoThrustForDensity(i/10.0);pThrustDensity[i]=PrototechThrustForDensity(i/10.0);}}public string Inventory(){return"("+iThrusters.Count+
" I, "+aThrusters.Count+" A, "+hThrusters.Count+" H, "+pThrusters.Count+" P)";}public string DebugString(){return"["+GroupName
+"] A:"+aOverride.ToString("+0.00;-0.00")+" I:"+iOverride.ToString("+0.00;-0.00")+" H:"+hOverride.ToString("+0.00;-0.00")
+"P: "+pOverride.ToString("+0.00;-0.00")+" WD"+WorstDensity().ToString("0.00")+"\nA: "+Helpers.FormatCompact(aThrustEff)+
" I: "+Helpers.FormatCompact(iThrustEff)+" H: "+Helpers.FormatCompact(hThrustEff)+" P: "+Helpers.FormatCompact(pThrustEff);}}
public class RunTimeCounter{readonly Program program;RollingBuffer t1_buffer=new RollingBuffer(120);RollingBuffer t10_buffer=
new RollingBuffer(12);RollingBuffer t100_buffer=new RollingBuffer(2);public RunTimeCounter(Program program){this.program=
program;}public void Count(bool ranTick1,bool ranTick10,bool ranTick100){double runtime=program.Runtime.LastRunTimeMs;if(
ranTick1&&!ranTick10&&!ranTick100)t1_buffer.Add(runtime);if(ranTick10&&!ranTick100)t10_buffer.Add(runtime);if(ranTick100)
t100_buffer.Add(runtime);}public string RunTimeString(){string s="";s+="Avg t1:"+t1_buffer.Average().ToString("0.00")+"ms";s+=
", t10: "+t10_buffer.Average().ToString("0.00")+"ms";s+=", t100:"+t100_buffer.Average().ToString("0.00")+"ms";s+="\nMax t1:"+
t1_buffer.Max().ToString("0.00")+"ms";s+=", t10: "+t10_buffer.Max().ToString("0.00")+"ms";s+=", t100:"+t100_buffer.Max().ToString
("0.00")+"ms";return s;}public List<string>LogNames(){return new List<string>{"Avg t1","Avg t10","Avg t100","Max t1",
"Max t10","Max t100"};}public List<double>LogValues(){return new List<double>{t1_buffer.Average(),t10_buffer.Average(),
t100_buffer.Average(),t1_buffer.Max(),t10_buffer.Max(),t100_buffer.Max()};}}public class Helpers{public static double NotNan(double
val){if(double.IsNaN(val))return 0;return val;}public static double SatMinMax(double value,double min,double max){if(value>
max||min>max)return max;if(value<min)return min;return value;}public static double MaxAbs(double value,double maxabs){
return Math.Min(Math.Abs(value),maxabs)*Math.Sign(value);}public static float MaxAbs(float value,float maxabs){return Math.Min
(Math.Abs(value),maxabs)*Math.Sign(value);}public static double Min3(double a,double b,double c){return Math.Min(a,Math.
Min(b,c));}public static double Max3(double a,double b,double c){return Math.Max(a,Math.Max(b,c));}public static double
DeadZone(double value,double deadzone){if(Math.Abs(value)<deadzone){return 0;}else{return value;}}public static double
Interpolate(double X1,double X2,double Y1,double Y2,double x){if(X1==X2)return Y1;if(x<=X1)return Y1;if(x>=X2)return Y2;return Y1+(
Y2-Y1)*(x-X1)/(X2-X1);}public static double InterpolateSmooth(double X1,double X2,double Y1,double Y2,double x){if(X1==X2)
return Y1;if(x<=X1)return Y1;if(x>=X2)return Y2;double t=(x-X1)/(X2-X1);return Y1+(Y2-Y1)*t*t*(3.0-2.0*t);}public static
double Mix(double a,double b,double ratio_of_a){double ratio=SatMinMax(ratio_of_a,0,1);return a*ratio+b*(1-ratio);}public
static double g_to_ms2(double g){return g*9.81;}public static double ms2_to_g(double a){return a/9.81;}public static void
Rectangle(MySpriteDrawFrame frame,float x1,float x2,float y1,float y2,VRageMath.RectangleF view,float thickness,VRageMath.Color
color){float[]x=new float[4]{(x1+x2)/2,(x1+x2)/2,x1,x2};float[]y=new float[4]{y1,y2,(y1+y2)/2,(y1+y2)/2};float[]w=new float[4
]{x2-x1,x2-x1,thickness,thickness};float[]h=new float[4]{thickness,thickness,y2-y1,y2-y1};for(int i=0;i<4;i++){MySprite s
=MySprite.CreateSprite("SquareSimple",new Vector2(x[i],y[i])+view.Position,new Vector2(w[i],h[i]));s.Color=color;frame.
Add(s);}}public static string FormatCompact(double value){if(Math.Abs(value)>100)return value.ToString("000");else if(Math.
Abs(value)>10)return value.ToString("00.0");else return value.ToString("0.00");}public static string Truncate(string str,
int maxLength){if(str.Length>maxLength)return str.Substring(0,maxLength);return str;}public static int FindN(string
inString,string prefix){for(int N=0;N<=9;N++){string prefixN=prefix+N.ToString();if(inString.Contains(prefixN))return N;}return-
1;}public static int FindN(string inString,List<string>prefixes){foreach(string prefix in prefixes){for(int N=0;N<=9;N++)
{string prefixN=prefix+N.ToString();if(inString.Contains(prefixN))return N;}}return-1;}public const double halfPi=Math.PI
/2;public const double radToDeg=180/Math.PI;public const double degToRad=Math.PI/180;}public class Logger{List<string>
names=new List<string>();List<List<double>>records=new List<List<double>>();int cnter;int FACTOR;double tstart=0;bool allow;
public Logger(List<string>names,int factor,bool allow){this.names=names;this.FACTOR=factor;this.allow=allow;}public void Clear
(){records.Clear();cnter=0;}public void Log(List<double>record){if(!allow)return;if(cnter==0)tstart=DateTime.Now.
TimeOfDay.TotalMilliseconds;if(cnter%FACTOR==0){List<double>new_record=new List<double>();new_record.Add(DateTime.Now.TimeOfDay.
TotalMilliseconds-tstart);new_record.AddRange(record);records.Add(new_record);}cnter++;}public string Output(){string output="time(ms),";
foreach(string name in names)output+=name+",";output+="\n";foreach(List<double>record in records){foreach(double value in
record)output+=value.ToString("0.00")+",";output+="\n";}return output;}}public class MovingAverage{double[]values;int index,
size;double sum;public MovingAverage(int set_size){values=new double[set_size];index=0;size=set_size;sum=0;for(int i=0;i<
size;i++){values[i]=0;}}public double AddValue(double value){sum-=values[index];values[index]=value;sum+=value;index=(index+
1)%size;return sum/size;}public double Get(){return sum/size;}public void Clear(){Set(0);}public void Set(double value){
sum=value*size;for(int i=0;i<size;i++){values[i]=value;}}}public class RollingBuffer{double[]buffer;int index;public
RollingBuffer(int size){buffer=new double[size];index=0;}public void Add(double item){buffer[index]=item;index=(index+1)%buffer.
Length;}public double[]GetBuffer(){return buffer;}public double Average(){return buffer.Average();}public double Max(){return
buffer.Max();}}public class RateLimiter{double maxRatePositive;double maxRateNegative;double lastValue;public RateLimiter(
double maxRatePositive,double maxRateNegative){this.maxRatePositive=maxRatePositive;this.maxRateNegative=maxRateNegative;
lastValue=0;}public double Limit(double value){double delta=value-lastValue;if(delta>maxRatePositive)value=lastValue+
maxRatePositive;else if(delta<maxRateNegative)value=lastValue+maxRateNegative;lastValue=value;return value;}public void Init(double
initialValue){lastValue=initialValue;}}public class AutoPilot{public double fwdSpeedSP,leftSpeedSP,vertSpeedSP;public double
mode4DesiredSpeed,mode4DesiredAltitude;public double forward;public bool forwardValid;public enum AltitudeMode{Ground,SeaLevel}public
AltitudeMode altitudeMode;public MovingAverage altitudeFilter;readonly double speedIncrement;readonly double maxSpeed,ssamin,ssamax,
ssmin,ssmax;PIDController alt_PID;MovingAverage fwdSpeedFilter,leftSpeedFilter,fwdAltFilter;MovingAverage safeSpeedFilter;
public AutoPilot(SLMConfiguration config){alt_PID=new PIDController(config.altKp,config.altKi,config.altKd,config.alt_aiMin,
config.alt_aiMax,config.altAdFilt,config.altAdMax);fwdSpeedFilter=new MovingAverage(config.speedFilterLength);leftSpeedFilter=
new MovingAverage(config.speedFilterLength);altitudeFilter=new MovingAverage(config.altFilterLength);safeSpeedFilter=new
MovingAverage(config.safeSpeedFilterLength);fwdAltFilter=new MovingAverage(10);speedIncrement=config.speedIncrement;maxSpeed=config.
maxSpeed;ssamin=config.safeSpeedAltMin;ssamax=config.safeSpeedAltMax;ssmin=config.safeSpeedMin;ssmax=config.safeSpeedMax;forward
=ssamax;forwardValid=false;}public void Init(){fwdSpeedSP=0;leftSpeedSP=0;vertSpeedSP=0;alt_PID.Reset();fwdSpeedFilter.
Clear();leftSpeedFilter.Clear();altitudeFilter.Clear();safeSpeedFilter.Clear();altitudeMode=AltitudeMode.Ground;}public void
UpdateSpeedDirect(Vector3 moveIndicator){fwdSpeedSP=0;leftSpeedSP=0;double safe=safeSpeedFilter.Get();if(moveIndicator.Z>0.0f)
fwdSpeedFilter.AddValue(-safe);else if(moveIndicator.Z<0.0f)fwdSpeedFilter.AddValue(safe);else fwdSpeedFilter.AddValue(0);fwdSpeedSP=
fwdSpeedFilter.Get();if(moveIndicator.X>0.0f)leftSpeedFilter.AddValue(-safe);else if(moveIndicator.X<0.0f)leftSpeedFilter.AddValue(
safe);else leftSpeedFilter.AddValue(0);leftSpeedSP=leftSpeedFilter.Get();}public void UpdateSpeedProgressive(Vector3
moveIndicator){leftSpeedSP=0;if(moveIndicator.Z>0.0f&&mode4DesiredSpeed>=speedIncrement)mode4DesiredSpeed-=speedIncrement;else if(
moveIndicator.Z<0.0f&&mode4DesiredSpeed<maxSpeed)mode4DesiredSpeed+=speedIncrement;fwdSpeedSP=Math.Min(mode4DesiredSpeed,
safeSpeedFilter.Get());}public void UpdateVertSpeedSP(double gndAltitude,double slAltitude,double gravity){altitudeFilter.AddValue(
mode4DesiredAltitude);double altDelta=0;double relevantGndAltitude;if(forwardValid)fwdAltFilter.AddValue(forward);if(mode4DesiredSpeed>1)
relevantGndAltitude=forwardValid?Math.Min(gndAltitude,fwdAltFilter.Get()+5):gndAltitude;else relevantGndAltitude=gndAltitude;switch(
altitudeMode){case AltitudeMode.Ground:altDelta=altitudeFilter.Get()-relevantGndAltitude;break;case AltitudeMode.SeaLevel:double
minGNDaltitude=Helpers.Interpolate(ssmin,ssmax,ssamin,ssamax,mode4DesiredSpeed);double altDeltaSL=altitudeFilter.Get()-slAltitude;
double altDeltaGND=minGNDaltitude-relevantGndAltitude;altDelta=Math.Max(altDeltaSL,altDeltaGND);break;}altDelta=altDelta/
Helpers.SatMinMax(relevantGndAltitude/50,.1,1);alt_PID.UpdatePIDController(altDelta,-5,5);vertSpeedSP=alt_PID.output+Math.Max(
altDelta-10,0)*0.5;if(altDelta>0){double equiv_speed=Math.Sqrt(2*gravity*altDelta+5);vertSpeedSP=Math.Min(vertSpeedSP,
equiv_speed);}}public void UpdateSafeSpeed(double gndAltitude,double forward){if(forward>0)safeSpeedFilter.AddValue(Helpers.
Interpolate(ssamin,ssamax,ssmin,ssmax,Math.Min(gndAltitude,forward)));else safeSpeedFilter.AddValue(Helpers.Interpolate(ssamin,
ssamax,ssmin,ssmax,gndAltitude));}public string DebugString(){return"[AUTOPILOT]\nforward:"+Helpers.FormatCompact(forward)+
"m "+forwardValid.ToString()+" PIDout:"+Helpers.FormatCompact(alt_PID.output)+"m/s "+Helpers.FormatCompact(vertSpeedSP)+
" ms/s\n"+alt_PID.DebugString();}public List<string>LogNames(){return new List<string>{"nforward","vertSpeedSP"};}public List<
double>LogValues(){return new List<double>{forward,vertSpeedSP};}}// End of partial class
