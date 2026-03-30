using System;
using YSHSteamNet;

// Objet synchronisé représentant un joueur dans l'app cible.
// Même structure que dans les tests — deviendra la vraie classe de jeu en V2 (Unity).
class AppPlayer : ObjSync
{
    public float X;
    private float _lastX;

    public override byte[] Serialize()
    {
        _lastX = X;
        return BitConverter.GetBytes(X);
    }

    public override void Deserialize(byte[] data)
    {
        X = BitConverter.ToSingle(data, 0);
    }

    public override bool HasChanged() => X != _lastX;

    public override string ToString() => $"X={X}";
}
