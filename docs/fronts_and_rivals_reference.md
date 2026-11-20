# Fronts & Rivals: native reference pack

Built from primary sources only: the NativeDB data (alloc8or/gta5-nativedb-data) and the CitizenFX natives docs (citizenfx/natives). No mod code was used.

Each entry: signature, hash, and the documentation notes. "Missing" means the native does not exist in single-player GTA V.

## Sources

| Source | What it is | Link |
| --- | --- | --- |
| NativeDB data | Signatures, hashes and notes for every GTA V native | https://github.com/alloc8or/gta5-nativedb-data |
| CitizenFX natives docs | Longer write-ups and examples per native | https://github.com/citizenfx/natives |
| ScriptHookVDotNet | The C# framework the mod runs on. We build on its API, we don't copy it. | https://github.com/crosire/scripthookvdotnet |
| LemonUI | Native-looking menus (MIT licence) | https://github.com/LemonUIbyLemon/LemonUI |
| DurtyFree data dumps | Raw lists pulled from the game files | https://github.com/DurtyFree/gta-v-data-dumps |

The data dumps are current as of GTA V update v3717.0. The SHVDN source is from 15 November 2026.

## Framework shortcuts (SHVDN v3)

SHVDN already wraps many natives safely. Its wrappers are simpler and less crash-prone than raw native calls:

- `Game.IsMissionActive` and `Game.IsRandomEventActive` for the story-mission pause
- `World.CreatePed`, `World.CreateVehicle` and `Vehicle.CreateRandomPedOnSeat` for spawning
- `World.GetNextPositionOnStreetWithHeading` for safe road spawn points
- `World.AddRelationshipGroup` and `RelationshipGroup.SetRelationshipBetweenGroups` for crew factions
- `World.AddRope` for Vault Drag
- `World.AddExplosion` for accident kills
- `Vehicle.HandlingData.BrakeForce` for cut brakes. `SET_VEHICLE_HANDLING_FLOAT` is FiveM-only and doesn't exist in single player, so this property replaces it.

## Game data index (DurtyFree dumps)

| File | Use in Fronts & Rivals | Entries |
| --- | --- | --- |
| peds.json | Ped models for crew members and guards | 1109 |
| vehicles.json | Getaway, convoy and tow vehicles | 921 |
| weapons.json | Crew and guard loadouts | 184 |
| mloInteriors.json | Interiors for the HQ and fronts | 385 |
| ipls.json | Map sections to load for those interiors | 895 |
| scenariosCompact.json | Idle animations for guards (smoking, standing guard) | 247 |
| drivingStyleFlagValues.json | Driving-style flags for getaways and escorts | 33 |
| explosionTypesCompact.json | Explosion types for accident kills | 88 |
| pickupTypes.json | Cash pickups for Vault Drag spills | 175 |
| policeReportNames.json | Police scanner lines | 50 |
| soundNames.json | Frontend sounds for native-style alerts | 2204 |
| zones.json | Zone names for tips ("Crew X is hitting Vinewood") | 97 |
| garages.json | Garage locations | 32 |
| traintracks.json | Track data for rail-crossing escapes | 12 |

## Core: stay out of the story's way

### GET_MISSION_FLAG

`BOOL MISC::GET_MISSION_FLAG()` · `0xA33CDCCDA663159E`

### GET_RANDOM_EVENT_FLAG

`BOOL MISC::GET_RANDOM_EVENT_FLAG()` · `0xD2D57F1D764117B1`

### IS_PLAYER_SWITCH_IN_PROGRESS

`BOOL STREAMING::IS_PLAYER_SWITCH_IN_PROGRESS()` · `0xD9D2CFFF49FAB35F`

```
Returns true if the player is currently switching, false otherwise.  
(When the camera is in the sky moving from Trevor to Franklin for example)  
```

### IS_CUTSCENE_ACTIVE

`BOOL CUTSCENE::IS_CUTSCENE_ACTIVE()` · `0x991251AFC3981F84`

### IS_SCREEN_FADED_OUT

`BOOL CAMERA::IS_SCREEN_FADED_OUT()` · `0xB16FCE9DDC7BA182`

### IS_PLAYER_PLAYING

`BOOL PLAYER::IS_PLAYER_PLAYING(Player player)` · `0x5E9564D8246B909A`

```
Checks whether the specified player has a Ped, the Ped is not dead, is not injured and is not arrested.  
```


* **player**:

### GET_CLOCK_HOURS

`int CLOCK::GET_CLOCK_HOURS()` · `0x25223CA6B4D20B7F`

```
Gets the current ingame hour, expressed without zeros. (09:34 will be represented as 9)  
```

### GET_CLOCK_DAY_OF_WEEK

`int CLOCK::GET_CLOCK_DAY_OF_WEEK()` · `0xD972E4BD7AEB235F`

```
Gets the current day of the week.  
0: Sunday  
1: Monday  
2: Tuesday  
3: Wednesday  
4: Thursday  
5: Friday  
6: Saturday  
```

## Core: entity lifetime (stability)

### SET_ENTITY_AS_MISSION_ENTITY

`void ENTITY::SET_ENTITY_AS_MISSION_ENTITY(Entity entity, BOOL bScriptHostObject, BOOL bGrabFromOtherScript)` · `0xAD738C3085FE7E11`

Assigns an existing entity to be owned by the current script. If the entity was not owned by a script yet, this also means the entity will remain persistent until released.

Note that this is not needed right after creating an entity as a script-created entity will automatically be assigned.


* **entity**: An entity handle.
* **scriptHostObject**: Whether or not to assign script info to this entity, if set to false the entity will only be protected from despawning locally.
* **bGrabFromOtherScript**: Changes the entity so it's owned by the current script if the entity is already a mission entity. This will only work on entities the machine has control of (non-remote entities).

### SET_ENTITY_AS_NO_LONGER_NEEDED

`void ENTITY::SET_ENTITY_AS_NO_LONGER_NEEDED(Entity* entity)` · `0xB736A491E64A32CF`

```
Marks the specified entity (ped, vehicle or object) as no longer needed.  
Entities marked as no longer needed, will be deleted as the engine sees fit.  
```


* **entity**:

### DELETE_ENTITY

`void ENTITY::DELETE_ENTITY(Entity* entity)` · `0xAE3CBE5BF394C9C9`

Delete the specified entity, and invalidate the passed handle (i.e., the in/out argument).
You might want to check if the entity exists before with [DOES_ENTITY_EXIST](#_0x7239B21A38F536BA).


- **entity**: The entity to delete.


```lua
-- Retrieve the vehicle the player is currently in. 
local vehicle = GetVehiclePedIsIn(PlayerPedId(), false)

-- Check if the vehicle exists in the game world.
if not DoesEntityExist(vehicle) then 
    -- If the vehicle does not exist, end the execution of the code here.
    return 
end

-- If the vehicle does exist, delete the vehicle entity from the game world.
DeleteEntity(vehicle)
```

```js
// Retrieve the vehicle the player is currently in.
const vehic…

_CitizenFX docs include an example._

### DOES_ENTITY_EXIST

`BOOL ENTITY::DOES_ENTITY_EXIST(Entity entity)` · `0x7239B21A38F536BA`

Checks whether an entity exists in the game world.



- **entity**: The entity to check if it exists.



Whether the entity exists or not.



```lua
local currentVehicle = GetVehiclePedIsIn(PlayerPedId(), false)

if DoesEntityExist(currentVehicle) then
    DeleteEntity(currentVehicle)
end
```

```js
const currentVehicle = GetVehiclePedIsIn(PlayerPedId(), false);

if (DoesEntityExist(currentVehicle)) {
  DeleteEntity(currentVehicle);
}
```

```cs
using static CitizenFX.Core.Native.API
// ...

int currentVehicle = GetVehiclePedIsIn(PlayerPedId(), false);

if(DoesEntityExist(currentVehicle))
{
    DeleteEntity(currentVehicle);
}

// or the preferred use of C# wrapper
Vehicle currentVehicle = Ga…

_CitizenFX docs include an example._

### SET_MODEL_AS_NO_LONGER_NEEDED

`void STREAMING::SET_MODEL_AS_NO_LONGER_NEEDED(Hash model)` · `0xE532F5D78798DAAB`

Releases the script ownership assigned by REQUEST\_MODEL. This command should be used when done using a specific model hash in script.


* **model**: The model to release ownership of.

### REQUEST_MODEL

`void STREAMING::REQUEST_MODEL(Hash model)` · `0x963D27A58DF860AC`

Request a model (archetype) to be loaded for use by the current script. Use SET\_MODEL\_AS\_NO\_LONGER\_NEEDED when done using the model in script.


* **model**: The model to request.


```lua
local modelHash = `adder`
RequestModel(modelHash)
while not HasModelLoaded(modelHash) do
  Wait(0)
end

-- omitted: creating a vehicle

-- when done using the model
SetModelAsNoLongerNeeded(modelHash)
```

_CitizenFX docs include an example._

### HAS_MODEL_LOADED

`BOOL STREAMING::HAS_MODEL_LOADED(Hash model)` · `0x98A4EB5D89A0C952`

Returns whether the specified model (archetype) is currently loaded.

Note that this will return 'true' even if the model has been requested and loaded by something other than the current script, if you're intending to actually use the model in a later frame, you should call REQUEST\_MODEL anyway.


* **model**: The model hash to check for.


A boolean indicating whether the archetype is loaded (true) or not (false).

### SET_PED_KEEP_TASK

`void PED::SET_PED_KEEP_TASK(Ped ped, BOOL toggle)` · `0x971D38760FBC02EF`

* **ped**: 
* **toggle**:

## Rival Crews: spawning and movement

### GET_CLOSEST_VEHICLE_NODE_WITH_HEADING

`BOOL PATH::GET_CLOSEST_VEHICLE_NODE_WITH_HEADING(float x, float y, float z, Vector3* outPosition, float* outHeading, int nodeType, float p6, float p7)` · `0xFF071FB798B803B0`

Same as [`GET_CLOSEST_VEHICLE_NODE`](#_0x240A18690AE96513), but with the node flag `GCNF_GET_HEADING` set, causing the native to also return the heading.


* **x**: X coordinate
* **y**: Y coordinate
* **z**: Z coordinate
* **outPosition**: Pointer to the found nodes coords
* **outHeading**: Pointer to the found nodes heading
* **nodeFlags**: Node flags, see [`GET_CLOSEST_VEHICLE_NODE`](#_0x240A18690AE96513)
* **zMeasureMult**: How strongly the difference in the Z direction should be weighted
* **zTolerance**: How far apart the Z coords have to be before `zMeasureMult` kicks in


Returns the closest vehicle node with its heading.

### GET_SAFE_COORD_FOR_PED

`BOOL PATH::GET_SAFE_COORD_FOR_PED(float x, float y, float z, BOOL onGround, Vector3* outPosition, int flags)` · `0xB61C8E878A4199CA`

```cpp
enum eSafePositionFlags {
    // Only navmesh polygons marked as pavement
    GSC_FLAG_ONLY_PAVEMENT = 1,
    // Only navmesh polygons not marked as "isolated"
    GSC_FLAG_NOT_ISOLATED = 2,
    // No navmesh polygons created from interiors
    GSC_FLAG_NOT_INTERIOR = 4,
    // No navmesh polygons marked as water
    GSC_FLAG_NOT_WATER = 8,
    // Only navmesh polygons marked as "network spawn candidate"
    GSC_FLAG_ONLY_NETWORK_SPAWN = 16,
    // Specify whether to use a flood-fill from the starting position, as opposed to scanning all polygons within the search volume
    GSC_FLAG_USE_FLOOD_FILL = 32
}
```


* **x**: The X coordinate of the point to check.
* **y**: The Y coordinate…

### CREATE_PED_INSIDE_VEHICLE

`Ped PED::CREATE_PED_INSIDE_VEHICLE(Vehicle vehicle, int pedType, Hash modelHash, int seat, BOOL isNetwork, BOOL bScriptHostPed)` · `0x7DD959874C1FD534`

* **vehicle**: 
* **pedType**: See [`CREATE_PED`](#_0xD49F9B0955C367DE)
* **modelHash**: 
* **seat**: 
* **isNetwork**: 
* **bScriptHostPed**:

### TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE

`void TASK::TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE(Ped ped, Vehicle vehicle, float x, float y, float z, float speed, int driveMode, float stopRange)` · `0x158BB33F920D360C`

You can let your character drive to the destination at the speed and driving style you set. You can use map marks to set the destination.

```c
enum eDriveBehaviorFlags {
  DF_StopForCars = 1,
  DF_StopForPeds = 2,
  DF_SwerveAroundAllCars = 4,
  DF_SteerAroundStationaryCars = 8,
  DF_SteerAroundPeds = 16,
  DF_SteerAroundObjects = 32,
  DF_DontSteerAroundPlayerPed = 64,
  DF_StopAtLights = 128,
  DF_GoOffRoadWhenAvoiding = 256,
  DF_DriveIntoOncomingTraffic = 512,
  DF_DriveInReverse = 1024,
  DF_UseWanderFallbackInsteadOfStraightLine = 2048,
  DF_AvoidRestrictedAreas = 4096,
  DF_PreventBackgroundPathfinding = 8192, // **These only work on MISSION_CRUISE**
  DF_AdjustCruiseSpeedBasedOnRoad…

_CitizenFX docs include an example._

### TASK_VEHICLE_DRIVE_TO_COORD

`void TASK::TASK_VEHICLE_DRIVE_TO_COORD(Ped ped, Vehicle vehicle, float x, float y, float z, float speed, Any p6, Hash vehicleModel, int drivingMode, float stopRange, float straightLineDistance)` · `0xE2A2AA2F659D77A7`

```
info about driving modes: HTTP://gtaforums.com/topic/822314-guide-driving-styles/  
---------------------------------------------------------------  
Passing P6 value as floating value didn't throw any errors, though unsure what is it exactly, looks like radius or something.  
P10 though, it is mentioned as float, however, I used bool and set it to true, that too worked.  
Here the e.g. code I used  
Function.Call(Hash.TASK_VEHICLE_DRIVE_TO_COORD, Ped, Vehicle, Cor X, Cor Y, Cor Z, 30f, 1f, Vehicle.GetHashCode(), 16777216, 1f, true);  
```


* **ped**: 
* **vehicle**: 
* **x**: 
* **y**: 
* **z**: 
* **speed**: 
* **p6**: 
* **vehicleModel**: 
* **drivingMode**: 
* **stopRange**: 
* **p1…

### TASK_VEHICLE_ESCORT

`void TASK::TASK_VEHICLE_ESCORT(Ped ped, Vehicle vehicle, Vehicle targetVehicle, int mode, float speed, int drivingStyle, float minDistance, int minHeightAboveTerrain, float noRoadsDistance)` · `0x0FA6E4B75F302400`

```
Makes a ped follow the targetVehicle with <minDistance> in between.  
note: minDistance is ignored if drivingstyle is avoiding traffic, but Rushed is fine.  
Mode: The mode defines the relative position to the targetVehicle. The ped will try to position its vehicle there.  
-1 = behind  
0 = ahead  
1 = left  
2 = right  
3 = back left  
4 = back right  
if the target is closer than noRoadsDistance, the driver will ignore pathing/roads and follow you directly.  
Driving Styles guide: gtaforums.com/topic/822314-guide-driving-styles/  
```


* **ped**: 
* **vehicle**: 
* **targetVehicle**: 
* **mode**: 
* **speed**: 
* **drivingStyle**: The driving style (default is 0) (see [SetDriveTaskDr…

### TASK_VEHICLE_MISSION_PED_TARGET

`void TASK::TASK_VEHICLE_MISSION_PED_TARGET(Ped ped, Vehicle vehicle, Ped pedTarget, int missionType, float maxSpeed, int drivingStyle, float minDistance, float straightLineDistance, BOOL DriveAgainstTraffic)` · `0x9454528DF15D657A`

All parameters except ped, vehicle, pedTarget and speed are optional; with `missionType` being only those that require a target entity.

If you don't want to use a parameter; pass `0` for int parameters, and `-1.0f` for the remaining float parameters.


* **ped**: The ped to be tasked.
* **vehicle**: The vehicles' entity handle.
* **pedTarget**: The target ped.
* **missionType**: The mission type (default is 0) (see [TaskVehicleMission](#_0x659427E0EF36BCDE)).
* **speed**: The speed in m/s.
* **drivingStyle**: The driving style (default is 0) (see [SetDriveTaskDrivingStyle](#_0xDACE1BE37D88AF67)).
* **radius**: The radius of when the task will be completed (default is -1.0f).
* **straightLin…

_CitizenFX docs include an example._

### TASK_LEAVE_VEHICLE

`void TASK::TASK_LEAVE_VEHICLE(Ped ped, Vehicle vehicle, int flags)` · `0xD3DBCE61A490BE02`

```
Flags from decompiled scripts:  
0 = normal exit and closes door.  
1 = normal exit and closes door.  
16 = teleports outside, door kept closed.  (This flag does not seem to work for the front seats in buses, NPCs continue to exit normally)
64 = normal exit and closes door, maybe a bit slower animation than 0.  
256 = normal exit but does not close the door.  
4160 = ped is throwing himself out, even when the vehicle is still.  
262144 = ped moves to passenger seat first, then exits normally  
Others to be tried out: 320, 512, 131072.  
```


* **ped**: 
* **vehicle**: 
* **flags**:

### SET_DRIVER_ABILITY

`void PED::SET_DRIVER_ABILITY(Ped driver, float ability)` · `0xB195FFA8042FC5C3`

```
The function specifically verifies the value is equal to, or less than 1.0f. If it is greater than 1.0f, the function does nothing at all.  
```


* **driver**: 
* **ability**:

### SET_DRIVER_AGGRESSIVENESS

`void PED::SET_DRIVER_AGGRESSIVENESS(Ped driver, float aggressiveness)` · `0xA731F608CA104E3C`

```
range 0.0f - 1.0f  
```


* **driver**: 
* **aggressiveness**:

### TASK_SMART_FLEE_PED

`void TASK::TASK_SMART_FLEE_PED(Ped ped, Ped fleeTarget, float safeDistance, int fleeTime, BOOL preferPavements, BOOL updateToNearestHatedPed)` · `0x22B0D0E37CCB840D`

```
Makes a ped run away from another ped (fleeTarget).  
distance = ped will flee this distance.  
fleeTime = ped will flee for this amount of time, set to "-1" to flee forever  
```


* **ped**: 
* **fleeTarget**: 
* **distance**: 
* **fleeTime**: 
* **p4**: 
* **p5**:

## Rival Crews: who fights whom

### ADD_RELATIONSHIP_GROUP

`BOOL PED::ADD_RELATIONSHIP_GROUP(const char* name, Hash* groupHash)` · `0xF372BC22FCB88606`

```
Can't select void. This function returns nothing. The hash of the created relationship group is output in the second parameter.  
```


* **name**: 
* **groupHash**:

### SET_RELATIONSHIP_BETWEEN_GROUPS

`void PED::SET_RELATIONSHIP_BETWEEN_GROUPS(int relationship, Hash group1, Hash group2)` · `0xBF25EB89375A37AD`

```
Sets the relationship between two groups. This should be called twice (once for each group).  
Relationship types:  
0 = Companion  
1 = Respect  
2 = Like  
3 = Neutral  
4 = Dislike  
5 = Hate  
255 = Pedestrians  
Example:  
PED::SET_RELATIONSHIP_BETWEEN_GROUPS(2, l_1017, 0xA49E591C);  
PED::SET_RELATIONSHIP_BETWEEN_GROUPS(2, 0xA49E591C, l_1017);  
```


* **relationship**: 
* **group1**: 
* **group2**:

### SET_PED_RELATIONSHIP_GROUP_HASH

`void PED::SET_PED_RELATIONSHIP_GROUP_HASH(Ped ped, Hash hash)` · `0xC80A74AC829DDD92`

* **ped**: 
* **hash**:

### TASK_COMBAT_PED

`void TASK::TASK_COMBAT_PED(Ped ped, Ped targetPed, int combatFlags, int threatResponseFlags)` · `0xF166E48407BAC484`

```
Makes the specified ped attack the target ped.  
p2 should be 0  
p3 should be 16  
```


* **ped**: 
* **targetPed**: 
* **p2**: 
* **p3**:

### TASK_COMBAT_HATED_TARGETS_AROUND_PED

`void TASK::TASK_COMBAT_HATED_TARGETS_AROUND_PED(Ped ped, float radius, int combatFlags)` · `0x7BF835BB9E2698C8`

```
Despite its name, it only attacks ONE hated target. The one closest hated target.  
p2 seems to be always 0  
```


* **ped**: 
* **radius**: 
* **p2**:

### SET_PED_COMBAT_ATTRIBUTES

`void PED::SET_PED_COMBAT_ATTRIBUTES(Ped ped, int attributeId, BOOL enabled)` · `0x9F7794730795E019`

These combat attributes seem to be the same as the BehaviourFlags from combatbehaviour.meta.

So far, these are the equivalents found:

```c
enum eCombatAttribute
{
  CA_INVALID = -1,	
  // AI will only use cover if this is set
  CA_USE_COVER = 0,
  // AI will only use vehicles if this is set
  CA_USE_VEHICLE = 1,
  // AI will only driveby from a vehicle if this is set
  CA_DO_DRIVEBYS = 2,
  // Will be forced to stay in a ny vehicel if this isn't set
  CA_LEAVE_VEHICLES = 3,
  // This ped can make decisions on whether to strafe or not based on distance to destination, recent bullet events, etc.
  CA_CAN_USE_DYNAMIC_STRAFE_DECISIONS	= 4,
  // Ped will always fight upon getting threat respons…

### SET_PED_FLEE_ATTRIBUTES

`void PED::SET_PED_FLEE_ATTRIBUTES(Ped ped, int attributeFlags, BOOL enable)` · `0x70A2D1137C8ED7C9`

```
bit 15 (0x8000) = force cower
```


* **ped**: 
* **attributeFlags**: 
* **enable**:

## Rival Crews: police versus crews

### CREATE_INCIDENT_WITH_ENTITY

`BOOL MISC::CREATE_INCIDENT_WITH_ENTITY(int dispatchService, Ped ped, int numUnits, float radius, int* outIncidentID, Any p5, Any p6)` · `0x05983472F0494E60`

```
NativeDB Added Parameter 6: Any p5
NativeDB Added Parameter 7: Any p6
```


* **dispatchService**: Refer to [ENABLE_DISPATCH_SERVICE](#_0xDC0F817884CDD856) for dispatch types
* **ped**: 
* **numUnits**: 
* **radius**: 
* **outIncidentID**: 


Returns true if the incident was successfully made

### CREATE_INCIDENT

`BOOL MISC::CREATE_INCIDENT(int dispatchService, float x, float y, float z, int numUnits, float radius, int* outIncidentID, Any p7, Any p8)` · `0x3F892CAF67444AE7`

```
NativeDB Added Parameter 8: Any p7
NativeDB Added Parameter 9: Any p8
```


* **dispatchService**: Refer to [ENABLE_DISPATCH_SERVICE](#_0xDC0F817884CDD856) for dispatch types
* **x**: 
* **y**: 
* **z**: 
* **numUnits**: 
* **radius**: 
* **outIncidentID**:

### DELETE_INCIDENT

`void MISC::DELETE_INCIDENT(int incidentId)` · `0x556C1AA270D5A207`

```
Delete an incident with a given id.  
=======================================================  
Correction, I have change this to int, instead of int*  
as it doesn't use a pointer to the createdIncident.  
If you try it you will crash (or) freeze.  
=======================================================  
```


* **incidentId**:

### IS_INCIDENT_VALID

`BOOL MISC::IS_INCIDENT_VALID(int incidentId)` · `0xC8BC6461E629BEAA`

```
=======================================================  
Correction, I have change this to int, instead of int*  
as it doesn't use a pointer to the createdIncident.  
If you try it you will crash (or) freeze.  
=======================================================  
```


* **incidentId**:

### REPORT_CRIME

`void PLAYER::REPORT_CRIME(Player player, int crimeType, int wantedLvlThresh)` · `0xE9B09589827545E7`

```
PLAYER::REPORT_CRIME(PLAYER::PLAYER_ID(), 37, PLAYER::GET_WANTED_LEVEL_THRESHOLD(1));  
From am_armybase.ysc.c4:  
PLAYER::REPORT_CRIME(PLAYER::PLAYER_ID(4), 36, PLAYER::GET_WANTED_LEVEL_THRESHOLD(4));  
-----  
This was taken from the GTAV.exe v1.334. The function is called sub_140592CE8. For a full decompilation of the function, see here: pastebin.com/09qSMsN7   
-----  
crimeType:  
1: Firearms possession  
2: Person running a red light ("5-0-5")  
3: Reckless driver  
4: Speeding vehicle (a "5-10")  
5: Traffic violation (a "5-0-5")  
6: Motorcycle rider without a helmet  
7: Vehicle theft (a "5-0-3")  
8: Grand Theft Auto  
9: ???  
10: ???  
11: Assault on a civilian (a "2-40")  
1…

## Fronts and guards

### TASK_GUARD_CURRENT_POSITION

`void TASK::TASK_GUARD_CURRENT_POSITION(Ped ped, float maxPatrolProximity, float defensiveAreaRadius, BOOL setDefensiveArea)` · `0x4A58A47A72E3FCB4`

```
From re_prisonvanbreak:
TASK::TASK_GUARD_CURRENT_POSITION(l_DD, 35.0, 35.0, 1);
```


* **p0**: 
* **p1**: 
* **p2**: 
* **p3**:

### TASK_START_SCENARIO_AT_POSITION

`void TASK::TASK_START_SCENARIO_AT_POSITION(Ped ped, const char* scenarioName, float x, float y, float z, float heading, int duration, BOOL sittingScenario, BOOL teleport)` · `0xFA4EFC79F69D4F07`

The ped will move or warp to the position and heading given, then start the scenario passed. See [`TASK_START_SCENARIO_IN_PLACE`](#_0x142A02425FF02BD9) for a list of scenarios.


* **ped**: The ped handle.
* **scenarioName**: The name of the scenario to start.
* **x**: The x coordinate to start at.
* **y**: The y coordinate to start at.
* **z**: The z coordinate to start at. Should be the ground position plus the ped's ground to root offset.
* **heading**: The heading to start at.
* **timeToLeave**: Sets the timeout on the scenario point to the specified time in milliseconds. If less than 0, sets the `IdleForever` flag.
* **playIntro**: If the ped should play the scenarios enter animation. S…

### TASK_START_SCENARIO_IN_PLACE

`void TASK::TASK_START_SCENARIO_IN_PLACE(Ped ped, const char* scenarioName, int unkDelay, BOOL playEnterAnim)` · `0x142A02425FF02BD9`

Puts the ped into the given scenario immediately at their current location. [List of scenario names](https://pastebin.com/6mrYTdQv) or in `update/update.rpf/common/data/ai/scenarios.meta`.


* **ped**: The ped handle.
* **scenarioName**: The name of the scenario to start.
* **timeToLeave**: Sets the timeout on the scenario point to the specified time in milliseconds. If less than 0, sets the `IdleForever` flag.
* **playIntroClip**: If the ped should play the scenarios enter animation. Sets the `SkipEnterClip` flag.

### SET_PED_SPHERE_DEFENSIVE_AREA

`void PED::SET_PED_SPHERE_DEFENSIVE_AREA(Ped ped, float x, float y, float z, float radius, BOOL p5, BOOL p6)` · `0x9D3151A373974804`

* **ped**: 
* **x**: 
* **y**: 
* **z**: 
* **radius**: 
* **p5**: 
* **p6**:

### SET_PED_COMBAT_RANGE

`void PED::SET_PED_COMBAT_RANGE(Ped ped, int combatRange)` · `0x3C606747B23E497B`

Define the scope within which the ped will engage in combat with the target.

```c
enum eCombatRange {
    CR_NEAR = 0, // keeps within 5-15m
    CR_MEDIUM = 1, // keeps within 7-30m
    CR_FAR = 2, // keeps within 15-40m
    CR_VERY_FAR = 3 // keeps within 22-45m
};
```


* **ped**: Ped index
* **range**: See `eCombatRange` enum.

### SET_PED_COMBAT_MOVEMENT

`void PED::SET_PED_COMBAT_MOVEMENT(Ped ped, int combatMovement)` · `0x4D9CA1009AFBD057`

```
0 - Stationary (Will just stand in place)  
1 - Defensive (Will try to find cover and very likely to blind fire)  
2 - Offensive (Will attempt to charge at enemy but take cover as well)  
3 - Suicidal Offensive (Will try to flank enemy in a suicidal attack)  
```


* **ped**: 
* **combatMovement**:

### SET_PED_ACCURACY

`void PED::SET_PED_ACCURACY(Ped ped, int accuracy)` · `0x7AEFB85C1D49DEB6`

```
accuracy = 0-100, 100 being perfectly accurate
```


* **ped**: 
* **accuracy**:

### SET_PED_ARMOUR

`void PED::SET_PED_ARMOUR(Ped ped, int amount)` · `0xCEA04D83135264CC`

```
Sets the armor of the specified ped.  
ped: The Ped to set the armor of.  
amount: A value between 0 and 100 indicating the value to set the Ped's armor to.  
```


* **ped**: 
* **amount**:

### GIVE_WEAPON_TO_PED

`void WEAPON::GIVE_WEAPON_TO_PED(Ped ped, Hash weaponHash, int ammoCount, BOOL isHidden, BOOL bForceInHand)` · `0xBF0FD6E56C964FCB`

* **ped**: 
* **weaponHash**: 
* **ammoCount**: 
* **isHidden**: 
* **bForceInHand**:

## Native look: map and alerts

### ADD_BLIP_FOR_COORD

`Blip HUD::ADD_BLIP_FOR_COORD(float x, float y, float z)` · `0x5A039BB0BCA604B6`

Creates a blip for the specified coordinates. You can use `SET_BLIP_` natives to change the blip.


* **x**: The X coordinate to create the blip on.
* **y**: The Y coordinate.
* **z**: The Z coordinate.


A blip handle.


```lua
local destination = AddBlipForCoord(200.0, 200.0, 5.0)
```

_CitizenFX docs include an example._

### ADD_BLIP_FOR_RADIUS

`Blip HUD::ADD_BLIP_FOR_RADIUS(float posX, float posY, float posZ, float radius)` · `0x46818D79B1F7499A`

Create a blip with a radius for the specified coordinates (it doesnt create the blip sprite, so you need to use [AddBlipCoords](#_0xC6F43D0E))

Example image:
![example](https://i.imgur.com/fDCmHVD.png)


* **posX**: The x position of the blip (you can also send a vector3 instead of the bulk coordinates)
* **posY**: The y position of the blip (you can also send a vector3 instead of the bulk coordinates)
* **posZ**: The z position of the blip (you can also send a vector3 instead of the bulk coordinates)
* **radius**: The number that defines the radius of the blip circle


The blip handle that can be manipulated with every `SetBlip` natives



```lua
local coords = vector3(0.0, 0.0, 0.0)

loca…

_CitizenFX docs include an example._

### SET_BLIP_SPRITE

`void HUD::SET_BLIP_SPRITE(Blip blip, int spriteId)` · `0xDF735600A4696DAF`

Sets the displayed sprite for a specific blip.

There's a [list of sprites](https://docs.fivem.net/docs/game-references/blips/) on the FiveM documentation site.


* **blip**: The blip to change.
* **spriteId**: The sprite ID to set.


```lua
SetBlipSprite(blip, 588) -- changes `blip` to radar_nhp_orbit
```

_CitizenFX docs include an example._

### SET_BLIP_COLOUR

`void HUD::SET_BLIP_COLOUR(Blip blip, int color)` · `0x03D7FB09E75D6B7E`

* **blip**: The blip handle to set the color of
* **color**: This can be a hex color code, or a [blip color](https://docs.fivem.net/docs/game-references/blips/#blip-colors).


```lua
local coords = GetEntityCoords(PlayerPedId())
local blip = AddBlipForCoord(coords.x, coords.y, coords.z)

SetBlipColour(blip, 1) -- Sets the color to red (see list above)

-- You can also use a hex like so:
SetBlipColour(blip, 0x3A7F3AFF) -- Our custom green
```

_CitizenFX docs include an example._

### SET_BLIP_AS_SHORT_RANGE

`void HUD::SET_BLIP_AS_SHORT_RANGE(Blip blip, BOOL toggle)` · `0xBE8BE4FE60E27B72`

Sets whether or not the specified blip should only be displayed when nearby, or on the minimap.


* **blip**: The blip handle.
* **toggle**: True to only display the blip as 'short range', false to display the blip from a longer distance.

### BEGIN_TEXT_COMMAND_SET_BLIP_NAME

`void HUD::BEGIN_TEXT_COMMAND_SET_BLIP_NAME(const char* textLabel)` · `0xF9113A30DE5C6670`

Starts a text command to change the name of a blip displayed in the pause menu.

This should be paired with [`END_TEXT_COMMAND_SET_BLIP_NAME`](#_0xBC38B49BCB83BC9B), once adding all required text components.


* **textLabel**: The text label to set.


```lua
-- creates a blip called 'Food for me!' at 0.0, 0.0, 0.0
AddTextEntry('MYBLIP', 'Food for ~a~!')

local blip = AddBlipForCoord(0.0, 0.0, 0.0)
BeginTextCommandSetBlipName('MYBLIP')
AddTextComponentSubstringPlayerName('me')
EndTextCommandSetBlipName(blip)
```

_CitizenFX docs include an example._

### BEGIN_TEXT_COMMAND_THEFEED_POST

`void HUD::BEGIN_TEXT_COMMAND_THEFEED_POST(const char* text)` · `0x202709F4C58A0424`

```
Declares the entry type of a notification, for example "STRING".
int ShowNotification(char *text)
{
	BEGIN_TEXT_COMMAND_THEFEED_POST("STRING");
	ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME(text);
	return _DRAW_NOTIFICATION(1, 1);
}
```


* **text**: The text label to use.


```lua
BeginTextCommandThefeedPost("STRING")
AddTextComponentSubstringPlayerName("Hello " .. GetPlayerName(PlayerId()) .. ".")
EndTextCommandThefeedPostTicker(true, true)
```

_CitizenFX docs include an example._

### END_TEXT_COMMAND_THEFEED_POST_MESSAGETEXT

`int HUD::END_TEXT_COMMAND_THEFEED_POST_MESSAGETEXT(const char* txdName, const char* textureName, BOOL flash, int iconType, const char* sender, const char* subject)` · `0x1CCD9A37359072CF`

Sets some extra options for a notification. It adds an image (or icon type) and sets a notification title (sender) and subtitle (subject).

Texture dictionary and texture name parameters are usually the same exact value.

Example result:

![](https://i.imgur.com/c2P5KBn.png)

Old description with list of possible icons and texture names:

```
List of picNames: pastebin.com/XdpJVbHz  
flash is a bool for fading in.  
iconTypes:  
1 : Chat Box  
2 : Email  
3 : Add Friend Request  
4 : Nothing  
5 : Nothing  
6 : Nothing  
7 : Right Jumping Arrow  
8 : RP Icon  
9 : $ Icon  
"sender" is the very top header. This can be any old string.  
"subject" is the header under the sender.  
```


* **tex…

_CitizenFX docs include an example._

### BEGIN_TEXT_COMMAND_DISPLAY_HELP

`void HUD::BEGIN_TEXT_COMMAND_DISPLAY_HELP(const char* inputType)` · `0x8509B634FBE7DA11`

```
Used to be known as _SET_TEXT_COMPONENT_FORMAT  
```


* **inputType**:

### END_TEXT_COMMAND_DISPLAY_HELP

`void HUD::END_TEXT_COMMAND_DISPLAY_HELP(int p0, BOOL loop, BOOL beep, int shape)` · `0x238FFE5C7B0498A6`

* **shape**: 0 for a normal rectangle shape, 1 and 2 has rounded edges, and can be used for floating help text hud component `FLOATING_HELP_TEXT_1` and `FLOATING_HELP_TEXT_2`. Use 1 for `FLOATING_HELP_TEXT_1` and 2 for `FLOATING_HELP_TEXT_2`, otherwise the help text will be reset to a normal help text on the top left corner on your screen.
* **loop**: If this is true, the message will stay forever unless you call [`CLEAR_ALL_HELP_MESSAGES`](#_0x6178F68A87A4D3A0).
* **beep**: If this is true, the beeping sound will play. Beeping sound name is `INFO` in sound set `HUD_FRONTEND_DEFAULT_SOUNDSET`.
* **duration**: Duration in ms of how long this help message should display for. -1 for default.


…

_CitizenFX docs include an example._

### BEGIN_TEXT_COMMAND_PRINT

`void HUD::BEGIN_TEXT_COMMAND_PRINT(const char* GxtEntry)` · `0xB87A37EEB7FAA67D`

```
Used to be known as _SET_TEXT_ENTRY_2  
void ShowSubtitle(char *text)  
{  
	BEGIN_TEXT_COMMAND_PRINT("STRING");  
	ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME(text);  
	END_TEXT_COMMAND_PRINT(2000, 1);  
}  
```


* **GxtEntry**:

### END_TEXT_COMMAND_PRINT

`void HUD::END_TEXT_COMMAND_PRINT(int duration, BOOL drawImmediately)` · `0x9D77056A530643F6`

```
Draws the subtitle at middle center of the screen.  
int duration = time in milliseconds to show text on screen before disappearing  
drawImmediately = If true, the text will be drawn immediately, if false, the text will be drawn after the previous subtitle has finished  
Used to be known as _DRAW_SUBTITLE_TIMED  
```


* **duration**: 
* **drawImmediately**:

## Native look: banners and sounds

### REQUEST_SCALEFORM_MOVIE

`int GRAPHICS::REQUEST_SCALEFORM_MOVIE(const char* scaleformName)` · `0x11FE353CF9733E6F`

* **scaleformName**:

### HAS_SCALEFORM_MOVIE_LOADED

`BOOL GRAPHICS::HAS_SCALEFORM_MOVIE_LOADED(int scaleformHandle)` · `0x85F01B8D5B90570E`

* **scaleformHandle**:

### BEGIN_SCALEFORM_MOVIE_METHOD

`BOOL GRAPHICS::BEGIN_SCALEFORM_MOVIE_METHOD(int scaleform, const char* methodName)` · `0xF6E48914C7A8694E`

```
Push a function from the Scaleform onto the stack  
```


* **scaleform**: 
* **methodName**:

### END_SCALEFORM_MOVIE_METHOD

`void GRAPHICS::END_SCALEFORM_MOVIE_METHOD()` · `0xC6796A8FFA375E53`

```
Pops and calls the Scaleform function on the stack  
```

### DRAW_SCALEFORM_MOVIE_FULLSCREEN

`void GRAPHICS::DRAW_SCALEFORM_MOVIE_FULLSCREEN(int scaleform, int red, int green, int blue, int alpha, int p5)` · `0x0DF606929C105BE1`

```
unk is not used so no need  
```


* **scaleform**: 
* **red**: 
* **green**: 
* **blue**: 
* **alpha**: 
* **unk**:

### PLAY_SOUND_FRONTEND

`void AUDIO::PLAY_SOUND_FRONTEND(int soundId, const char* audioName, const char* audioRef, BOOL p3)` · `0x67C540AA08E4A6F5`

```
List: https://pastebin.com/DCeRiaLJ
All occurrences as of Cayo Perico Heist DLC (b2189), sorted alphabetically and identical lines removed: https://git.io/JtLxM
```


* **soundId**: 
* **audioName**: 
* **audioRef**: 
* **p3**:

### REQUEST_STREAMED_TEXTURE_DICT

`void GRAPHICS::REQUEST_STREAMED_TEXTURE_DICT(const char* textureDict, BOOL p1)` · `0xDFA2EF8E04127DD5`

```
This function can requests texture dictonaries from following RPFs:
scaleform_generic.rpf
scaleform_minigames.rpf
scaleform_minimap.rpf
scaleform_web.rpf
last param isnt a toggle
```


* **textureDict**: 
* **p1**:

## Vault Drag

### ROPE_LOAD_TEXTURES

`void PHYSICS::ROPE_LOAD_TEXTURES()` · `0x9B9039DBF2D258C1`

```
Loads rope textures for all ropes in the current scene.
```

### ADD_ROPE

`int PHYSICS::ADD_ROPE(float x, float y, float z, float rotX, float rotY, float rotZ, float length, int ropeType, float maxLength, float minLength, float windingSpeed, BOOL p11, BOOL p12, BOOL rigid, float p14, BOOL breakWhenShot, Any* unkPtr)` · `0xE832D760399EB220`

```
Creates a rope at the specific position, that extends in the specified direction when not attached to any entities.
__
Rope does NOT interact with anything you attach it to, in some cases it make interact with the world AFTER it breaks (seems to occur if you set the type to -1).
Rope will sometimes contract and fall to the ground like you'd expect it to, but since it doesn't interact with the world the effect is just jaring.
```

There are 8 different rope types in the base game. Full rope data can be found in `ropedata.xml`.

```c
enum ePhysicsRopeType {
    RopeThin = 0, // Verticies: 1, Radius: 0.03, Textures: rope & rope_n
    RopeWire6 = 1, // Verticies: 4, Radius: 0.015, Textures: …

_CitizenFX docs include an example._

### ATTACH_ENTITIES_TO_ROPE

`void PHYSICS::ATTACH_ENTITIES_TO_ROPE(int ropeId, Entity ent1, Entity ent2, float ent1_x, float ent1_y, float ent1_z, float ent2_x, float ent2_y, float ent2_z, float length, BOOL p10, BOOL p11, Any* p12, Any* p13)` · `0x3D95EC8B6D940AC3`

```
Attaches entity 1 to entity 2.  
```


* **ropeId**: 
* **ent1**: 
* **ent2**: 
* **ent1_x**: 
* **ent1_y**: 
* **ent1_z**: 
* **ent2_x**: 
* **ent2_y**: 
* **ent2_z**: 
* **length**: 
* **p10**: 
* **p11**: 
* **boneName1**: 
* **boneName2**:

### DELETE_ROPE

`void PHYSICS::DELETE_ROPE(int* ropeId)` · `0x52B4829281364649`

Deletes the rope with the specified handle.

You should check if the rope exists before trying to delete it, see [DOES_ROPE_EXIST](#_0xFD5448BE3111ED96).

For an example on how to use this native please refer to [ADD_ROPE](#_0xE832D760399EB220)


* **ropeId**: The handle of the rope to delete

### START_ROPE_WINDING

`void PHYSICS::START_ROPE_WINDING(int ropeId)` · `0x1461C72C889E343E`

* **ropeId**:

### CREATE_MODEL_HIDE

`void ENTITY::CREATE_MODEL_HIDE(float x, float y, float z, float radius, Hash modelHash, BOOL p5)` · `0x8A97BCA30A0CE478`

Hides all objects of the specified model intersecting a sphere centered at (x, y, z) with the given radius.
Network players do not see changes done with this.
Use [`CREATE_MODEL_HIDE_EXCLUDING_SCRIPT_OBJECTS`](#_0x3A52AE588830BF7F) if you only want to hide map objects and exclude those created by scripts.
Use [`REMOVE_MODEL_HIDE`](#_0xD9E3006FB3CBD765) to make hidden objects visible again.


* **x**: X Coordinate of the hiding sphere center.
* **y**: Y Coordinate of the hiding sphere center.
* **z**: Z Coordinate of the hiding sphere center.
* **radius**: Radius of the hiding sphere.
* **model**: The object's model hash to hide.
* **surviveMapReload**: If false, only currently loaded objects…

### REMOVE_MODEL_HIDE

`void ENTITY::REMOVE_MODEL_HIDE(float x, float y, float z, float radius, Hash modelHash, BOOL p5)` · `0xD9E3006FB3CBD765`

Makes all objects of the specified model that were hidden using [`CREATE_MODEL_HIDE`](#_0x8A97BCA30A0CE478) or [`CREATE_MODEL_HIDE_EXCLUDING_SCRIPT_OBJECTS`](#_0x3A52AE588830BF7F) visible again.

If lazy is false, all matching objects currently in scope are restored immediately.
If lazy is true, objects will only reappear when their map is reloaded.



* **x**: X Coordinate of the sphere center.
* **y**: Y Coordinate of the sphere center.
* **z**: Z Coordinate of the sphere center.
* **radius**: Radius of the sphere.
* **model**: The model hash to make visible again.
* **lazy**: false to apply the change immediately.


```lua
-- This could be any coordinates, in this example I am using the p…

_CitizenFX docs include an example._

### CREATE_OBJECT

`Object OBJECT::CREATE_OBJECT(Hash modelHash, float x, float y, float z, BOOL isNetwork, BOOL bScriptHostObj, BOOL dynamic)` · `0x509D5878EB39E842`

Creates an object (prop) with the specified model at the specified position, offset on the Z axis by the radius of the object's model.
This object will initially be owned by the creating script as a mission entity, and the model should be loaded already (e.g. using REQUEST_MODEL).


* **modelHash**: The model to spawn.
* **x**: Spawn coordinate X component.
* **y**: Spawn coordinate Y component.
* **z**: Spawn coordinate Z component, 'ground level'.
* **isNetwork**: Whether to create a network object for the object. If false, the object exists only locally.
* **netMissionEntity**: Whether to register the object as pinned to the script host in the R* network model.
* **doorFlag**: False to cr…

_CitizenFX docs include an example._

### SET_OBJECT_PHYSICS_PARAMS

`void OBJECT::SET_OBJECT_PHYSICS_PARAMS(Object object, float weight, float p2, float p3, float p4, float p5, float gravity, float p7, float p8, float p9, float p10, float buoyancy)` · `0xF6DF6E90DE7DF90F`

```
rage::phArchetypeDamp
p9: Some phBoundGeometry margin value, limited to (0.0, 0.1) exclusive.
```


* **object**: 
* **mass**: 
* **gravityFactor**: 
* **linearC**: 
* **linearV**: 
* **linearV2**: 
* **angularC**: 
* **angularV**: 
* **angularV2**: 
* **p9**: 
* **maxAngSpeed**: 
* **buoyancyFactor**:

### HAS_ENTITY_COLLIDED_WITH_ANYTHING

`BOOL ENTITY::HAS_ENTITY_COLLIDED_WITH_ANYTHING(Entity entity)` · `0x8BAD02F0368D9E14`

```
Called on tick.  
Tested with vehicles, returns true whenever the vehicle is touching any entity.  
Note: for vehicles, the wheels can touch the ground and it will still return false, but if the body of the vehicle touches the ground, it will return true.  
```


* **entity**:

### GET_ENTITY_SPEED

`float ENTITY::GET_ENTITY_SPEED(Entity entity)` · `0xD5037BA82E12416F`

Get the speed of a entity.


* **entity**: Entity id you want to test for speed.


Speed of a entity in m/s. You can convert to km/h using the value you get and multiply by 3.6, and miles/h by 2.236936. Also, the float returned is the length of [GET_ENTITY_SPEED_VECTOR](#_0x9A8D700A51CB7B0D)

### CREATE_AMBIENT_PICKUP

`Object OBJECT::CREATE_AMBIENT_PICKUP(Hash pickupHash, float posX, float posY, float posZ, int flags, int value, Hash modelHash, BOOL p7, BOOL p8)` · `0x673966A0C0FD7171`

Creates an ambient pickup given the hash. Pickup hashes can be found [here](https://gist.github.com/4mmonium/1eabfb6b3996e3aa6b9525a3eccf8a0b).


* **pickupHash**: 
* **posX**: 
* **posY**: 
* **posZ**: 
* **flags**: 
* **value**: 
* **modelHash**: 
* **returnHandle**: 
* **p8**:

### APPLY_FORCE_TO_ENTITY

`void ENTITY::APPLY_FORCE_TO_ENTITY(Entity entity, int forceFlags, float x, float y, float z, float offX, float offY, float offZ, int boneIndex, BOOL isDirectionRel, BOOL ignoreUpVec, BOOL isForceRel, BOOL p12, BOOL p13)` · `0xC5F68BE9613E2D18`

```c
enum eApplyForceTypes {
    APPLY_TYPE_FORCE = 0,
    APPLY_TYPE_IMPULSE = 1,
    APPLY_TYPE_EXTERNAL_FORCE = 2,
    APPLY_TYPE_EXTERNAL_IMPULSE = 3,
    APPLY_TYPE_TORQUE = 4,
    APPLY_TYPE_ANGULAR_IMPULSE = 5
}
```


* **entity**: The entity handle
* **forceType**: The force type
* **x**: The x component of the force to apply
* **y**: The y component of the force to apply
* **z**: The z component of the force to apply
* **offX**: Offset from center of entity (X)
* **offY**: Offset from center of entity (Y)
* **offZ**: Offset from center of entity (Z)
* **nComponent**: Component of the entity to apply the force too. Only matters for breakable or articulated (ragdoll) physics. 0 means …

_CitizenFX docs include an example._

## Accident Contracts

### GET_PED_CAUSE_OF_DEATH

`Hash PED::GET_PED_CAUSE_OF_DEATH(Ped ped)` · `0x16FFE42AB2D2DC59`

```
Returns the hash of the weapon/model/object that killed the ped.  
```


* **ped**:

### GET_PED_SOURCE_OF_DEATH

`Entity PED::GET_PED_SOURCE_OF_DEATH(Ped ped)` · `0x93C8B64DEB84728C`

```
Returns the Entity (Ped, Vehicle, or ?Object?) that killed the 'ped'  
Is best to check if the Ped is dead before asking for its killer.  
```


* **ped**:

### GET_PED_TIME_OF_DEATH

`int PED::GET_PED_TIME_OF_DEATH(Ped ped)` · `0x1E98817B311AE98A`

* **ped**:

### HAS_ENTITY_CLEAR_LOS_TO_ENTITY

`BOOL ENTITY::HAS_ENTITY_CLEAR_LOS_TO_ENTITY(Entity entity1, Entity entity2, int traceType)` · `0xFCDFF7B72D23A1AC`

Checks if entity1 has a clear line of sight to entity2. So a simple raycast which if it collides with any of the given colliderTypes returns false.

The direction of the check matters with for example bushes, so checking from inside to outside a bush with traceType 256 returns true, but the other way around returns false.


* **entity1**: Entity to start from
* **entity2**: Entity to check to
* **flags**: A bit mask with bits 1, 2, 4, etc, relating to collider types (see: [START_EXPENSIVE_SYNCHRONOUS_SHAPE_TEST_LOS_PROBE](#_0x377906D8A31E5586)). 17 is most common in GTA scripts.


Returns `true` if no collider of specified bitmask is hit.

### ADD_EXPLOSION

`void FIRE::ADD_EXPLOSION(float x, float y, float z, int explosionType, float damageScale, BOOL isAudible, BOOL isInvisible, float cameraShake, BOOL noDamage)` · `0xE3AD2BDBAEE269AC`

```
NativeDB Added Parameter 9: BOOL noDamage
```

```
BOOL isAudible = If explosion makes a sound.  
BOOL isInvisible = If the explosion is invisible or not.
BOOL noDamage = false: damage || nodamage = true: no damage
```

```c
enum eExplosionTag
{
	DONTCARE = -1,
	GRENADE = 0,
	GRENADELAUNCHER = 1,
	STICKYBOMB = 2,
	MOLOTOV = 3,
	ROCKET = 4,
	TANKSHELL = 5,
	HI_OCTANE = 6,
	CAR = 7,
	PLANE = 8,
	PETROL_PUMP = 9,
	BIKE = 10,
	DIR_STEAM = 11,
	DIR_FLAME = 12,
	DIR_WATER_HYDRANT = 13,
	DIR_GAS_CANISTER = 14,
	BOAT = 15,
	SHIP_DESTROY = 16,
	TRUCK = 17,
	BULLET = 18,
	SMOKE_GRENADE_LAUNCHER = 19,
	SMOKE_GRENADE = 20,
	BZGAS = 21,
	FLARE = 22,
	GAS_CANISTER = 23,
	EXTINGUISHER = 24,
	PROGRAMMAB…

### ADD_OWNED_EXPLOSION

`void FIRE::ADD_OWNED_EXPLOSION(Ped ped, float x, float y, float z, int explosionType, float damageScale, BOOL isAudible, BOOL isInvisible, float cameraShake)` · `0x172AA1B624FA1013`

* **ped**: 
* **x**: 
* **y**: 
* **z**: 
* **explosionType**: See [`ADD_EXPLOSION`](#_0xE3AD2BDBAEE269AC).
* **damageScale**: 
* **isAudible**:  If explosion makes a sound.
* **isInvisible**: If the explosion is invisible or not.
* **cameraShake**:

### SET_PED_TO_RAGDOLL

`BOOL PED::SET_PED_TO_RAGDOLL(Ped ped, int time1, int time2, int ragdollType, BOOL p4, BOOL p5, BOOL p6)` · `0xAE99FB955581844A`

p4/p5: Unusued in TU27

### Ragdoll Types
**0**: CTaskNMRelax
**1**: CTaskNMScriptControl: Hardcoded not to work in networked environments.
**Else**: CTaskNMBalance


* **ped**: The ped to ragdoll.
* **minTime**: Time(ms) Ped is in ragdoll mode; only applies to ragdoll types 0 and not 1.
* **maxTime**:
* **ragdollType**: 
* **bAbortIfInjured**: unused
* **bAbortIfDead**: unused
* **bForceScriptControl**:

### SET_VEHICLE_ENGINE_HEALTH

`void VEHICLE::SET_VEHICLE_ENGINE_HEALTH(Vehicle vehicle, float health)` · `0x45F6D8EEF34ABEF1`

```
1000 is max health
Begins leaking gas at around 650 health
-999.90002441406 appears to be minimum health, although nothing special occurs <- false statement
-------------------------
Minimum: -4000
Maximum: 1000
-4000: Engine is destroyed
0 and below: Engine catches fire and health rapidly declines
300: Engine is smoking and losing functionality
1000: Engine is perfect
```


* **vehicle**: 
* **health**:

### SET_VEHICLE_HANDLING_FLOAT

**Missing in NativeDB.** Not a single-player native.

## Chase pack

### SET_VEHICLE_TYRE_BURST

`void VEHICLE::SET_VEHICLE_TYRE_BURST(Vehicle vehicle, int index, BOOL onRim, float p3)` · `0xEC6A202EE4960385`

```
"To burst tyres VEHICLE::SET_VEHICLE_TYRE_BURST(vehicle, 0, true, 1000.0)  
to burst all tyres type it 8 times where p1 = 0 to 7.  
p3 seems to be how much damage it has taken. 0 doesn't deflate them, 1000 completely deflates them.  
'0 = wheel_lf / bike, plane or jet front  
'1 = wheel_rf  
'2 = wheel_lm / in 6 wheels trailer, plane or jet is first one on left  
'3 = wheel_rm / in 6 wheels trailer, plane or jet is first one on right  
'4 = wheel_lr / bike rear / in 6 wheels trailer, plane or jet is last one on left  
'5 = wheel_rr / in 6 wheels trailer, plane or jet is last one on right  
'45 = 6 wheels trailer mid wheel left  
'47 = 6 wheels trailer mid wheel right  
```


* **vehicle*…

### SET_VEHICLE_LIGHTS

`void VEHICLE::SET_VEHICLE_LIGHTS(Vehicle vehicle, int state)` · `0x34E710FF01247C5A`

Sets the vehicle lights state. Allowing for different lighting modes.

```
NativeDB Introduced: v323
```

```c
enum eVehicleLightSetting {
    // Normal light behavior. Lights cycle through off, then low beams, then high beams.
    // Note: It's affected by day or night; high beams don't exist in daytime.
    NO_VEHICLE_LIGHT_OVERRIDE = 0,
    // Vehicle doesn't have lights, always off.
    FORCE_VEHICLE_LIGHTS_OFF  = 1, 
    // Vehicle has always-on lights.
    // During day: Cycles between low beams and high beams. 
    // At night: Cycles between low beams, low beams, and high beams.
    FORCE_VEHICLE_LIGHTS_ON   = 2,
    // Sets vehicle lights on. Behaves like normal lights (same as 0).
…

_CitizenFX docs include an example._

### DETACH_VEHICLE_FROM_TRAILER

`void VEHICLE::DETACH_VEHICLE_FROM_TRAILER(Vehicle vehicle)` · `0x90532EDF0D2BDD86`

* **vehicle**:

### SET_VEHICLE_BOOST_ACTIVE

`void AUDIO::SET_VEHICLE_BOOST_ACTIVE(Vehicle vehicle, BOOL toggle)` · `0x4A04DE7CAB2739A1`

```
SET_VEHICLE_BOOST_ACTIVE(vehicle, 1, 0);  
SET_VEHICLE_BOOST_ACTIVE(vehicle, 0, 0);   
Will give a boost-soundeffect.  
```


* **vehicle**: 
* **toggle**:

### SET_VEHICLE_FORWARD_SPEED

`void VEHICLE::SET_VEHICLE_FORWARD_SPEED(Vehicle vehicle, float speed)` · `0xAB54A438726D25D5`

```
SCALE: Setting the speed to 30 would result in a speed of roughly 60mph, according to speedometer.  
Speed is in meters per second  
You can convert meters/s to mph here:  
http://www.calculateme.com/Speed/MetersperSecond/ToMilesperHour.htm  
```


* **vehicle**: 
* **speed**:

### SET_PLAYER_WANTED_CENTRE_POSITION

`void PLAYER::SET_PLAYER_WANTED_CENTRE_POSITION(Player player, float x, float y, float z)` · `0x520E541A97A13354`

```
# Predominant call signatures  
PLAYER::SET_PLAYER_WANTED_CENTRE_POSITION(PLAYER::PLAYER_ID(), ENTITY::GET_ENTITY_COORDS(PLAYER::PLAYER_PED_ID(), 1));  
# Parameter value ranges  
P0: PLAYER::PLAYER_ID()  
P1: ENTITY::GET_ENTITY_COORDS(PLAYER::PLAYER_PED_ID(), 1)  
P2: Not set by any call  
```


* **player**: 
* **position**: 
* **p2**: 
* **p3**:

### SET_DISPATCH_COPS_FOR_PLAYER

`void PLAYER::SET_DISPATCH_COPS_FOR_PLAYER(Player player, BOOL toggle)` · `0xDB172424876553F4`

* **player**: 
* **toggle**:

### SET_WANTED_LEVEL_DIFFICULTY

`void PLAYER::SET_WANTED_LEVEL_DIFFICULTY(Player player, float difficulty)` · `0x9B0BB33B04405E7A`

```
Max value is 1.0  
```


* **player**: 
* **difficulty**:

