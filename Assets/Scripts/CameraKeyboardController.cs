using UnityEngine;

public class CameraKeyboardController : MonoBehaviour
{
    float moveSpeed = 25f;

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

        if (Input.GetKeyDown(KeyCode.Home))
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

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) rawZ += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) rawZ -= 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) rawX -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) rawX += 1f;

        // Fallback to Input axes if gamepad or other binding is active
        if (rawX == 0f && rawZ == 0f)
        {
            rawX = Input.GetAxisRaw("Horizontal");
            rawZ = Input.GetAxisRaw("Vertical");
        }

        if (rawX != 0f || rawZ != 0f)
        {
            Vector3 translate = new Vector3(rawX, 0f, rawZ).normalized;
            float speed = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? 70f : 35f;
            this.transform.Translate(translate * (speed * Time.deltaTime), Space.World);
        }
    }
}
