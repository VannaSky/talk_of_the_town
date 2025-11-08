using UnityEngine;

public class GameCamera : MonoBehaviour
{
    [SerializeField] float panSpeed = 20f;
    [SerializeField] float zoomSpeed = 1000f;
    [SerializeField] float minHeight = 5f;
    [SerializeField] float maxHeight = 80f;
    [SerializeField] float dragSpeed = 1f;

    Camera cam;
    Vector3 dragOrigin;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        HandleKeyboardPan();
        HandleMouseDrag();
        HandleZoom();
    }

    void HandleKeyboardPan()
    {
        float h = Input.GetAxis("Horizontal"); // A/D or left/right
        float v = Input.GetAxis("Vertical");   // W/S or up/down

        // Move along camera's XZ plane (ignore vertical component)
        Vector3 forward = Vector3.Scale(transform.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 right = Vector3.Scale(transform.right, new Vector3(1, 0, 1)).normalized;

        Vector3 move = (forward * v + right * h) * panSpeed * Time.deltaTime;
        transform.position += move;
        ClampHeight();
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            transform.position += transform.forward * scroll * zoomSpeed * Time.deltaTime;
            ClampHeight();
        }
    }

    void HandleMouseDrag()
    {
        if (cam == null)
            return;

        Plane ground = new Plane(Vector3.up, Vector3.zero);

        if (Input.GetMouseButtonDown(2))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (ground.Raycast(ray, out float enter))
                dragOrigin = ray.GetPoint(enter);
        }

        if (Input.GetMouseButton(2))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (ground.Raycast(ray, out float enter))
            {
                Vector3 currentPoint = ray.GetPoint(enter);
                Vector3 diff = (dragOrigin - currentPoint) * dragSpeed;
                transform.position += diff;
                ClampHeight();
            }
        }
    }

    void ClampHeight()
    {
        Vector3 pos = transform.position;
        pos.y = Mathf.Clamp(pos.y, minHeight, maxHeight);
        transform.position = pos;
    }
}
