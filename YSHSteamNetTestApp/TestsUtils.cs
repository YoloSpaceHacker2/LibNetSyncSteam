using System;
using System.Linq;
using System.Threading;
using YSHSteamNet;

// --- Shared object types ---

class PlayerPos : ObjSync
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

    public override string ToString() => $"PlayerPos(netId={NetId}, owner={Owner}, X={X})";
}

class MobPos : ObjSync
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

    public override string ToString() => $"MobPos(netId={NetId}, owner={Owner}, X={X})";
}