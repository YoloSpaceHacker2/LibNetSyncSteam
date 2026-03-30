using System;

namespace YSHSteamNet
{
    public abstract class ObjSync
    {
        public uint NetId;
        public ulong Owner;
        public float SyncInterval = 0.1f;

        private DateTime lastSync;

        public bool ShouldSync()
        {
            return (DateTime.Now - lastSync).TotalSeconds >= SyncInterval
                   && HasChanged();
        }

        public byte[] Build()
        {
            lastSync = DateTime.Now;
            return Serialize();
        }

        public abstract byte[] Serialize();
        public abstract void Deserialize(byte[] data);
        public abstract bool HasChanged();
    }
}