using UnityEngine;

public class CameraKeyboardController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 35f;
    [SerializeField] private float fastMoveSpeed = 70f;

    // Update is called once per frame
    void Update()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != null)
        {
            var go = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            if (go.GetComponent<UnityEngine.UI.InputField>() != null || go.GetComponent<TMPro.TMP_InputField>() != null)
            {
                return;
            }
        }

        if (GameInput.GetKeyDown(KeyCode.Home))
        {
            CameraMotion motion = GetComponent<CameraMotion>();
            if (motion != null)
            {
                motion.ResetToOverview();
            }
            return;
        }

        float rawX = 0f;
        float rawZ = 0f;

        if (GameInput.GetKey(KeyCode.W) || GameInput.GetKey(KeyCode.UpArrow)) rawZ += 1f;
        if (GameInput.GetKey(KeyCode.S) || GameInput.GetKey(KeyCode.DownArrow)) rawZ -= 1f;
        if (GameInput.GetKey(KeyCode.A) || GameInput.GetKey(KeyCode.LeftArrow)) rawX -= 1f;
        if (GameInput.GetKey(KeyCode.D) || GameInput.GetKey(KeyCode.RightArrow)) rawX += 1f;

        // Fallback to Input axes if gamepad or other binding is active
        if (rawX == 0f && rawZ == 0f)
        {
            rawX = GameInput.GetAxisRaw("Horizontal");
            rawZ = GameInput.GetAxisRaw("Vertical");
        }

        if (rawX != 0f || rawZ != 0f)
        {
            Vector3 translate = new Vector3(rawX, 0f, rawZ).normalized;
            float speed = (GameInput.GetKey(KeyCode.LeftShift) || GameInput.GetKey(KeyCode.RightShift)) ? fastMoveSpeed : moveSpeed;
            this.transform.Translate(translate * (speed * Time.deltaTime), Space.World);
        }
    }
}
