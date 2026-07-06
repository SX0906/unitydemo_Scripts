using UnityEngine;

[System.Serializable]
public class AttackVFXEntry
{
    public string eventKey;
    public GameObject prefab;
    public Vector3 localPosition;
    public Vector3 localEulerAngles;
    public Vector3 localScale = Vector3.one;
    public bool parentToSpawnPoint = true;
    [Min(0f)] public float lifetime = 2f;
}

[CreateAssetMenu(menuName = "Combat/Attack VFX Config", fileName = "AttackVFXConfig")]
public class AttackVFXConfig : ScriptableObject
{
    public AttackVFXEntry[] entries;

    public bool TryGetEntry(string eventKey, out AttackVFXEntry entry)
    {
        entry = null;

        if (string.IsNullOrEmpty(eventKey) || entries == null)
            return false;

        foreach (AttackVFXEntry candidate in entries)
        {
            if (candidate != null && candidate.eventKey == eventKey)
            {
                entry = candidate;
                return true;
            }
        }

        return false;
    }
}