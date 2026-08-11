public static class GameStats
{
    public static long WheatHarvested;
    public static long EnemiesDefeated;
    public static long MoneyEarned;
    public static long MoneyStolen;

    public static void AddWheat(int amount)
    {
        if (amount > 0) WheatHarvested += amount;
    }

    public static void AddEnemy()
    {
        EnemiesDefeated++;
    }

    public static void AddMoneyEarned(long amount)
    {
        if (amount > 0) MoneyEarned += amount;
    }

    public static void AddMoneyStolen(long amount)
    {
        if (amount > 0) MoneyStolen += amount;
    }
}
