using UnityEngine;

[System.Serializable]
public class DashData
{
    public float width = 1f;
    public float length = 4f;
    public float height = 2f;
    public Vector3 offset = Vector3.zero;
    public LayerMask targetMask;
    public int hitCount = 1;
    public float hitInterval = 0.2f;
    public bool ignoreRepeatHit = true;
}
