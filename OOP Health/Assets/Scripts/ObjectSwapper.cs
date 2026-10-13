using UnityEngine;
using static UnityEngine.Rendering.DebugUI.Table;

// Attach to your "UI Manager" empty object.
// Both objects must already be set up for grabbing (Rigidbody, collider,
// Grabbable, Hand Grab / Ray Grab Interactable, Physics Grabbable).
public class ObjectSwapper : MonoBehaviour
{
    [Header("Objects")]
    [SerializeField] private GameObject ball;
    [SerializeField] private GameObject alternateObject;
    [SerializeField] private GameObject thirdObject;

    [Header("Options")]
    [Tooltip("Place the new object where the current one is, instead of its own start position.")]
    [SerializeField] private bool appearAtCurrentPosition = true;

    private Vector3 ballStartPosition;
    private Quaternion ballStartRotation;
    private Vector3 alternateStartPosition;
    private Quaternion alternateStartRotation;
    private Vector3 thirdStartPosition;
    private Quaternion thirdStartRotation;

    private void Awake()
    {
        if (ball != null)
        {
            ballStartPosition = ball.transform.position;
            ballStartRotation = ball.transform.rotation;
        }
        if (alternateObject != null)
        {
            alternateStartPosition = alternateObject.transform.position;
            alternateStartRotation = alternateObject.transform.rotation;
        }
        if (thirdObject != null)
        {
            thirdStartPosition = thirdObject.transform.position;
            thirdStartRotation = thirdObject.transform.rotation;
        }

    }

    private void Start()
    {
        // Experience always begins with the ball.
        ShowBall(true);
    }

    // Hook to the "Swap" button's On Click ()
    public void SwapToAlternate()
    {
        if (ball == null || alternateObject == null || thirdObject == null) return;

        Vector3 pos;
        Quaternion rot;

        if (ball.activeSelf)
        {
            pos = appearAtCurrentPosition ? ball.transform.position : alternateStartPosition;
            rot = appearAtCurrentPosition ? ball.transform.rotation : alternateStartRotation;
        }
        else
        {
            pos = appearAtCurrentPosition ? thirdObject.transform.position : alternateStartPosition;
            rot = appearAtCurrentPosition ? thirdObject.transform.rotation : alternateStartRotation;
        }

        Deactivate(ball);
        Deactivate(thirdObject);
        Activate(alternateObject, pos, rot);
    }

    public void SwapToThird()
    {
        if (ball == null || thirdObject == null || alternateObject == null) return;

        Vector3 pos;
        Quaternion rot;

        if (ball.activeSelf)
        {
            pos = appearAtCurrentPosition ? ball.transform.position : thirdStartPosition;
            rot = appearAtCurrentPosition ? ball.transform.rotation : thirdStartRotation;
        }
        else
        {
            pos = appearAtCurrentPosition ? alternateObject.transform.position : thirdStartPosition;
            rot = appearAtCurrentPosition ? alternateObject.transform.rotation : thirdStartRotation;
        }

        Deactivate(ball);
        Deactivate(alternateObject);
        Activate(thirdObject, pos, rot);
    }

    // Hook to the "Reset" button's On Click ()
    public void ResetToBall()
    {
        ShowBall(true);
    }

    private void ShowBall(bool atStart)
    {
        if (ball == null) return;

        if (alternateObject != null)
            Deactivate(alternateObject);

        if (thirdObject != null)
            Deactivate(thirdObject);

        Activate(ball, ballStartPosition, ballStartRotation);
    }

    private static void Activate(GameObject obj, Vector3 position, Quaternion rotation)
    {
        obj.SetActive(true);
        StopMotion(obj);
        obj.transform.SetPositionAndRotation(position, rotation);
    }

    private static void Deactivate(GameObject obj)
    {
        StopMotion(obj);
        obj.SetActive(false);
    }

    private static void StopMotion(GameObject obj)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
}
