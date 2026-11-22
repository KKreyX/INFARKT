using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class CardView : MonoBehaviour
{
    [Header("Card Visuals")]
    [SerializeField] private Image cardImage;
    [SerializeField] private Image cardFrame;
    [SerializeField] private Sprite cardBackSprite;
    public TMP_FontAsset StatFont;

    [Header("Text Fields")]
    [SerializeField] private TextMeshProUGUI cardNameText;
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI baseValueText;
    [SerializeField] private TextMeshProUGUI sleepTimerText;

    [Header("Colors")]
    [SerializeField] private Color cardNameColor = Color.white;
    [SerializeField] private Color attackTextColor = Color.red;
    [SerializeField] private Color healthTextColor = Color.green;
    [SerializeField] private Color costTextColor = Color.yellow;
    [SerializeField] private Color baseValueTextColor = Color.cyan;
    [SerializeField] private Color sleepTimerTextColor = Color.white;

    [Header("Animations")]
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float hoverYOffset = 20f;
    [SerializeField] private float selectedYOffset = 40f;
    [SerializeField] private float animationDuration = 0.15f;
    [SerializeField] private Color selectedOutlineColor = Color.cyan;

    public event System.Action OnAnimationComplete;

    private Card currentCard;
    private ClassConfig currentClassConfig;
    private RectTransform rectTransform;
    private Vector2 originalAnchoredPosition;
    private Vector3 originalScale;
    private bool isHovered;
    private bool isSelected;
    private bool isOnBoard;
    private Coroutine animationCoroutine;
    private Outline outlineEffect;
    private bool showFace = true;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;
        originalAnchoredPosition = rectTransform.anchoredPosition;

        outlineEffect = GetComponent<Outline>();
        if (outlineEffect == null) outlineEffect = gameObject.AddComponent<Outline>();
        outlineEffect.enabled = false;
        outlineEffect.effectDistance = new Vector2(4, 4);
        outlineEffect.effectColor = selectedOutlineColor;
    }

    void Start()
    {
        if (originalScale == Vector3.zero) originalScale = rectTransform.localScale;
        if (originalAnchoredPosition == Vector2.zero) originalAnchoredPosition = rectTransform.anchoredPosition;
        ResetCardColor();
        UpdateFace();
    }

    void OnEnable() => ResetCardColor();

    public void ResetCardColor()
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        foreach (Image img in images)
        {
            Color c = img.color; c.a = 1f; img.color = c;
        }
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group != null) group.alpha = 1f;

        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI txt in texts)
        {
            Color c = txt.color; c.a = 1f; txt.color = c;
        }
        if (cardImage != null) { Color c = cardImage.color; c.a = 1f; cardImage.color = c; }
        if (cardFrame != null) { Color c = cardFrame.color; c.a = 1f; cardFrame.color = c; }
    }

    public void SetOnBoard(bool onBoard) => isOnBoard = onBoard;
    
    public void SetInteractable(bool interactable)
    {
        Graphic graphic = GetComponent<Graphic>();
        if (graphic != null) graphic.raycastTarget = interactable;
        Button button = GetComponent<Button>();
        if (button != null) button.interactable = interactable;
    }

    public void DisplayCard(Card card)
    {
        if (card == null) return;
        currentCard = card;
        currentClassConfig = CardLibrary.Instance?.GetClassConfig(card.classId);
        UpdateCardDisplay();
        ResetCardColor();
        UpdateFace();
    }

    public Card GetCurrentCard() => currentCard;
    public void UpdateVisuals() => UpdateCardDisplay();
    
    public void SaveOriginalState(Vector2 position, Vector3 scale) 
    { 
        originalAnchoredPosition = position; 
        originalScale = scale; 
    }

    public void SetPending(bool pending)
    {
        if (pending && cardImage != null) { Color c = cardImage.color; c.a = 0.5f; cardImage.color = c; }
        else ResetCardColor();
        SetInteractable(!pending);
    }

    public void SetFace(bool show) { showFace = show; UpdateFace(); }

    public void SetSleepVisual(int turnsLeft)
    {
        if (turnsLeft > 0)
        {
            if (sleepTimerText != null) 
            {
                sleepTimerText.gameObject.SetActive(true);
                sleepTimerText.text = turnsLeft.ToString();
                sleepTimerText.color = sleepTimerTextColor;
            }
            if (cardImage != null) { Color c = cardImage.color; c.a = 0.5f; cardImage.color = c; }
            if (cardFrame != null) { Color c = cardFrame.color; c.a = 0.5f; cardFrame.color = c; }
            
            SetInteractable(false);
        }
        else
        {
            if (sleepTimerText != null) sleepTimerText.gameObject.SetActive(false);
            ResetCardColor();
            SetInteractable(true);
        }
    }

    private void UpdateFace()
    {
        if (cardImage == null) return;
        if (showFace)
        {
            if (currentCard != null && currentClassConfig != null)
                cardImage.sprite = currentClassConfig.GetCardImage(currentCard);
            SetTextsActive(true);
        }
        else
        {
            if (cardBackSprite != null) cardImage.sprite = cardBackSprite;
            SetTextsActive(false);
        }
    }

    private void SetTextsActive(bool active)
    {
        if (cardNameText) cardNameText.gameObject.SetActive(active);
        if (attackText) attackText.gameObject.SetActive(active);
        if (healthText) healthText.gameObject.SetActive(active);
        if (costText) costText.gameObject.SetActive(active);
        if (baseValueText) baseValueText.gameObject.SetActive(active);
    }

    public void SetHovered(bool hovered, bool animate = true)
    {
        if (hovered && isSelected) return;
        
        isHovered = hovered;
        
        if (animate)
        {
            if (hovered) StartAnimation(originalScale * hoverScale, originalAnchoredPosition + new Vector2(0, hoverYOffset));
            else StartAnimation(originalScale, originalAnchoredPosition);
        }
        else
        {
            if (animationCoroutine != null) StopCoroutine(animationCoroutine);
            if (hovered)
            {
                rectTransform.localScale = originalScale * hoverScale;
                rectTransform.anchoredPosition = originalAnchoredPosition + new Vector2(0, hoverYOffset);
            }
            else
            {
                rectTransform.localScale = originalScale;
                rectTransform.anchoredPosition = originalAnchoredPosition;
            }
        }
    }

    public void SetSelected(bool selected, bool animate = true)
    {
        isSelected = selected;
        if (selected) isHovered = false;

        if (outlineEffect != null)
        {
            outlineEffect.enabled = selected;
            outlineEffect.effectColor = selected ? selectedOutlineColor : Color.white;
        }

        if (animate)
        {
            if (selected) StartAnimation(originalScale * selectedScale, originalAnchoredPosition + new Vector2(0, selectedYOffset));
            else StartAnimation(originalScale, originalAnchoredPosition);
        }
        else
        {
            if (animationCoroutine != null) StopCoroutine(animationCoroutine);
            if (selected)
            {
                rectTransform.localScale = originalScale * selectedScale;
                rectTransform.anchoredPosition = originalAnchoredPosition + new Vector2(0, selectedYOffset);
            }
            else
            {
                rectTransform.localScale = originalScale;
                rectTransform.anchoredPosition = originalAnchoredPosition;
            }
        }
    }

    public void ResetToOriginalState(bool animate = true) 
    { 
        SetSelected(false, animate); 
        SetHovered(false, animate); 
        
        if (!animate)
        {
            rectTransform.localScale = originalScale;
            rectTransform.anchoredPosition = originalAnchoredPosition;
        }
    }

    public bool IsSelected() => isSelected;
    public bool IsHovered() => isHovered;

    private void UpdateCardDisplay()
    {
        if (currentCard == null) return;
        if (cardImage != null && currentClassConfig != null) 
            cardImage.sprite = currentClassConfig.GetCardImage(currentCard);
            
        if (cardNameText != null) { cardNameText.text = currentCard.cardName; cardNameText.color = cardNameColor; }
        if (baseValueText != null) { baseValueText.text = GetBaseValueDisplay(currentCard); baseValueText.color = baseValueTextColor; }
        if (costText != null) { costText.text = currentCard.actionPointCost.ToString(); costText.color = costTextColor; }

        if (currentCard.category == CardCategory.Unit)
        {
            if (attackText != null) { attackText.text = currentCard.attack.ToString(); attackText.color = attackTextColor; attackText.gameObject.SetActive(true); }
            if (healthText != null) { healthText.text = currentCard.health.ToString(); healthText.color = healthTextColor; healthText.gameObject.SetActive(true); }
        }
        else
        {
            if (attackText != null) attackText.gameObject.SetActive(false);
            if (healthText != null) healthText.gameObject.SetActive(false);
        }
    }

    private string GetBaseValueDisplay(Card card)
    {
        if (card.category == CardCategory.Spell) return "S";
        switch (card.cardType)
        {
            case CardType.J: return "J";
            case CardType.Q: return "Q";
            case CardType.D: return "D";
            case CardType.K: return "K";
            case CardType.T: return "T";
            default: return card.baseValue.ToString();
        }
    }

    private void StartAnimation(Vector3 targetScale, Vector2 targetPosition)
    {
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimateToTarget(targetScale, targetPosition));
    }

    private IEnumerator AnimateToTarget(Vector3 targetScale, Vector2 targetPosition)
    {
        Vector3 startScale = rectTransform.localScale;
        Vector2 startPosition = rectTransform.anchoredPosition;
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            t = t * t * (3f - 2f * t);
            rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
            yield return null;
        }
        rectTransform.localScale = targetScale;
        rectTransform.anchoredPosition = targetPosition;
        animationCoroutine = null;
        OnAnimationComplete?.Invoke();
    }
}