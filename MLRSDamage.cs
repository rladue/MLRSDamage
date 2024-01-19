using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using Oxide.Core.Plugins;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MLRS Damage", "iLakSkiL", "1.5.1")]
    [Description("Edits the damage down by the MLRS.")]
    public class MLRSDamage : RustPlugin
    {

        int rocketsFired = 0;

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

                [JsonProperty(PropertyName = "Rocket Launch Interval (in seconds)")]
                public float launchTime = 0.5f;

                [JsonProperty(PropertyName = "Requires Aiming Module")]
                public bool needModule = true;

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
            Puts("MLRS cooldown time reset to 10 minutes");
            SetRocketAmount(12);
            UpdateMLRSContainers(12);
            Puts("MLRS total rockets to fire reset to 12");
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<MLRS>())
            {
                StorageContainer dashboardContainer = entity.GetDashboardContainer();
                dashboardContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
            }
            Puts("Aiming Modules now required to operate MLRS");
        }

        private void Loaded()
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server, $"MLRS.brokenDownMinutes {_config.defsettings.broken}"); //Sets MLRS cooldown timer

            SetRocketAmount(_config.defsettings.rocketAmount);
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<MLRS>())
            {
                if (entity == null || !(entity is MLRS)) return;
                if (!_config.defsettings.needModule) ResetModule(entity);
                NextTick(() =>
                {
                    UpdateMLRSContainers(_config.defsettings.rocketAmount);
                });
            }
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity == null || !(entity is MLRS)) return;
            MLRS mlrs = entity as MLRS;
            if (!_config.defsettings.needModule) ResetModule(mlrs);
            NextTick(() =>
            {
                UpdateMLRSContainer(mlrs, _config.defsettings.rocketAmount);
            });

        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            float newDam = (float)_config.defsettings.damageMod;
            var victim = entity as BasePlayer;

            if (entity == null || info == null || info.WeaponPrefab == null) return null; //null checks
            if (info.WeaponPrefab.ShortPrefabName.Equals("rocket_mlrs"))
            {
                if (newDam == null || newDam <= 0) return true; //disables all MLRS damage when modifier is 0 or below

                //Checks Player Damage
                if (victim is BasePlayer && !victim.IsNpc && _config.defsettings.pvPlayer) 
                {
                    info.damageTypes.ScaleAll(newDam);
                    return null;
                }
                if (victim is BasePlayer && !victim.IsNpc && !_config.defsettings.pvPlayer) return true;


                //Checks for NPC Players
                if (_config.defsettings.npc && (victim is NPCPlayer || entity is BaseNpc || entity is BaseAnimalNPC))
                {
                    info.damageTypes.ScaleAll(newDam);
                    return null;
                }
                if (!_config.defsettings.npc && (victim is NPCPlayer || entity is BaseNpc || entity is BaseAnimalNPC)) return true;

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

            }
            return null;
        }

        private void OnMlrsRocketFired(MLRS ent, ServerProjectile serverProjectile) 
        {
            if ((ent.RocketAmmoCount + rocketsFired) > _config.defsettings.rocketAmount)
            {
                ent.RocketAmmoCount = (_config.defsettings.rocketAmount - rocketsFired);
            }
            if (ent.RocketAmmoCount > 12)
            {
                ent.nextRocketIndex = (int)((ent.RocketAmmoCount % 11) + 1);
                rocketsFired++;
                return;
            }
            else
            {
                ent.nextRocketIndex = ent.RocketAmmoCount - 1;
                rocketsFired++;
                return;
            }
        }

        private object OnMlrsFire(MLRS ent, BasePlayer owner)
        {
            ent.SetFlag(BaseEntity.Flags.Reserved6, true, false, true);
            ent.radiusModIndex = 0;
            if (ent.RocketAmmoCount > 12)
            {
                ent.nextRocketIndex = (int)((ent.RocketAmmoCount % 11) + 1);
            }
            else ent.nextRocketIndex = ent.RocketAmmoCount - 1;
            ent.rocketOwnerRef.Set(owner);
            ent.InvokeRepeating(new Action(ent.FireNextRocket), 0f, _config.defsettings.launchTime);
            Interface.CallHook("OnMlrsFired", ent, owner);
            return true;
        }
        
        private void OnMlrsFiringEnded(MLRS entity)
        {
            if (!_config.defsettings.needModule) ResetModule(entity);
            rocketsFired = 0;
        }

        #endregion

        #region Helpers

        public void SetRocketAmount(int amount)
        {
            _config.defsettings.rocketAmount = amount;
            Puts($"Total MLRS Rocket Capacity set to: {_config.defsettings.rocketAmount} rockets");
        }

        private void UpdateMLRSContainers(int amount)
        {
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<MLRS>())
            {
                if (entity == null || !(entity is MLRS)) return;
                NextTick(() =>
                {
                    var mlrsVeh = entity as MLRS;
                    StorageContainer rocketContainer = mlrsVeh.GetRocketContainer();
                    rocketContainer.inventory.maxStackSize = (amount / 2);
                });
            }
        }

        private void UpdateMLRSContainer(MLRS entity, int amount)
        {
            if (entity == null || !(entity is MLRS)) return;
            NextTick(() =>
            {
                var mlrsVeh = entity as MLRS;
                StorageContainer rocketContainer = mlrsVeh.GetRocketContainer();
                rocketContainer.inventory.maxStackSize = (amount / 2);
            });
        }

        private void ResetModule(MLRS entity)
        {
            timer.Once(2f, () =>
                {
                entity.VehicleFixedUpdate();
                StorageContainer dashboardContainer = entity.GetDashboardContainer();
                if (dashboardContainer.inventory.IsEmpty())
                {
                    dashboardContainer.inventory.AddItem(ItemManager.FindItemDefinition("aiming.module.mlrs"), 1, (ulong)0, ItemContainer.LimitStack.Existing);
                }
                dashboardContainer.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
            });
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
                SetRocketAmount(rocketsNum);
                UpdateMLRSContainers(rocketsNum);
                SaveConfig();
            }
        }

        [ConsoleCommand("mlrsdamage.module")]
        private void NeedModule(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            bool module;
            if (arg.Args == null || !(bool.TryParse(arg.Args[0], out module)))
            {
                Puts("Error: Enter either true of false!");
                return;
            }
            else
            {
                bool.TryParse(arg.Args[0], out module);
                _config.defsettings.needModule = module;
                SaveConfig();
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<MLRS>())
                {
                    if (entity == null || !(entity is MLRS)) return;
                    if (!_config.defsettings.needModule) ResetModule(entity);
                }
                if (_config.defsettings.needModule) Puts("An Aiming Module will be needed needed to activate the MLRS!");
                if (!_config.defsettings.needModule) Puts("An Aiming Module is no longer needed to activate the MLRS!");
            }
        }

        [ConsoleCommand("mlrsdamage.interval")]
        private void LaunchInterval(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin) return;
            float speed;
            if (arg.Args == null || !(float.TryParse(arg.Args[0], out speed)))
            {

                Puts("Error: Must enter a number!");
                return;
            }
            if (speed < 0)
            {
                Puts("Error: Missile interval cannot be a negative number");
                return;
            }
            if (speed == 0 || (speed > 0 && speed < 0.1))
            {
                Puts("CAUTION: It is not advisable to have such a short interval on missile launches. You may encounter server issues.");
                float.TryParse(arg.Args[0], out speed);
                _config.defsettings.launchTime = speed;
                SaveConfig();
                Puts($"MLRS missile launch interval is now set for {_config.defsettings.launchTime} seconds!");
            }
            else
            {
                float.TryParse(arg.Args[0], out speed);
                _config.defsettings.launchTime = speed;
                SaveConfig();
                Puts($"MLRS missile launch interval is now set for {_config.defsettings.launchTime} seconds!");
            }
        }
        #endregion
    }
}
