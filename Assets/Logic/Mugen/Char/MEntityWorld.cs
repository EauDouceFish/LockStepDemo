// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/char.go helper/projectile/explod helpers + system.go entity lists.
// Adapted to fixed-point lockstep. See Docs/移植方案_Ikemen.md.
using System.Collections.Generic;
using Lockstep.Core;
using Lockstep.Math;

namespace Lockstep.Mugen.Char
{
    /// <summary>Helper spawn request queued by Helper controller and drained by the battle engine.</summary>
    public struct MHelperRequest
    {
        public MChar Owner;
        public int StateNo;
        public int HelperType;
        public int PosType;
        public FFloat PosX;
        public FFloat PosY;
        public int Facing;
        public bool KeyCtrl;
    }

    /// <summary>Projectile spawn request queued by Projectile controller and drained by the battle engine.</summary>
    public struct MProjectileRequest
    {
        public MChar Owner;
        public int ProjId;
        public FFloat VelX;
        public FFloat VelY;
        public FFloat AccelX;
        public FFloat AccelY;
        public FFloat PosX;
        public FFloat PosY;
        public int RemoveTime;
        public int AnimNo;
        public Hit.MHitDef HitDef;
    }

    /// <summary>
    /// Minimal deterministic Explod runtime state. It backs numexplod, ModifyExplod and RemoveExplod.
    /// Rendering fields are intentionally deferred to the view layer.
    /// </summary>
    public sealed class MExplod
    {
        public int Id;
        public int OwnerId;
        public int ExplodId;
        public int AnimNo;
        public FVector3 Pos;
        public FVector3 Vel;
        public FVector3 Accel;
        public FFloat ScaleX = FFloat.One;
        public FFloat ScaleY = FFloat.One;
        public int Facing = 1;
        public int VFacing = 1;
        public int BindTime;
        public int RemoveTime = -2;
        public int SprPriority;
        public bool OwnPal;
        public bool RemoveOnGetHit;
        public bool RemoveOnChangeState;

        public void Step()
        {
            Vel = new FVector3(Vel.X + Accel.X, Vel.Y + Accel.Y, Vel.Z + Accel.Z);
            Pos = new FVector3(Pos.X + Vel.X, Pos.Y + Vel.Y, Pos.Z + Vel.Z);
            if (BindTime > 0)
            {
                BindTime--;
            }
            if (RemoveTime >= 0)
            {
                RemoveTime--;
                if (RemoveTime < 0)
                {
                    RemoveTime = -3;
                }
            }
        }

        public bool Expired => RemoveTime == -3;

        public MExplod Clone()
        {
            return new MExplod
            {
                Id = Id,
                OwnerId = OwnerId,
                ExplodId = ExplodId,
                AnimNo = AnimNo,
                Pos = Pos,
                Vel = Vel,
                Accel = Accel,
                ScaleX = ScaleX,
                ScaleY = ScaleY,
                Facing = Facing,
                VFacing = VFacing,
                BindTime = BindTime,
                RemoveTime = RemoveTime,
                SprPriority = SprPriority,
                OwnPal = OwnPal,
                RemoveOnGetHit = RemoveOnGetHit,
                RemoveOnChangeState = RemoveOnChangeState,
            };
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(Id);
            hash.AddInt32(OwnerId);
            hash.AddInt32(ExplodId);
            hash.AddInt32(AnimNo);
            hash.AddFixed(Pos);
            hash.AddFixed(Vel);
            hash.AddFixed(Accel);
            hash.AddFixed(ScaleX);
            hash.AddFixed(ScaleY);
            hash.AddInt32(Facing);
            hash.AddInt32(VFacing);
            hash.AddInt32(BindTime);
            hash.AddInt32(RemoveTime);
            hash.AddInt32(SprPriority);
            hash.AddBool(OwnPal);
            hash.AddBool(RemoveOnGetHit);
            hash.AddBool(RemoveOnChangeState);
        }
    }

    /// <summary>
    /// Shared entity world for helper, projectile and explod state. Controllers write requests or records here;
    /// the engine owns frame stepping and lifecycle pruning.
    /// </summary>
    public sealed class MEntityWorld
    {
        public readonly List<MHelperRequest> SpawnQueue = new List<MHelperRequest>();
        public readonly List<MProjectileRequest> ProjSpawnQueue = new List<MProjectileRequest>();
        public readonly List<MChar> Helpers = new List<MChar>();
        public readonly List<MProjectile> Projectiles = new List<MProjectile>();
        public readonly List<MExplod> Explods = new List<MExplod>();
        public readonly MFrameEvents Events = new MFrameEvents();

        public int NextEntityId = 1000;

        public int AllocId()
        {
            return NextEntityId++;
        }

        public void RequestHelper(MHelperRequest request)
        {
            SpawnQueue.Add(request);
        }

        public void RequestProjectile(MProjectileRequest request)
        {
            ProjSpawnQueue.Add(request);
        }

        public int CountHelpers(int helperType)
        {
            if (helperType < 0) { return Helpers.Count; }
            int count = 0;
            for (int index = 0; index < Helpers.Count; index++)
            {
                if (Helpers[index].HelperType == helperType) { count++; }
            }
            return count;
        }

        public int CountHelpers(int helperType, int ownerId)
        {
            int count = 0;
            for (int index = 0; index < Helpers.Count; index++)
            {
                MChar helper = Helpers[index];
                if (helper == null)
                {
                    continue;
                }
                int helperOwnerId = helper.Root != null ? helper.Root.Id : (helper.Parent != null ? helper.Parent.Id : -1);
                if (helperOwnerId == ownerId && (helperType < 0 || helper.HelperType == helperType))
                {
                    count++;
                }
            }
            return count;
        }

        public int CountProjectiles(int projId)
        {
            if (projId < 0) { return Projectiles.Count; }
            int count = 0;
            for (int index = 0; index < Projectiles.Count; index++)
            {
                if (Projectiles[index].ProjId == projId) { count++; }
            }
            return count;
        }

        public int CountProjectiles(int projId, int ownerId)
        {
            int count = 0;
            for (int index = 0; index < Projectiles.Count; index++)
            {
                MProjectile projectile = Projectiles[index];
                if (projectile.OwnerId == ownerId && (projId < 0 || projectile.ProjId == projId))
                {
                    count++;
                }
            }
            return count;
        }

        public void StepExplods()
        {
            for (int index = 0; index < Explods.Count; index++)
            {
                Explods[index].Step();
            }
            for (int index = Explods.Count - 1; index >= 0; index--)
            {
                if (Explods[index].Expired)
                {
                    Explods.RemoveAt(index);
                }
            }
        }

        public void AddExplod(MExplod explod)
        {
            Explods.Add(explod);
        }

        public int CountExplods(int explodId, int ownerId)
        {
            int count = 0;
            for (int index = 0; index < Explods.Count; index++)
            {
                MExplod explod = Explods[index];
                if (explod.OwnerId == ownerId && (explodId < 0 || explod.ExplodId == explodId))
                {
                    count++;
                }
            }
            return count;
        }

        public List<MExplod> FindExplods(int explodId, int ownerId, int matchIndex)
        {
            List<MExplod> matches = new List<MExplod>();
            int seen = 0;
            for (int index = 0; index < Explods.Count; index++)
            {
                MExplod explod = Explods[index];
                if (explod.OwnerId != ownerId || (explodId >= 0 && explod.ExplodId != explodId))
                {
                    continue;
                }
                if (matchIndex >= 0)
                {
                    if (seen == matchIndex)
                    {
                        matches.Add(explod);
                        return matches;
                    }
                    seen++;
                }
                else
                {
                    matches.Add(explod);
                }
            }
            return matches;
        }

        public void RemoveExplods(int explodId, int ownerId, int matchIndex)
        {
            int seen = 0;
            for (int index = 0; index < Explods.Count; index++)
            {
                MExplod explod = Explods[index];
                if (explod.OwnerId != ownerId || (explodId >= 0 && explod.ExplodId != explodId))
                {
                    continue;
                }
                bool remove = matchIndex < 0 || seen == matchIndex;
                seen++;
                if (remove)
                {
                    Explods.RemoveAt(index);
                    if (matchIndex >= 0)
                    {
                        return;
                    }
                    index--;
                }
            }
        }

        public MEntityWorld Clone()
        {
            MEntityWorld world = new MEntityWorld { NextEntityId = NextEntityId };
            world.SpawnQueue.AddRange(SpawnQueue);
            world.ProjSpawnQueue.AddRange(ProjSpawnQueue);
            world.Helpers.AddRange(Helpers);
            for (int index = 0; index < Projectiles.Count; index++)
            {
                world.Projectiles.Add(Projectiles[index].Clone());
            }
            for (int index = 0; index < Explods.Count; index++)
            {
                world.Explods.Add(Explods[index].Clone());
            }
            return world;
        }

        public void WriteHash(ref Hash64 hash)
        {
            hash.AddInt32(NextEntityId);
            hash.AddInt32(SpawnQueue.Count);
            for (int index = 0; index < SpawnQueue.Count; index++)
            {
                WriteHash(ref hash, SpawnQueue[index]);
            }
            hash.AddInt32(ProjSpawnQueue.Count);
            for (int index = 0; index < ProjSpawnQueue.Count; index++)
            {
                WriteHash(ref hash, ProjSpawnQueue[index]);
            }
            hash.AddInt32(Explods.Count);
            for (int index = 0; index < Explods.Count; index++)
            {
                Explods[index].WriteHash(ref hash);
            }
        }

        static void WriteHash(ref Hash64 hash, MHelperRequest request)
        {
            hash.AddInt32(request.Owner != null ? request.Owner.Id : -1);
            hash.AddInt32(request.StateNo);
            hash.AddInt32(request.HelperType);
            hash.AddInt32(request.PosType);
            hash.AddFixed(request.PosX);
            hash.AddFixed(request.PosY);
            hash.AddInt32(request.Facing);
            hash.AddBool(request.KeyCtrl);
        }

        static void WriteHash(ref Hash64 hash, MProjectileRequest request)
        {
            hash.AddInt32(request.Owner != null ? request.Owner.Id : -1);
            hash.AddInt32(request.ProjId);
            hash.AddFixed(request.VelX);
            hash.AddFixed(request.VelY);
            hash.AddFixed(request.AccelX);
            hash.AddFixed(request.AccelY);
            hash.AddFixed(request.PosX);
            hash.AddFixed(request.PosY);
            hash.AddInt32(request.RemoveTime);
            hash.AddInt32(request.AnimNo);
            hash.AddBool(request.HitDef != null);
            if (request.HitDef != null)
            {
                request.HitDef.WriteHash(ref hash);
            }
        }
    }
}
