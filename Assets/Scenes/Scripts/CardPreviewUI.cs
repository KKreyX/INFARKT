using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardPreviewUI : MonoBehaviour
{
    [Header("Preview Area")]
    [SerializeField] private RectTransform previewContainer;
    [SerializeField] private CardView previewCardPrefab;
    [SerializeField] private float previewScale = 2.5f;

    [Header("Description")]
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Sprite descriptionBackground;
    [SerializeField] private Sprite scrollbarTrack;
    [SerializeField] private Sprite scrollbarThumb;

    private RectTransform panel;
    private TMP_Text titleText;
    private ScrollRect descriptionScroll;

    [Header("Optional")]
    [SerializeField] private CanvasGroup previewCanvasGroup;

    public TMP_FontAsset MoveLabelFont => descriptionText != null ? descriptionText.font : null;

    private CardView currentPreview;
    private string currentCardId = "";

    void Awake()
    {
        if (previewCardPrefab != null && previewContainer != null)
        {
            if (descriptionBackground != null) BuildDescriptionPanel();
            currentPreview = Instantiate(previewCardPrefab, panel != null ? panel : previewContainer);
            currentPreview.SetInteractable(false);

            foreach (var img in currentPreview.GetComponentsInChildren<Image>(true)) img.raycastTarget = false;
            foreach (var txt in currentPreview.GetComponentsInChildren<TextMeshProUGUI>(true)) txt.raycastTarget = false;

            var rt = currentPreview.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = panel != null ? new Vector2(-110f, 0f) : Vector2.zero;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one * (panel != null ? 1.05f : previewScale);
            }
        }
        Hide();
    }

    public void Show(Card card)
    {
        if (card == null) { Hide(); return; }
        if (currentPreview == null) return;

        bool changedCard = !currentPreview.gameObject.activeSelf || currentCardId != card.cardId;

        currentCardId = card.cardId;
        currentPreview.DisplayCard(card);
        currentPreview.gameObject.SetActive(true);

        if (descriptionText != null)
        {
            if (panel == null)
            {
                descriptionText.text = card.GetDisplayInfo();
            }
            else
            {
                panel.gameObject.SetActive(true);
                titleText.text = card.cardName;
                descriptionText.text = GetDescription(card);
                var viewport = descriptionScroll.viewport;
                float height = descriptionText.GetPreferredValues(descriptionText.text, viewport.rect.width, 0f).y;
                descriptionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    Mathf.Max(viewport.rect.height, height + 8f));
                if (changedCard)
                {
                    descriptionScroll.StopMovement();
                    descriptionScroll.verticalNormalizedPosition = 1f;
                }
            }
        }

        if (previewCanvasGroup != null) previewCanvasGroup.alpha = 1f;
    }

    private void BuildDescriptionPanel()
    {
        panel = CreateRect("Card Details", previewContainer, new Vector2(408f, 266f), new Vector2(108f, 0f));
        var background = panel.gameObject.AddComponent<Image>();
        background.sprite = descriptionBackground;
        background.raycastTarget = false;

        var title = CreateRect("Name", panel, new Vector2(188f, 42f), new Vector2(84f, 105f));
        titleText = CreateText(title, 19f);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold | FontStyles.Italic | FontStyles.Underline;
        titleText.enableAutoSizing = true;
        titleText.fontSizeMin = 12f;
        titleText.fontSizeMax = 19f;

        var viewport = CreateRect("Description viewport", panel, new Vector2(174f, 190f), new Vector2(77f, -11f));
        viewport.gameObject.AddComponent<Image>().color = Color.clear;
        viewport.GetComponent<CanvasRenderer>().cullTransparentMesh = false;
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = CreateRect("Effects", viewport, Vector2.zero, Vector2.zero);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        descriptionText = CreateText(content, 15f);

        var track = CreateRect("Scroll track", panel, new Vector2(8f, 190f), new Vector2(180f, -11f));
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.sprite = scrollbarTrack;
        trackImage.color = new Color(1f, 1f, 1f, 0.28f);
        var slidingArea = CreateRect("Sliding area", track, Vector2.zero, Vector2.zero);
        slidingArea.anchorMin = Vector2.zero;
        slidingArea.anchorMax = Vector2.one;
        var handle = CreateRect("Scroll handle", slidingArea, Vector2.zero, Vector2.zero);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = scrollbarThumb;
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;

        descriptionScroll = panel.gameObject.AddComponent<ScrollRect>();
        descriptionScroll.viewport = viewport;
        descriptionScroll.content = content;
        descriptionScroll.horizontal = false;
        descriptionScroll.movementType = ScrollRect.MovementType.Clamped;
        descriptionScroll.verticalScrollbar = scrollbar;
        descriptionScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        descriptionScroll.scrollSensitivity = 26f;
    }

    private TextMeshProUGUI CreateText(RectTransform rect, float size)
    {
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (previewCardPrefab.StatFont != null) label.font = previewCardPrefab.StatFont;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.raycastTarget = false;
        return label;
    }

    private static RectTransform CreateRect(string label, Transform parent, Vector2 size, Vector2 position)
    {
        var child = new GameObject(label, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static string GetDescription(Card card)
    {
        string active = "";
        string passive = "";
        if (card.category == CardCategory.Spell)
        {
            active = JoinEffect(card.spellEffectName, card.spellEffectDescription);
            if (card.hasArcaneAlternative)
                active += "\n\n" + JoinEffect(card.arcaneEffectName, card.arcaneEffectDescription);
        }
        else if (card.abilityType == AbilityType.Passive)
            passive = JoinEffect(card.specialAbilityName, card.abilityDescription);
        else
            active = JoinEffect(card.specialAbilityName, card.abilityDescription);

        return $"<b><color=#30AAC5>Активный</color> эффект:</b>\n{active}\n\n" +
               $"<b><color=#77B45E>Пассивный</color> эффект:</b>\n{passive}";
    }

    private static string JoinEffect(string title, string description) =>
        string.IsNullOrWhiteSpace(title) ? description : $"{title}\n{description}";

    public void HideIfShowing(string cardId)
    {
        if (currentCardId == cardId) Hide();
    }

    public void Hide()
    {
        if (currentPreview != null) currentPreview.gameObject.SetActive(false);
        currentCardId = "";
        if (panel != null) panel.gameObject.SetActive(false);
        if (descriptionText != null) descriptionText.text = "";
        if (previewCanvasGroup != null) previewCanvasGroup.alpha = 0f;
    }
}