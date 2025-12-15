using Verse;

namespace MRWD
{
    public class ObservationDistributorComponent : GameComponent
    {
        public ObservationDistributorComponent() { }
        public ObservationDistributorComponent(Game game) { }
        public override void GameComponentTick() => ObservationDistributor.Tick();
    }
}
