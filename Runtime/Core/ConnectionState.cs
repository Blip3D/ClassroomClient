namespace ClassroomClient.Core
{
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        InLobby,
        InSession,
        Reconnecting,
        PendingApproval
    }
}
