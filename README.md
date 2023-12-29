# About MLRS Damage
<p>A must have for PVE servers!

Allows you to modify the damage rate of the MLRS for Player bases, raidable/abandonded bases, and NPCs. Also allows you to adjust the cooldown timer of the MLRS.</p>

<h2>Console Commands</h2>

<strong>mlrsdamage.damage {amount}</strong> - Changes damage multiplier amount.<br />
<strong>mlrsdamage.cooldown {amount}</strong>    - Changes server cooldown timer for MLRS<br />
<strong>mlrsdamage.pvp {true/false}</strong>    - Enable/Disable player pvp damage done by MLRS<br />
<strong>mlrsdamage.pvpbase {true/false}</strong>    - Enable/Disable player base damage done by MLRS<br />
<strong>mlrsdamage.raidable {true/false}</strong>    - Enable/Disable raidable/abandoned base damage done by MLRS<br />
<strong>mlrsdamage.npc {true/false}</strong>    - Enable/Disable NPC damage done by MLRS<br />
<strong>mlrsdamage.rockets {amount}</strong>    - Change total capacity of rockets able to be fired by MLRS<br />
<strong>mlrsdamage.module {true/false}</strong>    - Enable/Disable the need for an Aiming Module<br />
<strong>mlrsdamage.interval {amount}</strong>    - Change the interval between rocket launches<br />

<h2>Configuration</h2>
<code>"MLRS Settings": {<br />
    "MLRS Damage Multiplier": 1.0,                              // scale at which the MLRS does damage<br />
    "Allow Damage to Player entities": true,                    // allow damage to player owned entities<br />
    "Allow Damage to Players": true,                            // allow damage to players<br />
    "Allow Damage to Raidable and Abandoned Bases": true,       // allow damage to Raidable or Abandoned Bases<br />
    "Allow Damage to NPCs": true,                               // allow damage to NPCs on the map<br />
    "MLRS Cooldown time (in minutes)": 10.0                     // cooldown timer for MLRS, default is 10<br />
    "Total Rockets for MLRS to fire": 12                        // total number of rockets to shoot in a single barrage, default is 12<br />
    "Rocket Launch Interval (in seconds)": 0.5,                 // time inbetween rocket launches. Must be positive, but can cause issues if set lower than 0.1<br />
    "Requires Aiming Module": false                             // When set to true, an aiming module is always put in the MLRS and is locked from being looted by players<br />
  }</code>
