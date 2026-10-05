using UnityEngine;

public struct Void
{ 

}



[CreateAssetMenu(fileName = "New void Event", menuName = "Game Events/Void Event")]
public class VoidEvent : GameEvent<Void> { }

