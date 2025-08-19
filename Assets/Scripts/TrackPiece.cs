using UnityEngine;

public class TrackPiece : MonoBehaviour
{
    public Transform SocketStart;
    public Transform SocketEnd;

    public enum PieceType
    {
        Straight,
        GentleLeft, GentleRight,
        SharpLeft,  SharpRight,
        SLeft,      SRight,
        HairpinLeft, HairpinRight // usá 8 que quieras; podés ignorar los que sobren
    }
    public PieceType type;
}
