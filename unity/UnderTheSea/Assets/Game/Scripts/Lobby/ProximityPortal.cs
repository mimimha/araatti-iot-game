using UnityEngine;

/// <summary>
/// Opens an ARPG Effects portal when the player comes close and closes it again when they leave.
/// Put this on an empty GameObject where the portal should stand, then fill the three slots with
/// the Open / Idle / Close prefabs of one colour (Assets/ARPG Effects/Prefabs/Interactive/Portals).
/// The three effects are spawned as children, so moving this object moves the portal with it.
/// </summary>
public class ProximityPortal : MonoBehaviour
{
    enum State { Closed, Opening, Open, Closing }

    [Header("Portal prefabs (one colour)")]
    [SerializeField] GameObject openPrefab;
    [SerializeField] GameObject idlePrefab;
    [SerializeField] GameObject closePrefab;

    [Header("Player")]
    [Tooltip("Leave empty to look the player up by name on Start.")]
    [SerializeField] Transform player;
    [SerializeField] string playerObjectName = "JaeYoung";

    [Header("Distances")]
    [Tooltip("The portal starts opening once the player is this close.")]
    [SerializeField] float openDistance = 6f;
    [Tooltip("Must be larger than Open Distance. The gap keeps the portal from flickering when the player lingers on the edge.")]
    [SerializeField] float closeDistance = 8f;
    [Tooltip("Ignore height, so a portal on a slope still reacts at the same ground distance.")]
    [SerializeField] bool ignoreHeight = true;

    [Header("Timing")]
    [Tooltip("Length of the open effect before the idle loop takes over.")]
    [SerializeField] float openDuration = 0.8f;
    [Tooltip("Length of the close effect.")]
    [SerializeField] float closeDuration = 1f;

    GameObject openInstance;
    GameObject idleInstance;
    GameObject closeInstance;

    State state = State.Closed;
    float timer;

    void Start()
    {
        if (player == null)
        {
            var found = GameObject.Find(playerObjectName);
            if (found != null)
                player = found.transform;
            else
                Debug.LogWarning($"{name}: no player assigned and none named '{playerObjectName}' in the scene.", this);
        }

        openInstance = Spawn(openPrefab, "open");
        idleInstance = Spawn(idlePrefab, "idle");
        closeInstance = Spawn(closePrefab, "close");
    }

    GameObject Spawn(GameObject prefab, string slot)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"{name}: the {slot} prefab slot is empty.", this);
            return null;
        }

        var go = Instantiate(prefab, transform.position, transform.rotation, transform);
        go.SetActive(false);
        return go;
    }

    void Update()
    {
        if (player == null)
            return;

        // Once open, the player has to walk past the larger radius to shut it again.
        float trigger = (state == State.Closed || state == State.Closing) ? openDistance : closeDistance;
        bool wantOpen = SqrDistanceToPlayer() <= trigger * trigger;

        switch (state)
        {
            case State.Closed:
                if (wantOpen) Enter(State.Opening);
                break;

            case State.Opening:
                timer -= Time.deltaTime;
                if (!wantOpen) Enter(State.Closing);
                else if (timer <= 0f) Enter(State.Open);
                break;

            case State.Open:
                if (!wantOpen) Enter(State.Closing);
                break;

            case State.Closing:
                timer -= Time.deltaTime;
                if (wantOpen) Enter(State.Opening);
                else if (timer <= 0f) Enter(State.Closed);
                break;
        }
    }

    float SqrDistanceToPlayer()
    {
        Vector3 delta = player.position - transform.position;
        if (ignoreHeight) delta.y = 0f;
        return delta.sqrMagnitude;
    }

    void Enter(State next)
    {
        state = next;

        // Toggling the object off and on replays the one-shot effects.
        Show(openInstance, next == State.Opening);
        Show(idleInstance, next == State.Open);
        Show(closeInstance, next == State.Closing);

        timer = next == State.Opening ? openDuration
              : next == State.Closing ? closeDuration
              : 0f;
    }

    static void Show(GameObject go, bool on)
    {
        if (go == null || go.activeSelf == on) return;
        go.SetActive(on);
    }

    void OnValidate()
    {
        openDistance = Mathf.Max(0f, openDistance);
        // The close radius must stay outside the open radius or the portal flickers.
        closeDistance = Mathf.Max(closeDistance, openDistance + 0.5f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, openDistance);
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, closeDistance);
    }
}
