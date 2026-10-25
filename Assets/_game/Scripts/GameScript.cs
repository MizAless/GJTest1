using System;

public class GameScript
{
    public const int WaveCount = 5;

    public static int[][] Round(int number)
    {
        int[][] groups;
        switch (number)
        {
            case 1:
                return CreateWave(3, 6);
            case 2:
                groups = CreateWave(4, 5);
                groups[2] = CreateGroup(5, mediumCount: 2);
                groups[3] = CreateGroup(5, mediumCount: 2);
                return groups;
            case 3:
                groups = CreateWave(5, 4, mediumCount: 1);
                groups[4] = CreateGroup(4, mediumCount: 3);
                return groups;
            case 4:
                groups = CreateWave(10, 5, mediumCount: 2);
                groups[9] = CreateGroup(5, mediumCount: 2, strongCount: 1);
                return groups;
            case 5:
                groups = CreateWave(15, 8, mediumCount: 2, strongCount: 1);
                groups[14] = CreateGroup(8, mediumCount: 2, strongCount: 1, boss: true);
                return groups;
            default:
                throw new ArgumentOutOfRangeException(nameof(number), number, "Expected a wave from 1 to 5.");
        }
    }

    private static int[][] CreateWave(int groupCount, int groupSize, int mediumCount = 0, int strongCount = 0)
    {
        int[][] groups = new int[groupCount][];
        for (int index = 0; index < groups.Length; index++)
        {
            groups[index] = CreateGroup(groupSize, mediumCount, strongCount);
        }
        return groups;
    }

    private static int[] CreateGroup(int size, int mediumCount = 0, int strongCount = 0, bool boss = false)
    {
        int[] health = new int[size];
        int weakCount = size - mediumCount - strongCount;
        if (boss)
        {
            weakCount--;
        }
        for (int index = 0; index < size; index++)
        {
            if (index < weakCount)
            {
                health[index] = 30;
            }
            else if (index < weakCount + mediumCount)
            {
                health[index] = 50;
            }
            else
            {
                health[index] = 80;
            }
        }
        if (boss)
        {
            health[size - 1] = 200;
        }
        return health;
    }
}
