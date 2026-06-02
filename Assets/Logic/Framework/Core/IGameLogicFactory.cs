namespace Lockstep.Core
{
    public interface IGameLogicFactory
    {
        void Build(World world, int playerCount);
    }
}
