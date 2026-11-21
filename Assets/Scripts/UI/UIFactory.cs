using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Shared helpers for building uGUI elements from code. Dialog classes use these
/// together with layout groups instead of hand-computed offsets.
/// </summary>
public static class UIFactory
{
    public static GameObject CreateElement(string name, Transform parent, params System.Type[] components)
    {
        System.Type[] types = new System.Type[components.Length + 1];
        types[0] = typeof(RectTransform);
        for (int i = 0; i < components.Length; i++)
        {
            types[i + 1] = components[i];
        }

        GameObject go = new GameObject(name, types);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return go;
    }

    public static RectTransform Rect(GameObject go)
    {
        return go.GetComponent<RectTransform>();
    }

    public static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    public static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    public static Image CreatePanel(string name, Transform parent, Color color, bool raycastTarget = false)
    {
        GameObject go = CreateElement(name, parent, typeof(CanvasRenderer), typeof(Image));
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = raycastTarget;
        return image;
    }

    public static Text CreateText(Transform parent, Font font, string value, int fontSize, Color color,
        TextAnchor alignment = TextAnchor.UpperLeft, FontStyle style = FontStyle.Normal)
    {
        GameObject go = CreateElement("Text", parent, typeof(CanvasRenderer), typeof(Text));
        Text text = go.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = style;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    public static Button CreateButton(Transform parent, Font font, string label, UnityAction onClick,
        Color color, int fontSize = 12, float preferredHeight = 30f)
    {
        GameObject go = CreateElement("Button", parent, typeof(CanvasRenderer), typeof(Image), typeof(Button),
            typeof(LayoutElement));
        Image image = go.GetComponent<Image>();
        image.color = color;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        if (onClick != null)
        {
            button.onClick.AddListener(onClick);
        }

        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        layout.minHeight = preferredHeight;

        Text text = CreateText(go.transform, font, label, fontSize, Color.white, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
        return button;
    }

    public static InputField CreateInputField(Transform parent, Font font, string initialValue,
        UnityAction<string> onValueChanged, UnityAction<string> onEndEdit,
        Color backgroundColor, Color textColor, int fontSize = 12, float preferredHeight = 32f,
        int characterLimit = 10, string placeholderText = "")
    {
        GameObject go = CreateElement("InputField", parent, typeof(CanvasRenderer), typeof(Image),
            typeof(InputField), typeof(LayoutElement));
        Image image = go.GetComponent<Image>();
        image.color = backgroundColor;
        image.raycastTarget = true;

        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        layout.minHeight = preferredHeight;

        GameObject textGo = CreateElement("Text", go.transform, typeof(CanvasRenderer), typeof(Text));
        Text text = textGo.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.supportRichText = false;
        text.raycastTarget = false;
        Stretch(text.rectTransform, new Vector2(6, 2), new Vector2(-6, -2));

        GameObject placeholderGo = CreateElement("Placeholder", go.transform, typeof(CanvasRenderer), typeof(Text));
        Text placeholder = placeholderGo.GetComponent<Text>();
        placeholder.font = font;
        placeholder.fontSize = fontSize;
        placeholder.color = new Color(textColor.r, textColor.g, textColor.b, 0.45f);
        placeholder.alignment = TextAnchor.MiddleCenter;
        placeholder.text = placeholderText;
        placeholder.fontStyle = FontStyle.Italic;
        placeholder.raycastTarget = false;
        Stretch(placeholder.rectTransform, new Vector2(6, 2), new Vector2(-6, -2));

        InputField inputField = go.GetComponent<InputField>();
        inputField.targetGraphic = image;
        inputField.textComponent = text;
        inputField.placeholder = placeholder;
        inputField.characterLimit = characterLimit;
        inputField.text = initialValue;
        inputField.navigation = new Navigation { mode = Navigation.Mode.None };

        ColorBlock colors = inputField.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
        inputField.colors = colors;

        if (onValueChanged != null) inputField.onValueChanged.AddListener(onValueChanged);
        if (onEndEdit != null) inputField.onEndEdit.AddListener(onEndEdit);

        return inputField;
    }

    public static VerticalLayoutGroup AddVerticalLayout(GameObject go, float spacing = 4f, int padding = 8,
        bool controlWidth = true, bool controlHeight = true)
    {
        VerticalLayoutGroup group = go.AddComponent<VerticalLayoutGroup>();
        group.spacing = spacing;
        group.padding = new RectOffset(padding, padding, padding, padding);
        group.childControlWidth = controlWidth;
        group.childControlHeight = controlHeight;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
        group.childAlignment = TextAnchor.MiddleLeft;
        return group;
    }

    public static HorizontalLayoutGroup AddHorizontalLayout(GameObject go, float spacing = 4f, int padding = 0,
        bool controlWidth = true, bool controlHeight = true)
    {
        HorizontalLayoutGroup group = go.AddComponent<HorizontalLayoutGroup>();
        group.spacing = spacing;
        group.padding = new RectOffset(padding, padding, padding, padding);
        group.childControlWidth = controlWidth;
        group.childControlHeight = controlHeight;
        group.childForceExpandWidth = true;
        group.childForceExpandHeight = false;
        group.childAlignment = TextAnchor.MiddleLeft;
        return group;
    }

    public static GridLayoutGroup AddGridLayout(GameObject go, Vector2 cellSize, Vector2 spacing, int columns)
    {
        GridLayoutGroup group = go.AddComponent<GridLayoutGroup>();
        group.cellSize = cellSize;
        group.spacing = spacing;
        group.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        group.constraintCount = columns;
        group.childAlignment = TextAnchor.UpperCenter;
        return group;
    }

    public static ContentSizeFitter AddSizeFitter(GameObject go,
        ContentSizeFitter.FitMode vertical = ContentSizeFitter.FitMode.PreferredSize,
        ContentSizeFitter.FitMode horizontal = ContentSizeFitter.FitMode.Unconstrained)
    {
        ContentSizeFitter fitter = go.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = vertical;
        fitter.horizontalFit = horizontal;
        return fitter;
    }
}

