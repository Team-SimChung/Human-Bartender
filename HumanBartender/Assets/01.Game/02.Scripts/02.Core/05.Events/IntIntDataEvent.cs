using UnityEngine;


public struct IntIntData
{
    public int value;
    public int value2;

    public IntIntData(int a, int b)
    {
        value = a; value2 = b;
    }
}

public struct IntIntIntData
{
    public int value;
    public int value2;
    public int value3;

    public IntIntIntData(int a, int b, int c)
    {
        value = a; value2 = b; value3 = c;
    }
}

[CreateAssetMenu(fileName = "New IntInt Event", menuName = "Game Events/IntInt Event")]
public class IntIntDataEvent : GameEvent<IntIntData> { }