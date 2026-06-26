using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class GameplayGuidePopup : MonoBehaviour
    {
        private static GameplayGuidePopup _instance;

        // Comic Style Theme Colors
        private static readonly Color Bg          = new Color(0.00f, 0.00f, 0.00f, 0.75f);
        private static readonly Color InkColor     = new Color(0.09f, 0.07f, 0.06f, 1.00f); // Comic black ink
        private static readonly Color TextPri      = new Color(0.09f, 0.07f, 0.06f, 1.00f); // Main text ink
        private static readonly Color TextMuted    = new Color(0.40f, 0.36f, 0.34f, 1.00f); // Muted text ink
        private static readonly Color ArtworkBg    = new Color(0.88f, 0.85f, 0.80f, 1.00f); // Paper background for artwork
        private static readonly Color DividerCol   = new Color(0.09f, 0.07f, 0.06f, 0.15f); // Transparent line

        // Buttons colors (Orange Accent)
        private static readonly Color NavBg       = new Color(0.92f, 0.48f, 0.15f, 1.00f);
        private static readonly Color NavHov      = new Color(0.98f, 0.58f, 0.25f, 1.00f);
        private static readonly Color NavPrs      = new Color(0.80f, 0.38f, 0.08f, 1.00f);

        private int _currentPageIndex;
        private List<GuidePage> _pages;
        private TextMeshProUGUI _cardNameText;
        private TextMeshProUGUI _cardTypeText;
        private Image            _cardTypeBadge;
        private Image            _cardArtworkImage;
        private TextMeshProUGUI _cardDescriptionText;
        private TextMeshProUGUI _pageIndicatorText;
        private Button           _prevButton;
        private Button           _nextButton;
        private TMP_FontAsset _font;

        private struct GuidePage
        {
            public string Name;
            public string Type;
            public string Description;
            public Color  BadgeColor;
            public string SpriteKeyword;
        }

        private static Sprite GetComicPopupSprite()
        {
            // 1. Try loading directly from resources
            var sprite = Resources.Load<Sprite>("bg-SettingPopup");
            if (sprite != null) return sprite;

            // 2. Fallback to loading from UniversalMessagePopup prefab
            var prefab = Resources.Load<GameObject>("UniversalMessagePopup");
            if (prefab != null)
            {
                var img = prefab.GetComponent<Image>();
                if (img != null) return img.sprite;
            }
            return null;
        }

        public static void Show()
        {
            if (_instance != null)
            {
                _instance.gameObject.SetActive(true);
                _instance.transform.SetAsLastSibling();
                _instance._currentPageIndex = 0;
                _instance.DisplayPage(0);
                return;
            }

            Canvas target = null;
            var canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                { target = c; break; }
            }
            target ??= FindFirstObjectByType<Canvas>();
            if (target == null) { Debug.LogWarning("[GameplayGuidePopup] No Canvas found."); return; }

            var go = new GameObject("GameplayGuidePopup", typeof(RectTransform));
            go.transform.SetParent(target.transform, false);
            go.transform.SetAsLastSibling();
            _instance = go.AddComponent<GameplayGuidePopup>();
        }

        private void Awake()
        {
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var bgImg = gameObject.AddComponent<Image>();
            bgImg.color = Bg;
            var backdropBtn = gameObject.AddComponent<Button>();
            backdropBtn.transition = Selectable.Transition.None;
            backdropBtn.onClick.AddListener(Close);

            _font = ResolveFontSafe();
            InitializePages();
            BuildUI();
            DisplayPage(0);
        }

        private void BuildUI()
        {
            var panel = MakeRect("Panel", transform);
            SetCentered(panel, new Vector2(820, 580));
            
            var panelImg = AddImg(panel, Color.white);
            panelImg.sprite = GetComicPopupSprite();
            panelImg.type = Image.Type.Sliced;
            panelImg.raycastTarget = true;

            // Comic outline shadow
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = InkColor;
            outline.effectDistance = new Vector2(6f, -6f);
            outline.useGraphicAlpha = true;

            // Header area
            var header = MakeRect("Header", panel.transform);
            header.anchorMin = new Vector2(0f, 1f); header.anchorMax = Vector2.one;
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 56f);
            header.anchoredPosition = Vector2.zero;
            AddImg(header, Color.clear);

            var hLine = MakeRect("HeaderLine", header.transform);
            hLine.anchorMin = new Vector2(0f, 0f); hLine.anchorMax = new Vector2(1f, 0f);
            hLine.pivot = new Vector2(0.5f, 0f);
            hLine.sizeDelta = new Vector2(0f, 2f);
            hLine.anchoredPosition = Vector2.zero;
            AddImg(hLine, DividerCol);

            var titleTxt = MakeTMP("Title", header.transform, "📖  HƯỚNG DẪN CÁCH CHƠI", 18f, TextPri, FontStyles.Bold);
            Stretch(titleTxt.rectTransform);
            titleTxt.rectTransform.offsetMin = new Vector2(28f, 2f);
            titleTxt.alignment = TextAlignmentOptions.Left;

            var closeRt = MakeRect("Close", header.transform);
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.sizeDelta = new Vector2(44f, 44f);
            closeRt.anchoredPosition = new Vector2(-22f, 0f);
            AddImg(closeRt, Color.clear);
            var closeBtn = closeRt.gameObject.AddComponent<Button>();
            AddColors(closeBtn, Color.clear, new Color(0.86f, 0.12f, 0.10f, 0.15f), new Color(0.86f, 0.12f, 0.10f, 0.35f));
            var closeX = MakeTMP("X", closeRt.transform, "✕", 20f, TextMuted, FontStyles.Bold);
            Stretch(closeX.rectTransform);
            closeX.alignment = TextAlignmentOptions.Center;
            closeBtn.onClick.AddListener(Close);

            // Body
            var body = MakeRect("Body", panel.transform);
            body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one;
            body.offsetMin = new Vector2(28f, 60f); body.offsetMax = new Vector2(-28f, -60f);

            var hl = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 24f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;

            // Card artwork column
            var artColumn = MakeRect("ArtColumn", body.transform);
            var artLE = artColumn.gameObject.AddComponent<LayoutElement>();
            artLE.preferredWidth = 260; artLE.flexibleWidth = 0;

            var artBg = MakeRect("ArtBg", artColumn.transform);
            Stretch(artBg);
            artBg.offsetMin = new Vector2(0f, 16f); artBg.offsetMax = new Vector2(0f, -16f);
            AddImg(artBg, ArtworkBg);
            
            // Artwork box thin border
            var artOutline = artBg.gameObject.AddComponent<Outline>();
            artOutline.effectColor = InkColor;
            artOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var artImg = MakeRect("Art", artBg.transform);
            Stretch(artImg);
            artImg.offsetMin = new Vector2(12f, 12f); artImg.offsetMax = new Vector2(-12f, -12f);
            _cardArtworkImage = artImg.gameObject.AddComponent<Image>();
            _cardArtworkImage.preserveAspect = true;
            _cardArtworkImage.color = new Color(1f, 1f, 1f, 0.18f);

            // Information column
            var infoCol = MakeRect("InfoColumn", body.transform);
            var infoLE = infoCol.gameObject.AddComponent<LayoutElement>();
            infoLE.flexibleWidth = 1;

            var vl = infoCol.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 14f; vl.childAlignment = TextAnchor.UpperLeft;
            vl.childControlWidth = true; vl.childControlHeight = false;
            vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
            vl.padding = new RectOffset(0, 0, 8, 8);

            var badgeRow = MakeRect("BadgeRow", infoCol.transform);
            badgeRow.sizeDelta = new Vector2(0f, 42f);
            var bhl = badgeRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            bhl.spacing = 12f; bhl.childAlignment = TextAnchor.MiddleLeft;
            bhl.childControlWidth = false; bhl.childControlHeight = true;
            bhl.childForceExpandWidth = false;

            _cardNameText = MakeTMP("Name", badgeRow.transform, "Card Name", 24f, TextPri, FontStyles.Bold);
            _cardNameText.rectTransform.sizeDelta = new Vector2(0f, 42f);
            var nameLE = _cardNameText.gameObject.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            var pill = MakeRect("Pill", badgeRow.transform);
            pill.sizeDelta = new Vector2(100f, 28f);
            _cardTypeBadge = AddImg(pill, new Color(0.5f, 0.2f, 0.9f));
            
            // Badge border
            var badgeOutline = pill.gameObject.AddComponent<Outline>();
            badgeOutline.effectColor = InkColor;
            badgeOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var pillLE = pill.gameObject.AddComponent<LayoutElement>();
            pillLE.preferredWidth = 100;
            _cardTypeText = MakeTMP("PillTxt", pill.transform, "TYPE", 11f, Color.white, FontStyles.Bold);
            Stretch(_cardTypeText.rectTransform);
            _cardTypeText.alignment = TextAlignmentOptions.Center;

            // Divider
            var div = MakeRect("Divider", infoCol.transform);
            div.sizeDelta = new Vector2(0f, 1f);
            AddImg(div, DividerCol);

            // Description
            var descRt = MakeRect("Desc", infoCol.transform);
            descRt.sizeDelta = new Vector2(0f, 200f);
            _cardDescriptionText = MakeTMP("DescTxt", descRt.transform, "", 15f, TextPri, fontOverride: GetReadableFont());
            Stretch(_cardDescriptionText.rectTransform);
            _cardDescriptionText.alignment = TextAlignmentOptions.TopLeft;
            _cardDescriptionText.textWrappingMode = TextWrappingModes.Normal;
            _cardDescriptionText.lineSpacing = 5f;
            var descLE = descRt.gameObject.AddComponent<LayoutElement>();
            descLE.preferredHeight = 200; descLE.flexibleHeight = 1;

            // Footer
            var footer = MakeRect("Footer", panel.transform);
            footer.anchorMin = new Vector2(0f, 0f); footer.anchorMax = new Vector2(1f, 0f);
            footer.pivot = new Vector2(0.5f, 0f);
            footer.sizeDelta = new Vector2(0f, 56f);
            footer.anchoredPosition = Vector2.zero;
            AddImg(footer, Color.clear);

            var fLine = MakeRect("FooterLine", footer.transform);
            fLine.anchorMin = new Vector2(0f, 1f); fLine.anchorMax = new Vector2(1f, 1f);
            fLine.pivot = new Vector2(0.5f, 1f);
            fLine.sizeDelta = new Vector2(0f, 2f);
            fLine.anchoredPosition = Vector2.zero;
            AddImg(fLine, DividerCol);

            var fhl = footer.gameObject.AddComponent<HorizontalLayoutGroup>();
            fhl.spacing = 16f; fhl.childAlignment = TextAnchor.MiddleCenter;
            fhl.childControlWidth = false; fhl.childControlHeight = false;
            fhl.childForceExpandHeight = false;
            fhl.padding = new RectOffset(20, 20, 8, 8);

            _prevButton = MakeNavBtn(footer.transform, "◀  Trước");
            _prevButton.onClick.AddListener(ShowPrevPage);

            _pageIndicatorText = MakeTMP("PageInd", footer.transform, "1 / 10", 14f, TextMuted, FontStyles.Bold, GetReadableFont());
            _pageIndicatorText.rectTransform.sizeDelta = new Vector2(80f, 0f);
            var pLE = _pageIndicatorText.gameObject.AddComponent<LayoutElement>();
            pLE.preferredWidth = 80;
            _pageIndicatorText.alignment = TextAlignmentOptions.Center;

            _nextButton = MakeNavBtn(footer.transform, "Tiếp  ▶");
            _nextButton.onClick.AddListener(ShowNextPage);
        }

        private void InitializePages()
        {
            _pages = new List<GuidePage>
            {
                new() { Name = "Mèo Nổ", Type = "BOMB", Description = "Rút phải lá này mà không có Defuse → <b>thua ngay lập tức</b>.\n\nĐây là lá bài nguy hiểm nhất trong bộ bài — hãy cố tránh rút nó!", BadgeColor = new Color(0.86f, 0.12f, 0.10f), SpriteKeyword = "Exploding" },
                new() { Name = "Defuse", Type = "DEFUSE", Description = "Dùng để <b>vô hiệu hoá</b> lá Mèo Nổ khi vừa rút phải.\n\nSau khi dùng, bạn được chèn lại lá Mèo Nổ vào <b>vị trí bất kỳ</b> trong bộ bài.", BadgeColor = new Color(0.10f, 0.58f, 0.25f), SpriteKeyword = "Defuse" },
                new() { Name = "Nope", Type = "PHẢN ĐÒN", Description = "Chặn hành động của bất kỳ người chơi nào — <b>trừ Defuse và Mèo Nổ</b>.\n\nNope có thể chặn Nope khác (nope-chain). Chơi bất kỳ lúc nào trong cửa sổ phản ứng.", BadgeColor = new Color(0.18f, 0.35f, 0.67f), SpriteKeyword = "Nope" },
                new() { Name = "Attack", Type = "HÀNH ĐỘNG", Description = "Kết thúc lượt của bạn ngay lập tức.\n\nNgười chơi tiếp theo phải <b>chơi 2 lượt liên tiếp</b>. Nếu bị Attack thêm, số lượt tích lũy (+2 mỗi lần).", BadgeColor = new Color(0.90f, 0.40f, 0.00f), SpriteKeyword = "Attack" },
                new() { Name = "Skip", Type = "HÀNH ĐỘNG", Description = "Kết thúc lượt của bạn ngay lập tức <b>mà không cần rút bài</b>.\n\nNếu đang bị Attack (phải rút nhiều lần), Skip chỉ giảm đi 1 lượt rút.", BadgeColor = new Color(0.60f, 0.20f, 0.80f), SpriteKeyword = "Skip" },
                new() { Name = "Favor", Type = "HÀNH ĐỘNG", Description = "Chọn <b>một người chơi</b> làm mục tiêu.\n\nNgười đó phải tự chọn 1 lá bài từ tay mình và trao cho bạn.", BadgeColor = new Color(0.30f, 0.70f, 0.80f), SpriteKeyword = "Favor" },
                new() { Name = "Shuffle", Type = "HÀNH ĐỘNG", Description = "<b>Xáo trộn</b> toàn bộ bộ bài rút theo thứ tự ngẫu nhiên.\n\nHữu ích để phá kế hoạch của đối thủ vừa dùng See the Future.", BadgeColor = new Color(0.50f, 0.50f, 0.50f), SpriteKeyword = "Shuffle" },
                new() { Name = "See the Future", Type = "HÀNH ĐỘNG", Description = "Xem <b>3 lá bài trên cùng</b> của bộ bài rút.\n\nKhông thay đổi thứ tự bài. Chỉ mình bạn biết nội dung — thông tin rất quý giá!", BadgeColor = new Color(0.20f, 0.70f, 0.50f), SpriteKeyword = "See-the-Future" },
                new() { Name = "Combo 2 & 3 — Cat Cards", Type = "COMBO MÈO", Description = "Đánh <b>2 lá mèo giống nhau</b> → lấy <b>1 lá ngẫu nhiên</b> từ tay người chơi tự chọn.\n\nĐánh <b>3 lá mèo giống nhau</b> → chỉ định tên 1 lá bài để cướp từ đối thủ (nếu họ có).", BadgeColor = new Color(0.36f, 0.20f, 0.12f), SpriteKeyword = "Cat" },
                new() { Name = "Combo 5 — Khác Loại", Type = "COMBO ĐẶC BIỆT", Description = "Đánh <b>5 lá bài khác nhau</b> (không trùng loại) trong cùng một lượt.\n\nPhần thưởng: được chọn <b>bất kỳ lá bài nào</b> từ <b>chồng bài bỏ</b> để lấy về tay.", BadgeColor = new Color(0.70f, 0.10f, 0.50f), SpriteKeyword = "Favor" }
            };
        }

        private void DisplayPage(int index)
        {
            if (_pages == null || index < 0 || index >= _pages.Count) return;
            var page = _pages[index];
            _cardNameText.text        = page.Name;
            _cardTypeText.text        = page.Type;
            _cardTypeBadge.color      = page.BadgeColor;
            _cardDescriptionText.text = page.Description;
            _pageIndicatorText.text   = $"{index + 1} / {_pages.Count}";
            var sprite = FindCardSprite(page.SpriteKeyword);
            _cardArtworkImage.sprite = sprite;
            _cardArtworkImage.color  = sprite != null ? Color.white : new Color(page.BadgeColor.r, page.BadgeColor.g, page.BadgeColor.b, 0.25f);
            _prevButton.interactable = index > 0;
            _nextButton.interactable = index < _pages.Count - 1;
        }

        private void ShowPrevPage() { if (_currentPageIndex > 0) { _currentPageIndex--; DisplayPage(_currentPageIndex); } }
        private void ShowNextPage() { if (_currentPageIndex < _pages.Count - 1) { _currentPageIndex++; DisplayPage(_currentPageIndex); } }
        private void Close()        { gameObject.SetActive(false); }

        private TMP_FontAsset ResolveFontSafe()
        {
            try
            {
                if (UniversalPopup.Instance?.PopupFont != null)
                    return UniversalPopup.Instance.PopupFont;
            }
            catch {}

            try
            {
                var fontAssets = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                foreach (var f in fontAssets)
                {
                    if (f != null && !f.name.Contains("LiberationSans"))
                        return f;
                }
            }
            catch {}

            try
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<TextMeshProUGUI>())
                    if (t != null && t.font != null && !t.font.name.Contains("LiberationSans"))
                        return t.font;
            }
            catch {}

            try
            {
                return TMP_Settings.defaultFontAsset;
            }
            catch
            {
                return null;
            }
        }

        private TMP_FontAsset GetReadableFont()
        {
            foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                if (f != null && (f.name.Contains("Roboto") || f.name.Contains("LiberationSans")))
                    return f;
            }
            return TMP_Settings.defaultFontAsset;
        }

        private Sprite FindCardSprite(string keyword)
        {
            ScriptableObjects.CardDatabase db = UniversalPopup.Instance?.CardDatabase;
#if UNITY_EDITOR
            db ??= UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObjects.CardDatabase>(
                "Assets/ScriptableObjects/Cards/CardDatabase/ClassicCardDatabase.asset");
#endif
            if (db?.Cards != null)
            {
                var kLow = keyword.Replace("-","").Replace(" ","").ToLower();
                foreach (var card in db.Cards)
                {
                    if (card?.cardCode == null) continue;
                    var cLow = card.cardCode.Replace("-","").Replace(" ","").ToLower();
                    if (cLow.Contains(kLow) || kLow.Contains(cLow))
                        if (card.artworks?.Count > 0 && card.artworks[0] != null)
                            return card.artworks[0];
                }
            }
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
                if (s.name.ToLower().Contains(keyword.ToLower()) && !s.name.ToLower().Contains("button"))
                    return s;
            return null;
        }

        private static RectTransform MakeRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void SetCentered(RectTransform rt, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size; rt.anchoredPosition = Vector2.zero;
        }

        private static Image AddImg(RectTransform rt, Color c)
        {
            var img = rt.gameObject.AddComponent<Image>(); img.color = c; return img;
        }

        private TextMeshProUGUI MakeTMP(string name, Transform parent, string text, float size, Color col, FontStyles style = FontStyles.Normal, TMP_FontAsset fontOverride = null)
        {
            var rt = MakeRect(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.color = col; tmp.fontStyle = style;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.font = fontOverride != null ? fontOverride : _font;
            return tmp;
        }

        private Button MakeNavBtn(Transform parent, string label)
        {
            var rt = MakeRect("NavBtn_" + label, parent);
            rt.sizeDelta = new Vector2(130f, 40f);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 130; le.minWidth = 100; le.preferredHeight = 40f;
            
            AddImg(rt, NavBg);
            
            // Comic outline for Nav button
            var btnOutline = rt.gameObject.AddComponent<Outline>();
            btnOutline.effectColor = InkColor;
            btnOutline.effectDistance = new Vector2(2f, -2f);
            btnOutline.useGraphicAlpha = true;

            var txt = MakeTMP("Lbl", rt.transform, label, 14f, Color.white, FontStyles.Bold);
            Stretch(txt.rectTransform);
            txt.alignment = TextAlignmentOptions.Center;
            
            var textOutline = txt.gameObject.AddComponent<Outline>();
            textOutline.effectColor = InkColor;
            textOutline.effectDistance = new Vector2(1f, -1f);

            var btn = rt.gameObject.AddComponent<Button>();
            AddColors(btn, NavBg, NavHov, NavPrs);
            return btn;
        }

        private static void AddColors(Button btn, Color normal, Color hover, Color pressed)
        {
            btn.colors = new ColorBlock
            {
                normalColor      = normal,
                highlightedColor = hover,
                pressedColor     = pressed,
                selectedColor    = hover,
                disabledColor    = new Color(0.70f, 0.70f, 0.70f, 0.5f),
                colorMultiplier  = 1f,
                fadeDuration     = 0.1f
            };
        }
    }
}
