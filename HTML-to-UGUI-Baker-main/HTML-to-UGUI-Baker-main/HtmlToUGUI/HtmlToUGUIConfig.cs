using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Editor.UIBaker
{
    [System.Serializable]
    public class UIResolutionConfig
    {
        public string displayName;
        public Vector2 resolution;
    }

    [System.Serializable]
    public class UIFontMapping
    {
        [Tooltip("CSS 或 HTML 中的字体名关键字，例如 PingFang SC、Microsoft YaHei、Arial")]
        public string cssFontFamily;

        public TMP_FontAsset tmpFont;

        public Font legacyFont;
    }

    [System.Serializable]
    public class UISpriteMapping
    {
        [Tooltip("用于匹配 data-u-sprite、图片文件名、节点名的关键字")]
        public string key;

        public Sprite sprite;
    }

    [CreateAssetMenu(fileName = "HtmlToUGUIConfig", menuName = "UI Architecture/HtmlToUGUI Config")]
    public class HtmlToUGUIConfig : ScriptableObject
    {
        [Header("支持的分辨率预设")]
        public List<UIResolutionConfig> supportedResolutions = new List<UIResolutionConfig>()
        {
            new UIResolutionConfig { displayName = "PC 横屏 (1920x1080)", resolution = new Vector2(1920, 1080) },
            new UIResolutionConfig { displayName = "Mobile 竖屏 (1080x1920)", resolution = new Vector2(1080, 1920) },
            new UIResolutionConfig { displayName = "Pad 横屏 (2048x1536)", resolution = new Vector2(2048, 1536) }
        };

        [Header("DSL 文档模板 (.md 文件)")]
        [Tooltip("请拖入包含 {WIDTH} 和 {HEIGHT} 占位符的 Markdown 模板文件")]
        public TextAsset dslTemplateAsset;

        [Header("默认字体")]
        public TMP_FontAsset defaultTMPFont;

        public Font defaultLegacyFont;

        [Header("字体映射")]
        public List<UIFontMapping> fontMappings = new List<UIFontMapping>();

        [Header("图片映射")]
        public List<UISpriteMapping> spriteMappings = new List<UISpriteMapping>();
    }
}
