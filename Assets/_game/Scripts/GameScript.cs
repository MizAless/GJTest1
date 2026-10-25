using System.Collections.Generic;

// Every inner array is one pack. Spawn order is explicit and testable.
public class GameScript
{
    public static int[][] Round(int number)
    {
        if(number==1) return new[] { new[] {30,30,30,30,30,30} };
        if(number==2) return new[] { new[] {30,30,50,50}, new[] {50,50,30,80}, new[] {50,50,30,80} };
        return new[] { new[] {50,50,50,50}, new[] {50,50,50,80}, new[] {50,50,80,80}, new[] {50,50,80,80}, new[] {50,50,50,200} };
    }
}
