using UnityEngine;

namespace Pulse
{
    [CreateAssetMenu(menuName = "Pulse/Reactor")]
    public sealed class ReactorDef : ScriptableObject
    {
        public string displayName;
        [TextArea] public string description;
        public int powerCap = 10;
    }
}
