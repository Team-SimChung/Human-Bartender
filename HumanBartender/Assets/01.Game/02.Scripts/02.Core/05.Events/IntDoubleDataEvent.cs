using UnityEngine;

public struct IntDoubleData
{
    public int intValue;
    public double doubleValue;

    public IntDoubleData(int intval, double doubleval)
    {
        intValue = intval;
        doubleValue = doubleval;
    }
}



[CreateAssetMenu(fileName = "New IntDoubleData Event", menuName = "Game Events/IntDoubleData Event")]
public class IntDoubleDataEvent : GameEvent<IntDoubleData> { }

