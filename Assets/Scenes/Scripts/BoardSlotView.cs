using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BoardSlotView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public BoardPosition position;
    public int ownerId;
    public Color highlightFreeColor = Color.yellow;
    public Color highlightOccupiedColor = Color.red;

    private BoardManager boardManager;
    private Outline outlineEffect;
    private Image selectionImage;
    private Image hitFlash;
    private TMPro.TextMeshProUGUI damageLabel;
    private Coroutine hitAnimation;
    private bool initialized;

    void Awake()
    {
        outlineEffect = GetComponent<Outline>();
        if (outlineEffect == null) outlineEffect = gameObject.AddComponent<Outline>();
        outlineEffect.enabled = false;
        outlineEffect.effectDistance = new Vector2(4, 4);
        outlineEffect.effectColor = Color.white;
    }

    public void Initialize(BoardPosition pos, int owner, BoardManager manager)
    {
        position = pos;
        ownerId = owner;
        boardManager = manager;
        initialized = true;
        SetHighlightMode(false);
    }

    public void SetHighlightMode(bool active, Color? customColor = null, bool strong = false)
    {
        if (!initialized || boardManager == null) return;
        if (!active)
        {
            outlineEffect.enabled = false;
            return;
        }
        bool isFree = boardManager.IsSlotFree(ownerId, position);
        outlineEffect.enabled = true;
        outlineEffect.effectDistance = Vector2.one * (strong ? 9f : 4f);
        outlineEffect.effectColor = customColor ?? (isFree ? highlightFreeColor : highlightOccupiedColor);
    }

    public void SetSelection(bool selected, Sprite sprite = null)
    {
        if (selected && selectionImage == null)
        {
            if (sprite == null) return;

            var overlay = new GameObject("Card Selection", typeof(RectTransform), typeof(Image));
            overlay.layer = gameObject.layer;
            selectionImage = overlay.GetComponent<Image>();
            selectionImage.sprite = sprite;
            selectionImage.raycastTarget = false;
            var rect = selectionImage.rectTransform;
            rect.SetParent(transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-16f, -15f);
            rect.offsetMax = new Vector2(16f, 15f);
        }

        if (selectionImage != null) selectionImage.gameObject.SetActive(selected);
    }

    public void ClearSlot()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            if (transform.GetChild(i) != selectionImage?.transform && transform.GetChild(i) != hitFlash?.transform && transform.GetChild(i) != damageLabel?.transform)
                Destroy(transform.GetChild(i).gameObject);
        SetSelection(false);
        SetHighlightMode(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!initialized) return;
        boardManager.ShowMoveCost(this);
        boardManager.SetAttackTargetHover(this, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!initialized) return;
        boardManager.HideMoveCost(this);
        boardManager.SetAttackTargetHover(this, false);
    }

    public void PlayHit(int damage)
    {
        if (hitFlash == null)
        {
            var flash = new GameObject("Hit Flash", typeof(RectTransform), typeof(Image));
            flash.layer = gameObject.layer;
            hitFlash = flash.GetComponent<Image>();
            hitFlash.raycastTarget = false;
            var rect = hitFlash.rectTransform;
            rect.SetParent(transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        if (damageLabel == null)
        {
            var label = new GameObject("Damage", typeof(RectTransform), typeof(Canvas), typeof(TMPro.TextMeshProUGUI));
            label.layer = gameObject.layer;
            var canvas = label.GetComponent<Canvas>();
            label.transform.SetParent(transform, false);
            canvas.overrideSorting = true;
            canvas.sortingOrder = 110;

            damageLabel = label.GetComponent<TMPro.TextMeshProUGUI>();
            damageLabel.raycastTarget = false;
            damageLabel.fontSize = 48f;
            damageLabel.fontStyle = TMPro.FontStyles.Bold;
            damageLabel.alignment = TMPro.TextAlignmentOptions.Center;
            damageLabel.rectTransform.sizeDelta = new Vector2(180f, 72f);
        }

        damageLabel.text = damage > 0 ? $"-{damage}" : "0";
        hitFlash.transform.SetAsLastSibling();
        if (hitAnimation != null) StopCoroutine(hitAnimation);
        hitAnimation = StartCoroutine(AnimateHit());
    }

    private System.Collections.IEnumerator AnimateHit()
    {
        const float duration = 0.65f;
        hitFlash.gameObject.SetActive(true);
        damageLabel.gameObject.SetActive(true);
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = elapsed / duration;
            float fade = Mathf.Clamp01(1f - elapsed / 0.24f);
            damageLabel.rectTransform.anchoredPosition = new Vector2(0f, 32f + 90f * (1f - (1f - t) * (1f - t)));
            damageLabel.color = new Color(1f, 0.82f, 0.66f, Mathf.Clamp01((1f - t) * 2.5f));
            hitFlash.color = new Color(1f, 0.3f + 0.55f * fade, 0.2f + 0.65f * fade, 0.65f * fade);
            yield return null;
        }
        hitFlash.gameObject.SetActive(false);
        damageLabel.gameObject.SetActive(false);
        hitAnimation = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (initialized && boardManager != null)
            boardManager.OnSlotClicked(ownerId, position);
    }
}