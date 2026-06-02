using Lockstep.Core;
using Lockstep.Game.Combat;
using Lockstep.Game.Components;
using Lockstep.Game.States;
using Lockstep.Game.Systems;
using Lockstep.Math;

namespace Lockstep.Game
{
    /// <summary>
    /// 战斗世界装配。负责：
    ///   1. 创建实体并挂载 v1 所需的 6 类组件
    ///   2. 注入 BattleGameData（招式表）到 World.GameData
    ///   3. 注册系统顺序：InputBufferSystem → StateMachineSystem
    /// </summary>
    public sealed class BattleGameLogic : IGameLogicFactory
    {
        public void Build(World world, int playerCount)
        {
            // 注入只读游戏数据（招式表等）。先注入再 CreateEntity 顺序无关，但语义上 GameData 是世界属性。
            world.GameData = new BattleGameData(new StaticAttackTable());

            for (int i = 0; i < playerCount; i++)
            {
                Entity entity = world.CreateEntity();
                FVector3 spawnPos;
                if (world.Config != null && i < world.Config.InitialPositions.Length)
                {
                    spawnPos = world.Config.InitialPositions[i];
                }
                else
                {
                    FFloat offsetX = (i == 0) ? -FFloat.FromInt(4) : FFloat.FromInt(4);
                    spawnPos = new FVector3(offsetX, FFloat.Zero, FFloat.Zero);
                }
                entity.Add(new TransformC
                {
                    Pos = spawnPos,
                    FacingX = (i == 0) ? FFloat.One : FFloat.MinusOne,
                });
                entity.Add(new VelocityC());
                entity.Add(new PlayerTagC { PlayerIndex = i });
                entity.Add(new HealthC { HP = 100, MaxHP = 100 });
                entity.Add(new StateMachineC { Current = PlayerStateId.Idle });
                entity.Add(new ActiveMoveC());
                entity.Add(new InputBufferC());
                entity.Add(new IncomingHitC());
            }

            // 系统顺序：先采输入到 buffer，再跑状态机
            world.RegisterSystem(new InputBufferSystem());
            world.RegisterSystem(new StateMachineSystem());
        }
    }
}
