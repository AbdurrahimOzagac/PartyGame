using UnityEngine;

namespace ShooterParty
{
    // Placed once in every mini-game scene: where critters start and where pickups appear.
    // The scene itself holds no networked objects; the controller spawns critters and pickups here.
    public class MiniGameScene : MonoBehaviour
    {
        public Transform[] spawnPoints = new Transform[0];
        public Transform[] pickupSpots = new Transform[0];
        public Unity.Netcode.NetworkObject pickupPrefab;   // spawned by the server at every pickup spot

        void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            foreach (var s in spawnPoints) if (s) Gizmos.DrawWireSphere(s.position + Vector3.up * 0.5f, 0.5f);
            Gizmos.color = Color.yellow;
            foreach (var s in pickupSpots) if (s) Gizmos.DrawWireCube(s.position + Vector3.up * 0.5f, Vector3.one * 0.6f);
        }
    }
}
