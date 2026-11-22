using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActionPointsView : MonoBehaviour
{
    private const int Capacity = 5;
    private const float Scale = 0.42f;
    private static readonly float[] SquareCenters = { 69.5f, 150f, 231.5f, 312f, 393f };
    private readonly Image[] squares = new Image[Capacity];
    private TextMeshProUGUI countLabel;
    private int available = -1;
    private int preview;

    public static ActionPointsView Create(Button portrait, TextMeshProUGUI text, Sprite background, Sprite fill, bool above)
    {
        var portraitRect = (RectTransform)portrait.transform;
        var panel = new GameObject("Action Points", typeof(RectTransform), typeof(Image));
        panel.layer = portrait.gameObject.layer;
        var rect = (RectTransform)panel.transform;
        rect.SetParent(portraitRect.parent, false);
        rect.SetSiblingIndex(portraitRect.GetSiblingIndex() + 1);
        rect.anchorMin = portraitRect.anchorMin;
        rect.anchorMax = portraitRect.anchorMax;
        rect.sizeDelta = new Vector2(587f, 130f) * Scale;
        float offset = (portraitRect.rect.height + rect.rect.height) * 0.5f + 6f;
        rect.anchoredPosition = portraitRect.anchoredPosition +
            new Vector2((portraitRect.rect.width - rect.rect.width) * 0.5f, above ? offset : -offset);

        var panelImage = panel.GetComponent<Image>();
        panelImage.sprite = background;
        panelImage.raycastTarget = false;

        var view = panel.AddComponent<ActionPointsView>();
        for (int i = 0; i < Capacity; i++)
        {
            var square = new GameObject("Point", typeof(RectTransform), typeof(Image));
            square.layer = panel.layer;
            var image = square.GetComponent<Image>();
            image.sprite = fill;
            image.raycastTarget = false;
            var squareRect = image.rectTransform;
            squareRect.SetParent(rect, false);
            squareRect.sizeDelta = new Vector2(63f, 70f) * Scale;
            squareRect.anchoredPosition = new Vector2((SquareCenters[i] - 293.5f) * Scale, 1f * Scale);
            view.squares[i] = image;
        }

        view.countLabel = text;
        text.rectTransform.SetParent(rect, false);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        text.rectTransform.sizeDelta = new Vector2(140f, 100f) * Scale;
        text.rectTransform.anchoredPosition = new Vector2(216f * Scale, 0f);
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 25f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color32(47, 169, 198, 255);
        text.raycastTarget = false;
        return view;
    }

    public void Display(int points, int pendingCost)
    {
        int nextPreview = pendingCost > 0 && pendingCost <= points ? pendingCost : 0;
        if (available == points && preview == nextPreview) return;

        available = points;
        preview = nextPreview;
        countLabel.text = $"{available}/{Capacity}";
        for (int i = 0; i < Capacity; i++)
            squares[i].enabled = i < available;
        Update();
    }

    private void Update()
    {
        float pulse = Mathf.Lerp(0.18f, 1f, Mathf.PingPong(Time.unscaledTime * 2.5f, 1f));
        for (int i = 0; i < Mathf.Min(available, Capacity); i++)
            squares[i].color = new Color(1f, 1f, 1f, i >= available - preview ? pulse : 1f);
    }
}
