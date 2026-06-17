using UnityEngine;

namespace ClassroomClient.Core
{
    /// <summary>
    /// Internal marker attached by ClassroomClient to the dedicated WebRTC capture camera it
    /// creates. Developers never add this manually.
    /// </summary>
    [DisallowMultipleComponent]
    public class ClassroomStreamCameraMarker : MonoBehaviour { }
}
