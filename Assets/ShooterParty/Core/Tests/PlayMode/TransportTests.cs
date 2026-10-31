#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ShooterParty.Tests
{
    public class TransportTests
    {
        [UnityTearDown] public IEnumerator TearDown() => NetTestRig.Teardown();

        // Over WebSockets (TCP through Cloudflare) a few seconds of stall arrive as one burst. Unity Transport's
        // default 128-packet queue overflowed on the Pi, dropped packets and disconnected two players mid-round.
        [UnityTest]
        public IEnumerator PacketQueueAbsorbsABurstAfterAStall()
        {
            NetTestRig.StartNetworked();
            yield return null;
            Assert.GreaterOrEqual(PartyNet.Instance.Transport.MaxPacketQueueSize, PartyNet.PacketQueue);
            Assert.GreaterOrEqual(PartyNet.PacketQueue, 1024);
        }
    }
}
#endif
