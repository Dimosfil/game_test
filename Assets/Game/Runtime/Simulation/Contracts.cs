using System;
using System.Collections.Generic;

namespace ColonyRTS.Simulation
{
    // Coordinates are millimetres in the horizontal X/Z plane. Gameplay owns them.
    [Serializable]
    public struct SimPoint
    {
        public int X;
        public int Z;
        public SimPoint(int x, int z) { X = x; Z = z; }
        public static SimPoint World(float x, float z) => new SimPoint((int)Math.Round(x * 1000), (int)Math.Round(z * 1000));
    }

    public enum MatchPhase { Running, Finished }
    public enum CommandKind { Move, AttackMove, Attack, Gather, Stop, Hold, Build, Train, Cancel, Stim, Patrol, Repair, Research }
    public enum OrderKind { Idle, Move, AttackMove, Attack, Gather, ReturnCargo, Build, Hold, Patrol, Repair }

    [Serializable]
    public sealed class EntityDefinition
    {
        public string Id;
        public string DisplayName;
        public bool IsBuilding;
        public bool IsWorker;
        public bool IsResource;
        public bool IsBase;
        public bool Repairable;
        public int MaxHealth;
        public int Radius;
        public int Speed;
        public int Sight;
        public int Damage;
        public int Armor;
        public int AttackRange;
        public int AttackCooldown;
        public int MineralCost;
        public int GasCost;
        public int SupplyCost;
        public int SupplyProvided;
        public int BuildTicks;
        public string ProducerId;
        public string PrerequisiteId;
        public int ResourceAmount;
    }

    [Serializable]
    public sealed class UpgradeDefinition
    {
        public string Id;
        public string DisplayName;
        public string ProducerId;
        public string PrerequisiteId;
        public string PrerequisiteUpgradeId;
        public int MineralCost;
        public int GasCost;
        public int ResearchTicks;
        public int DamageBonus;
        public int ArmorBonus;
        public string[] AffectedDefinitionIds;
    }

    [Serializable]
    public sealed class MapBlock
    {
        public int X;
        public int Z;
        public int Width;
        public int Height;
    }

    [Serializable]
    public sealed class SpawnDefinition
    {
        public string DefinitionId;
        public int Owner;
        public SimPoint Position;
    }

    [Serializable]
    public sealed class RuleSet
    {
        public string VersionId;
        public string ProductTitle;
        public string MapName;
        public int TickRate;
        public int MapWidth;
        public int MapHeight;
        public int StartingMinerals;
        public int StartingGas;
        public int GatherTicks;
        public int CargoAmount;
        public int StimHealthCost;
        public int StimDurationTicks;
        public int RepairTicks;
        public int RepairHealth;
        public int RepairMineralCost;
        public int RepairGasCost;
        public EntityDefinition[] Definitions;
        public MapBlock[] Blocks;
        public SpawnDefinition[] Spawns;
        public UpgradeDefinition[] Upgrades = new UpgradeDefinition[0];
    }

    [Serializable]
    public sealed class GameCommand
    {
        public int Player;
        public long Sequence;
        public int Tick;
        public CommandKind Kind;
        public int[] EntityIds;
        public int TargetId;
        public SimPoint Target;
        public string DefinitionId;
        public bool Append;
    }

    [Serializable]
    public sealed class CommandResult
    {
        public int Player;
        public long Sequence;
        public bool Accepted;
        public string Reason;
    }

    [Serializable]
    public sealed class UnitOrder
    {
        public OrderKind Kind;
        public SimPoint Target;
        public int TargetId;
        public SimPoint PatrolOrigin;
        public bool PatrolReturning;
    }

    [Serializable]
    public sealed class ProductionItem
    {
        public string DefinitionId;
        public int RemainingTicks;
        public int TotalTicks;
        public int PaidMinerals;
        public int PaidGas;
    }

    [Serializable]
    public sealed class EntityState
    {
        public int Id;
        public string DefinitionId;
        public int Owner;
        public SimPoint Position;
        public int Health;
        public bool Completed;
        public int ConstructionRemaining;
        public int ConstructionTotal;
        public int BuilderId;
        public int AttackTimer;
        public int LastAttackTick = -100;
        public int LastAttackTarget;
        public int Cargo;
        public bool GasCargo;
        public int WorkTimer;
        public int ResourceRemaining;
        public int StimRemaining;
        public int LastSeenTick;
        public SimPoint RallyPoint;
        public int RallyTargetId;
        public int GatherTargetId;
        public List<UnitOrder> Orders = new List<UnitOrder>();
        public List<SimPoint> Path = new List<SimPoint>();
        public int PathIndex;
        public SimPoint PathTarget;
        public int PathStoppingDistance;
        public int AvoidanceSide;
        public List<ProductionItem> Production = new List<ProductionItem>();
        public List<ProductionItem> Research = new List<ProductionItem>();
    }

    [Serializable]
    public sealed class PlayerState
    {
        public int Id;
        public int Minerals;
        public int Gas;
        public int SupplyUsed;
        public int SupplyCap;
        public long IssuedSequence;
        public long LastProcessedSequence;
        public bool[] Explored;
        public List<EntityState> RememberedBuildings = new List<EntityState>();
        public List<string> CompletedUpgrades = new List<string>();
    }

    [Serializable]
    public sealed class BotState
    {
        public int Player;
        public bool Enabled;
        public int NextThinkTick;
        public int AttackWave;
    }

    [Serializable]
    public sealed class MatchState
    {
        public string RuleVersion;
        public string RuleHash;
        public int Seed;
        public uint RngState;
        public int Tick;
        public int NextEntityId = 1;
        public MatchPhase Phase;
        public int Winner = -1;
        public List<EntityState> Entities = new List<EntityState>();
        public PlayerState[] Players;
        public BotState[] Bots;
        public List<GameCommand> PendingCommands = new List<GameCommand>();
        public List<GameCommand> CommandLog = new List<GameCommand>();
    }

    [Serializable]
    public sealed class ReplayData
    {
        public string RuleVersion;
        public string RuleHash;
        public int Seed;
        public int FinalTick;
        public List<GameCommand> Commands = new List<GameCommand>();
    }

    public sealed class PlayerObservation
    {
        public int Tick;
        public MatchPhase Phase;
        public int Winner;
        public int Player;
        public int Minerals;
        public int Gas;
        public int SupplyUsed;
        public int SupplyCap;
        public bool[] Visible;
        public bool[] Explored;
        public List<EntityState> Entities = new List<EntityState>();
        public List<EntityState> RememberedBuildings = new List<EntityState>();
        public List<string> CompletedUpgrades = new List<string>();
    }
}
