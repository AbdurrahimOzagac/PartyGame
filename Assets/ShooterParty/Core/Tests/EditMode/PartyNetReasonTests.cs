using NUnit.Framework;

namespace ShooterParty.Tests
{
    public class PartyNetReasonTests
    {
        [Test] public void TransportNoiseBecomesConnectionLost() =>
            Assert.AreEqual("Connection lost", PartyNet.FriendlyReason("[Disconnect Event][Client-1][TransportClientId-4294967296][TransportShutdown] NetworkConnectionManager was shutdown."));

        [Test] public void EmptyBecomesConnectionLost() => Assert.AreEqual("Connection lost", PartyNet.FriendlyReason(""));

        [Test] public void ServerReasonsAreKept()
        {
            Assert.AreEqual("Room is full", PartyNet.FriendlyReason("Room is full"));
            Assert.AreEqual(ConnectionGate.Expired, PartyNet.FriendlyReason(ConnectionGate.Expired));
        }
    }
}
