using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Shown when the game has ended: announces the winner and the final standings and offers
/// to start a new game. After "View Board" a compact banner stays visible so that a new
/// game can still be started at any time.
/// </summary>
public class GameOverDialogUI : MonoBehaviour
{
    private Text _winnerText;
    private Text _standingsText;
    private Text _bannerText;
    private Image _dimmer;
    private GameObject _box;
    private GameObject _banner;

    public bool IsActive => gameObject.activeSelf && _box != null && _box.activeSelf;

    /// <summary>Builds the dialog under <paramref name="parent"/> (hidden initially).</summary>
    public static GameOverDialogUI Create(Transform parent, Font font)
    {
        // Full-screen dimmer that blocks all clicks on the board and UI behind the dialog.
        Image dimmer = UIFactory.CreatePanel("GameOverModal", parent, new Color(0f, 0f, 0f, 0.6f), true);
        GameObject root = dimmer.gameObject;
        UIFactory.Stretch(UIFactory.Rect(root), Vector2.zero, Vector2.zero);

        GameOverDialogUI dialog = root.AddComponent<GameOverDialogUI>();
        dialog._dimmer = dimmer;

        Image box = UIFactory.CreatePanel("GameOverBox", root.transform,
            new Color(0.015f, 0.045f, 0.08f, 0.98f), true);
        dialog._box = box.gameObject;
        UIFactory.SetAnchors(UIFactory.Rect(box.gameObject), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 0f));

        Outline border = box.gameObject.AddComponent<Outline>();
        border.effectColor = new Color(1f, 0.82f, 0.3f, 0.95f);
        border.effectDistance = new Vector2(2f, -2f);

        UIFactory.AddVerticalLayout(box.gameObject, 12f, 20);
        UIFactory.AddSizeFitter(box.gameObject);

        UIFactory.CreateText(box.transform, font, UIStrings.GameOverTitle.GetText(), 22,
            new Color(1f, 0.82f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Bold);

        dialog._winnerText = UIFactory.CreateText(box.transform, font, string.Empty, 16,
            Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        dialog._winnerText.gameObject.AddComponent<LayoutElement>().minHeight = 40f;

        dialog._standingsText = UIFactory.CreateText(box.transform, font, string.Empty, 13,
            new Color(0.92f, 0.95f, 1f), TextAnchor.MiddleCenter);
        dialog._standingsText.gameObject.AddComponent<LayoutElement>().minHeight = 80f;

        GameObject row = UIFactory.CreateElement("ButtonRow", box.transform);
        UIFactory.AddHorizontalLayout(row, 20f);
        row.AddComponent<LayoutElement>().minHeight = 38f;

        UIFactory.CreateButton(row.transform, font, UIStrings.GameOverNewGame.GetText(),
            RestartGame, new Color(0.2f, 0.5f, 0.3f, 1f), 13, 38f);
        UIFactory.CreateButton(row.transform, font, UIStrings.GameOverViewBoard.GetText(),
            dialog.ShowBannerOnly, new Color(0.15f, 0.35f, 0.55f, 1f), 13, 38f);
        UIFactory.CreateButton(row.transform, font, UIStrings.Quit.GetText(),
            ConfirmQuitDialogUI.QuitApplication, new Color(0.65f, 0.22f, 0.22f, 1f), 13, 38f);

        // Compact banner at the top: winner + "New Game" button, no click blocking elsewhere.
        Image banner = UIFactory.CreatePanel("GameOverBanner", root.transform,
            new Color(0.015f, 0.045f, 0.08f, 0.98f), true);
        dialog._banner = banner.gameObject;
        UIFactory.SetAnchors(UIFactory.Rect(banner.gameObject), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(620f, 0f));
        Outline bannerBorder = banner.gameObject.AddComponent<Outline>();
        bannerBorder.effectColor = new Color(1f, 0.82f, 0.3f, 0.95f);
        bannerBorder.effectDistance = new Vector2(2f, -2f);
        UIFactory.AddHorizontalLayout(banner.gameObject, 16f, 10);
        UIFactory.AddSizeFitter(banner.gameObject);
        dialog._bannerText = UIFactory.CreateText(banner.transform, font, string.Empty, 14,
            Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        dialog._bannerText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        UIFactory.CreateButton(banner.transform, font, UIStrings.GameOverNewGame.GetText(),
            RestartGame, new Color(0.2f, 0.5f, 0.3f, 1f), 13, 34f)
            .gameObject.GetComponent<LayoutElement>().preferredWidth = 130f;
        UIFactory.CreateButton(banner.transform, font, UIStrings.Quit.GetText(),
            ConfirmQuitDialogUI.QuitApplication, new Color(0.65f, 0.22f, 0.22f, 1f), 13, 34f)
            .gameObject.GetComponent<LayoutElement>().preferredWidth = 85f;
        dialog._banner.SetActive(false);

        root.SetActive(false);
        return dialog;
    }

    public void Show(Player winner, Player[] players)
    {
        string winnerLine = string.Format(UIStrings.GameOverWinnerFormat.GetText(),
            winner.PlayerName, winner.VictoryPoints);
        _winnerText.text = winnerLine;
        _bannerText.text = winnerLine;

        var standings = players
            .OrderByDescending(p => p == winner)
            .ThenByDescending(p => p.VictoryPoints)
            .Select((p, i) => string.Format(UIStrings.GameOverStandingsLine.GetText(),
                i + 1, p.PlayerName, p.VictoryPoints));
        _standingsText.text = string.Join("\n", standings.ToArray());

        _dimmer.raycastTarget = true;
        _box.SetActive(true);
        _banner.SetActive(false);
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
    }

    /// <summary>Hides the big dialog but keeps the winner banner with the "New Game" button.</summary>
    public void ShowBannerOnly()
    {
        _box.SetActive(false);
        _dimmer.raycastTarget = false;
        _banner.SetActive(true);
    }

    private static void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
