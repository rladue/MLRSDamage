using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MLRS Damage", "iLakSkiL", "1.3.1")]
    [Description("Edits the damage down by the MLRS.")]
    public class MLRSDamage : RustPlugin
    {

        #region Configuration
        private Configuration _config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "MLRS Settings")]
            public DefSettings defsettings = new DefSettings();

            public class DefSettings
            {
                [JsonProperty(PropertyName = "MLRS Damage Modifier")]
                public double damageMod = 1.0;

                [JsonProperty(PropertyName = "Allow Damage to Player Built Bases")]
                public bool pvBase = true;

                [JsonProperty(PropertyName = "Allow Damage to Players")]
                public bool pvPlayer = true;

                [JsonProperty(PropertyName = "Allow Damage to Raidable and Abandoned Bases")]
                public bool raidable = true;

                [JsonProperty(PropertyName = "Allow Damage to NPCs")]
                public bool npc = true;

                [JsonProperty(PropertyName = "MLRS Cooldown time (in minutes)")]
                public double broken = 10;

                [JsonProperty(PropertyName = "Total Rockets for MLRS to fire")]
                public int rocketAmount = 12;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                SaveConfig();
            }
            catch
            {
                PrintError("Error reading config, please check!");
                Unsubscribe(nameof(OnEntityTakeDamage));
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
            Puts("Loading Default Config");
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion

        #region Hooks
        private void Unload()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, "MLRS.brokenDownMinutes 10");
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<MLRS>())
            {
                if (entity == null) continue;
                if (entity is MLRS)
                {
                    var mlrsVeh = entity as MLRS;
                    StorageContainer rocketContainer = mlrsVeh.GetRocketContainer();
                    rocketContainer.inventory.maxStackSize = 6;
                }
            }
            Puts("MLRS cooldown time reset to 10 minutes");
            Puts("MLRS total rockets to fire reset to 12");
        }

        private void OnServerInitialized()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, $"MLRS.brokenDownMinutes {_config.defsettings.broken}"); //Sets MLRS cooldown timer
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<MLRS>())
            {
                if (entity == null) continue;
                if (entity is MLRS)
                {
                    var mlrsVeh = entity as MLRS;
                    StorageContainer rocketContainer = mlrsVeh.GetRocketContainer();
                    rocketContainer.inventory.maxStackSize = (_config.defsettings.rocketAmount / 2); //Sets container size in MLRS rocket holder
                }
            }
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            float newDam = (float)_config.defsettings.damageMod;
            var player = entity as BasePlayer;

            if (entity == null || info == null || info.WeaponPrefab == null) return null; //null checks
            if (info.WeaponPrefab.ShortPrefabName.Equals("rocket_mlrs"))
            {
                if (newDam == null || newDam <= 0) return true; //disables all MLRS damage when modifier is 0 or below

                //Checks Player Damage
                if (entity is BasePlayer && _config.defsettings.pvPlayer) 
                {
                    info.damageTypes.ScaleAll(newDam);
                    return null;
                }
                if (entity is BasePlayer && !_config.defsettings.pvPlayer) return true;

                //Checks for Raidable Bases and Abandoned Bases (bases with 0 ownership)
                if (entity.OwnerID.Equals((ulong)0) && _config.defsettings.raidable) 
                {
                    info.damageTypes.ScaleAll(newDam);
                    return null;
                }
                if (entity.OwnerID.Equals((ulong)0) && !_config.defsettings.raidable) return true;

                //Checks for Base entities owned by players
                if (!entity.OwnerID.Equals((ulong)0) && _config.defsettings.pvBase) 
                {
                    info.damageTypes.ScaleAll(newDam);
                    return null;
                }
                if (!entity.OwnerID.Equals((ulong)0) && !_config.defsettings.pvBase) return true;

                //Checks for NPC Players
                if (_config.defsettings.npc && player is NPCPlayer)
                {
                    info.damageTypes.ScaleAll(newDam);
                    return null;
                }
                if (!_config.defsettings.npc && player is NPCPlayer) return true;

            }
            return null;
        }

        private void OnEntitySpawned(BaseEntity entity) //Modifies any MLRS spawned in after Server Startup
        {
            if (entity == null) return;
            if (entity is MLRS)
            {
                NextTick(() =>
                {
                    var mlrsVeh = entity as MLRS;
                    StorageContainer rocketContainer = mlrsVeh.GetRocketContainer();
                    rocketContainer.inventory.maxStackSize = (_config.defsettings.rocketAmount / 2);
                });
            }
        }

        private void OnMlrsRocketFired(MLRS ent, ServerProjectile serverProjectile) 
        {
            ent.nextRocketIndex = 1; //Makes MLRS Shoot more than default 12 rockets
        }
        #endregion

        #region Commands
        [ConsoleCommand("mlrsdamage.damage")]
        private void Damage(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            double newDamage;
            if (arg.Args == null || !(double.TryParse(arg.Args[0], out newDamage)))
            {
                Puts("Error: Must enter a number!");
                return;
            }
            else
            {
                double.TryParse(arg.Args[0], out newDamage);
                _config.defsettings.damageMod = newDamage;
                SaveConfig();
                Puts($"MLRS damage was successfully changed to: {_config.defsettings.damageMod}");
            }
        }

        [ConsoleCommand("mlrsdamage.cooldown")]
        private void Cooldown(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            double newCooldown;
            if (arg.Args == null || !(double.TryParse(arg.Args[0], out newCooldown)))
            {
                Puts("Error: Must enter a number!");
                return;
            }
            else
            {
                double.TryParse(arg.Args[0], out newCooldown);
                _config.defsettings.broken = newCooldown;
                SaveConfig();
                ConsoleSystem.Run(ConsoleSystem.Option.Server, $"MLRS.brokenDownMinutes {_config.defsettings.broken}");
                Puts($"MLRS cooldown was successfully changed to: {_config.defsettings.broken} minutes");
            }
        }

        [ConsoleCommand("mlrsdamage.pvp")]
        private void PvpEnable(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            bool pvpEnable;
            if (arg.Args == null || !(bool.TryParse(arg.Args[0], out pvpEnable)))
            {
                Puts("Error: Enter either true of false!");
                return;
            }
            else
            {
                bool.TryParse(arg.Args[0], out pvpEnable);
                _config.defsettings.pvPlayer = pvpEnable;
                SaveConfig();
                if (_config.defsettings.pvPlayer) Puts("MLRS Player Damage is Enabled!");
                if (!_config.defsettings.pvPlayer) Puts("MLRS Player Damage is Disabled!");
            }
        }

        [ConsoleCommand("mlrsdamage.pvpbase")]
        private void PvpBaseEnable(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            bool pvpBaseEnable;
            if (arg.Args == null || !(bool.TryParse(arg.Args[0], out pvpBaseEnable)))
            {
                Puts("Error: Enter either true of false!");
                return;
            }
            else
            {
                bool.TryParse(arg.Args[0], out pvpBaseEnable);
                _config.defsettings.pvBase = pvpBaseEnable;
                SaveConfig();
                if (_config.defsettings.pvBase) Puts("MLRS Player Base Damage is Enabled!");
                if (!_config.defsettings.pvBase) Puts("MLRS Player Base Damage is Disabled!");
            }
        }

        [ConsoleCommand("mlrsdamage.raidable")]
        private void RaidableEnable(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            bool raidableEnable;
            if (arg.Args == null || !(bool.TryParse(arg.Args[0], out raidableEnable)))
            {
                Puts("Error: Enter either true of false!");
                return;
            }
            else
            {
                bool.TryParse(arg.Args[0], out raidableEnable);
                _config.defsettings.raidable = raidableEnable;
                SaveConfig();
                if (_config.defsettings.raidable) Puts("MLRS Raidable/Abandoned Base Damage is Enabled!");
                if (!_config.defsettings.raidable) Puts("MLRS Raidable/Abandoned Base Damage is Disabled!");
            }
        }

        [ConsoleCommand("mlrsdamage.npc")]
        private void NpcEnable(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            bool npcEnable;
            if (arg.Args == null || !(bool.TryParse(arg.Args[0], out npcEnable)))
            {
                Puts("Error: Enter either true of false!");
                return;
            }
            else
            {
                bool.TryParse(arg.Args[0], out npcEnable);
                _config.defsettings.npc = npcEnable;
                SaveConfig();
                if (_config.defsettings.npc) Puts("MLRS NPC Damage is Enabled!");
                if (!_config.defsettings.npc) Puts("MLRS NPC Damage is Disabled!");
            }
        }

        [ConsoleCommand("mlrsdamage.rockets")]
        private void RocketsNum(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            int rocketsNum;
            if (arg.Args == null || !(int.TryParse(arg.Args[0], out rocketsNum)))
            {
                Puts("Error: Must enter a whole number!");
                return;
            }
            else
            {
                int.TryParse(arg.Args[0], out rocketsNum);
                _config.defsettings.rocketAmount = rocketsNum;
                SaveConfig();
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<MLRS>())
                {
                    if (entity == null) continue;
                    if (entity is MLRS)
                    {
                        var mlrsVeh = entity as MLRS;
                        StorageContainer rocketContainer = mlrsVeh.GetRocketContainer();
                        rocketContainer.inventory.maxStackSize = (_config.defsettings.rocketAmount / 2); //Sets container size in MLRS rocket holder
                    }
                }
                Puts($"Total MLRS Rocket Capacity set to: {_config.defsettings.rocketAmount} rockets");
            }
        }
        #endregion
    }
}
