using Unity.Netcode.Components;

namespace ShooterParty
{
    // NetworkTransform that hands every owner state the server receives (owner tick + position) to the critter's
    // speed check, so the check runs on the owner's timeline instead of packet arrival times.
    public class CritterNetworkTransform : NetworkTransform
    {
        NetworkCritter critter;

        protected override void OnNetworkTransformStateUpdated(ref NetworkTransformState oldState, ref NetworkTransformState newState)
        {
            base.OnNetworkTransformStateUpdated(ref oldState, ref newState);
            if (!IsServer || IsOwner || !newState.HasPositionChange) return;
            if (!critter) critter = GetComponent<NetworkCritter>();
            if (critter) critter.OnOwnerState(newState.GetNetworkTick(), newState.GetPosition());
        }
    }
}
