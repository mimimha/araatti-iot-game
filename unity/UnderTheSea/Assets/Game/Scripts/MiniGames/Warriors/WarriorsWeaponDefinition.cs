using UnityEngine;

namespace Warriors
{
    [CreateAssetMenu(menuName = "UnderTheSea/Warriors/Weapon Definition")]
    public sealed class WarriorsWeaponDefinition : ScriptableObject
    {
        public GameObject weaponPrefab;
        public Vector3 gripLocalPosition;
        public Vector3 gripLocalRotation;
        public Vector3 localScale = Vector3.one;
        public Vector3 hitboxCenter;
        public Vector3 hitboxSize = Vector3.one;
        public int damage = 25;
    }
}
