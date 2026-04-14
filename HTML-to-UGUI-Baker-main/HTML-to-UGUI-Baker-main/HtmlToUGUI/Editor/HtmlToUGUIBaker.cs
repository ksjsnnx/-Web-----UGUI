
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Editor.UIBaker
{
    [Serializable]
    public class UIDataNode
    {
        public string name, type, dir, color, fontColor, text, textAlign, fontFamily, fontWeight, fontStyle, borderColor, imagePath, spriteKey;
        public float value, x, y, width, height, lineHeight, letterSpacing, opacity, borderWidth, borderRadius;
        public int fontSize;
        public bool isChecked;
        public List<string> options;
        public List<UIDataNode> children;
    }

    public class HtmlToUGUIBaker : EditorWindow
    {
        enum InputMode { FileAsset, RawString }

        InputMode currentMode = InputMode.FileAsset;
        TextAsset jsonAsset;
        string rawJsonString = "";
        string converterUrl = "";
        Vector2 scrollPosition;
        Canvas targetCanvas;
        HtmlToUGUIConfig config;
        int selectedResolutionIndex;
        bool useLegacyText;
        SerializedObject configSO;
        SerializedProperty resolutionsProp, dslTemplateProp, defaultTMPFontProp, defaultLegacyFontProp, fontMappingsProp, spriteMappingsProp;

        const string PREFS_URL_KEY = "HtmlToUGUIBaker_ConverterUrl";
        const string PREFS_CONFIG_PATH_KEY = "HtmlToUGUIBaker_ConfigPath";
        const string PREFS_RES_INDEX_KEY = "HtmlToUGUIBaker_ResIndex";
        const string PREFS_USE_LEGACY_TEXT_KEY = "HtmlToUGUIBaker_UseLegacyText";

        [MenuItem("Tools/UI Architecture/HTML to UGUI Baker (Full Controls)")]
        public static void ShowWindow() => GetWindow<HtmlToUGUIBaker>("UI 原型烘焙器");

        void OnEnable()
        {
            converterUrl = EditorPrefs.GetString(PREFS_URL_KEY, "");
            string configPath = EditorPrefs.GetString(PREFS_CONFIG_PATH_KEY, "");
            if (!string.IsNullOrEmpty(configPath)) config = AssetDatabase.LoadAssetAtPath<HtmlToUGUIConfig>(configPath);
            selectedResolutionIndex = EditorPrefs.GetInt(PREFS_RES_INDEX_KEY, 0);
            useLegacyText = EditorPrefs.GetBool(PREFS_USE_LEGACY_TEXT_KEY, false);
        }

        void OnGUI()
        {
            GUILayout.Label("基于坐标烘焙的 UI 原型生成工具 (增强样式版)", EditorStyles.boldLabel);
            GUILayout.Space(10);
            DrawConfigUI();
            DrawExternalToolchainUI();
            targetCanvas = (Canvas)EditorGUILayout.ObjectField("目标 Canvas", targetCanvas, typeof(Canvas), true);
            EditorGUI.BeginChangeCheck();
            useLegacyText = EditorGUILayout.Toggle("使用旧版 Text (Legacy)", useLegacyText);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool(PREFS_USE_LEGACY_TEXT_KEY, useLegacyText);
            GUILayout.Space(10);
            currentMode = (InputMode)GUILayout.Toolbar((int)currentMode, new[] { "读取 JSON 文件", "直接粘贴 JSON 字符" });
            GUILayout.Space(10);
            if (currentMode == InputMode.FileAsset) DrawFileModeUI(); else DrawStringModeUI();
            GUILayout.Space(20);
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            if (GUILayout.Button("执行烘焙生成", GUILayout.Height(40))) ExecuteBake();
            GUI.backgroundColor = Color.white;
        }

        void DrawConfigUI()
        {
            GUILayout.Label("多分辨率与资源配置", EditorStyles.label);
            GUILayout.BeginVertical("box");
            EditorGUI.BeginChangeCheck();
            config = (HtmlToUGUIConfig)EditorGUILayout.ObjectField("配置文件 (SO)", config, typeof(HtmlToUGUIConfig), false);
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetString(PREFS_CONFIG_PATH_KEY, config != null ? AssetDatabase.GetAssetPath(config) : "");
                selectedResolutionIndex = 0;
                EditorPrefs.SetInt(PREFS_RES_INDEX_KEY, selectedResolutionIndex);
                configSO = null;
            }
            if (config == null)
            {
                EditorGUILayout.HelpBox("请先创建并分配 HtmlToUGUIConfig 配置文件。", MessageType.Warning);
                GUILayout.EndVertical();
                GUILayout.Space(10);
                return;
            }

            EnsureConfigSO();
            configSO.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(resolutionsProp, new GUIContent("分辨率预设列表"), true);
            EditorGUILayout.PropertyField(dslTemplateProp);
            EditorGUILayout.PropertyField(defaultTMPFontProp);
            EditorGUILayout.PropertyField(defaultLegacyFontProp);
            EditorGUILayout.PropertyField(fontMappingsProp, new GUIContent("字体映射"), true);
            EditorGUILayout.PropertyField(spriteMappingsProp, new GUIContent("图片映射"), true);
            if (EditorGUI.EndChangeCheck())
            {
                configSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(config);
            }
            if (config.supportedResolutions == null || config.supportedResolutions.Count == 0)
            {
                EditorGUILayout.HelpBox("配置文件中未定义任何分辨率数据。", MessageType.Error);
                GUILayout.EndVertical();
                GUILayout.Space(10);
                return;
            }

            selectedResolutionIndex = Mathf.Clamp(selectedResolutionIndex, 0, config.supportedResolutions.Count - 1);
            string[] resNames = new string[config.supportedResolutions.Count];
            for (int i = 0; i < config.supportedResolutions.Count; i++) resNames[i] = config.supportedResolutions[i].displayName;
            EditorGUI.BeginChangeCheck();
            selectedResolutionIndex = EditorGUILayout.Popup("目标分辨率", selectedResolutionIndex, resNames);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetInt(PREFS_RES_INDEX_KEY, selectedResolutionIndex);
            if (GUILayout.Button("复制对应分辨率的 DSL 规范文档", GUILayout.Height(25))) CopyDSLToClipboard();
            GUILayout.EndVertical();
            GUILayout.Space(10);
        }

        void EnsureConfigSO()
        {
            if (configSO != null && configSO.targetObject == config) return;
            configSO = new SerializedObject(config);
            resolutionsProp = configSO.FindProperty("supportedResolutions");
            dslTemplateProp = configSO.FindProperty("dslTemplateAsset");
            defaultTMPFontProp = configSO.FindProperty("defaultTMPFont");
            defaultLegacyFontProp = configSO.FindProperty("defaultLegacyFont");
            fontMappingsProp = configSO.FindProperty("fontMappings");
            spriteMappingsProp = configSO.FindProperty("spriteMappings");
        }

        void DrawExternalToolchainUI()
        {
            GUILayout.Label("外部工具链桥接", EditorStyles.label);
            GUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            converterUrl = EditorGUILayout.TextField("转换器路径 / URL", converterUrl);
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetString(PREFS_URL_KEY, converterUrl);
            if (GUILayout.Button("浏览...", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFilePanel("选择 HTML 转换器", "", "html");
                if (!string.IsNullOrEmpty(path))
                {
                    converterUrl = "file:///" + path.Replace("\\", "/");
                    EditorPrefs.SetString(PREFS_URL_KEY, converterUrl);
                    GUI.FocusControl(null);
                }
            }
            if (GUILayout.Button("在浏览器中打开", GUILayout.Width(120)))
            {
                if (string.IsNullOrWhiteSpace(converterUrl)) { Debug.LogError("[HtmlToUGUIBaker] 转换器路径或 URL 为空。"); return; }
                Application.OpenURL(converterUrl);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        void DrawFileModeUI()
        {
            jsonAsset = (TextAsset)EditorGUILayout.ObjectField("JSON 数据源", jsonAsset, typeof(TextAsset), false);
            EditorGUILayout.HelpBox("请将工程目录下的 .json 文件拖拽至此。", MessageType.Info);
        }

        void DrawStringModeUI()
        {
            GUILayout.Label("在此粘贴 JSON 文本:", EditorStyles.label);
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(220));
            rawJsonString = EditorGUILayout.TextArea(rawJsonString, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
            if (GUILayout.Button("将当前 JSON 保存为文件到工程目录...")) SaveRawJsonToProject();
        }

        void CopyDSLToClipboard()
        {
            if (config == null || config.supportedResolutions.Count <= selectedResolutionIndex) { Debug.LogError("[HtmlToUGUIBaker] 配置文件缺失或分辨率索引越界。"); return; }
            if (config.dslTemplateAsset == null) { Debug.LogError("[HtmlToUGUIBaker] 未指定 DSL 模板文件。"); return; }
            Vector2 res = config.supportedResolutions[selectedResolutionIndex].resolution;
            GUIUtility.systemCopyBuffer = config.dslTemplateAsset.text.Replace("{WIDTH}", res.x.ToString()).Replace("{HEIGHT}", res.y.ToString());
            Debug.Log($"[HtmlToUGUIBaker] 已复制 {res.x}x{res.y} DSL 规范文档。");
        }

        void SaveRawJsonToProject()
        {
            if (string.IsNullOrWhiteSpace(rawJsonString)) { Debug.LogError("[HtmlToUGUIBaker] 当前 JSON 字符串为空。"); return; }
            string savePath = EditorUtility.SaveFilePanelInProject("保存 JSON 数据", "NewUIWindow.json", "json", "请选择要保存的目录");
            if (string.IsNullOrEmpty(savePath)) return;
            try
            {
                File.WriteAllText(savePath, rawJsonString);
                AssetDatabase.Refresh();
                TextAsset savedAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(savePath);
                if (savedAsset != null) { jsonAsset = savedAsset; currentMode = InputMode.FileAsset; }
            }
            catch (Exception e) { Debug.LogError($"[HtmlToUGUIBaker] 文件写入失败: {e.Message}"); }
        }

        void ExecuteBake()
        {
            if (targetCanvas == null) { Debug.LogError("[HtmlToUGUIBaker] 未指定目标 Canvas。"); return; }
            string jsonContent = currentMode == InputMode.FileAsset ? (jsonAsset != null ? jsonAsset.text : "") : rawJsonString;
            if (string.IsNullOrWhiteSpace(jsonContent)) { Debug.LogError("[HtmlToUGUIBaker] JSON 内容为空。"); return; }
            ConfigureCanvasScaler(targetCanvas);
            UIDataNode rootNode;
            try { rootNode = JsonConvert.DeserializeObject<UIDataNode>(jsonContent); }
            catch (Exception e) { Debug.LogError($"[HtmlToUGUIBaker] JSON 解析异常: {e.Message}"); return; }
            if (rootNode == null) { Debug.LogError("[HtmlToUGUIBaker] JSON 解析结果为空。"); return; }
            NormalizeNodeData(rootNode);
            GameObject rootGo = CreateUINode(rootNode, targetCanvas.transform, 0f, 0f);
            Undo.RegisterCreatedObjectUndo(rootGo, "Bake UI Prototype");
            Selection.activeGameObject = rootGo;
            Debug.Log($"[HtmlToUGUIBaker] 烘焙完成: [{rootGo.name}]");
        }

        void NormalizeNodeData(UIDataNode node)
        {
            if (node == null) return;
            if (string.IsNullOrEmpty(node.type)) node.type = "div";
            if (node.opacity <= 0f) node.opacity = 1f;
            if (node.options == null) node.options = new List<string>();
            if (node.children == null) node.children = new List<UIDataNode>();
            foreach (UIDataNode child in node.children) NormalizeNodeData(child);
        }
        void ConfigureCanvasScaler(Canvas canvas)
        {
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            Vector2 targetRes = new Vector2(1920, 1080);
            if (config != null && config.supportedResolutions != null && config.supportedResolutions.Count > selectedResolutionIndex)
                targetRes = config.supportedResolutions[selectedResolutionIndex].resolution;
            scaler.referenceResolution = targetRes;
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameObject CreateUINode(UIDataNode nodeData, Transform parent, float parentAbsX, float parentAbsY)
        {
            GameObject go = new GameObject(string.IsNullOrWhiteSpace(nodeData.name) ? "uiNode" : nodeData.name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(nodeData.x - parentAbsX, -(nodeData.y - parentAbsY));
            rect.sizeDelta = new Vector2(nodeData.width, nodeData.height);
            Transform childrenContainer = ApplyComponentByType(go, nodeData);
            if (nodeData.children != null) foreach (UIDataNode childNode in nodeData.children) CreateUINode(childNode, childrenContainer, nodeData.x, nodeData.y);
            PostProcessGeneratedNode(nodeData, childrenContainer);
            return go;
        }

        void PostProcessGeneratedNode(UIDataNode nodeData, Transform childrenContainer)
        {
            if (!string.Equals(nodeData.type, "scroll", StringComparison.OrdinalIgnoreCase)) return;
            RectTransform contentRect = childrenContainer as RectTransform;
            if (contentRect == null) return;
            float maxWidth = nodeData.width, maxHeight = nodeData.height;
            foreach (UIDataNode child in nodeData.children)
            {
                maxWidth = Mathf.Max(maxWidth, child.x - nodeData.x + child.width);
                maxHeight = Mathf.Max(maxHeight, child.y - nodeData.y + child.height);
            }
            contentRect.sizeDelta = new Vector2(maxWidth, maxHeight);
        }

        Transform ApplyComponentByType(GameObject go, UIDataNode nodeData)
        {
            Color bgColor = GetNodeColor(nodeData.color, Color.white, nodeData.opacity);
            Color fontColor = GetNodeColor(nodeData.fontColor, Color.black, nodeData.opacity);
            int fontSize = nodeData.fontSize > 0 ? nodeData.fontSize : 24;
            bool isMultiLine = nodeData.height > fontSize * 1.5f || (!string.IsNullOrEmpty(nodeData.text) && nodeData.text.Contains("\n"));
            switch ((nodeData.type ?? "div").ToLowerInvariant())
            {
                case "div":
                case "image":
                    Image img = go.AddComponent<Image>();
                    ConfigureImageGraphic(img, nodeData, bgColor, nodeData.type == "image");
                    return go.transform;
                case "text":
                    if (useLegacyText)
                    {
                        Text txt = go.AddComponent<Text>();
                        ApplyLegacyTextStyle(txt, nodeData, fontColor, fontSize, isMultiLine, ParseLegacyTextAlign(nodeData.textAlign));
                    }
                    else
                    {
                        TextMeshProUGUI txt = go.AddComponent<TextMeshProUGUI>();
                        ApplyTMPTextStyle(txt, nodeData, fontColor, fontSize, isMultiLine, ParseTextAlign(nodeData.textAlign));
                    }
                    return go.transform;
                case "button": return BuildButton(go, nodeData, bgColor, fontColor, fontSize);
                case "input": return BuildInput(go, nodeData, bgColor, fontColor, fontSize, isMultiLine);
                case "scroll": return BuildScroll(go, nodeData, bgColor);
                case "toggle": return BuildToggle(go, nodeData, bgColor, fontColor, fontSize);
                case "slider": return BuildSlider(go, nodeData, bgColor, fontColor);
                case "dropdown": return BuildDropdown(go, nodeData, bgColor, fontColor, fontSize);
                default:
                    Debug.LogWarning($"[HtmlToUGUIBaker] 未知节点类型: {nodeData.type}");
                    return go.transform;
            }
        }

        Transform BuildButton(GameObject go, UIDataNode nodeData, Color bgColor, Color fontColor, int fontSize)
        {
            Image btnImg = go.AddComponent<Image>();
            ConfigureImageGraphic(btnImg, nodeData, bgColor, false);
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            float inset = Mathf.Max(8f, nodeData.borderWidth + 4f);
            GameObject txtGo = CreateChildRect(go, useLegacyText ? "Text" : "Text (TMP)", Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));
            if (useLegacyText)
            {
                Text txt = txtGo.AddComponent<Text>();
                ApplyLegacyTextStyle(txt, nodeData, fontColor, fontSize, false, ParseLegacyTextAlign(nodeData.textAlign));
            }
            else
            {
                TextMeshProUGUI txt = txtGo.AddComponent<TextMeshProUGUI>();
                ApplyTMPTextStyle(txt, nodeData, fontColor, fontSize, false, ParseTextAlign(nodeData.textAlign));
            }
            return go.transform;
        }

        Transform BuildInput(GameObject go, UIDataNode nodeData, Color bgColor, Color fontColor, int fontSize, bool isMultiLine)
        {
            Image inputBg = go.AddComponent<Image>();
            ConfigureImageGraphic(inputBg, nodeData, bgColor, false);
            float h = Mathf.Max(10f, nodeData.borderWidth + 8f), v = Mathf.Max(5f, nodeData.borderWidth + 4f);
            GameObject textAreaGo = CreateChildRect(go, "Text Area", Vector2.zero, Vector2.one, new Vector2(h, v), new Vector2(-h, -v));
            textAreaGo.AddComponent<RectMask2D>();
            GameObject phGo = CreateChildRect(textAreaGo, "Placeholder", Vector2.zero, Vector2.one);
            GameObject textGo = CreateChildRect(textAreaGo, "Text", Vector2.zero, Vector2.one);
            Color placeholderColor = fontColor; placeholderColor.a *= 0.55f;
            if (useLegacyText)
            {
                InputField field = go.AddComponent<InputField>();
                field.targetGraphic = inputBg;
                field.lineType = isMultiLine ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
                Text ph = phGo.AddComponent<Text>(); ApplyLegacyTextStyle(ph, nodeData, placeholderColor, fontSize, false, ParseLegacyTextAlign(nodeData.textAlign));
                Text text = textGo.AddComponent<Text>(); ApplyLegacyTextStyle(text, nodeData, fontColor, fontSize, isMultiLine, ParseLegacyTextAlign(nodeData.textAlign));
                field.placeholder = ph; field.textComponent = text;
            }
            else
            {
                TMP_InputField field = go.AddComponent<TMP_InputField>();
                field.targetGraphic = inputBg;
                field.lineType = isMultiLine ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
                TextMeshProUGUI ph = phGo.AddComponent<TextMeshProUGUI>(); ApplyTMPTextStyle(ph, nodeData, placeholderColor, fontSize, false, ParseTextAlign(nodeData.textAlign));
                TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>(); ApplyTMPTextStyle(text, nodeData, fontColor, fontSize, isMultiLine, ParseTextAlign(nodeData.textAlign));
                field.textViewport = textAreaGo.GetComponent<RectTransform>(); field.placeholder = ph; field.textComponent = text;
            }
            return go.transform;
        }

        Transform BuildScroll(GameObject go, UIDataNode nodeData, Color bgColor)
        {
            Image bg = go.AddComponent<Image>();
            ConfigureImageGraphic(bg, nodeData, bgColor, false);
            ScrollRect scrollRect = go.AddComponent<ScrollRect>();
            bool isVertical = string.IsNullOrEmpty(nodeData.dir) || nodeData.dir.Equals("v", StringComparison.OrdinalIgnoreCase);
            scrollRect.horizontal = !isVertical; scrollRect.vertical = isVertical; scrollRect.movementType = ScrollRect.MovementType.Clamped;
            GameObject viewportGo = CreateChildRect(go, "Viewport", Vector2.zero, Vector2.one);
            viewportGo.AddComponent<RectMask2D>();
            GameObject contentGo = CreateChildRect(viewportGo, "Content", new Vector2(0, 1), new Vector2(0, 1));
            RectTransform contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.pivot = new Vector2(0, 1);
            contentRect.sizeDelta = new Vector2(nodeData.width, nodeData.height);
            scrollRect.viewport = viewportGo.GetComponent<RectTransform>();
            scrollRect.content = contentRect;
            return contentGo.transform;
        }
        Transform BuildToggle(GameObject go, UIDataNode nodeData, Color bgColor, Color fontColor, int fontSize)
        {
            Toggle toggle = go.AddComponent<Toggle>();
            toggle.isOn = nodeData.isChecked;
            float boxSize = Mathf.Min(nodeData.height, 30f);
            GameObject bgGo = CreateChildRect(go, "Background", new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            RectTransform bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(boxSize, boxSize);
            bgRect.anchoredPosition = new Vector2(boxSize * 0.5f, 0f);
            Image bgImg = bgGo.AddComponent<Image>();
            ConfigureImageGraphic(bgImg, nodeData, bgColor.a > 0.01f ? bgColor : new Color(1f, 1f, 1f, nodeData.opacity), false);
            GameObject checkGo = CreateChildRect(bgGo, "Checkmark", Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
            Image checkImg = checkGo.AddComponent<Image>();
            checkImg.color = fontColor;
            GameObject lblGo = CreateChildRect(go, "Label", Vector2.zero, Vector2.one);
            lblGo.GetComponent<RectTransform>().offsetMin = new Vector2(boxSize + 12f, 0f);
            if (useLegacyText)
            {
                Text lbl = lblGo.AddComponent<Text>();
                ApplyLegacyTextStyle(lbl, nodeData, fontColor, fontSize, false, TextAnchor.MiddleLeft);
            }
            else
            {
                TextMeshProUGUI lbl = lblGo.AddComponent<TextMeshProUGUI>();
                ApplyTMPTextStyle(lbl, nodeData, fontColor, fontSize, false, TextAlignmentOptions.MidlineLeft);
            }
            toggle.targetGraphic = bgImg; toggle.graphic = checkImg;
            return go.transform;
        }

        Transform BuildSlider(GameObject go, UIDataNode nodeData, Color bgColor, Color fontColor)
        {
            Slider slider = go.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.value = Mathf.Clamp01(nodeData.value);
            GameObject bgGo = CreateChildRect(go, "Background", new Vector2(0, 0.25f), new Vector2(1, 0.75f));
            Image bgImg = bgGo.AddComponent<Image>(); bgImg.color = bgColor; ApplyBorderIfNeeded(bgImg, nodeData);
            GameObject fillAreaGo = CreateChildRect(go, "Fill Area", Vector2.zero, Vector2.one, new Vector2(5, 0), new Vector2(-15, 0));
            GameObject fillGo = CreateChildRect(fillAreaGo, "Fill", Vector2.zero, Vector2.one);
            Image fillImg = fillGo.AddComponent<Image>(); fillImg.color = fontColor;
            GameObject handleAreaGo = CreateChildRect(go, "Handle Slide Area", Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0));
            GameObject handleGo = CreateChildRect(handleAreaGo, "Handle", Vector2.zero, Vector2.one);
            RectTransform handleRect = handleGo.GetComponent<RectTransform>(); handleRect.sizeDelta = new Vector2(20, 0);
            Image handleImg = handleGo.AddComponent<Image>(); handleImg.color = Color.white;
            slider.targetGraphic = handleImg; slider.fillRect = fillGo.GetComponent<RectTransform>(); slider.handleRect = handleRect;
            return go.transform;
        }

        Transform BuildDropdown(GameObject go, UIDataNode nodeData, Color bgColor, Color fontColor, int fontSize)
        {
            Image bgImg = go.AddComponent<Image>();
            ConfigureImageGraphic(bgImg, nodeData, bgColor, false);
            GameObject lblGo = CreateChildRect(go, "Label", Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-30, 0));
            GameObject arrowGo = CreateChildRect(go, "Arrow", new Vector2(1, 0.5f), new Vector2(1, 0.5f));
            RectTransform arrowRect = arrowGo.GetComponent<RectTransform>(); arrowRect.sizeDelta = new Vector2(20, 20); arrowRect.anchoredPosition = new Vector2(-15, 0);
            Image arrowImg = arrowGo.AddComponent<Image>(); arrowImg.color = fontColor;
            GameObject templateGo = CreateChildRect(go, "Template", new Vector2(0, 0), new Vector2(1, 0));
            RectTransform templateRect = templateGo.GetComponent<RectTransform>(); templateRect.pivot = new Vector2(0.5f, 1f); templateRect.sizeDelta = new Vector2(0, Mathf.Max(150f, nodeData.options.Count * 32f)); templateRect.anchoredPosition = new Vector2(0, -2);
            Image tempImg = templateGo.AddComponent<Image>(); tempImg.color = Color.white;
            ScrollRect tempScroll = templateGo.AddComponent<ScrollRect>(); tempScroll.horizontal = false; tempScroll.vertical = true; templateGo.SetActive(false);
            GameObject viewportGo = CreateChildRect(templateGo, "Viewport", Vector2.zero, Vector2.one);
            viewportGo.AddComponent<Image>().color = Color.white;
            Mask mask = viewportGo.AddComponent<Mask>(); mask.showMaskGraphic = false;
            GameObject contentGo = CreateChildRect(viewportGo, "Content", new Vector2(0, 1), new Vector2(1, 1));
            RectTransform contentRect = contentGo.GetComponent<RectTransform>(); contentRect.pivot = new Vector2(0.5f, 1f); contentRect.sizeDelta = new Vector2(0, 28);
            GameObject itemGo = CreateChildRect(contentGo, "Item", new Vector2(0, 0.5f), new Vector2(1, 0.5f));
            itemGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 28);
            Toggle itemToggle = itemGo.AddComponent<Toggle>();
            GameObject itemBgGo = CreateChildRect(itemGo, "Item Background", Vector2.zero, Vector2.one);
            Image itemBgImg = itemBgGo.AddComponent<Image>(); itemBgImg.color = Color.white;
            GameObject itemCheckGo = CreateChildRect(itemGo, "Item Checkmark", new Vector2(0, 0.5f), new Vector2(0, 0.5f));
            RectTransform itemCheckRect = itemCheckGo.GetComponent<RectTransform>(); itemCheckRect.sizeDelta = new Vector2(20, 20); itemCheckRect.anchoredPosition = new Vector2(15, 0);
            Image itemCheckImg = itemCheckGo.AddComponent<Image>(); itemCheckImg.color = Color.black;
            GameObject itemLblGo = CreateChildRect(itemGo, "Item Label", Vector2.zero, Vector2.one, new Vector2(30, 0), new Vector2(-10, 0));
            itemToggle.targetGraphic = itemBgImg; itemToggle.graphic = itemCheckImg; tempScroll.viewport = viewportGo.GetComponent<RectTransform>(); tempScroll.content = contentRect;
            string caption = nodeData.options != null && nodeData.options.Count > 0 ? nodeData.options[0] : nodeData.text;
            UIDataNode captionNode = CloneNodeWithText(nodeData, caption);
            if (useLegacyText)
            {
                Dropdown dropdown = go.AddComponent<Dropdown>();
                Text lbl = lblGo.AddComponent<Text>(); ApplyLegacyTextStyle(lbl, captionNode, fontColor, fontSize, false, TextAnchor.MiddleLeft);
                Text itemLbl = itemLblGo.AddComponent<Text>(); ApplyLegacyTextStyle(itemLbl, captionNode, Color.black, fontSize, false, TextAnchor.MiddleLeft);
                dropdown.targetGraphic = bgImg; dropdown.template = templateRect; dropdown.captionText = lbl; dropdown.itemText = itemLbl;
                dropdown.ClearOptions(); List<Dropdown.OptionData> opts = new List<Dropdown.OptionData>(); foreach (string opt in nodeData.options) opts.Add(new Dropdown.OptionData(opt)); dropdown.AddOptions(opts);
            }
            else
            {
                TMP_Dropdown dropdown = go.AddComponent<TMP_Dropdown>();
                TextMeshProUGUI lbl = lblGo.AddComponent<TextMeshProUGUI>(); ApplyTMPTextStyle(lbl, captionNode, fontColor, fontSize, false, TextAlignmentOptions.MidlineLeft);
                TextMeshProUGUI itemLbl = itemLblGo.AddComponent<TextMeshProUGUI>(); ApplyTMPTextStyle(itemLbl, captionNode, Color.black, fontSize, false, TextAlignmentOptions.MidlineLeft);
                dropdown.targetGraphic = bgImg; dropdown.template = templateRect; dropdown.captionText = lbl; dropdown.itemText = itemLbl;
                dropdown.ClearOptions(); List<TMP_Dropdown.OptionData> opts = new List<TMP_Dropdown.OptionData>(); foreach (string opt in nodeData.options) opts.Add(new TMP_Dropdown.OptionData(opt)); dropdown.AddOptions(opts);
            }
            return go.transform;
        }

        UIDataNode CloneNodeWithText(UIDataNode nodeData, string text) => new UIDataNode { text = text, fontFamily = nodeData.fontFamily, fontWeight = nodeData.fontWeight, fontStyle = nodeData.fontStyle, letterSpacing = nodeData.letterSpacing, lineHeight = nodeData.lineHeight, opacity = nodeData.opacity, textAlign = nodeData.textAlign };

        void ConfigureImageGraphic(Image img, UIDataNode nodeData, Color color, bool preserveAspect)
        {
            img.color = color; img.preserveAspect = preserveAspect;
            Sprite sprite = ResolveSprite(nodeData);
            if (sprite != null) { img.sprite = sprite; if (nodeData.borderRadius > 0.1f || sprite.border.sqrMagnitude > 0f) img.type = Image.Type.Sliced; }
            if (img.color.a <= 0.01f && img.sprite == null) img.raycastTarget = false;
            ApplyBorderIfNeeded(img, nodeData);
        }

        void ApplyTMPTextStyle(TextMeshProUGUI txt, UIDataNode nodeData, Color color, int fontSize, bool isMultiLine, TextAlignmentOptions alignment)
        {
            txt.text = nodeData.text ?? string.Empty; txt.color = color; txt.fontSize = fontSize; txt.alignment = alignment; txt.enableWordWrapping = isMultiLine; txt.overflowMode = isMultiLine ? TextOverflowModes.Truncate : TextOverflowModes.Overflow; txt.raycastTarget = false; txt.enableAutoSizing = false; txt.fontStyle = ParseTMPFontStyle(nodeData); txt.characterSpacing = nodeData.letterSpacing; if (nodeData.lineHeight > 0f) txt.lineSpacing = nodeData.lineHeight - fontSize;
            TMP_FontAsset font = ResolveTMPFont(nodeData); if (font != null) txt.font = font;
        }

        void ApplyLegacyTextStyle(Text txt, UIDataNode nodeData, Color color, int fontSize, bool isMultiLine, TextAnchor alignment)
        {
            txt.text = nodeData.text ?? string.Empty; txt.color = color; txt.fontSize = fontSize; txt.alignment = alignment; txt.fontStyle = ParseLegacyFontStyle(nodeData); txt.horizontalOverflow = isMultiLine ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow; txt.verticalOverflow = isMultiLine ? VerticalWrapMode.Truncate : VerticalWrapMode.Overflow; txt.raycastTarget = false;
            Font font = ResolveLegacyFont(nodeData); if (font != null) txt.font = font;
        }
        Color GetNodeColor(string hex, Color defaultColor, float opacity)
        {
            Color color = ParseHexColor(hex, defaultColor);
            color.a *= Mathf.Clamp01(opacity <= 0f ? 1f : opacity);
            return color;
        }

        void ApplyBorderIfNeeded(Graphic graphic, UIDataNode nodeData)
        {
            if (graphic == null || nodeData == null || nodeData.borderWidth <= 0f) return;
            Outline outline = graphic.GetComponent<Outline>();
            if (outline == null) outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = GetNodeColor(nodeData.borderColor, Color.black, nodeData.opacity);
            float thickness = Mathf.Clamp(nodeData.borderWidth, 1f, 8f);
            outline.effectDistance = new Vector2(thickness, thickness);
            outline.useGraphicAlpha = true;
        }

        Sprite ResolveSprite(UIDataNode nodeData)
        {
            foreach (string key in GetSpriteLookupKeys(nodeData))
            {
                if (string.IsNullOrWhiteSpace(key)) continue;
                Sprite mapped = ResolveMappedSprite(key);
                if (mapped != null) return mapped;
                string[] guids = AssetDatabase.FindAssets($"{key} t:Sprite");
                if (guids.Length > 0)
                {
                    Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    if (sprite != null) return sprite;
                }
            }
            return null;
        }

        IEnumerable<string> GetSpriteLookupKeys(UIDataNode nodeData)
        {
            List<string> keys = new List<string>();
            AddLookupKey(keys, nodeData.spriteKey);
            AddLookupKey(keys, Path.GetFileNameWithoutExtension(nodeData.imagePath));
            AddLookupKey(keys, nodeData.imagePath);
            AddLookupKey(keys, nodeData.name);
            return keys;
        }

        void AddLookupKey(List<string> keys, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string key = value.Trim();
            if (!keys.Contains(key)) keys.Add(key);
        }

        Sprite ResolveMappedSprite(string lookupKey)
        {
            if (config == null || config.spriteMappings == null) return null;
            string lowerKey = lookupKey.ToLowerInvariant();
            foreach (UISpriteMapping mapping in config.spriteMappings)
            {
                if (mapping == null || mapping.sprite == null || string.IsNullOrWhiteSpace(mapping.key)) continue;
                string mappingKey = mapping.key.Trim().ToLowerInvariant();
                if (lowerKey == mappingKey || lowerKey.Contains(mappingKey) || mappingKey.Contains(lowerKey)) return mapping.sprite;
            }
            return null;
        }

        TMP_FontAsset ResolveTMPFont(UIDataNode nodeData)
        {
            if (config != null && config.fontMappings != null && !string.IsNullOrWhiteSpace(nodeData.fontFamily))
            {
                string target = nodeData.fontFamily.ToLowerInvariant();
                foreach (UIFontMapping mapping in config.fontMappings)
                    if (mapping != null && mapping.tmpFont != null && !string.IsNullOrWhiteSpace(mapping.cssFontFamily) && (target.Contains(mapping.cssFontFamily.ToLowerInvariant()) || mapping.cssFontFamily.ToLowerInvariant().Contains(target))) return mapping.tmpFont;
            }
            if (config != null && config.defaultTMPFont != null) return config.defaultTMPFont;
            return TMP_Settings.defaultFontAsset;
        }

        Font ResolveLegacyFont(UIDataNode nodeData)
        {
            if (config != null && config.fontMappings != null && !string.IsNullOrWhiteSpace(nodeData.fontFamily))
            {
                string target = nodeData.fontFamily.ToLowerInvariant();
                foreach (UIFontMapping mapping in config.fontMappings)
                    if (mapping != null && mapping.legacyFont != null && !string.IsNullOrWhiteSpace(mapping.cssFontFamily) && (target.Contains(mapping.cssFontFamily.ToLowerInvariant()) || mapping.cssFontFamily.ToLowerInvariant().Contains(target))) return mapping.legacyFont;
            }
            if (config != null && config.defaultLegacyFont != null) return config.defaultLegacyFont;
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        FontStyles ParseTMPFontStyle(UIDataNode nodeData)
        {
            FontStyles style = FontStyles.Normal;
            string weight = nodeData.fontWeight ?? "", fontStyle = nodeData.fontStyle ?? "";
            if (weight.IndexOf("bold", StringComparison.OrdinalIgnoreCase) >= 0 || ParseFontWeightValue(weight) >= 600) style |= FontStyles.Bold;
            if (fontStyle.IndexOf("italic", StringComparison.OrdinalIgnoreCase) >= 0) style |= FontStyles.Italic;
            return style;
        }

        FontStyle ParseLegacyFontStyle(UIDataNode nodeData)
        {
            bool bold = (nodeData.fontWeight ?? "").IndexOf("bold", StringComparison.OrdinalIgnoreCase) >= 0 || ParseFontWeightValue(nodeData.fontWeight) >= 600;
            bool italic = (nodeData.fontStyle ?? "").IndexOf("italic", StringComparison.OrdinalIgnoreCase) >= 0;
            if (bold && italic) return FontStyle.BoldAndItalic;
            if (bold) return FontStyle.Bold;
            if (italic) return FontStyle.Italic;
            return FontStyle.Normal;
        }

        int ParseFontWeightValue(string fontWeight) => int.TryParse(fontWeight, out int value) ? value : 400;

        TextAlignmentOptions ParseTextAlign(string alignStr)
        {
            if (string.IsNullOrEmpty(alignStr)) return TextAlignmentOptions.Midline;
            switch (alignStr.ToLowerInvariant())
            {
                case "left":
                case "start": return TextAlignmentOptions.MidlineLeft;
                case "right":
                case "end": return TextAlignmentOptions.MidlineRight;
                case "justify": return TextAlignmentOptions.Justified;
                default: return TextAlignmentOptions.Midline;
            }
        }

        TextAnchor ParseLegacyTextAlign(string alignStr)
        {
            if (string.IsNullOrEmpty(alignStr)) return TextAnchor.MiddleCenter;
            switch (alignStr.ToLowerInvariant())
            {
                case "left":
                case "start": return TextAnchor.MiddleLeft;
                case "right":
                case "end": return TextAnchor.MiddleRight;
                default: return TextAnchor.MiddleCenter;
            }
        }

        GameObject CreateChildRect(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = offsetMin ?? Vector2.zero; rect.offsetMax = offsetMax ?? Vector2.zero;
            return go;
        }

        Color ParseHexColor(string hex, Color defaultColor) => !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color color) ? color : defaultColor;
    }
}
