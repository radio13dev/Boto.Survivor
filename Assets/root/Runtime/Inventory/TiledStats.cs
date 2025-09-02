public static partial class TiledStats
{
    public static readonly TiledStatData[] StatData = new TiledStatData[36]
    {
        new TiledStatData(
            curve.exponential(0.10f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.30f),
            curve.linear(-0.10f),
            curve.zero,
            new string[]{ "Heavy Blow" },
            new string[]{ "Slower but stronger hits" },
            new string[]{ "Damage:" },
            new string[]{ "Attack Speed:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.2f),
            curve.linear(-0.1f),
            curve.zero,
            new string[]{ "Quick Hands" },
            new string[]{ "Faster weapon swings" },
            new string[]{ "Attack Speed:" },
            new string[]{ "Damage:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.1f),
            curve.zero,
            curve.zero,
            new string[]{ "Bloodlust" },
            new string[]{ "Kill feeds your fury" },
            new string[]{ "Damage per Kill (5s):" },
            new string[]{ "" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.linear(1),
            curve.linear(-0.05f),
            curve.zero,
            new string[]{ "Iron Skin" },
            new string[]{ "Fortify your body" },
            new string[]{ "Max HP:" },
            new string[]{ "Move Speed:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.linear(0.15f),
            curve.zero, 
            curve.zero,
            new string[]{ "Swift Step" },
            new string[]{ "Faster on your feet" },
            new string[]{ "Move Speed:" },
            new string[]{ "" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.20f),
            curve.linear(-1),
            curve.zero,
            new string[]{ "Glass Cannon" },
            new string[]{ "Pure offense" },
            new string[]{ "Damage:" },
            new string[]{ "Max HP:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.linear(-0.05f),
            curve.zero, 
            curve.zero,
            new string[]{ "Guardian" },
            new string[]{ "Endurance above all" },
            new string[]{ "Shield Cooldown (100s default):" },
            new string[]{ "" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.constant(0.05f),
            curve.exponential(0.2f),
            curve.zero,
            new string[]{ "Lucky Strike" },
            new string[]{ "Chance for stronger crits" },
            new string[]{ "Crit Chance:" },
            new string[]{ "Crit Damage:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.1f),
            curve.zero,
            curve.zero,
            new string[]{ "Reckless Swing" },
            new string[]{ "Attack with abandon" },
            new string[]{ "Knockback:" },
            new string[]{ "" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.1f),
            curve.zero, 
            curve.zero,
            new string[]{ "Arcane Touch" },
            new string[]{ "Channel raw energy" },
            new string[]{ "Add Topological Damage:" },
            new string[]{ "" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.10f),
            curve.zero, 
            curve.zero,
            new string[]{ "Vital Spark" },
            new string[]{ "Health fuels strength" },
            new string[]{ "Damage per missing HP:" },
            new string[]{ "" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.05f),
            curve.linear(-0.05f),
            curve.zero,
            new string[]{ "Focused Mind" },
            new string[]{ "Steady and precise" },
            new string[]{ "Crit Chance:" },
            new string[]{ "Attack Speed:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.05f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Frenzy" },
            new string[]{ "Fighting builds momentum" },
            new string[]{ "Attack Speed per Hit:" },
            new string[]{ "Duration (Default 0.5s):" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
        new TiledStatData(
            curve.exponential(0.15f),
            curve.exponential(0.05f),
            curve.zero,
            new string[]{ "Sharp Edge" },
            new string[]{ "Your strikes cut deeper" },
            new string[]{ "Damage:" },
            new string[]{ "Crit Chance:" },
            new string[]{ "" }
        ),
    };
}