using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Modal dialog shown to clients when the connection to the game server is lost during active gameplay.
/// </summary>
public class DisconnectDialogUI : MonoBehaviour
{
    private Text _reasonText;
    private Image _dimmer;

    public bool IsActive => gameObject != null && gameObject.activeSelf;

    /// <summary>Builds the dialog under <paramref name="parent"/> (hidden initially).</summary>
    public static DisconnectDialogUI Create(Transform parent, Font font)
    {
        Image dimmer = UIFactory.CreatePanel("DisconnectModal", parent,
            new Color(0.015f, 0.045f, 0.08f, 0.98f), true);
        GameObject root = dimmer.gameObject;
        UIFactory.SetAnchors(UIFactory.Rect(root), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 0f));

        DisconnectDialogUI dialog = root.AddComponent<DisconnectDialogUI>();
        dialog._dimmer = dimmer;

        Outline border = root.AddComponent<Outline>();
        border.effectColor = new Color(0.95f, 0.35f, 0.25f, 0.95f);
        border.effectDistance = new Vector2(2f, -2f);

        UIFactory.AddVerticalLayout(root, 14f, 20);
        UIFactory.AddSizeFitter(root);

        UIFactory.CreateText(root.transform, font, "Verbindung zum Server verloren", 18,
            new Color(1f, 0.82f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);

        dialog._reasonText = UIFactory.CreateText(root.transform, font,
            "Die Verbindung zum Host bzw. Dedicated Server wurde unterbrochen.", 13,
            new Color(0.92f, 0.95f, 1f), TextAnchor.MiddleCenter);
        dialog._reasonText.gameObject.AddComponent<LayoutElement>().minHeight = 60f;

        Button menuBtn = UIFactory.CreateButton(root.transform, font, "Zurück zum Hauptmenü",
            OnReturnToMainMenu, new Color(0.15f, 0.35f, 0.55f, 1f), 13, 40f);
        menuBtn.GetComponent<LayoutElement>().preferredWidth = 220f;

        root.SetActive(false);
        return dialog;
    }

    public void Show(string reason = null)
    {
        if (_dimmer != null) _dimmer.raycastTarget = true;
        if (_reasonText != null && !string.IsNullOrEmpty(reason))
        {
            _reasonText.text = $"Die Verbindung zum Host bzw. Dedicated Server wurde unterbrochen:\n{reason}";
        }
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (_dimmer != null) _dimmer.raycastTarget = false;
        gameObject.SetActive(false);
    }

    private static void OnReturnToMainMenu()
    {
        Networking.NetworkManager.Instance?.DisconnectAll();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}

