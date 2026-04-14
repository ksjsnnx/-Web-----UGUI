using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public static class LoginUIBuilder
{
    [MenuItem("Tools/Create UGUI Login UI")]
    public static void CreateLoginUI()
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        Canvas canvas = GetOrCreateCanvas();
        EnsureEventSystemExists();

        GameObject root = new GameObject("LoginUI");
        Undo.RegisterCreatedObjectUndo(root, "Create Login UI Root");
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(420f, 320f);
        rootRect.anchoredPosition = Vector2.zero;

        Image panelImage = root.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.55f);

        CreateLabel(root.transform, "Title", "账号登录", new Vector2(0f, 122f), 30, 32);
        CreateLabel(root.transform, "UserLabel", "用户名", new Vector2(-140f, 55f), 18, 24);
        CreateLabel(root.transform, "PassLabel", "密码", new Vector2(-140f, -5f), 18, 24);

        GameObject userInput = CreateInputField(root.transform, "UsernameInput", new Vector2(30f, 55f), "请输入用户名", false);
        GameObject passInput = CreateInputField(root.transform, "PasswordInput", new Vector2(30f, -5f), "请输入密码", true);

        GameObject loginBtn = CreateButton(root.transform, "LoginButton", "登录", new Vector2(0f, -95f));

        // Keep selection on root for convenience.
        Selection.activeGameObject = root;

        Undo.CollapseUndoOperations(undoGroup);
    }

    private static Canvas GetOrCreateCanvas()
    {
        Canvas existingCanvas = Object.FindObjectOfType<Canvas>();
        if (existingCanvas != null)
        {
            return existingCanvas;
        }

        GameObject canvasGo = new GameObject("Canvas");
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void EnsureEventSystemExists()
    {
        EventSystem es = Object.FindObjectOfType<EventSystem>();
        if (es != null)
        {
            return;
        }

        GameObject esGo = new GameObject("EventSystem");
        Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();
    }

    private static GameObject CreateLabel(Transform parent, string name, string content, Vector2 anchoredPos, int fontSize, float height)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create Label");
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(280f, height);
        rect.anchoredPosition = anchoredPos;

        Text text = go.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        return go;
    }

    private static GameObject CreateInputField(Transform parent, string name, Vector2 anchoredPos, string placeholderText, bool isPassword)
    {
        GameObject inputGo = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(inputGo, "Create InputField");
        inputGo.transform.SetParent(parent, false);

        RectTransform rect = inputGo.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(250f, 40f);
        rect.anchoredPosition = anchoredPos;

        Image bg = inputGo.AddComponent<Image>();
        bg.color = Color.white;

        InputField inputField = inputGo.AddComponent<InputField>();
        if (isPassword)
        {
            inputField.contentType = InputField.ContentType.Password;
        }

        GameObject placeholderGo = new GameObject("Placeholder");
        Undo.RegisterCreatedObjectUndo(placeholderGo, "Create Placeholder");
        placeholderGo.transform.SetParent(inputGo.transform, false);
        RectTransform placeholderRect = placeholderGo.AddComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(10f, 6f);
        placeholderRect.offsetMax = new Vector2(-10f, -7f);

        Text placeholder = placeholderGo.AddComponent<Text>();
        placeholder.text = placeholderText;
        placeholder.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholder.fontSize = 18;
        placeholder.fontStyle = FontStyle.Italic;
        placeholder.color = new Color(0.5f, 0.5f, 0.5f, 0.75f);
        placeholder.alignment = TextAnchor.MiddleLeft;

        GameObject textGo = new GameObject("Text");
        Undo.RegisterCreatedObjectUndo(textGo, "Create Input Text");
        textGo.transform.SetParent(inputGo.transform, false);
        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 6f);
        textRect.offsetMax = new Vector2(-10f, -7f);

        Text text = textGo.AddComponent<Text>();
        text.text = string.Empty;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        text.color = new Color(0.1f, 0.1f, 0.1f);
        text.alignment = TextAnchor.MiddleLeft;

        inputField.textComponent = text;
        inputField.placeholder = placeholder;

        return inputGo;
    }

    private static GameObject CreateButton(Transform parent, string name, string buttonText, Vector2 anchoredPos)
    {
        GameObject btnGo = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(btnGo, "Create Button");
        btnGo.transform.SetParent(parent, false);

        RectTransform rect = btnGo.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(220f, 44f);
        rect.anchoredPosition = anchoredPos;

        Image image = btnGo.AddComponent<Image>();
        image.color = new Color(0.24f, 0.5f, 0.96f, 1f);

        Button button = btnGo.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.35f, 0.6f, 1f, 1f);
        colors.pressedColor = new Color(0.15f, 0.4f, 0.85f, 1f);
        button.colors = colors;

        GameObject textGo = new GameObject("Text");
        Undo.RegisterCreatedObjectUndo(textGo, "Create Button Text");
        textGo.transform.SetParent(btnGo.transform, false);

        RectTransform textRect = textGo.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textGo.AddComponent<Text>();
        text.text = buttonText;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;

        return btnGo;
    }
}
